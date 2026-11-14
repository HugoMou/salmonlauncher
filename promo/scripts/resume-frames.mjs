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
const outDir = path.resolve('out/fullseq');
fs.mkdirSync(outDir, {recursive: true});
const total = comp.durationInFrames;
console.log('total', total);
let done = 0;
for (let f = 0; f < total; f++) {
  const out = path.join(outDir, `element-${String(f).padStart(4, '0')}.jpeg`);
  if (fs.existsSync(out) && fs.statSync(out).size > 1000) { done++; continue; }
  await renderStill({composition: comp, serveUrl, output: out, frame: f, imageFormat: 'jpeg', jpegQuality: 90});
  done++;
  if (f % 100 === 0 || f === total - 1) console.log('rendered', f, 'progress', done, '/', total);
}
console.log('sequence complete');
