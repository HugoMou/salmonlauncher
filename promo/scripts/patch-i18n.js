// patch-i18n.js — 修正 i18n.js 中 2.0.15 新增文案的日文翻译与换行
const fs = require('fs');
const p = 'D:/SalmonLauncher/web/assets/js/i18n.js';
let s = fs.readFileSync(p, 'utf8');

// 1) 修复被合并到一行的日文条目
s = s.replace(
  '安裝新版後才會提示下一個新版本。",    "本版本同时提供安装包与免安装便携版。"',
  '安裝新版後才會提示下一個新版本。",\n    "本版本同时提供安装包与免安装便携版。"'
);

// 2) 把误插入到日文表的繁中条目改为日文
const hantBlock =
  '    "更新提示不再反复弹出。": "更新提示不再反覆彈出。",\n' +
  '    "启动时的「发现新版本」提示此前只判断线上是否有更新版本，没有记录是否已经提示过；装上旧版而线上已有新版时，每次打开都会弹一次。": "啟動時的「發現新版本」提示先前只判斷線上是否有更新版本，沒有記錄是否已經提示過；裝上舊版而線上已有新版時，每次開啟都會彈一次。",\n' +
  '    "现在会记录已提示过的发行版本：同一版本只提醒一次，安装新版后才会提示下一个新版本。": "現在會記錄已提示過的發行版本：同一版本只提醒一次，安裝新版後才會提示下一個新版本。",';

const jaBlock =
  '    "更新提示不再反复弹出。": "更新通知が繰り返し表示されなくなりました。",\n' +
  '    "启动时的「发现新版本」提示此前只判断线上是否有更新版本，没有记录是否已经提示过；装上旧版而线上已有新版时，每次打开都会弹一次。": "起動時の「新しいバージョンがあります」通知は、以前はオンラインに新しい版があるかどうかだけを判定し、通知済みかどうかを記録していませんでした。古い版を使い続けていると起動のたびに表示されていました。",\n' +
  '    "现在会记录已提示过的发行版本：同一版本只提醒一次，安装新版后才会提示下一个新版本。": "通知済みのリリースタグを記録するようになり、同じバージョンは 1 回だけ通知されます。次の通知はさらに新しいリリースが出た後にだけ表示されます。",';

// 只替换日文表内的那一处（日文表从 "ja": { 开始）
const jaStart = s.indexOf('"ja": {');
if (jaStart < 0) throw new Error('ja table not found');
const before = s.slice(0, jaStart);
const after = s.slice(jaStart);
if (after.includes(hantBlock)) {
  s = before + after.replace(hantBlock, jaBlock);
} else {
  console.log('WARN: hant block not found in ja table');
}

// 3) 日文表的更新日志 meta 改为日文
s = s.replace(
  '"SalmonLauncher 版本更新日志：2.0.15 更新提示只提醒一次，以及 2.0 系列的功能变更与历史版本。": "SalmonLauncher 版本更新日誌：2.0.15 更新提示只提醒一次，以及 2.0 系列的功能變更與歷史版本。",',
  '"SalmonLauncher 版本更新日志：2.0.15 更新提示只提醒一次，以及 2.0 系列的功能变更与历史版本。": "SalmonLauncher のリリースノート：2.0.15 で更新通知を 1 回に制限、2.0 シリーズの変更点と過去バージョン。",'
);

fs.writeFileSync(p, s, 'utf8');

// 校验语法
const src = fs.readFileSync(p, 'utf8');
new Function('window', src);
console.log('i18n.js 语法 OK');
console.log('Hant 条目数 =', (src.match(/不再反覆彈出/g) || []).length);
console.log('Ja 条目数 =', (src.match(/更新通知が繰り返し/g) || []).length);
