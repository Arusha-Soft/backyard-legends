import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { run } from './ci.mjs';

const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-windows-sign-'));
try {
  const cert = path.join(directory, 'certificate.pfx');
  const password = path.join(directory, 'password.txt');
  fs.writeFileSync(cert, Buffer.from(process.env.WIN_CERT_PFX_BASE64, 'base64'), { mode: 0o600 });
  fs.writeFileSync(password, process.env.WIN_CERT_PASSWORD, { mode: 0o600 });
  const executable = 'build/Windows/Game.exe';
  if (!fs.existsSync(executable)) throw new Error('Windows executable was not produced.');
  const signed = `${executable}.signed`;
  run('osslsigncode', ['sign', '-pkcs12', cert, '-readpass', password, '-h', 'sha256',
    '-ts', 'http://timestamp.digicert.com', '-in', executable, '-out', signed], { stdio: 'inherit' });
  run('osslsigncode', ['verify', '-in', signed], { stdio: 'inherit' });
  fs.renameSync(signed, executable);
  console.log('Windows executable signed and signature verified.');
} catch (error) {
  fs.writeFileSync('.ci-error.txt', error.message);
  console.error(`::error::${error.message}`);
  process.exitCode = 1;
} finally {
  fs.rmSync(directory, { recursive: true, force: true });
}
