// spec.ts — SalmonLauncher promo, Apple-keynote energy, 177.6s synced to "3 Strikes" (108 BPM).
// Real launcher screenshots from clean default state (v2.0.13, Mica off, no personal traces).
import {loadFont} from '@remotion/google-fonts/Inter';
import {BEAT_COUNT, BPM, DURATION, PERIOD, PHASE} from './lib/beats';

const inter = loadFont('normal', {weights: ['400','500','600','700','800','900'], subsets: ['latin']});

export const AUDIO_FILE = 'audio/track.m4a';
export const AUDIO_DURATION = DURATION;
export {BPM, PERIOD, PHASE, BEAT_COUNT};

export const FILM = {duration: DURATION, fps: 30, width: 1920, height: 1080};

export const COLOR = {
  bg: '#000000',
  ink: '#F5F5F7',
  dim: '#86868B',
  accent: '#7ED957',
  accent2: '#5BC236',
  cyan: '#64D2FF',
  blue: '#0A84FF',
  gold: '#FFD60A',
  line: 'rgba(255,255,255,0.14)',
  glass: 'rgba(255,255,255,0.07)',
};

export const FONT = `'${inter.fontFamily}', 'SF Pro Display', 'Microsoft YaHei', 'PingFang SC', 'Noto Sans SC', sans-serif`;

export const LINKS = {
  site: 'sl.hugomou.com',
  siteFull: 'https://sl.hugomou.com',
  github: 'github.com/HugoMou/salmonlauncher',
  githubFull: 'https://github.com/HugoMou/salmonlauncher',
  blog: 'www.hugomou.com',
  blogFull: 'https://www.hugomou.com',
};

export const SHOTS = {
  wLang: '01-welcome-language.png',
  wTerms: '02-welcome-terms.png',
  wMigrate: '03-welcome-migrate.png',
  wAccount: '04-welcome-account.png',
  wTheme: '05-welcome-theme.png',
  wDone: '06-welcome-done.png',
  home: '07-home.png',
  tasks: '08-tasks.png',
  versions: '09-versions.png',
  loaders: '10-loaders.png',
  install: '11-install.png',
  overview: '12-overview.png',
  mods: '13-mods.png',
  saves: '14-saves.png',
  instCfg: '15-instance-settings.png',
  instLogs: '16-instance-logs.png',
  repoMods: '17-modrinth-mods.png',
  repoShaders: '18-shaders.png',
  repoPacks: '19-resourcepacks.png',
  instPacks: '20-instance-packs.png',
  instShaders: '21-instance-shaders.png',
  online: '22-online.png',
  skins: '23-skins.png',
  diag: '24-diagnostics.png',
  appLog: '25-app-log.png',
  global: '26-global-settings.png',
  launchSet: '27-launch-settings.png',
  theme: '28-theme.png',
  about: '29-about.png',
  terms: '30-terms.png',
} as const;

export const CREDITS = {
  maker: '制作人员：Hugo Mou（产品设计 / 开发 / 维护）',
  thanks: '致谢名单：Modrinth 内容平台与社区作者 · Campfire 联机组件 · .NET / Windows App SDK / WinUI 3 · Mojang 官方版本与资源生态',
  music: '配乐：“3 Strikes” — Terror Jr（来源：Bilibili BV1tek2YsEYY，本片仅作展示）',
  assets: '画面素材：SalmonLauncher 2.0.13 真实界面截图（全新默认状态采集，云母默认关闭，无个人使用痕迹）',
  license: '项目许可：GPL-3.0-or-later（开源免费，Windows x64）',
  blog: '作者博客：www.hugomou.com（可阅读 SalmonLauncher 相关文章，在文章下提建议，也可通过博客联系作者）',
  disclaimer:
    '版权与声明：Minecraft 为 Mojang Studios / Microsoft 的商标，本项目与 Mojang/Microsoft 无官方关联；Modrinth、Campfire 等名称与服务归各自权利人所有。本视频仅用于 SalmonLauncher 产品展示与介绍。',
};

export const toF = (sec: number) => Math.round(sec * FILM.fps);
