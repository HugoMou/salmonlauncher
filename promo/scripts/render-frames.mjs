import {bundle} from '@remotion/bundler';
import {getCompositions, renderStill} from '@remotion/renderer';
import path from 'path';
import fs from 'fs';

const entry = path.resolve('src/index.ts');
console.log('bundling...');
const serveUrl = await bundle({entryPoint: entry, webpackOverride: (c) => c});
const comps = await getCompositions(serveUrl, {inputProps: {}});
const comp = comps.find((c) => c.id === 'Film');
if (!comp) throw new Error('Film composition not found');
const outDir = path.resolve('out/frames');
fs.mkdirSync(outDir, {recursive: true});
console.log('total', comp.durationInFrames);
for (let f = 0; f < comp.durationInFrames; f++) {
  const out = path.join(outDir, `element-${String(f).padStart(4, '0')}.jpeg`);
  if (fs.existsSync(out) && fs.statSync(out).size > 0) { if (f % 200 === 0) console.log('skip', f); continue; }
  await renderStill({
    composition: comp,
    serveUrl,
    output: out,
    frame: f,
    imageFormat: 'jpeg',
    jpegQuality: 92,
  });
  if (f % 50 === 0 || f === comp.durationInFrames - 1) console.log('rendered', f);
}
console.log('done');
