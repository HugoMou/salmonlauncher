const fs=require('fs');const {PNG}=require('pngjs');const jsQR=require('jsqr');
for(const f of ['site','github','blog']){
  const p=`public/qr/${f}.png`;
  const png=PNG.sync.read(fs.readFileSync(p));
  const res=jsQR(new Uint8ClampedArray(png.data),png.width,png.height);
  console.log(f.padEnd(8), png.width+'x'+png.height, res? JSON.stringify(res.data) : 'DECODE FAILED');
}
