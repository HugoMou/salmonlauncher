const fs=require('fs');const {PNG}=require('pngjs');const jsQR=require('jsqr');
const png=PNG.sync.read(fs.readFileSync('out/qtest/outro.png'));
console.log('frame', png.width+'x'+png.height);
// 三个二维码在帧内的近似位置（左/中/右），裁剪后解码
const regions=[
  {name:'site',  x:230, y:200, w:300, h:300},
  {name:'github',x:670, y:200, w:300, h:300},
  {name:'blog',  x:1110,y:200, w:300, h:300},
];
for(const r of regions){
  const crop=new PNG({width:r.w,height:r.h});
  for(let y=0;y<r.h;y++){
    for(let x=0;x<r.w;x++){
      const si=((png.width*(r.y+y))+(r.x+x))<<2;
      const di=((r.w*y)+x)<<2;
      crop.data[di]=png.data[si];crop.data[di+1]=png.data[si+1];crop.data[di+2]=png.data[si+2];crop.data[di+3]=255;
    }
  }
  const res=jsQR(new Uint8ClampedArray(crop.data),crop.width,crop.height,{inversionAttempts:'dontInvert'});
  console.log(r.name.padEnd(7), res? 'SCAN OK -> '+JSON.stringify(res.data) : 'SCAN FAILED');
  fs.writeFileSync(`out/qtest/${r.name}-crop.png`, PNG.sync.write(crop));
}
