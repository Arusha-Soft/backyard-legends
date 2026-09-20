#Requires -Version 5.1
<#
.SYNOPSIS
Configures repository Actions secrets and variables without organization secrets.
.EXAMPLE
.\scripts\Setup-CI.ps1
.EXAMPLE
.\scripts\Setup-CI.ps1 -Repository Arusha-Soft/new-game -Configure
.EXAMPLE
.\scripts\Setup-CI.ps1 -Preview
.NOTES
Run on Windows. Shared credentials and per-repository profiles are encrypted with
Windows DPAPI, tied to your Windows account and computer, outside the repository.
Only Unity activation and Telegram defaults are shared. Store accounts and all
signing credentials are configured and saved separately for each repository.
Only the CI setting names declared below are managed. Disabling a feature removes
its managed settings from the selected repository. Unrelated settings are untouched.
#>
[CmdletBinding()]
param(
    [string]$Repository,
    [switch]$Configure,
    [switch]$Preview,
    [string]$ProfileDirectory = (Join-Path $env:LOCALAPPDATA 'ArushaSoft\UnityCI')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Run this script on Windows to use DPAPI credential encryption.' }
$projectRoot = Split-Path $PSScriptRoot -Parent

$secretNames = @(
    'UNITY_LICENSE', 'UNITY_SERIAL', 'UNITY_EMAIL', 'UNITY_PASSWORD', 'TELEGRAM_BOT_TOKEN',
    'ANDROID_KEYSTORE_BASE64', 'ANDROID_KEYSTORE_PASS', 'ANDROID_KEY_ALIAS_PASS',
    'GOOGLE_PLAY_SERVICE_ACCOUNT_JSON', 'WIN_CERT_PFX_BASE64', 'WIN_CERT_PASSWORD',
    'IOS_CERT_P12_BASE64', 'IOS_CERT_PASSWORD', 'IOS_PROFILE_DEVELOPMENT_BASE64',
    'IOS_PROFILE_APPSTORE_BASE64', 'APP_STORE_CONNECT_PRIVATE_KEY'
)
$variableNames = @(
    'TELEGRAM_CHAT_ID', 'TELEGRAM_CHAT_THREAD_ID', 'ANDROID_KEY_ALIAS_NAME', 'ANDROID_PACKAGE_NAME',
    'ANDROID_VERSION_CODE_BASE', 'IOS_BUNDLE_ID', 'IOS_TEAM_ID', 'APP_STORE_CONNECT_KEY_ID',
    'APP_STORE_CONNECT_ISSUER_ID', 'ARTIFACT_RETENTION_DAYS', 'CACHE_UNITY_LIBRARY'
)
$sharedSecretNames = @('UNITY_LICENSE', 'UNITY_SERIAL', 'UNITY_EMAIL', 'UNITY_PASSWORD', 'TELEGRAM_BOT_TOKEN')
$sharedVariableNames = @('TELEGRAM_CHAT_ID')

function New-Profile { return @{ Secrets = @{}; Variables = @{}; Features = @{} } }
function Read-Profile([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        $profile = Import-Clixml -LiteralPath $Path
        if ($profile -isnot [hashtable] -or !$profile.ContainsKey('Secrets') -or !$profile.ContainsKey('Variables')) {
            throw "Invalid CI profile: $Path"
        }
        return $profile
    }
    return New-Profile
}
function Plain-Text([Security.SecureString]$Value) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}
function Ask-Feature($Profile, [string]$Name, [string]$Prompt) {
    $default = if ($Profile.Features[$Name]) { 'y' } else { 'n' }
    $answer = Read-Host "$Prompt (y/n) [$default]"
    if (!$answer) { $answer = $default }
    if ($answer -notmatch '^(y|yes|n|no)$') { throw 'Please answer y or n.' }
    $Profile.Features[$Name] = $answer -match '^(y|yes)$'
    return $Profile.Features[$Name]
}
function Ask-Secret($Profile, [string]$Name, [string]$Prompt) {
    $suffix = if ($Profile.Secrets.ContainsKey($Name)) { ' (Enter keeps saved value)' } else { '' }
    $value = Read-Host ($Prompt + $suffix) -AsSecureString
    if ($value.Length) { $Profile.Secrets[$Name] = $value }
    elseif (!$Profile.Secrets.ContainsKey($Name)) { throw "$Name cannot be empty." }
}
function Ask-File($Profile, [string]$Name, [string]$Prompt, [bool]$Binary = $false, [string]$DefaultPath = '') {
    $suffix = if ($Profile.Secrets.ContainsKey($Name)) { ' (Enter keeps saved file)' } elseif ($DefaultPath) { " [$DefaultPath]" } else { '' }
    $file = Read-Host ($Prompt + $suffix)
    if (!$file -and $Profile.Secrets.ContainsKey($Name)) { return }
    if (!$file) { $file = $DefaultPath }
    if (!$file) { throw "A file is required for $Name." }
    $resolved = (Resolve-Path -LiteralPath $file.Trim('"')).Path
    $value = if ($Binary) { [Convert]::ToBase64String([IO.File]::ReadAllBytes($resolved)) } else { [IO.File]::ReadAllText($resolved) }
    if (!$value) { throw "$Name file is empty." }
    if ([Text.Encoding]::UTF8.GetByteCount($value) -gt 49152) { throw "$Name exceeds GitHub's 48 KB secret limit. Export a smaller certificate/keystore bundle." }
    if ($Name -eq 'UNITY_LICENSE' -and !$value.TrimStart().StartsWith('<')) { throw 'Select the original .ulf XML file, not a base64 file.' }
    if ($Name -eq 'GOOGLE_PLAY_SERVICE_ACCOUNT_JSON') {
        try { $account = $value | ConvertFrom-Json } catch { throw 'Select a valid Google service account JSON file.' }
        if ($account.type -ne 'service_account' -or !$account.private_key -or !$account.client_email) { throw 'The JSON file must contain a Google service account private key.' }
    }
    $Profile.Secrets[$Name] = ConvertTo-SecureString -String $value -AsPlainText -Force
    $value = $null
}
function Ask-Variable($Profile, [string]$Name, [string]$Prompt, [string]$Default = '') {
    if ($Profile.Variables.ContainsKey($Name)) { $Default = $Profile.Variables[$Name] }
    $value = Read-Host "$Prompt [$Default] (Enter keeps default; - clears)"
    if (!$value) { $value = $Default }
    if ($value -eq '-') { $value = '' }
    if ($value) { $Profile.Variables[$Name] = $value } else { $Profile.Variables.Remove($Name) }
}
function Remove-Settings($Profile, [string[]]$Secrets, [string[]]$Variables = @()) {
    foreach ($name in $Secrets) { $Profile.Secrets.Remove($name) }
    foreach ($name in $Variables) { $Profile.Variables.Remove($name) }
}
function Project-Identifier([string]$Platform) {
    $settingsPath = Join-Path $projectRoot 'ProjectSettings\ProjectSettings.asset'
    $text = [IO.File]::ReadAllText($settingsPath)
    $block = [regex]::Match($text, '(?m)^  applicationIdentifier:\s*\r?\n((?:    .*\r?\n)+)').Groups[1].Value
    return [regex]::Match($block, "(?m)^    ${Platform}:\s*(.+)$").Groups[1].Value.Trim().Trim('"')
}

if (!$Repository) {
    $remote = & git -C $projectRoot remote get-url origin
    if ($LASTEXITCODE -ne 0) { throw 'Supply -Repository OWNER/REPO or configure the origin remote.' }
    if ($remote -match '(?:https://github\.com/|git@github\.com:)([^/]+/[^/]+?)(?:\.git)?$') { $Repository = $Matches[1] }
    else { throw 'Cannot detect a github.com repository. Supply -Repository OWNER/REPO.' }
}
if ($Repository -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*/[A-Za-z0-9][A-Za-z0-9_.-]*$') { throw 'Repository must be OWNER/REPO.' }
$sharedPath = Join-Path $ProfileDirectory 'shared.clixml'
$repoPath = Join-Path $ProfileDirectory ($Repository.Replace('/', '--') + '.clixml')
$shared = Read-Profile $sharedPath
$repo = Read-Profile $repoPath
# Bind saved choices to the repository as well as its filename. This also catches
# a copied profile or ambiguous owner--repo filenames before any settings change.
if ($repo.ContainsKey('Repository') -and $repo['Repository'] -ine $Repository) {
    throw "This local profile belongs to a different repository. Use a separate -ProfileDirectory for $Repository."
}
$repo['Repository'] = $Repository
$configureShared = $Configure -or !(Test-Path -LiteralPath $sharedPath)

if ($configureShared) {
    Write-Host 'Shared Unity and Telegram setup (saved once for this Windows account; never committed).'
    Ask-Secret $shared 'UNITY_EMAIL' 'Unity account email'
    Ask-Secret $shared 'UNITY_PASSWORD' 'Unity account password'
    if (Ask-Feature $shared 'UnityPro' 'Use a Unity Professional serial instead of a Personal .ulf license?') {
        Ask-Secret $shared 'UNITY_SERIAL' 'Unity serial'
        Remove-Settings $shared @('UNITY_LICENSE')
    } else {
        Ask-File $shared 'UNITY_LICENSE' 'Path to Unity license .ulf' $false 'C:\ProgramData\Unity\Unity_lic.ulf'
        Remove-Settings $shared @('UNITY_SERIAL')
    }
    if (Ask-Feature $shared 'Telegram' 'Enable Telegram delivery by default?') {
        Ask-Secret $shared 'TELEGRAM_BOT_TOKEN' 'Telegram bot token'
        Ask-Variable $shared 'TELEGRAM_CHAT_ID' 'Default Telegram chat ID'
        if (!$shared.Variables['TELEGRAM_CHAT_ID']) { throw 'Telegram requires a chat ID.' }
    } else { Remove-Settings $shared @('TELEGRAM_BOT_TOKEN') @('TELEGRAM_CHAT_ID') }
}

if ($Configure -or !(Test-Path -LiteralPath $repoPath)) {
    Write-Host "Project setup: $Repository"
    Ask-Variable $repo 'ANDROID_PACKAGE_NAME' 'Android application ID' (Project-Identifier 'Android')
    Ask-Variable $repo 'IOS_BUNDLE_ID' 'iOS bundle ID' (Project-Identifier 'iPhone')
    Ask-Variable $repo 'ANDROID_VERSION_CODE_BASE' 'Android version-code base (use 0 for a new app)' '0'
    Ask-Variable $repo 'ARTIFACT_RETENTION_DAYS' 'GitHub artifact retention days (1-90)' '1'
    Ask-Variable $repo 'CACHE_UNITY_LIBRARY' 'Cache Unity Library? (true/false; false saves GitHub storage)' 'false'
    if ($shared.Features['Telegram']) {
        Ask-Variable $repo 'TELEGRAM_CHAT_ID' 'Telegram chat for this project' $shared.Variables['TELEGRAM_CHAT_ID']
        Ask-Variable $repo 'TELEGRAM_CHAT_THREAD_ID' 'Telegram forum topic ID (optional)'
    } else { Remove-Settings $repo @() @('TELEGRAM_CHAT_ID', 'TELEGRAM_CHAT_THREAD_ID') }
    if (Ask-Feature $repo 'AndroidSigning' 'Configure this app Android signing key (required for release/store builds)?') {
        Ask-File $repo 'ANDROID_KEYSTORE_BASE64' 'Path to this app keystore' $true
        Ask-Secret $repo 'ANDROID_KEYSTORE_PASS' 'Keystore password'
        Ask-Variable $repo 'ANDROID_KEY_ALIAS_NAME' 'Key alias'
        Ask-Secret $repo 'ANDROID_KEY_ALIAS_PASS' 'Key alias password'
    } else { Remove-Settings $repo @('ANDROID_KEYSTORE_BASE64', 'ANDROID_KEYSTORE_PASS', 'ANDROID_KEY_ALIAS_PASS') @('ANDROID_KEY_ALIAS_NAME') }
}

# Older versions saved publisher credentials in the shared profile. Never inherit
# them: require a choice for this repository before applying any GitHub settings.
if ($Configure -or !$repo.Features['PublisherSettingsConfigured']) {
    Write-Host "Store accounts and signing for $Repository only (saved for this repository)."
    if (@($shared.Secrets.Keys | Where-Object { $sharedSecretNames -notcontains $_ }).Count -or
        @($shared.Variables.Keys | Where-Object { $sharedVariableNames -notcontains $_ }).Count) {
        Write-Host 'Old shared publishing/signing settings will be ignored. Select this repository credentials below; n disables that feature here.'
    }
    if (Ask-Feature $repo 'Play' "Configure Google Play uploads for $Repository`?") {
        Ask-File $repo 'GOOGLE_PLAY_SERVICE_ACCOUNT_JSON' 'Path to Google service account JSON for this app publisher'
    } else { Remove-Settings $repo @('GOOGLE_PLAY_SERVICE_ACCOUNT_JSON') }
    if (Ask-Feature $repo 'AppleSigning' "Configure iOS IPA signing for $Repository`?") {
        Ask-File $repo 'IOS_CERT_P12_BASE64' 'Path to this publisher signing P12 (include private keys; may contain development and distribution certificates)' $true
        Ask-Secret $repo 'IOS_CERT_PASSWORD' 'P12 password'
        Ask-Variable $repo 'IOS_TEAM_ID' 'Apple Developer Team ID for this app'
        if (Ask-Feature $repo 'DevelopmentProfile' 'Add an iOS development profile for installable test IPAs?') {
            Ask-File $repo 'IOS_PROFILE_DEVELOPMENT_BASE64' 'Path to development .mobileprovision' $true
        } else { Remove-Settings $repo @('IOS_PROFILE_DEVELOPMENT_BASE64') }
        if (Ask-Feature $repo 'AppStoreProfile' 'Add an iOS App Store profile for release IPAs and TestFlight?') {
            Ask-File $repo 'IOS_PROFILE_APPSTORE_BASE64' 'Path to App Store .mobileprovision' $true
        } else { Remove-Settings $repo @('IOS_PROFILE_APPSTORE_BASE64') }
    } else {
        Remove-Settings $repo @('IOS_CERT_P12_BASE64', 'IOS_CERT_PASSWORD', 'IOS_PROFILE_DEVELOPMENT_BASE64', 'IOS_PROFILE_APPSTORE_BASE64') @('IOS_TEAM_ID')
        $repo.Features['DevelopmentProfile'] = $false
        $repo.Features['AppStoreProfile'] = $false
    }
    if (Ask-Feature $repo 'AppleUpload' "Configure TestFlight / App Store Connect uploads for $Repository`?") {
        Ask-File $repo 'APP_STORE_CONNECT_PRIVATE_KEY' 'Path to this publisher App Store Connect API private key (.p8)'
        Ask-Variable $repo 'APP_STORE_CONNECT_KEY_ID' 'App Store Connect key ID'
        Ask-Variable $repo 'APP_STORE_CONNECT_ISSUER_ID' 'App Store Connect issuer ID for this publisher'
    } else { Remove-Settings $repo @('APP_STORE_CONNECT_PRIVATE_KEY') @('APP_STORE_CONNECT_KEY_ID', 'APP_STORE_CONNECT_ISSUER_ID') }
    if (Ask-Feature $repo 'WindowsSigning' "Configure Windows release signing for $Repository`?") {
        Ask-File $repo 'WIN_CERT_PFX_BASE64' 'Path to this publisher Windows signing PFX' $true
        Ask-Secret $repo 'WIN_CERT_PASSWORD' 'Windows PFX password'
    } else { Remove-Settings $repo @('WIN_CERT_PFX_BASE64', 'WIN_CERT_PASSWORD') }
    $repo.Features['PublisherSettingsConfigured'] = $true
}

$secrets = @{}
$variables = @{}
# An allowlist prevents legacy shared store/signing keys from crossing repositories,
# even when a feature is disabled or has no repository-specific replacement.
foreach ($name in $sharedSecretNames) {
    if ($shared.Secrets.ContainsKey($name)) { $secrets[$name] = $shared.Secrets[$name] }
}
foreach ($name in $sharedVariableNames) {
    if ($shared.Variables.ContainsKey($name)) { $variables[$name] = [string]$shared.Variables[$name] }
}
foreach ($name in $repo.Secrets.Keys) { $secrets[$name] = $repo.Secrets[$name] }
foreach ($name in $repo.Variables.Keys) { $variables[$name] = [string]$repo.Variables[$name] }
foreach ($name in $secrets.Keys) {
    if ($secretNames -notcontains $name -or $secrets[$name] -isnot [Security.SecureString]) { throw "Invalid secret in saved profile: $name" }
    $value = Plain-Text $secrets[$name]
    if ([Text.Encoding]::UTF8.GetByteCount($value) -gt 49152) { throw "$name exceeds GitHub's 48 KB secret limit." }
    $value = $null
}
foreach ($name in $variables.Keys) { if ($variableNames -notcontains $name) { throw "Unknown variable in profile: $name" } }
if ($variables['ARTIFACT_RETENTION_DAYS'] -notmatch '^([1-9]|[1-8][0-9]|90)$') { throw 'Artifact retention must be 1-90 days.' }
if ($variables['CACHE_UNITY_LIBRARY'] -notmatch '^(true|false)$') { throw 'CACHE_UNITY_LIBRARY must be true or false.' }
if ($variables['ANDROID_VERSION_CODE_BASE'] -notmatch '^\d+$' -or [long]$variables['ANDROID_VERSION_CODE_BASE'] -gt 2000000000) { throw 'ANDROID_VERSION_CODE_BASE must be between 0 and 2000000000.' }
if ($variables['TELEGRAM_CHAT_THREAD_ID'] -and $variables['TELEGRAM_CHAT_THREAD_ID'] -notmatch '^[1-9]\d*$') { throw 'Telegram topic ID must be a positive integer.' }
if ($secrets.ContainsKey('ANDROID_KEYSTORE_BASE64') -and !$variables['ANDROID_KEY_ALIAS_NAME']) { throw 'Android signing requires an alias name.' }
if ($secrets.ContainsKey('IOS_CERT_P12_BASE64') -and $variables['IOS_TEAM_ID'] -notmatch '^[A-Z0-9]{10}$') { throw 'IOS_TEAM_ID must be the 10-character Apple Team ID.' }
if ($secrets.ContainsKey('APP_STORE_CONNECT_PRIVATE_KEY') -and (!$variables['APP_STORE_CONNECT_KEY_ID'] -or !$variables['APP_STORE_CONNECT_ISSUER_ID'])) { throw 'App Store Connect uploads require key and issuer IDs.' }

Write-Host "Repository: $Repository"
Write-Host ('Set secrets: ' + (($secrets.Keys | Sort-Object) -join ', '))
Write-Host ('Set variables: ' + (($variables.Keys | Sort-Object) -join ', '))
Write-Host 'Unused managed CI settings and old secret copies of ordinary variables will be removed if present.'
if ($Preview) { Write-Host 'Preview only: no profile files or GitHub settings were changed.'; return }

$ghPath = (Get-Command gh -ErrorAction Stop).Source
& $ghPath auth status
if ($LASTEXITCODE -ne 0) { throw 'Run gh auth login first, then rerun this script.' }

function Invoke-Gh([string[]]$Arguments, [string]$InputText = '') {
    # All arguments are validated setting names/repo names; credentials go through
    # stdin, never process arguments, temporary plaintext files, or console output.
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $ghPath
    $info.Arguments = $Arguments -join ' '
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    try {
        [void]$process.Start()
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        # .NET Framework / Windows PowerShell 5.1 has no StandardInputEncoding
        # property. Write UTF-8 bytes directly, including multiline licenses.
        $bytes = [Text.Encoding]::UTF8.GetBytes($InputText)
        $process.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
        $process.StandardInput.BaseStream.Close()
        $process.WaitForExit()
        $stdout = $stdoutTask.Result
        $stderr = $stderrTask.Result
        if ($process.ExitCode -ne 0) { throw "GitHub CLI failed for $($Arguments -join ' '). Check your repository permissions and connection. $stderr" }
        return $stdout
    } finally { $process.Dispose() }
}

# Save before upload so a connection failure can be retried without re-entering keys.
[void](New-Item -ItemType Directory -Force -Path $ProfileDirectory)
if ($configureShared) { $shared | Export-Clixml -LiteralPath $sharedPath }
$repo | Export-Clixml -LiteralPath $repoPath
$existingSecrets = Invoke-Gh @('secret', 'list', '--repo', $Repository, '--json', 'name') | ConvertFrom-Json
$existingVariables = Invoke-Gh @('variable', 'list', '--repo', $Repository, '--json', 'name') | ConvertFrom-Json
foreach ($name in ($secrets.Keys | Sort-Object)) {
    $value = Plain-Text $secrets[$name]
    try { $null = Invoke-Gh @('secret', 'set', $name, '--repo', $Repository) $value }
    finally { $value = $null }
    Write-Host "Set secret $name"
}
foreach ($name in ($variables.Keys | Sort-Object)) {
    $null = Invoke-Gh @('variable', 'set', $name, '--repo', $Repository) $variables[$name]
    Write-Host "Set variable $name"
}
# Variables have been installed before their obsolete secret copies are removed.
foreach ($item in $existingSecrets) {
    if (($secretNames -contains $item.name -and !$secrets.ContainsKey($item.name)) -or $variableNames -contains $item.name) {
        $null = Invoke-Gh @('secret', 'delete', $item.name, '--repo', $Repository)
        Write-Host "Removed unused/migrated secret $($item.name)"
    }
}
foreach ($item in $existingVariables) {
    if ($variableNames -contains $item.name -and !$variables.ContainsKey($item.name)) {
        $null = Invoke-Gh @('variable', 'delete', $item.name, '--repo', $Repository)
    }
}
Write-Host "Setup complete. Open https://github.com/$Repository/actions and choose Build > Run workflow."
Write-Host "Encrypted local profiles: $ProfileDirectory"
