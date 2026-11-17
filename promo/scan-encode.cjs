const fs=require('fs');const {PNG}=require('pngjs');const jsQR=require('jsqr');
function scan(file){
  const png=PNG.sync.read(fs.readFileSync(file));
  const regions=[{n:'site',x:230,y:200},{n:'github',x:670,y:200},{n:'blog',x:1110,y:200}];
  const out=[];
  for(const r of regions){
    const W=300,H=300, crop=new PNG({width:W,height:H});
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const si=((png.width*(r.y+y))+(r.x+x))<<2, di=((W*y)+x)<<2;
      crop.data[di]=png.data[si];crop.data[di+1]=png.data[si+1];crop.data[di+2]=png.data[si+2];crop.data[di+3]=255;
    }
    const res=jsQR(new Uint8ClampedArray(crop.data),W,H,{inversionAttempts:'dontInvert'});
    out.push(r.n+'='+(res?'OK':'FAIL'));
  }
  console.log(file.padEnd(24), out.join('  '));
}
scan('out/qtest/outro.png');
scan('out/qtest/from-video.png');
const jpg=PNG.sync.read(fs.readFileSync('out/qtest/outro.png')); // 仅作对照
