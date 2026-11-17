const QR = require('qrcode');
const fs = require('fs');
const targets = {
  site: 'https://sl.hugomou.com',
  github: 'https://github.com/HugoMou/salmonlauncher',
  blog: 'https://www.hugomou.com',
};
(async () => {
  for (const [name, url] of Object.entries(targets)) {
    // 1024px、纯黑白、quiet zone 4 模块、最高纠错等级 H（被压缩/轻微模糊也能扫）
    await QR.toFile(`public/qr/${name}.png`, url, {
      type: 'png',
      width: 1024,
      margin: 4,
      errorCorrectionLevel: 'H',
      color: {dark: '#000000ff', light: '#ffffffff'},
    });
    const st = fs.statSync(`public/qr/${name}.png`);
    console.log(name.padEnd(8), url, st.size + ' bytes');
  }
})();
