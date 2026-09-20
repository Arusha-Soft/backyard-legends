import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

export function output(name, value) {
  if (process.env.GITHUB_OUTPUT) fs.appendFileSync(process.env.GITHUB_OUTPUT, `${name}=${String(value).replace(/[\r\n]/g, '')}\n`);
}
export function readConfig() {
  return fs.existsSync('.ci-build.json') ? JSON.parse(fs.readFileSync('.ci-build.json', 'utf8')) : {};
}
export function run(command, args, options = {}) {
  const result = spawnSync(command, args, { encoding: 'utf8', ...options });
  if (result.error || result.status !== 0) {
    const sensitive = Object.entries(process.env).filter(([key]) => /PASSWORD|_PASS$|PRIVATE_KEY/.test(key)).map(([, value]) => value);
    for (let i = 0; i < args.length - 1; i++) if (['-p', '-P', '-k'].includes(args[i])) sensitive.push(args[i + 1]);
    const diagnostic = redact(`${result.stderr || ''}\n${result.stdout || ''}`, sensitive).trim();
    if (diagnostic) {
      fs.mkdirSync('Logs', { recursive: true });
      fs.appendFileSync('Logs/tool-errors.log', `${path.basename(command)}:\n${diagnostic}\n`);
      console.error(diagnostic.slice(-4000));
    }
    throw new Error(`${path.basename(command)} failed (exit ${result.status ?? 'unavailable'}). ${diagnostic.slice(-500) || 'See the workflow step log.'}`);
  }
  return result.stdout;
}
export function safeName(value) {
  return value.normalize('NFKD').replace(/[^a-zA-Z0-9._-]+/g, '_').replace(/^[-.]+/, '').slice(0, 70) || 'UnityProject';
}
export function integer(value, fallback, min, max, name) {
  const text = value === undefined || value === '' ? String(fallback) : String(value);
  if (!/^\d+$/.test(text) || Number(text) < min || Number(text) > max) throw new Error(`${name} must be an integer from ${min} to ${max}.`);
  return Number(text);
}
function yamlValue(text, key) {
  const value = text.match(new RegExp(`^[\\t ]*${key}:[\\t ]*(.+)$`, 'm'))?.[1]?.trim() || '';
  if (value.startsWith('"')) return JSON.parse(value);
  return value.replace(/^'|'$/g, '');
}
function requireValues(env, names, reason) {
  const missing = names.filter(name => !env[name]);
  if (missing.length) throw new Error(`${reason}: missing ${missing.join(', ')}. Run scripts/Setup-CI.ps1 -Configure.`);
}
function checkGroup(env, names, reason) {
  if (!names.some(name => env[name])) return false;
  requireValues(env, names, reason);
  return true;
}
export function prepareConfig(env, settings, versionFile) {
  const platform = env.CI_PLATFORM;
  const flavor = env.CI_BUILD_TYPE;
  if (!['Android', 'iOS', 'Windows'].includes(platform) || !['test', 'release'].includes(flavor)) throw new Error('Select a supported platform and test/release build type.');
  const upload = env.CI_UPLOAD_STORE === 'true';
  if (upload && platform === 'Windows') throw new Error('Store upload is supported for Android and iOS only.');
  const runNumber = integer(env.GITHUB_RUN_NUMBER, 1, 1, 10000000, 'Run number');
  const attempt = integer(env.GITHUB_RUN_ATTEMPT, 1, 1, 99, 'Run attempt');
  const versionCode = integer(env.ANDROID_VERSION_CODE_BASE, 0, 0, 2000000000, 'ANDROID_VERSION_CODE_BASE') + runNumber * 100 + attempt;
  if (versionCode > 2100000000) throw new Error('Android version code exceeds the Play Store maximum.');
  const identifiers = settings.match(/^  applicationIdentifier:\s*\r?\n((?:    .*(?:\r?\n|$))+)/m)?.[1] || '';
  const config = {
    platform, flavor, upload, development: flavor === 'test' && !upload,
    productName: yamlValue(settings, 'productName'), version: yamlValue(settings, 'bundleVersion'),
    unityVersion: yamlValue(versionFile, 'm_EditorVersion'),
    androidPackage: env.ANDROID_PACKAGE_NAME || yamlValue(identifiers, 'Android'),
    iosBundleId: env.IOS_BUNDLE_ID || yamlValue(identifiers, 'iPhone'),
    androidVersionCode: versionCode, iosBuildNumber: `${runNumber}.${attempt}`,
    androidAab: flavor === 'release' || upload,
    started: new Date().toISOString(),
    retention: integer(env.ARTIFACT_RETENTION_DAYS, 1, 1, 90, 'ARTIFACT_RETENTION_DAYS'),
  };
  config.artifactName = `${safeName(config.productName)}_${safeName(config.version)}_${platform}_${flavor}_${runNumber}.${attempt}`;
  requireValues(env, ['UNITY_EMAIL', 'UNITY_PASSWORD'], 'Unity activation');
  if (!env.UNITY_LICENSE && !env.UNITY_SERIAL) throw new Error('Unity activation requires UNITY_LICENSE (raw .ulf contents) or UNITY_SERIAL, plus email and password.');
  if (env.UNITY_LICENSE && env.UNITY_SERIAL) throw new Error('Choose one Unity activation method; remove the unused UNITY_LICENSE or UNITY_SERIAL repository secret.');
  if (env.UNITY_LICENSE && !env.UNITY_LICENSE.trim().startsWith('<')) throw new Error('UNITY_LICENSE must contain the original .ulf XML, not base64. Rerun Setup-CI.ps1 -Configure.');
  config.androidSign = platform === 'Android' && checkGroup(env, ['ANDROID_KEYSTORE_BASE64', 'ANDROID_KEYSTORE_PASS', 'ANDROID_KEY_ALIAS_NAME', 'ANDROID_KEY_ALIAS_PASS'], 'Android signing');
  config.windowsSign = platform === 'Windows' && flavor === 'release' && checkGroup(env, ['WIN_CERT_PFX_BASE64', 'WIN_CERT_PASSWORD'], 'Windows signing');
  const profile = flavor === 'release' || upload ? 'IOS_PROFILE_APPSTORE_BASE64' : 'IOS_PROFILE_DEVELOPMENT_BASE64';
  config.iosSign = platform === 'iOS' && Boolean(env[profile]);
  if (config.iosSign || (upload && platform === 'iOS')) requireValues(env, [profile, 'IOS_CERT_P12_BASE64', 'IOS_CERT_PASSWORD', 'IOS_TEAM_ID'], 'iOS signing');
  if (platform === 'Android' && (flavor === 'release' || upload) && !config.androidSign) throw new Error('Android release/store builds require your app upload keystore. Test APKs can use the debug key.');
  if (platform === 'Android' && !/^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$/.test(config.androidPackage)) throw new Error('Set ANDROID_PACKAGE_NAME to a valid application ID.');
  if (platform === 'iOS' && !/^[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$/.test(config.iosBundleId)) throw new Error('Set IOS_BUNDLE_ID to a valid bundle ID.');
  if (upload && platform === 'Android') {
    requireValues(env, ['GOOGLE_PLAY_SERVICE_ACCOUNT_JSON'], 'Google Play upload');
    let account;
    try { account = JSON.parse(env.GOOGLE_PLAY_SERVICE_ACCOUNT_JSON); }
    catch { throw new Error('GOOGLE_PLAY_SERVICE_ACCOUNT_JSON is not valid JSON. Select the original service account file in Setup-CI.ps1.'); }
    if (account?.type !== 'service_account' || !account.client_email || !account.private_key) throw new Error('GOOGLE_PLAY_SERVICE_ACCOUNT_JSON must be a Google service account key JSON file.');
  }
  if (upload && platform === 'iOS') requireValues(env, ['APP_STORE_CONNECT_PRIVATE_KEY', 'APP_STORE_CONNECT_KEY_ID', 'APP_STORE_CONNECT_ISSUER_ID'], 'App Store Connect upload');
  return config;
}
export function redact(text, secrets) {
  const values = new Set();
  function collect(value) {
    if (typeof value === 'string' && value) {
      values.add(value);
      values.add(value.replace(/\n/g, '\\n'));
      values.add(encodeURIComponent(value));
      if (value.startsWith('{')) { try { collect(JSON.parse(value)); } catch {} }
    } else if (value && typeof value === 'object') Object.values(value).forEach(collect);
  }
  collect(secrets);
  for (const value of [...values].sort((a, b) => b.length - a.length)) text = text.split(value).join('[REDACTED]');
  return text;
}
function zip(source, destination) {
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  // Windows' bundled bsdtar also allows developers to run the offline staging tests.
  if (process.platform === 'win32') run('tar', ['-a', '-cf', path.resolve(destination), '-C', source, '.']);
  else run('zip', ['-qr', path.resolve(destination), '.'], { cwd: source });
}
export function storeDescription(env, outcome) {
  if (env.CI_UPLOAD_STORE !== 'true') return 'Not requested';
  if (outcome !== 'success') return outcome === 'failure' ? '❌ Failed — see store upload step in the run log' : 'Not uploaded — build/setup did not finish';
  if (env.CI_PLATFORM === 'Android') return env.CI_BUILD_TYPE === 'test' ? '✅ Google Play internal testing uploaded' : '✅ Google Play production draft uploaded (not published)';
  return '✅ App Store Connect upload accepted (processing/testing availability is managed by Apple)';
}
async function stage() {
  const env = process.env;
  const config = readConfig();
  fs.mkdirSync('ci-delivery', { recursive: true });
  fs.mkdirSync('ci-diagnostics', { recursive: true });
  const name = config.artifactName || env.ARTIFACT_NAME || `${env.CI_PLATFORM}_${env.CI_BUILD_TYPE}_${env.GITHUB_RUN_ID}`;
  const details = {
    artifact: name, build: env.BUILD_RESULT || 'failure', store: storeDescription(env, env.STORE_RESULT),
    branch: env.GITHUB_REF_NAME, commit: env.GITHUB_SHA,
    run: `${env.GITHUB_SERVER_URL}/${env.GITHUB_REPOSITORY}/actions/runs/${env.GITHUB_RUN_ID}`,
    error: fs.existsSync('.ci-error.txt') ? fs.readFileSync('.ci-error.txt', 'utf8') : '',
  };
  const secrets = JSON.parse(env.REDACT_VALUES || '{}');
  output('artifact_name', name);
  output('retention_days', config.retention || 1);
  fs.writeFileSync('.ci-report.json', JSON.stringify(details));
  fs.writeFileSync('ci-diagnostics/status.json', redact(JSON.stringify(details, null, 2), secrets));
  for (const candidate of ['Logs/UnityBuild.log', 'out.log', 'Logs/xcode-archive.log', 'Logs/xcode-export.log', 'Logs/pods.log', 'Logs/signing.log', 'Logs/tool-errors.log']) {
    if (!fs.existsSync(candidate)) continue;
    const fd = fs.openSync(candidate, 'r');
    try {
      const size = fs.fstatSync(fd).size;
      const buffer = Buffer.alloc(Math.min(size, 10 * 1024 * 1024));
      fs.readSync(fd, buffer, 0, buffer.length, Math.max(0, size - buffer.length));
      fs.writeFileSync(`ci-diagnostics/${path.basename(candidate)}`, redact(buffer.toString('utf8'), secrets));
    } finally { fs.closeSync(fd); }
  }
  if (env.DIAGNOSTICS_ONLY !== 'true' && env.BUILD_RESULT === 'success') {
    if (env.CI_PLATFORM === 'Windows') zip('build/Windows', `ci-delivery/${name}.zip`);
    else {
      const folder = env.CI_PLATFORM === 'Android' ? 'build/Android' : 'build/ipa';
      const extensions = env.CI_PLATFORM === 'Android' ? /\.(apk|aab)$/ : /\.ipa$/;
      const files = fs.existsSync(folder) ? fs.readdirSync(folder).filter(file => extensions.test(file)) : [];
      if (!files.length && env.CI_PLATFORM === 'iOS' && !config.iosSign) {
        zip('build/iOS', `ci-delivery/${name}_Xcode.zip`);
        details.note = 'Xcode project only — configure an iOS provisioning profile and signing certificate for an installable IPA.';
      } else if (!files.length) throw new Error(`No build output found in ${folder}.`);
      for (const file of files) fs.copyFileSync(path.join(folder, file), `ci-delivery/${name}${path.extname(file)}`);
    }
  }
  fs.writeFileSync('ci-diagnostics/status.json', redact(JSON.stringify(details, null, 2), secrets));
  // Diagnostics travel with builds too, including failed store submissions.
  zip('ci-diagnostics', `ci-delivery/${name}_logs.zip`);
  const sums = [];
  for (const file of fs.readdirSync('ci-delivery')) {
    if (file === 'SHA256SUMS.txt') continue;
    const hash = createHash('sha256');
    for await (const chunk of fs.createReadStream(`ci-delivery/${file}`)) hash.update(chunk);
    sums.push(`${hash.digest('hex')}  ${file}`);
  }
  fs.writeFileSync('ci-delivery/SHA256SUMS.txt', `${sums.join('\n')}\n`);
  fs.writeFileSync('.ci-report.json', JSON.stringify(details));
}
function summary() {
  const env = process.env;
  const report = fs.existsSync('.ci-report.json') ? JSON.parse(fs.readFileSync('.ci-report.json', 'utf8')) : {};
  const telegramWarning = env.TELEGRAM_RESULT === 'failure' && env.ARTIFACT_RESULT === 'success' && env.STAGE_RESULT === 'success';
  const lines = [
    `### ${env.CI_PLATFORM} / ${env.CI_BUILD_TYPE}`, '',
    `- Build: ${env.BUILD_RESULT}`,
    `- Store: ${storeDescription(env, env.STORE_RESULT)}`,
    `- GitHub artifacts: ${env.ARTIFACT_RESULT || 'skipped'}`,
    `- Telegram: ${env.TELEGRAM_RESULT || 'skipped'}${telegramWarning ? ' (warning; build available in GitHub artifacts)' : ''}`,
    report.note ? `- ${report.note}` : '',
    report.error ? `- Setup error: ${report.error}` : '',
    '- Full Unity / signing / store action output is in this workflow run.',
  ];
  if (env.GITHUB_STEP_SUMMARY) fs.appendFileSync(env.GITHUB_STEP_SUMMARY, `${lines.filter(Boolean).join('\n')}\n`);
  // Either delivery route can preserve the build. Telegram failures are warnings
  // when GitHub has the artifact; losing both routes must still fail visibly.
  const delivered = env.ARTIFACT_RESULT === 'success' ||
    (env.TELEGRAM_RESULT === 'delivered' && env.TELEGRAM_DELIVERED === 'true');
  const failed = env.BUILD_RESULT !== 'success' || env.STORE_RESULT === 'failure' ||
    (env.CI_UPLOAD_STORE === 'true' && env.STORE_RESULT !== 'success') || env.STAGE_RESULT === 'failure' ||
    !delivered;
  if (failed && env.DIAGNOSTICS_ONLY !== 'true') throw new Error('Build, deployment, or delivery failed. See the status summary and the failed step.');
}
export async function main(command) {
  if (command === 'prepare') {
    fs.mkdirSync('Logs', { recursive: true });
    const config = prepareConfig(process.env, fs.readFileSync('ProjectSettings/ProjectSettings.asset', 'utf8'), fs.readFileSync('ProjectSettings/ProjectVersion.txt', 'utf8'));
    fs.writeFileSync('.ci-build.json', JSON.stringify(config, null, 2));
    for (const [key, value] of Object.entries({
      unity_version: config.unityVersion, target: { Android: 'Android', iOS: 'iOS', Windows: 'StandaloneWindows64' }[config.platform],
      artifact_name: config.artifactName, started: config.started, android_package: config.androidPackage,
      android_sign: config.androidSign, windows_sign: config.windowsSign, ios_sign: config.iosSign,
    })) output(key, value);
    console.log(`${config.platform} ${config.flavor}: ${config.artifactName}`);
    if (config.upload && config.flavor === 'test') console.log('Store testing uses an optimized, non-debuggable distribution build.');
  } else if (command === 'cleanup-transfer') {
    const env = process.env;
    if (!/^\d+$/.test(env.TRANSFER_ID || '')) throw new Error('Missing transfer artifact ID.');
    let response;
    try {
      response = await fetch(`${env.GITHUB_API_URL || 'https://api.github.com'}/repos/${env.GITHUB_REPOSITORY}/actions/artifacts/${env.TRANSFER_ID}`, {
        method: 'DELETE', headers: { Authorization: `Bearer ${env.GH_TOKEN}`, Accept: 'application/vnd.github+json' }, signal: AbortSignal.timeout(30_000),
      });
    } catch { throw new Error('Could not remove the temporary transfer artifact; it will expire after one day.'); }
    if (response.status !== 204 && response.status !== 404) throw new Error(`Temporary artifact cleanup failed (HTTP ${response.status}); it will expire after one day.`);
    console.log('Temporary iOS transfer artifact removed from GitHub storage.');
  } else if (command === 'stage') await stage();
  else if (command === 'summary') summary();
  else throw new Error(`Unknown CI command: ${command}`);
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv[2]).catch(error => {
    const message = error.message.replace(/[\r\n]/g, ' ');
    fs.writeFileSync('.ci-error.txt', message);
    console.error(`::error::${message}`);
    process.exitCode = 1;
  });
}
