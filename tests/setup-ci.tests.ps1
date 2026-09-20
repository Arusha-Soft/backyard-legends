# Offline integration test: dummy credentials and a fake gh executable only.
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path $env:TEMP ('unity-ci-test-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $testRoot)
$oldPath = $env:PATH
$oldLog = $env:UNITY_CI_TEST_LOG
$testAnswers = New-Object 'System.Collections.Generic.Queue[object]'
function Add-Answer([string]$Prompt, [string]$Value = '') {
    $testAnswers.Enqueue(@{ Prompt = $Prompt; Value = $Value })
}
function Read-Host([string]$Prompt, [switch]$AsSecureString) {
    if (!$testAnswers.Count) { throw "Unexpected setup prompt: $Prompt" }
    $answer = $testAnswers.Dequeue()
    if ($Prompt -notlike $answer.Prompt) { throw "Unexpected prompt order: $Prompt" }
    if ($AsSecureString) { return ConvertTo-SecureString $answer.Value -AsPlainText -Force }
    return $answer.Value
}
function Assert-AnswersUsed {
    if ($testAnswers.Count) { throw 'Setup skipped expected configuration prompts.' }
}
function Clear-Calls {
    if (Test-Path -LiteralPath $env:UNITY_CI_TEST_LOG) { Remove-Item -LiteralPath $env:UNITY_CI_TEST_LOG }
}
function Get-UploadedValue([string]$Kind, [string]$Name, [string]$Repository) {
    $prefix = "$Kind set $Name --repo $Repository|"
    $lines = @([IO.File]::ReadAllLines($env:UNITY_CI_TEST_LOG) | Where-Object { $_.StartsWith($prefix) })
    if ($lines.Count -ne 1) { throw "Expected one upload of $Name to $Repository." }
    return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($lines[0].Substring($prefix.Length)))
}
function Assert-NoPublisherUpload {
    $calls = [IO.File]::ReadAllLines($env:UNITY_CI_TEST_LOG)
    if ($calls -match '^(secret|variable) set (GOOGLE_PLAY_|IOS_CERT_|IOS_PROFILE_|IOS_TEAM_ID|APP_STORE_CONNECT_|WIN_CERT_)') {
        throw 'Disabled publishing inherited an account or certificate.'
    }
}
try {
    $env:UNITY_CI_TEST_LOG = Join-Path $testRoot 'calls.txt'
    $source = @'
using System;
using System.IO;
using System.Text;
public class FakeGitHub {
    public static int Main(string[] args) {
        Console.InputEncoding = new UTF8Encoding(false);
        string command = string.Join(" ", args);
        string input = Console.IsInputRedirected ? Console.In.ReadToEnd() : "";
        File.AppendAllText(Environment.GetEnvironmentVariable("UNITY_CI_TEST_LOG"), command + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(input)) + "\n");
        if (command.StartsWith("secret list")) Console.Write("[{\"name\":\"UNITY_SERIAL\"},{\"name\":\"TELEGRAM_CHAT_ID\"},{\"name\":\"GOOGLE_PLAY_SERVICE_ACCOUNT_JSON\"},{\"name\":\"IOS_CERT_P12_BASE64\"},{\"name\":\"IOS_CERT_PASSWORD\"},{\"name\":\"IOS_PROFILE_APPSTORE_BASE64\"},{\"name\":\"APP_STORE_CONNECT_PRIVATE_KEY\"},{\"name\":\"WIN_CERT_PFX_BASE64\"},{\"name\":\"WIN_CERT_PASSWORD\"},{\"name\":\"UNRELATED\"}]");
        if (command.StartsWith("variable list")) Console.Write("[{\"name\":\"IOS_TEAM_ID\"},{\"name\":\"APP_STORE_CONNECT_KEY_ID\"},{\"name\":\"APP_STORE_CONNECT_ISSUER_ID\"}]");
        return 0;
    }
}
'@
    Add-Type -TypeDefinition $source -Language CSharp -OutputAssembly (Join-Path $testRoot 'gh.exe') -OutputType ConsoleApplication
    $env:PATH = "$testRoot;$oldPath"
    $password = 'dummy-$()-password-"-&-123'
    $license = "<license>`n  dummy Unicode: $([char]0x06CC)`n</license>"
    $shared = @{ Secrets = @{
        UNITY_EMAIL = ConvertTo-SecureString 'dummy@example.test' -AsPlainText -Force
        UNITY_PASSWORD = ConvertTo-SecureString $password -AsPlainText -Force
        UNITY_LICENSE = ConvertTo-SecureString $license -AsPlainText -Force
    }; Variables = @{ TELEGRAM_CHAT_ID = '-100123' }; Features = @{} }
    $repo = @{ Secrets = @{}; Variables = @{
        ANDROID_PACKAGE_NAME = 'com.example.game'; IOS_BUNDLE_ID = 'com.example.game'
        ANDROID_VERSION_CODE_BASE = '0'; ARTIFACT_RETENTION_DAYS = '1'; CACHE_UNITY_LIBRARY = 'false'
    }; Features = @{ PublisherSettingsConfigured = $true } }
    $shared | Export-Clixml -LiteralPath (Join-Path $testRoot 'shared.clixml')
    $repo | Export-Clixml -LiteralPath (Join-Path $testRoot 'Example--game.clixml')
    $setup = Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Setup-CI.ps1'
    $preview = & $setup -Repository Example/game -ProfileDirectory $testRoot -Preview 6>&1 | Out-String
    if (Test-Path $env:UNITY_CI_TEST_LOG) { throw 'Preview invoked GitHub CLI.' }
    $output = & $setup -Repository Example/game -ProfileDirectory $testRoot 6>&1 | Out-String
    $calls = [IO.File]::ReadAllLines($env:UNITY_CI_TEST_LOG)
    $passwordCall = $calls | Where-Object { $_.StartsWith('secret set UNITY_PASSWORD ') }
    $licenseCall = $calls | Where-Object { $_.StartsWith('secret set UNITY_LICENSE ') }
    if ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($passwordCall -split '\|')[1])) -cne $password) { throw 'Password was modified before upload.' }
    if ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($licenseCall -split '\|')[1])) -cne $license) { throw 'Multiline UTF-8 license was modified before upload.' }
    if (!($calls -match '^variable set TELEGRAM_CHAT_ID ')) { throw 'Chat ID was not uploaded as a variable.' }
    if (!($calls -match '^secret delete TELEGRAM_CHAT_ID ')) { throw 'Legacy chat ID secret was not removed.' }
    if (!($calls -match '^secret delete UNITY_SERIAL ')) { throw 'Conflicting Unity activation secret was not removed.' }
    if ($calls -match '^secret delete UNRELATED ') { throw 'An unrelated secret was removed.' }
    if ($output.Contains($password) -or $preview.Contains($password)) { throw 'Credential appeared in console output.' }
    $encrypted = [IO.File]::ReadAllText((Join-Path $testRoot 'shared.clixml'))
    if ($encrypted.Contains($password) -or $encrypted.Contains('dummy@example.test')) { throw 'Credentials were saved as plaintext.' }

    # Simulate the older script's global publisher, including all its enabled flags.
    # These values must never be used, even for a repo with publishing disabled.
    $publisherSecretNames = @('GOOGLE_PLAY_SERVICE_ACCOUNT_JSON', 'IOS_CERT_P12_BASE64', 'IOS_CERT_PASSWORD',
        'APP_STORE_CONNECT_PRIVATE_KEY', 'WIN_CERT_PFX_BASE64', 'WIN_CERT_PASSWORD')
    foreach ($name in $publisherSecretNames) { $shared.Secrets[$name] = ConvertTo-SecureString "legacy-shared-$name" -AsPlainText -Force }
    foreach ($name in @('IOS_TEAM_ID', 'APP_STORE_CONNECT_KEY_ID', 'APP_STORE_CONNECT_ISSUER_ID')) { $shared.Variables[$name] = 'legacy-shared-variable' }
    foreach ($name in @('Play', 'AppleSigning', 'AppleUpload', 'WindowsSigning')) { $shared.Features[$name] = $true }
    $shared | Export-Clixml -LiteralPath (Join-Path $testRoot 'shared.clixml')
    $sharedHash = (Get-FileHash (Join-Path $testRoot 'shared.clixml')).Hash
    $repo.Features.Remove('PublisherSettingsConfigured')
    $publishers = @{}
    foreach ($client in @('client-one', 'team-games', 'client-two')) {
        $target = "Example/$client"
        $repoFile = Join-Path $testRoot "Example--$client.clixml"
        $repo | Export-Clixml -LiteralPath $repoFile
        $json = '{"type":"service_account","private_key":"dummy-' + $client + '","client_email":"' + $client + '@example.test"}'
        $jsonPath = Join-Path $testRoot "$client.json"
        $certificatePath = Join-Path $testRoot "$client.p12"
        $apiKeyPath = Join-Path $testRoot "$client.p8"
        [IO.File]::WriteAllText($jsonPath, $json)
        [IO.File]::WriteAllText($certificatePath, "dummy-certificate-$client")
        [IO.File]::WriteAllText($apiKeyPath, "dummy-api-key-$client")
        $teamId = 'TEAM00000' + ($publishers.Count + 1)
        $expected = @{
            GOOGLE_PLAY_SERVICE_ACCOUNT_JSON = $json
            IOS_CERT_P12_BASE64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($certificatePath))
            IOS_CERT_PASSWORD = "dummy-password-$client"
            IOS_PROFILE_APPSTORE_BASE64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($certificatePath))
            APP_STORE_CONNECT_PRIVATE_KEY = "dummy-api-key-$client"
            WIN_CERT_PFX_BASE64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($certificatePath))
            WIN_CERT_PASSWORD = "dummy-password-$client"
        }
        $publishers[$target] = $expected
        Add-Answer "Configure Google Play uploads for $target*" 'y'
        Add-Answer 'Path to Google service account JSON*' $jsonPath
        Add-Answer "Configure iOS IPA signing for $target*" 'y'
        Add-Answer 'Path to this publisher signing P12*' $certificatePath
        Add-Answer 'P12 password*' $expected.IOS_CERT_PASSWORD
        Add-Answer 'Apple Developer Team ID*' $teamId
        Add-Answer 'Add an iOS development profile*' 'n'
        Add-Answer 'Add an iOS App Store profile*' 'y'
        Add-Answer 'Path to App Store .mobileprovision*' $certificatePath
        Add-Answer "Configure TestFlight / App Store Connect uploads for $target*" 'y'
        Add-Answer 'Path to this publisher App Store Connect API private key*' $apiKeyPath
        Add-Answer 'App Store Connect key ID*' "key-$client"
        Add-Answer 'App Store Connect issuer ID*' "issuer-$client"
        Add-Answer "Configure Windows release signing for $target*" 'y'
        Add-Answer 'Path to this publisher Windows signing PFX*' $certificatePath
        Add-Answer 'Windows PFX password*' $expected.WIN_CERT_PASSWORD
        Clear-Calls
        $output = & $setup -Repository $target -ProfileDirectory $testRoot 6>&1 | Out-String
        Assert-AnswersUsed
        $encryptedRepo = [IO.File]::ReadAllText($repoFile)
        foreach ($name in $expected.Keys) {
            if ((Get-UploadedValue 'secret' $name $target) -cne $expected[$name]) { throw "Wrong publisher credential for $target ($name)." }
            if ($output.Contains($expected[$name]) -or $encryptedRepo.Contains($expected[$name])) { throw 'Publisher secret leaked into console/plaintext profile.' }
        }
        foreach ($setting in @(@('IOS_TEAM_ID', $teamId), @('APP_STORE_CONNECT_KEY_ID', "key-$client"), @('APP_STORE_CONNECT_ISSUER_ID', "issuer-$client"))) {
            if ((Get-UploadedValue 'variable' $setting[0] $target) -cne $setting[1]) { throw 'Publisher IDs did not stay with their repository.' }
        }
        if ((Get-FileHash (Join-Path $testRoot 'shared.clixml')).Hash -ne $sharedHash) { throw 'Publisher setup changed the shared profile.' }
        if (!(Import-Clixml -LiteralPath $repoFile).Features.PublisherSettingsConfigured) { throw 'Publisher choices were not saved.' }
    }

    # Returning to client one must reuse only its saved account, with no prompts.
    Clear-Calls
    $null = & $setup -Repository Example/client-one -ProfileDirectory $testRoot 6>&1
    foreach ($name in $publishers['Example/client-one'].Keys) {
        if ((Get-UploadedValue 'secret' $name 'Example/client-one') -cne $publishers['Example/client-one'][$name]) { throw 'Rerun reused a different client account.' }
    }

    # These two repository names produce the same legacy filename. The profile's
    # stored repository identity must stop accidental reuse before any GH call.
    $boundProfile = Import-Clixml -LiteralPath (Join-Path $testRoot 'Example--client-one.clixml')
    $boundProfile.Repository = 'Example/client--one'
    $boundPath = Join-Path $testRoot 'Example--client--one.clixml'
    $boundProfile | Export-Clixml -LiteralPath $boundPath
    $boundHash = (Get-FileHash $boundPath).Hash
    Clear-Calls
    $failed = $false
    try { $null = & $setup -Repository Example--client/one -ProfileDirectory $testRoot 6>&1 }
    catch { if ($_.Exception.Message -notlike '*profile belongs to a different repository*') { throw }; $failed = $true }
    if (!$failed -or (Test-Path -LiteralPath $env:UNITY_CI_TEST_LOG) -or (Get-FileHash $boundPath).Hash -ne $boundHash) { throw 'A different repository reused or changed a bound profile.' }

    # A brand-new repo can disable publishing without inheriting legacy defaults.
    Add-Answer 'Android application ID*' 'com.example.buildonly'
    Add-Answer 'iOS bundle ID*' 'com.example.buildonly'
    Add-Answer 'Android version-code base*' '0'
    Add-Answer 'GitHub artifact retention days*' '1'
    Add-Answer 'Cache Unity Library*' 'false'
    Add-Answer 'Configure this app Android signing key*' 'n'
    foreach ($prompt in @('Configure Google Play uploads*', 'Configure iOS IPA signing*', 'Configure TestFlight / App Store Connect uploads*', 'Configure Windows release signing*')) { Add-Answer $prompt 'n' }
    Clear-Calls
    $null = & $setup -Repository Example/build-only -ProfileDirectory $testRoot 6>&1
    Assert-AnswersUsed
    Assert-NoPublisherUpload

    # Preview of an old repo asks for choices, without saving or applying them.
    $legacyPath = Join-Path $testRoot 'Example--legacy.clixml'
    $repo | Export-Clixml -LiteralPath $legacyPath
    $legacyHash = (Get-FileHash $legacyPath).Hash
    foreach ($prompt in @('Configure Google Play uploads*', 'Configure iOS IPA signing*', 'Configure TestFlight / App Store Connect uploads*', 'Configure Windows release signing*')) { Add-Answer $prompt 'n' }
    Clear-Calls
    $null = & $setup -Repository Example/legacy -ProfileDirectory $testRoot -Preview 6>&1
    Assert-AnswersUsed
    if (Test-Path -LiteralPath $env:UNITY_CI_TEST_LOG) { throw 'Migration preview invoked GitHub CLI.' }
    if ((Get-FileHash $legacyPath).Hash -ne $legacyHash) { throw 'Migration preview changed the profile.' }
    Add-Answer 'Configure Google Play uploads*' 'invalid'
    $failed = $false
    try { $null = & $setup -Repository Example/legacy -ProfileDirectory $testRoot 6>&1 }
    catch { if ($_.Exception.Message -notlike '*Please answer y or n*') { throw }; $failed = $true }
    Assert-AnswersUsed
    if (!$failed -or (Test-Path -LiteralPath $env:UNITY_CI_TEST_LOG) -or (Get-FileHash $legacyPath).Hash -ne $legacyHash) { throw 'Incomplete migration changed settings.' }

    # Disable client one's publishing. Shared legacy values must not fill the gap,
    # and the other clients' local settings must remain unchanged.
    $otherPath = Join-Path $testRoot 'Example--client-two.clixml'
    $otherHash = (Get-FileHash $otherPath).Hash
    Add-Answer 'Unity account email*' 'dummy@example.test'
    Add-Answer 'Unity account password*' $password
    Add-Answer 'Use a Unity Professional serial*' 'n'
    Add-Answer 'Path to Unity license*'
    Add-Answer 'Enable Telegram delivery by default*' 'n'
    foreach ($prompt in @('Android application ID*', 'iOS bundle ID*', 'Android version-code base*', 'GitHub artifact retention days*', 'Cache Unity Library*')) { Add-Answer $prompt }
    Add-Answer 'Configure this app Android signing key*' 'n'
    foreach ($prompt in @('Configure Google Play uploads*', 'Configure iOS IPA signing*', 'Configure TestFlight / App Store Connect uploads*', 'Configure Windows release signing*')) { Add-Answer $prompt 'n' }
    Clear-Calls
    $null = & $setup -Repository Example/client-one -ProfileDirectory $testRoot -Configure 6>&1
    Assert-AnswersUsed
    Assert-NoPublisherUpload
    $calls = [IO.File]::ReadAllLines($env:UNITY_CI_TEST_LOG)
    foreach ($name in $publishers['Example/client-one'].Keys) {
        if (!($calls -match "^secret delete $name --repo Example/client-one")) { throw "Disabled publisher secret was not removed: $name" }
    }
    foreach ($name in @('IOS_TEAM_ID', 'APP_STORE_CONNECT_KEY_ID', 'APP_STORE_CONNECT_ISSUER_ID')) {
        if (!($calls -match "^variable delete $name --repo Example/client-one")) { throw 'Disabled publisher variable was not removed.' }
    }
    if ($calls -match '--repo Example/(client-two|team-games)') { throw 'Configuring one repo changed another repo in GitHub.' }
    if ((Get-FileHash $otherPath).Hash -ne $otherHash) { throw 'Configuring one repo changed another local profile.' }
    Clear-Calls
    $null = & $setup -Repository Example/client-two -ProfileDirectory $testRoot 6>&1
    if ((Get-UploadedValue 'secret' 'GOOGLE_PLAY_SERVICE_ACCOUNT_JSON' 'Example/client-two') -cne $publishers['Example/client-two'].GOOGLE_PLAY_SERVICE_ACCOUNT_JSON) { throw 'Disabling one publisher affected another repository.' }
    Write-Host 'Setup-CI offline integration tests passed (encryption, exact stdin, migration, three publishers, reruns, disabled uploads, repository isolation).'
} finally {
    $env:PATH = $oldPath
    $env:UNITY_CI_TEST_LOG = $oldLog
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    $resolvedTemp = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
    if (!$resolvedTestRoot.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolvedTestRoot -Leaf) -notlike 'unity-ci-test-*') { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
}
