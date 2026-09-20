import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createHash, randomBytes } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { output, readConfig, run } from './ci.mjs';

export function validateProfile(profile, config, team, now = Date.now()) {
  if (!profile.UUID || !Array.isArray(profile.TeamIdentifier) || !profile.TeamIdentifier.includes(team)) throw new Error('The iOS provisioning profile does not belong to IOS_TEAM_ID.');
  if (!(Date.parse(profile.ExpirationDate) > now)) throw new Error('The iOS provisioning profile has expired. Generate a new profile.');
  const prefix = profile.ApplicationIdentifierPrefix?.[0];
  const actual = profile.Entitlements?.['application-identifier'];
  const expected = `${prefix}.${config.iosBundleId}`;
  if (!(actual === expected || (actual?.endsWith('.*') && expected.startsWith(actual.slice(0, -1))))) throw new Error('The iOS provisioning profile does not match IOS_BUNDLE_ID.');
  const store = config.upload || config.flavor === 'release';
  if (store && (profile.ProvisionedDevices || profile.ProvisionsAllDevices || profile.Entitlements?.['get-task-allow'])) throw new Error('Release/TestFlight uploads require an App Store distribution profile, not development/ad hoc/enterprise.');
  if (!store && !profile.Entitlements?.['get-task-allow']) throw new Error('Test IPA builds require a development profile. For TestFlight, enable store upload.');
}

export function configureProject(project, config, team, profile, identity) {
  const objects = project.objects;
  const targets = Object.values(objects).filter(object => object.isa === 'PBXNativeTarget');
  if (!targets.some(target => target.name === 'Unity-iPhone')) throw new Error('Generated Xcode project has no Unity-iPhone target.');
  for (const target of targets) {
    if (target.productType === 'com.apple.product-type.app-extension') throw new Error('App extensions require additional provisioning profiles; extend the signing configuration for this project.');
    const configurations = objects[target.buildConfigurationList]?.buildConfigurations || [];
    for (const id of configurations) {
      const settings = objects[id].buildSettings;
      for (const key of Object.keys(settings)) {
        if (/^(PROVISIONING_PROFILE|CODE_SIGN_IDENTITY|CODE_SIGN_STYLE|DEVELOPMENT_TEAM|CODE_SIGNING_ALLOWED|CODE_SIGNING_REQUIRED)/.test(key)) delete settings[key];
      }
      settings.CODE_SIGN_STYLE = 'Manual';
      settings.DEVELOPMENT_TEAM = team;
      settings.CODE_SIGN_IDENTITY = identity;
      if (target.name === 'Unity-iPhone') {
        settings.PROVISIONING_PROFILE_SPECIFIER = profile.UUID;
        settings.PRODUCT_BUNDLE_IDENTIFIER = config.iosBundleId;
      }
    }
  }
  return project;
}
function logged(command, args, log, options = {}) {
  const descriptor = fs.openSync(log, 'w');
  let result;
  try { result = spawnSync(command, args, { stdio: ['ignore', descriptor, descriptor], ...options }); }
  finally { fs.closeSync(descriptor); }
  // Xcode output contains paths and public signing identifiers, not certificate passwords.
  const text = fs.readFileSync(log, 'utf8');
  process.stdout.write(text.slice(-100_000));
  if (result.error || result.status !== 0) throw new Error(`${command} failed; inspect ${path.basename(log)} in the diagnostics attachment and workflow log.`);
}
async function main() {
  if (process.platform !== 'darwin') throw new Error('iOS signing must run on macOS.');
  const config = readConfig();
  const env = process.env;
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-ios-sign-'));
  const keychain = path.join(directory, 'build.keychain-db');
  const keychainPassword = randomBytes(24).toString('hex');
  let oldKeychains = [];
  let installedProfile;
  let originalProfile;
  let createdKeychain = false;
  fs.mkdirSync('Logs', { recursive: true });
  try {
    const certificate = path.join(directory, 'certificate.p12');
    const mobileProvision = path.join(directory, 'profile.mobileprovision');
    const profilePlist = path.join(directory, 'profile.plist');
    fs.writeFileSync(certificate, Buffer.from(env.IOS_CERT_P12_BASE64, 'base64'), { mode: 0o600 });
    fs.writeFileSync(mobileProvision, Buffer.from(env.IOS_PROFILE_BASE64, 'base64'), { mode: 0o600 });
    fs.writeFileSync(profilePlist, run('security', ['cms', '-D', '-i', mobileProvision]));
    const profile = JSON.parse(run('python3', ['-c', 'import plistlib,json,sys,datetime,base64; print(json.dumps(plistlib.load(open(sys.argv[1],"rb")),default=lambda x:x.isoformat()+"Z" if isinstance(x,datetime.datetime) else base64.b64encode(x).decode()))', profilePlist]));
    validateProfile(profile, config, env.IOS_TEAM_ID);
    oldKeychains = run('security', ['list-keychains', '-d', 'user']).split('\n').map(line => line.trim().replace(/^"|"$/g, '')).filter(Boolean);
    run('security', ['create-keychain', '-p', keychainPassword, keychain]);
    createdKeychain = true;
    run('security', ['set-keychain-settings', '-lut', '7200', keychain]);
    run('security', ['unlock-keychain', '-p', keychainPassword, keychain]);
    run('security', ['import', certificate, '-P', env.IOS_CERT_PASSWORD, '-k', keychain, '-T', '/usr/bin/codesign', '-T', '/usr/bin/security']);
    run('security', ['set-key-partition-list', '-S', 'apple-tool:,apple:', '-k', keychainPassword, keychain]);
    run('security', ['list-keychains', '-d', 'user', '-s', keychain, ...oldKeychains]);
    const identities = run('security', ['find-identity', '-v', '-p', 'codesigning', keychain]);
    const identity = (profile.DeveloperCertificates || []).map(cert => createHash('sha1').update(Buffer.from(cert, 'base64')).digest('hex').toUpperCase()).find(hash => identities.includes(hash));
    if (!identity) throw new Error('The P12 must contain a private key and a valid signing certificate included in the selected provisioning profile.');
    const profiles = path.join(os.homedir(), 'Library/MobileDevice/Provisioning Profiles');
    fs.mkdirSync(profiles, { recursive: true });
    installedProfile = path.join(profiles, `${profile.UUID}.mobileprovision`);
    if (fs.existsSync(installedProfile)) originalProfile = fs.readFileSync(installedProfile);
    fs.copyFileSync(mobileProvision, installedProfile);

    const pbxPath = path.resolve('build/iOS/Unity-iPhone.xcodeproj/project.pbxproj');
    const projectJson = path.join(directory, 'project.json');
    run('plutil', ['-convert', 'json', '-o', projectJson, pbxPath]);
    const project = configureProject(JSON.parse(fs.readFileSync(projectJson, 'utf8')), config, env.IOS_TEAM_ID, profile, identity);
    fs.writeFileSync(projectJson, JSON.stringify(project));
    run('plutil', ['-convert', 'xml1', '-o', pbxPath, projectJson]);
    if (fs.existsSync('build/iOS/Podfile')) logged('pod', ['install'], 'Logs/pods.log', { cwd: 'build/iOS' });
    const workspace = fs.existsSync('build/iOS/Unity-iPhone.xcworkspace');
    const archive = path.join(directory, 'Game.xcarchive');
    logged('xcodebuild', [workspace ? '-workspace' : '-project', path.resolve(`build/iOS/Unity-iPhone.${workspace ? 'xcworkspace' : 'xcodeproj'}`),
      '-scheme', 'Unity-iPhone', '-configuration', 'Release', '-destination', 'generic/platform=iOS', '-archivePath', archive, 'archive'], 'Logs/xcode-archive.log');
    const exportOptions = {
      method: config.upload || config.flavor === 'release' ? 'app-store-connect' : 'debugging',
      teamID: env.IOS_TEAM_ID, signingStyle: 'manual', signingCertificate: identity,
      provisioningProfiles: { [config.iosBundleId]: profile.UUID },
      manageAppVersionAndBuildNumber: false, destination: 'export',
    };
    const exportJson = path.join(directory, 'export.json');
    const exportPlist = path.join(directory, 'ExportOptions.plist');
    fs.writeFileSync(exportJson, JSON.stringify(exportOptions));
    run('plutil', ['-convert', 'xml1', '-o', exportPlist, exportJson]);
    fs.mkdirSync('build/ipa', { recursive: true });
    logged('xcodebuild', ['-exportArchive', '-archivePath', archive, '-exportOptionsPlist', exportPlist, '-exportPath', path.resolve('build/ipa')], 'Logs/xcode-export.log');
    const ipas = fs.readdirSync('build/ipa').filter(file => file.endsWith('.ipa'));
    if (ipas.length !== 1) throw new Error('Xcode export did not produce exactly one IPA.');
    output('ipa_path', `build/ipa/${ipas[0]}`);
    console.log(`Signed IPA ready: ${ipas[0]}`);
  } finally {
    if (createdKeychain) {
      const restored = spawnSync('security', ['list-keychains', '-d', 'user', '-s', ...oldKeychains]);
      const deleted = spawnSync('security', ['delete-keychain', keychain]);
      if (restored.status || deleted.status) console.warn('::warning::Temporary keychain cleanup was incomplete; this hosted runner will be discarded.');
    }
    if (installedProfile) {
      if (originalProfile) fs.writeFileSync(installedProfile, originalProfile);
      else fs.rmSync(installedProfile, { force: true });
    }
    fs.rmSync(directory, { recursive: true, force: true });
  }
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(error => { fs.writeFileSync('.ci-error.txt', error.message); console.error(`::error::${error.message}`); process.exitCode = 1; });
}
