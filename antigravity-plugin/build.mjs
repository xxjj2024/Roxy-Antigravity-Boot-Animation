import { build } from 'esbuild';
import fs from 'node:fs';
import path from 'node:path';

const assetFiles = {
  closed: 'assets_src/roxy_closed.webp',
  open: 'assets_src/roxy_open.webp',
  climax: 'assets_src/roxy_climax.webp',
  line: 'assets_src/roxy_line.webp',
  neon: 'assets_src/roxy_neon.webp',
};

const assetMap = {};
for (const [key, rel] of Object.entries(assetFiles)) {
  const buf = fs.readFileSync(path.resolve(rel));
  assetMap[key] = `data:image/webp;base64,${buf.toString('base64')}`;
}

const chantMp3Path = path.resolve('assets_src/roxy_chant.mp3');
if (fs.existsSync(chantMp3Path)) {
  const mp3Buf = fs.readFileSync(chantMp3Path);
  assetMap.chantAudio = `data:audio/mpeg;base64,${mp3Buf.toString('base64')}`;
}

const timingPath = path.resolve('assets_src/chant_timing.json');
if (fs.existsSync(timingPath)) {
  assetMap.chantTiming = JSON.parse(fs.readFileSync(timingPath, 'utf8'));
}

const assetScript = `window.__ROXY_CELESTE__=${JSON.stringify(assetMap)};`;

const out = await build({
  entryPoints: ['src/main.js'],
  bundle: true,
  format: 'iife',
  minify: true,
  write: false,
  target: 'es2020',
  legalComments: 'none',
});
const js = out.outputFiles[0].text.replace(/<\/script/gi, '<\\/script');

let html = fs.readFileSync('src/intro.html', 'utf8');
html = html.replace('/*__ASSETS__*/', () => assetScript);
html = html.replace('/*__BUNDLE__*/', () => js);

fs.writeFileSync('intro.html', html);
console.log(`intro.html  ${(html.length / 1024).toFixed(1)} KiB (${(html.length / 1048576).toFixed(2)} MiB)`);
