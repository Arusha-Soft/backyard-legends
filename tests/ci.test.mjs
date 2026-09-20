import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { prepareConfig, redact, storeDescription } from '../.github/scripts/ci.mjs';
import { FILE_LIMIT, Telegram, batches, message, splitFile, textChunks } from '../.github/scripts/telegram.mjs';
import { configureProject, validateProfile } from '../.github/scripts/sign-ios.mjs';

const settings = fs.readFileSync(new URL('../ProjectSettings/ProjectSettings.asset', import.meta.url), 'utf8');
const version = fs.readFileSync(new URL('../ProjectSettings/ProjectVersion.txt', import.meta.url), 'utf8');
const basic = { CI_PLATFORM: 'Android', CI_BUILD_TYPE: 'test', CI_UPLOAD_STORE: 'false', UNITY_EMAIL: 'test@example.com', UNITY_PASSWORD: 'dummy-password', UNITY_LICENSE: '<license />' };
const android = { ANDROID_KEYSTORE_BASE64: 'dummy', ANDROID_KEYSTORE_PASS: 'dummy', ANDROID_KEY_ALIAS_NAME: 'upload', ANDROID_KEY_ALIAS_PASS: 'dummy' };
const apple = { IOS_PROFILE_APPSTORE_BASE64: 'dummy', IOS_CERT_P12_BASE64: 'dummy', IOS_CERT_PASSWORD: 'dummy', IOS_TEAM_ID: 'ABCDE12345',
  APP_STORE_CONNECT_PRIVATE_KEY: 'dummy', APP_STORE_CONNECT_KEY_ID: 'KEY123', APP_STORE_CONNECT_ISSUER_ID: 'issuer' };

test('all six platform/build combinations have the correct development setting', () => {
  for (const platform of ['Android', 'iOS', 'Windows']) for (const flavor of ['test', 'release']) {
    const config = prepareConfig({ ...basic, ...android, CI_PLATFORM: platform, CI_BUILD_TYPE: flavor }, settings, version);
    assert.equal(config.development, flavor === 'test');
    assert.equal(config.androidAab, flavor === 'release');
    assert.equal(config.unityVersion, '6000.0.60f1');
    assert.ok(config.androidPackage.includes('.'));
    assert.ok(config.iosBundleId.includes('.'));
  }
});
test('test store builds are signed distribution builds, not debuggable APKs', () => {
  const config = prepareConfig({ ...basic, ...android, CI_UPLOAD_STORE: 'true', GOOGLE_PLAY_SERVICE_ACCOUNT_JSON: JSON.stringify({ type: 'service_account', client_email: 'ci@example.com', private_key: 'dummy' }) }, settings, version);
  assert.equal(config.development, false);
  assert.equal(config.androidAab, true);
  assert.equal(config.androidSign, true);
  const ios = prepareConfig({ ...basic, ...apple, CI_PLATFORM: 'iOS', CI_UPLOAD_STORE: 'true' }, settings, version);
  assert.equal(ios.iosSign, true);
  assert.equal(ios.development, false);
});
test('missing release/store credentials produce actionable errors before building', () => {
  assert.throws(() => prepareConfig({ ...basic, CI_BUILD_TYPE: 'release' }, settings, version), /keystore/);
  assert.throws(() => prepareConfig({ ...basic, ANDROID_KEYSTORE_BASE64: 'dummy' }, settings, version), /ANDROID_KEYSTORE_PASS/);
  assert.throws(() => prepareConfig({ ...basic, CI_PLATFORM: 'iOS', CI_UPLOAD_STORE: 'true' }, settings, version), /IOS_PROFILE_APPSTORE_BASE64/);
  assert.throws(() => prepareConfig({ ...basic, UNITY_LICENSE: 'base64-data' }, settings, version), /original .ulf/);
  assert.throws(() => prepareConfig({ ...basic, UNITY_SERIAL: 'serial' }, settings, version), /one Unity activation/);
});
test('version codes increase on new runs and retries and stay in the Play range', () => {
  const config = values => prepareConfig({ ...basic, ...values }, settings, version);
  assert.ok(config({ GITHUB_RUN_NUMBER: '2' }).androidVersionCode > config({ GITHUB_RUN_NUMBER: '1', GITHUB_RUN_ATTEMPT: '99' }).androidVersionCode);
  assert.ok(config({ GITHUB_RUN_ATTEMPT: '2' }).androidVersionCode > config({ GITHUB_RUN_ATTEMPT: '1' }).androidVersionCode);
  assert.throws(() => config({ ARTIFACT_RETENTION_DAYS: '0' }), /retention/i);
  assert.throws(() => config({ ANDROID_VERSION_CODE_BASE: '2000000000', GITHUB_RUN_NUMBER: '10000000' }), /maximum/);
});
test('iOS without signing exports Xcode while an incomplete selected profile fails', () => {
  assert.equal(prepareConfig({ ...basic, CI_PLATFORM: 'iOS' }, settings, version).iosSign, false);
  assert.throws(() => prepareConfig({ ...basic, CI_PLATFORM: 'iOS', IOS_PROFILE_DEVELOPMENT_BASE64: 'dummy' }, settings, version), /IOS_CERT_P12_BASE64/);
});
test('log sanitization removes multiline, JSON-contained, and URL-encoded credentials', () => {
  const privateKey = '-----BEGIN PRIVATE KEY-----\ndummy-private-key\n-----END PRIVATE KEY-----';
  const secret = 'p@ss & word';
  const clean = redact(`${secret} ${encodeURIComponent(secret)} ${privateKey} ${privateKey.replace(/\n/g, '\\n')}`, { PASSWORD: secret, GOOGLE: JSON.stringify({ private_key: privateKey }) });
  assert.ok(!clean.includes(secret));
  assert.ok(!clean.includes('dummy-private-key'));
  assert.ok(!clean.includes(encodeURIComponent(secret)));
});
test('Telegram keeps the existing wording/footer and separately reports failed deployment', () => {
  const text = message({ CI_PLATFORM: 'Android', CI_BUILD_TYPE: 'release', CI_UPLOAD_STORE: 'true', BUILD_RESULT: 'success', STORE_RESULT: 'failure', GITHUB_REPOSITORY: 'org/game', GITHUB_SHA: 'abcdef1234' }, 'finish');
  assert.match(text, /\[🎮 Project: game\]/);
  assert.match(text, /📊 Build Status: ✅ Success/);
  assert.match(text, /🚚 Store:      ❌ Failed/);
  assert.ok(text.endsWith('©️ Arusha Soft'));
  assert.match(storeDescription({ CI_UPLOAD_STORE: 'true', CI_PLATFORM: 'Android', CI_BUILD_TYPE: 'release' }, 'success'), /draft.*not published/);
});
test('Telegram message splitting preserves Unicode and every character', () => {
  const text = '🎮<>&"\n'.repeat(2000);
  const chunks = textChunks(text);
  assert.equal(chunks.join(''), text);
  assert.ok(chunks.every(chunk => chunk.length <= 4096 && !/[\uD800-\uDBFF]$/.test(chunk)));
});
test('large artifacts split into bounded parts and reassemble without corruption', async () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ci-parts-'));
  try {
    const file = path.join(temp, 'Game with spaces.zip');
    const original = Buffer.from(Array.from({ length: 257 }, (_, i) => i % 256));
    fs.writeFileSync(file, original);
    const parts = await splitFile(file, path.join(temp, 'parts'), 100);
    assert.deepEqual(parts.map(part => fs.statSync(part).size), [100, 100, 57]);
    assert.deepEqual(Buffer.concat(parts.map(part => fs.readFileSync(part))), original);
    assert.deepEqual(await splitFile(file, path.join(temp, 'unused'), 257), [file]);
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
});
function mockTelegram(responder) {
  const calls = [];
  const waits = [];
  const telegram = new Telegram({ TELEGRAM_BOT_TOKEN: 'dummy-token', TELEGRAM_CHAT_ID: '123' }, async (url, init) => {
    calls.push({ method: url.split('/').at(-1), body: init.body });
    return responder ? responder(calls.length, calls.at(-1)) : { ok: true, status: 200, json: async () => ({ ok: true, result: {} }) };
  }, async milliseconds => { waits.push(milliseconds); });
  return { telegram, calls, waits };
}
test('albums use JSON serialization, omit empty topics, and respect ten-file batches', async () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ci-album-'));
  try {
    const files = Array.from({ length: 11 }, (_, i) => path.join(temp, `file ${i}.zip`));
    for (const file of files) fs.writeFileSync(file, 'fixture');
    const { telegram, calls } = mockTelegram();
    const caption = 'Title "quoted"\n<not HTML> & text 🎮';
    await telegram.deliver(files, caption);
    assert.deepEqual(calls.map(call => call.method), ['sendMediaGroup', 'sendDocument']);
    const media = JSON.parse(calls[0].body.get('media'));
    assert.equal(media.length, 10);
    assert.equal(media[0].caption, caption);
    assert.equal(calls[0].body.has('message_thread_id'), false);
    assert.equal(calls[0].body.has('parse_mode'), false);
    assert.equal(calls[0].body.get('file0').name, 'file 0.zip');
    assert.deepEqual(batches(files).map(group => group.length), [10, 1]);
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
});
test('long captions are delivered intact as text before files', async () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ci-caption-'));
  try {
    const file = path.join(temp, 'game.zip');
    fs.writeFileSync(file, 'fixture');
    const { telegram, calls } = mockTelegram();
    telegram.album = async (_, caption) => { assert.ok(caption.length < 1024); };
    const caption = 'hello 🎮'.repeat(1000);
    await telegram.deliver([file], caption);
    assert.equal(calls.map(call => call.body.get('text')).join(''), caption);
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
});

test('large parts are sent separately and smaller files share a size-limited album', async () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ci-batch-size-'));
  try {
    const sizes = [FILE_LIMIT, FILE_LIMIT, FILE_LIMIT - 100, 100, 1];
    const files = sizes.map((size, i) => {
      const file = path.join(temp, `Game.zip.${String(i + 1).padStart(3, '0')}`);
      fs.writeFileSync(file, '');
      fs.truncateSync(file, size);
      return file;
    });
    const { telegram, calls, waits } = mockTelegram();
    await telegram.deliver(files, 'Build status');
    assert.deepEqual(calls.map(call => call.method), ['sendDocument', 'sendDocument', 'sendMediaGroup', 'sendDocument']);
    const uploaded = [];
    for (const call of calls) {
      const attachments = call.method === 'sendDocument' ? [call.body.get('document')] :
        JSON.parse(call.body.get('media')).map(item => call.body.get(item.media.replace('attach://', '')));
      assert.ok(attachments.reduce((total, file) => total + file.size, 0) <= FILE_LIMIT);
      uploaded.push(...attachments.map(file => file.name));
    }
    assert.deepEqual(uploaded, files.map(file => path.basename(file)));
    assert.deepEqual(waits, [1100, 1100, 1100]);
    assert.equal(calls[0].body.get('caption'), 'Build status');
    assert.deepEqual(batches([]), []);
    fs.truncateSync(files[0], FILE_LIMIT + 1);
    assert.throws(() => batches([files[0]]), /exceeds the upload limit/);
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
});

test('413 album rejection falls back to individual files for JSON and gateway responses', async () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ci-413-'));
  try {
    const files = ['game.zip.001', 'game.zip.002', 'logs.zip'].map(name => path.join(temp, name));
    for (const file of files) fs.writeFileSync(file, 'fixture');
    for (const gateway of [false, true]) {
      const { telegram, calls, waits } = mockTelegram(index => index === 1 ? {
        ok: !gateway, status: gateway ? 413 : 200,
        json: async () => { if (gateway) throw new SyntaxError('HTML gateway response'); return { ok: false, error_code: 413, description: 'Request Entity Too Large' }; },
      } : { ok: true, status: 200, json: async () => ({ ok: true, result: {} }) });
      await telegram.deliver(files, 'Build status');
      assert.deepEqual(calls.map(call => call.method), ['sendMediaGroup', 'sendDocument', 'sendDocument', 'sendDocument']);
      assert.deepEqual(calls.slice(1).map(call => call.body.get('document').name), files.map(file => path.basename(file)));
      assert.deepEqual(calls.slice(1).map(call => call.body.get('caption')), ['Build status', null, null]);
      assert.deepEqual(waits, [1100, 1100]);
    }
    const failed = mockTelegram(index => ({ ok: false, status: index === 1 ? 413 : 403,
      json: async () => ({ ok: false, error_code: index === 1 ? 413 : 403, description: 'Rejected' }) }));
    await assert.rejects(failed.telegram.deliver(files, 'Build status'), /403/);
    assert.deepEqual(failed.calls.map(call => call.method), ['sendMediaGroup', 'sendDocument']);
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
});

test('Telegram CLI reports delivery failures as warnings with an explicit failed output', () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ci-telegram-warning-'));
  const script = fileURLToPath(new URL('../.github/scripts/telegram.mjs', import.meta.url));
  try {
    const mock = path.join(temp, 'mock-fetch.mjs');
    fs.writeFileSync(mock, 'globalThis.fetch = async () => ({ ok: false, status: 403, json: async () => ({ ok: false, error_code: 403, description: "Forbidden" }) });');
    fs.mkdirSync(path.join(temp, 'ci-delivery'));
    fs.writeFileSync(path.join(temp, 'ci-delivery/game.apk'), 'fixture');
    fs.writeFileSync(path.join(temp, 'ci-delivery/SHA256SUMS.txt'), 'checksum');
    fs.writeFileSync(path.join(temp, 'ci-delivery/READ-ME.txt'), 'instructions');
    const output = path.join(temp, 'outputs.txt');
    const result = spawnSync(process.execPath, ['--import', pathToFileURL(mock).href, script, 'finish'], { cwd: temp, encoding: 'utf8', env: {
      ...process.env, TELEGRAM_BOT_TOKEN: 'dummy-token', TELEGRAM_CHAT_ID: '123', STAGE_RESULT: 'success',
      BUILD_RESULT: 'success', ARTIFACT_RESULT: 'success', GITHUB_OUTPUT: output,
    } });
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /Telegram batch 1\/1: 1 file\(s\), 7 bytes/);
    assert.doesNotMatch(result.stdout, /SHA256SUMS|READ-ME|instructions/);
    assert.match(result.stderr, /::warning::Telegram delivery failed: Telegram 403/);
    assert.doesNotMatch(result.stderr, /::error::|dummy-token/);
    const outputs = fs.readFileSync(output, 'utf8');
    assert.match(outputs, /status=failure/);
    assert.match(outputs, /delivered=false/);
    assert.doesNotMatch(outputs, /delivered=true/);
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
});
test('HTTP 200 with ok=false fails, while 429 honors retry_after', async () => {
  const bad = mockTelegram(() => ({ ok: true, status: 200, json: async () => ({ ok: false, error_code: 403, description: 'Forbidden' }) }));
  await assert.rejects(bad.telegram.text('test'), /403/);
  const limited = mockTelegram(index => ({ ok: index > 1, status: index > 1 ? 200 : 429, json: async () => index > 1 ? { ok: true, result: {} } : { ok: false, error_code: 429, parameters: { retry_after: 3 } } }));
  await limited.telegram.text('test');
  assert.deepEqual(limited.waits, [3000]);
  assert.equal(limited.calls.length, 2);
});
test('ambiguous network errors are not retried and never expose the bot token', async () => {
  let calls = 0;
  const telegram = new Telegram({ TELEGRAM_BOT_TOKEN: 'super-secret', TELEGRAM_CHAT_ID: '123' }, async () => { calls++; throw new Error('https://api.telegram.org/botsuper-secret/sendMessage'); });
  await assert.rejects(telegram.text('test'), error => !error.message.includes('super-secret'));
  assert.equal(calls, 1);
});
test('profile validation rejects expiry, wrong apps/teams, and ad hoc store profiles', () => {
  const profile = { UUID: 'profile', TeamIdentifier: ['TEAM'], ApplicationIdentifierPrefix: ['TEAM'], ExpirationDate: '2099-01-01', Entitlements: { 'application-identifier': 'TEAM.com.example.game' } };
  const config = { iosBundleId: 'com.example.game', flavor: 'release', upload: false };
  validateProfile(profile, config, 'TEAM');
  assert.throws(() => validateProfile({ ...profile, ExpirationDate: '2000-01-01' }, config, 'TEAM'), /expired/);
  assert.throws(() => validateProfile(profile, config, 'OTHER'), /IOS_TEAM_ID/);
  assert.throws(() => validateProfile(profile, { ...config, iosBundleId: 'com.other.game' }, 'TEAM'), /IOS_BUNDLE_ID/);
  assert.throws(() => validateProfile({ ...profile, ProvisionedDevices: ['device'] }, config, 'TEAM'), /App Store distribution/);
});
test('Xcode signing assigns the profile only to the app, not UnityFramework', () => {
  const project = { objects: {
    app: { isa: 'PBXNativeTarget', name: 'Unity-iPhone', buildConfigurationList: 'appList' },
    framework: { isa: 'PBXNativeTarget', name: 'UnityFramework', buildConfigurationList: 'frameworkList' },
    appList: { buildConfigurations: ['appConfig'] }, frameworkList: { buildConfigurations: ['frameworkConfig'] },
    appConfig: { buildSettings: { 'CODE_SIGN_IDENTITY[sdk=iphoneos*]': 'old' } },
    frameworkConfig: { buildSettings: { PROVISIONING_PROFILE_SPECIFIER: 'old' } },
  } };
  configureProject(project, { iosBundleId: 'com.example.game' }, 'TEAM', { UUID: 'profile' }, 'fingerprint');
  assert.equal(project.objects.appConfig.buildSettings.PROVISIONING_PROFILE_SPECIFIER, 'profile');
  assert.equal(project.objects.frameworkConfig.buildSettings.PROVISIONING_PROFILE_SPECIFIER, undefined);
  assert.equal(project.objects.appConfig.buildSettings['CODE_SIGN_IDENTITY[sdk=iphoneos*]'], undefined);
});
test('failed store uploads fail the job even when artifact delivery succeeds', () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ci-outcome-'));
  const script = new URL('../.github/scripts/ci.mjs', import.meta.url);
  try {
    const result = spawnSync(process.execPath, [fileURLToPath(script), 'summary'], { cwd: temp, env: { ...process.env, BUILD_RESULT: 'success', STORE_RESULT: 'failure', STAGE_RESULT: 'success', ARTIFACT_RESULT: 'success', TELEGRAM_RESULT: 'success' } });
    assert.equal(result.status, 1);
    assert.match(result.stderr.toString(), /deployment/);
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
});
test('delivery outcomes preserve Telegram fallback without hiding requested upload failures', () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ci-fallback-'));
  const script = fileURLToPath(new URL('../.github/scripts/ci.mjs', import.meta.url));
  const env = { ...process.env, BUILD_RESULT: 'success', STORE_RESULT: 'skipped', STAGE_RESULT: 'success', ARTIFACT_RESULT: 'failure', TELEGRAM_RESULT: 'delivered', TELEGRAM_DELIVERED: 'true', CI_UPLOAD_STORE: 'false' };
  try {
    assert.equal(spawnSync(process.execPath, [script, 'summary'], { cwd: temp, env }).status, 0);
    assert.equal(spawnSync(process.execPath, [script, 'summary'], { cwd: temp, env: { ...env, TELEGRAM_RESULT: 'disabled', TELEGRAM_DELIVERED: 'false' } }).status, 1);
    assert.equal(spawnSync(process.execPath, [script, 'summary'], { cwd: temp, env: { ...env, CI_UPLOAD_STORE: 'true' } }).status, 1);
    const summary = path.join(temp, 'summary.md');
    const githubFallback = { ...env, ARTIFACT_RESULT: 'success', TELEGRAM_RESULT: 'failure', TELEGRAM_DELIVERED: 'false', GITHUB_STEP_SUMMARY: summary };
    assert.equal(spawnSync(process.execPath, [script, 'summary'], { cwd: temp, env: githubFallback }).status, 0);
    assert.match(fs.readFileSync(summary, 'utf8'), /Telegram: failure \(warning; build available in GitHub artifacts\)/);
    for (const failure of [
      { BUILD_RESULT: 'failure' }, { BUILD_RESULT: 'cancelled' }, { STAGE_RESULT: 'failure' },
      { CI_UPLOAD_STORE: 'true', STORE_RESULT: 'failure' }, { CI_UPLOAD_STORE: 'true', STORE_RESULT: 'skipped' },
      { ARTIFACT_RESULT: 'failure' },
    ]) assert.equal(spawnSync(process.execPath, [script, 'summary'], { cwd: temp, env: { ...githubFallback, ...failure } }).status, 1);
    assert.equal(spawnSync(process.execPath, [script, 'summary'], { cwd: temp, env: { ...env, TELEGRAM_RESULT: 'failure' } }).status, 1);
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
});
test('definitive album rejection falls back, and a failed document is not reported as delivered', async () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ci-fallback-album-'));
  try {
    const files = ['a.zip', 'b.zip'].map(file => path.join(temp, file));
    for (const file of files) fs.writeFileSync(file, 'fixture');
    const { telegram, calls } = mockTelegram((index) => ({ ok: index === 2, status: index === 2 ? 200 : index === 1 ? 400 : 403, json: async () => index === 2 ? { ok: true, result: {} } : { ok: false, error_code: index === 1 ? 400 : 403, description: 'Rejected' } }));
    await assert.rejects(telegram.deliver(files, 'caption'), /403/);
    assert.deepEqual(calls.map(call => call.method), ['sendMediaGroup', 'sendDocument', 'sendDocument']);
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
});
test('staging retains built binaries after a store failure and packages sanitized diagnostics', () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ci-stage-'));
  const script = fileURLToPath(new URL('../.github/scripts/ci.mjs', import.meta.url));
  try {
    fs.mkdirSync(path.join(temp, 'build/Android'), { recursive: true });
    fs.mkdirSync(path.join(temp, 'Logs'));
    fs.writeFileSync(path.join(temp, 'build/Android/Game.aab'), 'a real upload would contain the built app');
    fs.writeFileSync(path.join(temp, 'Logs/UnityBuild.log'), 'Build succeeded; dummy-secret must be removed');
    fs.writeFileSync(path.join(temp, '.ci-build.json'), JSON.stringify({ artifactName: 'Game_Android_release_1.1', retention: 1 }));
    const result = spawnSync(process.execPath, [script, 'stage'], { cwd: temp, encoding: 'utf8', env: {
      ...process.env, CI_PLATFORM: 'Android', CI_BUILD_TYPE: 'release', CI_UPLOAD_STORE: 'true', BUILD_RESULT: 'success', STORE_RESULT: 'failure', REDACT_VALUES: '["dummy-secret"]',
    } });
    assert.equal(result.status, 0, result.stderr);
    const binary = fs.readFileSync(path.join(temp, 'ci-delivery/Game_Android_release_1.1.aab'));
    const sums = fs.readFileSync(path.join(temp, 'ci-delivery/SHA256SUMS.txt'), 'utf8');
    assert.ok(sums.includes(createHash('sha256').update(binary).digest('hex')));
    assert.ok(fs.statSync(path.join(temp, 'ci-delivery/Game_Android_release_1.1_logs.zip')).size > 0);
    assert.ok(!fs.readFileSync(path.join(temp, 'ci-diagnostics/UnityBuild.log'), 'utf8').includes('dummy-secret'));
    assert.match(JSON.parse(fs.readFileSync(path.join(temp, 'ci-diagnostics/status.json'), 'utf8')).store, /Failed/);
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
});
