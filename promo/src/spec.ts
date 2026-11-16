// spec.ts — SalmonLauncher promo, Apple-keynote energy, 355.2s synced to "3 Strikes" played twice (108 BPM).
// Real launcher screenshots from clean default state (v2.0.13, Mica off, no personal traces).
import {loadFont} from '@remotion/google-fonts/Inter';
import {BEAT_COUNT, BPM, DURATION, PERIOD, PHASE} from './lib/beats';

const inter = loadFont('normal', {weights: ['400','500','600','700','800','900'], subsets: ['latin']});

export const AUDIO_FILE = 'audio/track_double.m4a';
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

// 真实操作录屏（v2 素材，按时间点剪辑）
export const CLIPS = {
  onboarding: 'clips/01-onboarding.mp4',
  install: 'clips/02-install.mp4',
  launch: 'clips/03-launch.mp4',
  game: 'clips/04-game.mp4',
  tour: 'clips/05-tour.mp4',
  tour2: 'clips/06-tour2.mp4',
  theme: 'clips/07-theme.mp4',
} as const;

// v3 新增动态演示素材（真实点击/滚动/安装/进度）
export const CLIPS3 = {
  shaderInstall: 'clips-v3/09-shader-install.mp4',
  packsSwitch: 'clips-v3/12-packs-switch.mp4',
  skin3d: 'clips-v3/14-skin3d.mp4',
  multiplayer: 'clips-v3/15-room-create.mp4',
  saves: 'clips-v3/16-saves-backup.mp4',
  logs: 'clips-v3/17-logs-scroll.mp4',
  overview: 'clips-v3/19-instance-toggles.mp4',
} as const;

// v3 片段打点（秒，来自录制脚本日志）
export const MARKS3 = {
  shaderInstall: {nav: 3.7, type: 6.0, search: 10.0, install: 10.7, wait: 23.4},
  packsSwitch: {nav: 3.6, install: 6.6, wait: 19.3},
  skin3d: {nav: 5.5, inspect: 8.1},
  multiplayer: {nav: 3.0, click: 5.5, confirm: 7.4},
  saves: {nav: 3.6, inspect: 6.4},
  logs: {nav: 3.6, scrollDown: 6.8, scrollUp: 8.4},
  overview: {nav: 3.7, inspect: 6.4},
} as const;

// 录屏关键时间点（秒），来自录制脚本的打点日志
export const CLIP_MARKS = {
  // 01 引导：选语言 2.2 / 条款 4.9 / 迁移 7.3 / 账户 9.6 / 主题 12.1 / 完成 16.1
  onboarding: {lang: 1.6, terms: 4.4, migrate: 7.0, account: 9.3, theme: 11.8, done: 15.5},
  // 02 安装：选版本 4.9 / 加载器 7.3 / 安装概览 9.6 / 开始安装 12.1（之后为真实下载进度）
  install: {versions: 2.0, pick: 4.8, loaders: 7.3, plan: 9.6, downloading: 13.5, progress: 30.0},
  // 03 启动：点击启动 2.1 / 定位 Java 与资源下载 / 66.4 游戏进程起来
  launch: {click: 1.8, preparing: 6.0, downloading: 20.0, verifying: 52.0, started: 66.0},
  // 04 游戏：主菜单光标移动
  game: {menu: 1.0, move: 4.0},
  // 05 巡览：各页面停留点（每页约 2.6 秒）
  tour: {tasks: 1.6, overview: 6.8, mods: 9.4, saves: 12.0, cfg: 14.7, logs: 17.3, versions: 19.9, online: 50.8, global: 53.3, diag: 55.9, applog: 58.4, theme: 60.9, skins: 63.6, about: 66.1, terms: 68.7},
  tour2: {loaders: 2.1, plan: 5.4, repoMods: 8.6, shaders: 12.0, packs: 15.4, instPacks: 18.8, instShaders: 22.0},
  // 06 主题工坊：滚动侧边栏 0.9 / 点击入口 2.8 / 鼠标扫过场景 9.2 / 应用草原清晨 10.5
  theme: {scroll: 0.9, click: 2.8, hover: 9.2, apply: 10.5},
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
