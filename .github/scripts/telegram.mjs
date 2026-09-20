import fs, { openAsBlob } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { output, readConfig, storeDescription } from './ci.mjs';

export const FILE_LIMIT = 45_000_000; // Below the hosted Bot API's 50 MB limit.
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

export function message(env, status, config = {}, extra = {}) {
  const title = (env.GITHUB_REPOSITORY || 'Unity project').split('/').at(-1);
  const flavor = env.CI_BUILD_TYPE === 'test' ? 'dev' : 'release';
  const result = { success: '✅ Success', failure: '❌ Failed', cancelled: '⏹️ Cancelled' }[env.BUILD_RESULT] || 'ℹ️ Info';
  const lines = [`[🎮 Project: ${title}]`, '=============================', '',
    `🖥️ Platform:   ${env.CI_PLATFORM}`, `🎨 Flavor:     ${flavor}`,
    `📦 Repo:       ${env.GITHUB_REPOSITORY}`, `🌿 Branch:     ${env.GITHUB_REF_NAME}`,
    `🔑 Commit:     ${(env.GITHUB_SHA || '').slice(0, 7)}`, `👤 By:         ${env.GITHUB_ACTOR}`, ''];
  if (status === 'start') lines.push('=============================', '📊 Build Status: 🚀 Started', '=============================');
  else {
    const started = Date.parse(config.started || env.BUILD_STARTED);
    const seconds = Number.isFinite(started) ? Math.max(0, Math.floor((Date.now() - started) / 1000)) : 0;
    lines.push(`📊 Build Status: ${result}`, `⏱️ Duration:   ${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`,
      `📎 Artifact:   ${config.artifactName || env.ARTIFACT_NAME || 'No build artifact'}`,
      `🚚 Store:      ${storeDescription(env, env.STORE_RESULT)}`);
    if (env.ARTIFACT_RESULT === 'failure') lines.push('📦 GitHub:     ❌ Artifact upload failed (see run log / storage quota)');
    if (env.STAGE_RESULT === 'failure') lines.push('📦 Packaging:  ❌ Failed — see run log');
    if (extra.note) lines.push(`ℹ️ ${extra.note}`);
    if (extra.error) lines.push(`⚠️ ${extra.error}`);
  }
  lines.push('', `➡️ Run: ${env.GITHUB_SERVER_URL}/${env.GITHUB_REPOSITORY}/actions/runs/${env.GITHUB_RUN_ID}`, '', '-----------------------------', '©️ Arusha Soft');
  return lines.join('\n');
}
export function textChunks(text, limit = 4096) {
  const chunks = [];
  let chunk = '';
  for (const char of text) {
    if (chunk.length + char.length > limit) { chunks.push(chunk); chunk = ''; }
    chunk += char;
  }
  if (chunk) chunks.push(chunk);
  return chunks;
}
export function batches(files, limit = FILE_LIMIT) {
  const groups = [];
  let group = [];
  let bytes = 0;
  for (const file of files) {
    const size = fs.statSync(file).size;
    if (size > limit) throw new Error(`Telegram file exceeds the upload limit: ${path.basename(file)} (${size} bytes).`);
    if (group.length && (group.length === 10 || bytes + size > limit)) {
      groups.push(group);
      group = [];
      bytes = 0;
    }
    group.push(file);
    bytes += size;
  }
  if (group.length) groups.push(group);
  return groups;
}
export async function splitFile(file, directory, limit = FILE_LIMIT) {
  const size = fs.statSync(file).size;
  if (size <= limit) return [file];
  fs.mkdirSync(directory, { recursive: true });
  const parts = [];
  const source = fs.openSync(file, 'r');
  try {
    const buffer = Buffer.alloc(Math.min(limit, 1024 * 1024));
    for (let offset = 0, index = 1; offset < size; index++) {
      const target = path.join(directory, `${path.basename(file)}.${String(index).padStart(3, '0')}`);
      const fd = fs.openSync(target, 'w');
      try {
        let remaining = Math.min(limit, size - offset);
        while (remaining > 0) {
          const read = fs.readSync(source, buffer, 0, Math.min(buffer.length, remaining), offset);
          if (!read) throw new Error('Unexpected end of file while splitting an artifact.');
          let written = 0;
          while (written < read) written += fs.writeSync(fd, buffer, written, read - written);
          offset += read;
          remaining -= read;
        }
      } finally { fs.closeSync(fd); }
      parts.push(target);
    }
  } finally { fs.closeSync(source); }
  return parts;
}
export class TelegramError extends Error {
  constructor(message, code) { super(message); this.code = code; }
}
export class Telegram {
  constructor(env, fetcher = fetch, waiter = sleep) {
    this.token = env.TELEGRAM_BOT_TOKEN;
    this.chat = env.TELEGRAM_CHAT_ID;
    this.thread = env.TELEGRAM_CHAT_THREAD_ID;
    this.fetcher = fetcher;
    this.waiter = waiter;
  }
  fields() {
    const fields = { chat_id: this.chat };
    if (this.thread) fields.message_thread_id = this.thread;
    return fields;
  }
  async api(method, bodyFactory) {
    for (let attempt = 0; attempt < 4; attempt++) {
      let response;
      try {
        response = await this.fetcher(`https://api.telegram.org/bot${this.token}/${method}`, {
          method: 'POST', body: await bodyFactory(), signal: AbortSignal.timeout(300_000),
        });
      } catch {
        // A timeout after sending might already have delivered the message.
        // Do not blindly resend an album and create duplicate attachments.
        throw new TelegramError('Telegram connection failed or timed out; delivery could not be confirmed.', 0);
      }
      // Upload gateways can return an HTML/plain-text 413 instead of Bot API JSON.
      if (response.status === 413) throw new TelegramError('Telegram 413: Request Entity Too Large', 413);
      let body;
      try { body = await response.json(); } catch { throw new TelegramError('Telegram returned an invalid response.', response.status); }
      if (response.ok && body.ok === true) return body.result;
      const code = body.error_code || response.status;
      if (attempt < 3 && (code === 429 || code >= 500)) {
        const delay = Number(body.parameters?.retry_after) || 2 ** attempt;
        if (delay > 120) throw new TelegramError(`Telegram requested a ${delay}s cooldown; retry delivery later.`, code);
        await this.waiter(Math.max(1, delay) * 1000);
        continue;
      }
      const description = String(body.description || 'Unknown API error').split(this.token).join('[REDACTED]');
      throw new TelegramError(`Telegram ${code}: ${description}`, code);
    }
  }
  async text(text) {
    for (const chunk of textChunks(text)) await this.api('sendMessage', () => new URLSearchParams({ ...this.fields(), text: chunk, disable_web_page_preview: 'true' }));
  }
  async document(file, caption) {
    return this.api('sendDocument', async () => {
      const form = new FormData();
      for (const [key, value] of Object.entries(this.fields())) form.set(key, value);
      form.set('document', await openAsBlob(file), path.basename(file));
      if (caption) form.set('caption', caption);
      return form;
    });
  }
  async album(files, caption) {
    if (files.length === 1) return this.document(files[0], caption);
    try {
      return await this.api('sendMediaGroup', async () => {
        const form = new FormData();
        for (const [key, value] of Object.entries(this.fields())) form.set(key, value);
        const media = [];
        for (let i = 0; i < files.length; i++) {
          const key = `file${i}`;
          form.set(key, await openAsBlob(files[i]), path.basename(files[i]));
          media.push({ type: 'document', media: `attach://${key}`, ...(i === 0 && caption ? { caption } : {}) });
        }
        form.set('media', JSON.stringify(media));
        return form;
      });
    } catch (error) {
      if (![400, 413].includes(error.code)) throw error;
      console.log(`Telegram rejected the album (${error.code}); trying individual documents.`);
      for (let i = 0; i < files.length; i++) {
        await this.document(files[i], i === 0 ? caption : '');
        if (i + 1 < files.length) await this.waiter(1100);
      }
    }
  }
  async deliver(files, caption) {
    const longCaption = caption.length > 1024;
    if (!files.length || longCaption) await this.text(caption);
    const groups = batches(files);
    for (let i = 0; i < groups.length; i++) {
      const sizes = groups[i].map(file => fs.statSync(file).size);
      console.log(`Telegram batch ${i + 1}/${groups.length}: ${groups[i].length} file(s), ${sizes.reduce((sum, size) => sum + size, 0)} bytes.`);
      for (let j = 0; j < groups[i].length; j++) console.log(`Telegram file ${JSON.stringify(path.basename(groups[i][j]))}: ${sizes[j]} bytes.`);
      const label = longCaption || i > 0 ? `📎 Build attachments (${i + 1}/${groups.length})` : caption;
      await this.album(groups[i], label);
      if (i + 1 < groups.length) await this.waiter(1100);
    }
  }
}
async function main() {
  const env = process.env;
  if (!env.TELEGRAM_BOT_TOKEN && !env.TELEGRAM_CHAT_ID) {
    console.log('Telegram is disabled. Build artifacts remain available in GitHub.');
    output('delivered', false);
    output('status', 'disabled');
    return;
  }
  if (!env.TELEGRAM_BOT_TOKEN || !env.TELEGRAM_CHAT_ID) throw new Error('Telegram setup is incomplete: configure TELEGRAM_BOT_TOKEN secret and TELEGRAM_CHAT_ID variable.');
  const telegram = new Telegram(env);
  const config = readConfig();
  if (process.argv[2] === 'start') return telegram.text(message(env, 'start', config));
  const report = fs.existsSync('.ci-report.json') ? JSON.parse(fs.readFileSync('.ci-report.json', 'utf8')) : {};
  const files = [];
  const directory = env.STAGE_RESULT === 'success' ? 'ci-delivery' : 'ci-diagnostics';
  if (fs.existsSync(directory)) {
    for (const file of fs.readdirSync(directory).sort()) {
      if (/^(SHA256SUMS|READ-ME)\.txt$/i.test(file)) continue;
      const source = path.join(directory, file);
      if (!fs.statSync(source).isFile()) continue;
      const parts = await splitFile(source, 'ci-telegram');
      files.push(...parts);
    }
  }
  const caption = message(env, 'finish', config, report);
  try {
    await telegram.deliver(files, caption);
    output('delivered', files.length > 0 && env.STAGE_RESULT === 'success');
    output('status', 'delivered');
    console.log(`Telegram confirmed delivery of ${files.length} file(s).`);
  } catch (error) {
    try { await telegram.text(message(env, 'finish', config, { ...report, error: `${error.message} Download artifacts from the workflow run if available.` })); } catch {}
    throw error;
  }
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(error => {
    output('delivered', false);
    output('status', 'failure');
    console.warn(`::warning::Telegram delivery failed: ${error.message.replace(/[\r\n]/g, ' ')}`);
    // The final summary decides whether another route preserved the build.
  });
}
