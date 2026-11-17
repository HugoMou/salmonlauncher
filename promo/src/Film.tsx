// Film.tsx — SalmonLauncher 宣传片 v3
// 355.2s / 108 BPM 两遍；160 小节（每小节 4 拍），切换全部压在小节线。
//
// 重点顺序：一键启动→进入游戏 > 内容生态（模组/光影/资源包/皮肤/联机）> 实机 > 极简与开源免费
// 全片无「无痕迹」表述。开头三词：一键。/ 全部功能。/ 开源免费。
import React from 'react';
import {AbsoluteFill, Audio, Easing, Img, Sequence, interpolate, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {AUDIO_FILE, CLIP_MARKS, CLIPS, CLIPS3, COLOR, FILM, FONT, LINKS, MARKS3, SHOTS, toF} from './spec';
import {BEAT_COUNT, PERIOD, beatFrame, hit} from './lib/beats';
import {Camera, ClickFx, LegalStrip, QRCard, RealProgress, Shot3D, Stage, Sub, Tag, VideoClip, beatPunch, clamp, easeOut} from './lib/fx';
import {
  BokehVignette, CursorTrail, ExtrudeText, LightSweep, ParticleField, ReflectFloor, RippleGrid,
  SplitCompare, SwatchExtractor, ZoomLens,
} from './lib/spectacle';
import {ChromaticEdge, GlowBands, WhipPan, ZoomBurst} from './lib/transitions';

// 缓动：让镜头运动带惯性，避免线性插值的生硬感
const easeSmooth = Easing.bezier(0.65, 0, 0.35, 1);
const easeSwoop = Easing.bezier(0.16, 1, 0.3, 1);

const BF = (i: number) => beatFrame(i, FILM.fps);
const stepDur = (beats: number) => Math.round(beats * PERIOD * FILM.fps);
const BAR = (i: number) => Math.round(i * 4 * PERIOD * FILM.fps); // 第 i 小节起始帧

const Pulse: React.FC<{x: number; y: number; at: number}> = ({x, y, at}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 24) return null;
  const s = interpolate(t, [0, 16], [0.2, 2.5], {extrapolateRight: 'clamp', easing: easeOut});
  const o = interpolate(t, [0, 2, 16], [0, 1, 0], clamp);
  return <div style={{position: 'absolute', left: x - 34, top: y - 34, width: 68, height: 68, borderRadius: 999,
    border: `4px solid ${COLOR.accent}`, opacity: o, transform: `scale(${s})`, boxShadow: `0 0 40px ${COLOR.accent}`, zIndex: 59}}/>;
};

const ClickPoint: React.FC<{x: number; y: number; at: number; label?: string}> = ({x, y, at, label}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 40) return null;
  return (
    <AbsoluteFill>
      <CursorTrail from={{x: x - 190, y: y - 110}} to={{x, y}} at={at} dur={stepDur(1.4)}/>
      <ClickFx x={x} y={y} at={at} label={label} size={1.2}/>
      <Pulse x={x} y={y} at={at + stepDur(1.6)}/>
    </AbsoluteFill>
  );
};

const Point: React.FC<{text: string; x: number; y: number; delay: number}> = ({text, x, y, delay}) => {
  const l = useCurrentFrame();
  const {fps} = useVideoConfig();
  const p = Math.max(0, Math.min(1, (l - delay) / 18));
  const o = interpolate(l, [delay, delay + 4], [0, 1], clamp);
  void fps;
  return (
    <div style={{position: 'absolute', left: x, top: y, opacity: o, transform: `translateX(${(1 - p) * 34}px)`, zIndex: 22}}>
      <span style={{color: COLOR.accent, fontWeight: 700, marginRight: 10, fontSize: 20}}>▸</span>
      <span style={{fontFamily: FONT, fontSize: 25, color: COLOR.ink, fontWeight: 600}}>{text}</span>
    </div>
  );
};

/** 通用场景：3D 屏（录屏/截图）+ 挤出标题 + 镜面地板 + 扫光 + 微距镜 */
const Scene: React.FC<{
  dur: number; head: string; sub?: string; caption?: string;
  clip?: {src: string; start?: number}; file?: string;
  cardW?: number; cardH?: number; cardY?: number;
  ry?: number; rx?: number; z?: number;
  camRy?: number; camRx?: number; camZ?: number;
  click?: {x: number; y: number; label: string; atRel: number};
  lens?: {x: number; y: number; r?: number; label?: string; atRel: number};
  points?: string[];
  progress?: {pct: number; label: string; detail?: string; atRel: number};
  deco?: boolean; closeup?: boolean; enterBars?: number; floor?: boolean;
}> = ({dur, head, sub, caption, clip, file, cardW = 1060, cardH, cardY = 470, ry = -12, rx = 6, z = 40,
      camRy = 0, camRx = 5, camZ = 120, click, lens, points, progress, deco = true, closeup = false,
      enterBars = 1, floor = true}) => {
  const l = useCurrentFrame();
  const entry = Math.max(6, stepDur(enterBars * 4));
  const camRyT = interpolate(l, [0, entry, dur], [camRy - 42, camRy, camRy + 22], {...clamp, easing: easeSmooth});
  const camRxT = interpolate(l, [0, entry, dur], [camRx + 16, camRx, camRx - 6], {...clamp, easing: easeSmooth});
  const camZT = interpolate(l, [0, entry, dur], [camZ - 620, camZ + 40, camZ - 30], {...clamp, easing: easeSwoop});
  const eZ = interpolate(l, [0, entry], [z - 900, z], {...clamp, easing: easeSwoop});
  const eRy = interpolate(l, [0, entry], [ry + 72, ry], {...clamp, easing: easeSmooth});
  const eRx = interpolate(l, [0, entry], [rx + 34, rx], {...clamp, easing: easeSmooth});
  const eS = interpolate(l, [0, entry], [0.34, 1.06], {...clamp, easing: easeSwoop});
  const ryT = interpolate(l, [entry, dur], [ry - 20, ry + 20], {...clamp, easing: easeSmooth});
  const rxT = interpolate(l, [entry, dur], [rx - 7, rx + 7], {...clamp, easing: easeSmooth});
  const punch = beatPunch(l, FILM.fps, PERIOD, 0.06);
  const isClose = closeup && l > dur * 0.55;
  const cw = isClose ? cardW * 1.24 : cardW;
  const cy = isClose ? cardY - 28 : cardY;
  const opacity = interpolate(l, [0, Math.max(1, entry / 2)], [0, 1], clamp);
  const pStart = entry + stepDur(1);
  return (
    <AbsoluteFill style={{opacity}}>
      <Camera ry={camRyT} rx={camRxT} tz={camZT} scale={punch}>
        <div style={{position: 'absolute', left: 0, right: 0, top: 96, textAlign: 'center', zIndex: 26}}>
          <ExtrudeText size={96} delay={0} tint="silver">{head}</ExtrudeText>
          {sub && <div style={{marginTop: 8}}><Sub delay={2} size={26} color={COLOR.dim} maxWidth={780}>{sub}</Sub></div>}
        </div>

        {floor && <ReflectFloor x={960 - cw / 2} y={cy + (cardH ?? cw * 0.68) - 30} w={cw} h={210} opacity={0.55}/>}

        {clip ? (
          <VideoClip src={clip.src} start={clip.start ?? 0} x={960 - cw / 2} y={cy} w={cw} h={cardH}
            delay={0} ry={l < entry ? eRy : ryT} rx={l < entry ? eRx : rxT} z={l < entry ? eZ : (isClose ? z - 120 : z)}
            floatAmp={10} scale={(l < entry ? eS : 1) * 1.02} mute/>
        ) : file ? (
          <Shot3D file={file} x={960 - cw / 2} y={cy} w={cw} h={cardH}
            delay={0} ry={l < entry ? eRy : ryT} rx={l < entry ? eRx : rxT} z={l < entry ? eZ : (isClose ? z - 120 : z)}
            floatAmp={12} scale={(l < entry ? eS : 1) * 1.02}/>
        ) : null}

        <LightSweep delay={0} periodBeats={8} strength={0.14}/>

        {deco && (
          <>
            <Shot3D file={SHOTS.versions} x={150} y={cardY - 210} w={300} delay={pStart} ry={-24} rx={10} z={-150} floatAmp={8}/>
            <Shot3D file={SHOTS.tasks}    x={1470} y={cardY - 210} w={290} delay={pStart + stepDur(1)} ry={24} rx={-8} z={-140} floatAmp={8}/>
            <Shot3D file={SHOTS.repoMods} x={210} y={cardY + 280} w={280} delay={pStart + stepDur(2)} ry={-14} rx={14} z={-165} floatAmp={7}/>
            <Shot3D file={SHOTS.online}   x={1500} y={cardY + 280} w={270} delay={pStart + stepDur(3)} ry={18} rx={-12} z={-150} floatAmp={7}/>
          </>
        )}

        {lens && <ZoomLens x={lens.x} y={lens.y} r={lens.r ?? 108} delay={pStart + stepDur(lens.atRel)} label={lens.label}/>}

        {points && points.map((t, i) => (
          <Point key={i} text={t} x={80} y={700 + i * 52} delay={pStart + i * stepDur(1)}/>
        ))}

        {caption && (
          <div style={{position: 'absolute', left: 80, top: dur > 0 ? 640 : 640, opacity: interpolate(l, [pStart, pStart + 6], [0, 1], clamp),
            fontFamily: FONT, fontSize: 21, color: COLOR.dim, letterSpacing: 0.5, zIndex: 20}}>{caption}</div>
        )}

        {progress && <RealProgress x={72} y={652} w={1320} pct={progress.pct} label={progress.label}
          detail={progress.detail} delay={pStart + stepDur(progress.atRel)}/>}

        {click && <ClickPoint x={click.x} y={click.y} at={click.atRel} label={click.label}/>}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 1) HOOK：0–9 小节（0–36 拍） ----------
const Hook: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const bar = l / (4 * PERIOD * 30);
  const g = CLIP_MARKS.game;
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(l, [0, dur], [-900, 260], {...clamp, easing: easeSwoop})}
        ry={interpolate(l, [0, dur / 2, dur], [-30, 18, -24], {...clamp, easing: easeSmooth})}
        rx={interpolate(l, [0, dur], [22, -8], {...clamp, easing: easeSmooth})}
        scale={beatPunch(l, FILM.fps, PERIOD, 0.12)}>
        {bar < 3 && (
          <>
            <ReflectFloor x={440} y={560} w={1040} h={200} opacity={0.6}/>
            <VideoClip src={CLIPS.game} start={g.menu} x={530} y={200} w={860} h={486} delay={0} ry={-12} rx={7} z={-30} floatAmp={10} scale={1.04}/>
          </>
        )}
        {bar >= 3 && bar < 5.5 && (
          <AbsoluteFill style={{display: 'flex', alignItems: 'center', justifyContent: 'center'}}>
            <ExtrudeText size={172} delay={0} tint="gold" depth={14} tilt={6}>一键。</ExtrudeText>
          </AbsoluteFill>
        )}
        {bar >= 5.5 && bar < 7.5 && (
          <AbsoluteFill style={{display: 'flex', alignItems: 'center', justifyContent: 'center'}}>
            <ExtrudeText size={156} delay={0} tint="blue" depth={14} tilt={6}>全部功能。</ExtrudeText>
          </AbsoluteFill>
        )}
        {(bar >= 1.5 && bar < 3) || (bar >= 7.5 && bar < 9) ? (
          [
            {x: 120, y: 140, w: 330, f: SHOTS.repoShaders},
            {x: 1470, y: 130, w: 320, f: SHOTS.repoMods},
            {x: 90, y: 620, w: 300, f: SHOTS.skins},
            {x: 1520, y: 610, w: 310, f: SHOTS.online},
            {x: 330, y: 820, w: 280, f: SHOTS.repoPacks},
            {x: 1320, y: 830, w: 280, f: SHOTS.instPacks},
          ].map((c, i) => (
            <div key={c.f} style={{position: 'absolute', left: c.x, top: c.y, width: c.w, transformStyle: 'preserve-3d',
              transform: `translateZ(${-220 + Math.sin(l / 18 + i) * 180}px) rotateY(${Math.sin(l / 14 + i) * 52}deg) rotateX(${Math.cos(l / 16 + i) * 16}deg) scale(${0.9 + Math.sin(l / 12 + i) * 0.16})`}}>
              <Shot3D file={c.f} x={0} y={0} w={c.w} delay={0} floatAmp={0} sweep/>
            </div>
          ))
        ) : null}
        {bar >= 9 && (
          <AbsoluteFill style={{display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center'}}>
            <ExtrudeText size={152} delay={0} tint="green" depth={14} tilt={6}>开源免费。</ExtrudeText>
            <div style={{marginTop: 18}}><Sub delay={2} size={30} color={COLOR.dim}>GPL-3.0-or-later · Windows x64</Sub></div>
          </AbsoluteFill>
        )}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 2) BRAND：9–16 小节 ----------
const Brand: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = Math.round(dur / 3);
  const idx = Math.min(2, Math.floor(l / seg));
  const items = [
    {file: SHOTS.home, head: '极简', sub: '一个启动面板：实例、账户、状态一目了然', pt: ['选好实例与账户即可开始']},
    {file: SHOTS.tasks, head: '一键启动', sub: '一个按钮完成启动、校验与进入游戏', pt: ['进度与状态实时可见']},
    {file: SHOTS.about, head: '开源免费', sub: 'GPL-3.0-or-later，Windows x64 免费使用', pt: ['源码与构建全部公开']},
  ];
  const it = items[idx];
  return (
    <Scene dur={seg} head={it.head} sub={it.sub} file={it.file} points={it.pt}
      cardW={1040} ry={idx % 2 ? 12 : -12} camRy={idx % 2 ? -9 : 9} camZ={130}
      lens={idx === 1 ? {x: 980, y: 560, r: 96, label: '2.2×', atRel: 2} : undefined}/>
  );
};

// ---------- 3) ONE-CLICK：16–40 小节（主线） ----------
const OneClick: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = Math.round(dur / 6);
  const idx = Math.min(5, Math.floor(l / seg));
  const local = l - idx * seg;
  const m = CLIP_MARKS.launch;
  const g = CLIP_MARKS.game;
  const stages = [
    {clip: CLIPS.launch, start: m.click, head: '打开即用', sub: '实例、账户、版本一目了然', pt: ['无需配置，直接进入'],
     pct: [0.02, 0.16], label: '准备启动', detail: '真实录屏 · 启动面板'},
    {clip: CLIPS.launch, start: m.click + 2.2, head: '点击启动', sub: '一个按钮，交给启动器', pt: ['点击「启动 Minecraft」'],
     pct: [0.16, 0.32], label: '启动准备', detail: '真实录屏 · 点击与响应',
     click: {x: 1150, y: 470, label: '启动 Minecraft', atRel: 1}},
    {clip: CLIPS.launch, start: m.downloading, head: '自动校验资源', sub: '首次启动自动补齐，逐文件校验', pt: ['分段下载 · 失败重试'],
     pct: [0.32, 0.94], label: '资源下载与校验', detail: '真实录屏 · 进度与速度实时刷新'},
    {clip: CLIPS.game, start: g.menu, head: '进入游戏', sub: '游戏窗口打开，直达主菜单', pt: ['窗口自动打开 · 无需手动'],
     pct: [0.94, 1], label: '启动完成', detail: '真实录屏 · Minecraft 主菜单', cardH: 620},
    {clip: CLIPS.game, start: g.move, head: '实机畅玩', sub: '进入世界后立即开玩', pt: ['启动器不参与游戏运行'],
     cardH: 620},
    {file: SHOTS.home, head: '不到一分钟', sub: '从点击到进入世界，全程无需手动干预', pt: ['一次点击，进入世界']},
  ];
  const s = stages[idx] as any;
  const pct = s.pct ? interpolate(local, [stepDur(3), seg - 4], s.pct as [number, number], clamp) : 0;
  return (
    <Scene dur={seg} head={s.head} sub={s.sub}
      clip={s.clip ? {src: s.clip, start: s.start} : undefined} file={s.file}
      cardH={s.cardH} cardW={1080} ry={idx % 2 ? 12 : -12} camRy={idx % 2 ? -9 : 9} camZ={130}
      click={s.click} points={s.pt} closeup={idx === 3}
      lens={idx === 2 ? {x: 1180, y: 620, r: 104, label: '2.2×', atRel: 1} : undefined}
      progress={s.pct ? {pct, label: s.label, detail: s.detail, atRel: 2} : undefined}/>
  );
};

// ---------- 4) CONTENT：40–80 小节（主卖点，10 段 ×4 小节） ----------
const Content: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = Math.round(dur / 10);
  const idx = Math.min(9, Math.floor(l / seg));
  const t = CLIP_MARKS.tour;
  const t2 = CLIP_MARKS.tour2;
  const m3 = MARKS3;
  const stages = [
    {clip: CLIPS.tour2, start: t2.repoMods, head: '模组仓库', sub: '集成 Modrinth：搜索、筛选、一键安装', pt: ['依赖与版本自动匹配'],
     lens: {x: 900, y: 300, r: 96, label: '2.2×', atRel: 1}},
    {clip: CLIPS3.shaderInstall, start: m3.shaderInstall.search, head: '光影仓库', sub: '搜索并安装光影包，画质立竿见影', pt: ['下载进度实时可见'],
     lens: {x: 1260, y: 420, r: 100, label: '2.2×', atRel: 1}},
    {clip: CLIPS3.packsSwitch, start: m3.packsSwitch.nav, head: '资源包仓库', sub: '纹理、音效、动画资源自由替换', pt: ['热门与搜索双模式']},
    {clip: CLIPS.tour, start: t.mods, head: '实例模组管理', sub: '按实例隔离，启用停用即时生效', pt: ['互不干扰 · 一键切换']},
    {clip: CLIPS3.skin3d, start: m3.skin3d.nav, head: '3D 皮肤预览', sub: '皮肤站集成，拖动即可 360° 查看', pt: ['所见即所得'],
     closeup: true},
    {clip: CLIPS3.multiplayer, start: m3.multiplayer.nav, head: '联机 · Campfire', sub: '进入世界即开放局域网，邀请码发给好友', pt: ['随包附带 · 无需端口映射']},
    {clip: CLIPS3.saves, start: m3.saves.nav, head: '存档管理', sub: '备份、恢复、重命名与自定义路径', pt: ['数据安全无忧']},
    {clip: CLIPS3.logs, start: m3.logs.scrollDown, head: '实例日志', sub: '实时读取、关键词过滤、崩溃报告解析', pt: ['问题定位更精准']},
    {clip: CLIPS3.overview, start: m3.overview.nav, head: '实例概览', sub: '版本、加载器、状态集中呈现', pt: ['快速切换与启动']},
    {file: SHOTS.instShaders, head: '内容生态', sub: '模组 · 光影 · 资源包 · 皮肤 · 联机，全部内置', pt: ['一个启动器，玩遍全部内容'], closeup: true},
  ];
  const s = stages[idx] as any;
  return (
    <Scene dur={seg} head={s.head} sub={s.sub}
      clip={s.clip ? {src: s.clip, start: s.start} : undefined} file={s.file}
      cardW={1100} ry={idx % 2 ? 13 : -13} camRy={idx % 2 ? -10 : 10} camZ={132}
      points={s.pt} closeup={s.closeup} lens={s.lens}
      progress={idx === 1 ? {pct: interpolate(l - idx * seg, [stepDur(2), seg - 4], [0.12, 1], clamp), label: '光影包下载与安装', detail: '真实录屏 · 进度实时刷新', atRel: 1} : undefined}/>
  );
};

// ---------- 5) CHARACTER：80–90 小节（主题工坊） ----------
const Character: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = Math.round(dur / 2);
  const m = CLIP_MARKS.theme;
  if (l < seg) {
    return (
      <Scene dur={seg} head="主题工坊" sub="从 Minecraft 风格画面取色，整套配色一键应用"
        clip={{src: CLIPS.theme, start: m.scroll}} cardW={1100} ry={-11}
        click={{x: 190, y: 620, label: '主题工坊', atRel: 2}}
        points={['壁纸自动提取配色']}
        lens={{x: 520, y: 560, r: 100, label: '色卡', atRel: 3}}/>
    );
  }
  return (
    <Scene dur={seg} head="一键整套主题" sub="壁纸 + 配色 + 强调色同时应用"
      file={SHOTS.theme} cardW={1100} ry={11} closeup
      points={['10 张内置场景 · 也支持本地图片']}/>
  );
};

// ---------- 6) COVERAGE：90–126 小节（12 段 ×3 小节快剪） ----------
const Coverage: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = Math.round(dur / 12);
  const idx = Math.min(11, Math.floor(l / seg));
  const m = CLIP_MARKS.onboarding;
  const i = CLIP_MARKS.install;
  const t = CLIP_MARKS.tour;
  const items = [
    {clip: CLIPS.onboarding, start: m.lang, head: '引导 · 语言'},
    {clip: CLIPS.onboarding, start: m.terms, head: '引导 · 条款'},
    {clip: CLIPS.onboarding, start: m.migrate, head: '引导 · 迁移'},
    {clip: CLIPS.onboarding, start: m.account, head: '引导 · 账户'},
    {clip: CLIPS.install, start: i.versions, head: '安装 · 版本'},
    {clip: CLIPS.install, start: i.loaders, head: '安装 · 加载器'},
    {clip: CLIPS.install, start: i.downloading, head: '安装 · 下载'},
    {clip: CLIPS.tour, start: t.global, head: '全局偏好'},
    {clip: CLIPS.tour, start: t.diag, head: '崩溃诊断'},
    {clip: CLIPS.tour, start: t.applog, head: '应用日志'},
    {clip: CLIPS.tour, start: t.terms, head: '用户协议'},
    {file: SHOTS.launchSet, head: '启动设置'},
  ];
  const it = items[idx] as any;
  return (
    <Scene dur={seg} head={it.head} sub="全功能覆盖 · 快速带过"
      clip={it.clip ? {src: it.clip, start: it.start} : undefined} file={it.file}
      cardW={1120} ry={idx % 2 ? 9 : -9} camRy={idx % 2 ? -7 : 7} camZ={128} deco={false} floor={false}/>
  );
};

// ---------- 7) MONTAGE：126–136 小节 ----------
const Montage: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const files = [SHOTS.home, SHOTS.tasks, SHOTS.versions, SHOTS.loaders, SHOTS.install, SHOTS.overview, SHOTS.mods, SHOTS.saves,
    SHOTS.instCfg, SHOTS.instLogs, SHOTS.repoMods, SHOTS.repoShaders, SHOTS.repoPacks, SHOTS.instPacks, SHOTS.instShaders,
    SHOTS.online, SHOTS.skins, SHOTS.diag, SHOTS.appLog, SHOTS.global, SHOTS.launchSet, SHOTS.theme, SHOTS.about, SHOTS.terms];
  const perCard = Math.floor(dur / files.length);
  const cardW = 306, gap = 16, cols = 4;
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(l, [0, dur], [-560, 220], clamp)} ry={interpolate(l, [0, dur], [-26, 26], clamp)} rx={8}
        scale={beatPunch(l, 30, PERIOD, 0.09)}>
        <div style={{position: 'absolute', left: 88, top: 78, zIndex: 30}}>
          <Tag text="功能全景 · 快速剪辑" accent/>
          <div style={{marginTop: 12}}><ExtrudeText size={72} delay={1} align="left" tint="silver">全部功能，一应俱全。</ExtrudeText></div>
        </div>
        {files.map((file, i) => {
          const d = i * perCard;
          const row = Math.floor(i / cols), col = i % cols;
          const p = interpolate(l, [d, d + 14], [0, 1], clamp);
          return (
            <div key={file + i} style={{position: 'absolute', left: col * (cardW + gap), top: 250 + row * (220 + gap),
              width: cardW, height: 220, transformStyle: 'preserve-3d',
              transform: `translateZ(${(1 - p) * -640}px) rotateY(${(col - 1.5) * 6}deg) rotateX(${(1 - p) * 34}deg) scale(${(0.72 + p * 0.28) * beatPunch(l - d, 30, PERIOD, 0.06)})`,
              opacity: interpolate(l, [d, d + 3], [0, 1], clamp)}}>
              <div style={{width: '100%', height: '100%', borderRadius: 12, overflow: 'hidden',
                border: `1px solid ${COLOR.line}`, boxShadow: '0 22px 60px rgba(0,0,0,0.78)'}}>
                <Img src={staticFile(`shots-v3/${file}`)} style={{width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top left'}}/>
              </div>
            </div>
          );
        })}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 8) CLOSE：136–148 小节 ----------
const Close: React.FC<{dur: number}> = ({dur}) => {
  const l = useCurrentFrame();
  const seg = Math.round(dur / 4);
  const idx = Math.min(3, Math.floor(l / seg));
  const lines: {t: string; tint: 'silver' | 'blue' | 'gold' | 'green'}[] = [
    {t: '极简。', tint: 'silver'},
    {t: '一键启动。', tint: 'green'},
    {t: '全部功能。', tint: 'gold'},
    {t: 'SalmonLauncher', tint: 'silver'},
  ];
  const cur = lines[idx];
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{background: 'radial-gradient(ellipse 70% 50% at 50% 45%, rgba(126,217,87,0.16), transparent 70%)'}}/>
      <Camera tz={interpolate(l, [0, dur], [-280, 90], clamp)} rx={interpolate(l, [0, dur], [6, 0], clamp)}
        ry={interpolate(l, [0, dur], [-10, 10], clamp)} scale={beatPunch(l, 30, PERIOD, 0.05)}>
        <AbsoluteFill style={{display: 'flex', alignItems: 'center', justifyContent: 'center'}}>
          <ExtrudeText size={idx === 3 ? 118 : 168} delay={0} tint={cur.tint} depth={12} tilt={5}>{cur.t}</ExtrudeText>
        </AbsoluteFill>
        {idx === 3 && (
          <div style={{position: 'absolute', left: 0, right: 0, bottom: 170, textAlign: 'center'}}>
            <Sub delay={2} size={26} color={COLOR.dim}>{LINKS.siteFull}　·　{LINKS.githubFull}</Sub>
          </div>
        )}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 9) OUTRO：148–160 小节 ----------
const Outro: React.FC<{total: number}> = ({total}) => {
  const l = useCurrentFrame();
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{background: 'radial-gradient(ellipse 75% 55% at 50% 34%, rgba(126,217,87,0.18), transparent 70%)'}}/>
      <Camera tz={interpolate(l, [0, Math.min(total, stepDur(16))], [-160, 50], clamp)}
        rx={interpolate(l, [0, total], [4, 0], clamp)} scale={beatPunch(l, 30, PERIOD, 0.04)}>
        <div style={{position: 'absolute', left: 0, right: 0, top: 52, textAlign: 'center'}}>
          <ExtrudeText size={92} delay={0} tint="silver">SalmonLauncher</ExtrudeText>
          <div style={{marginTop: 8}}><Sub delay={3} size={29} color={COLOR.dim}>把复杂留给启动器，把时间留给游戏。</Sub></div>
        </div>
        <QRCard file="site.png" title="官方网站" url={LINKS.site} x={300} y={240} delay={4} size={168}/>
        <QRCard file="github.png" title="GitHub 仓库" url={LINKS.github} x={700} y={240} delay={7} size={168}/>
        <QRCard file="blog.png" title="作者博客" url={LINKS.blog} x={1100} y={240} delay={10} size={168}/>

        <div style={{position: 'absolute', left: 90, right: 90, bottom: 92, padding: '15px 22px', borderRadius: 16,
          background: 'rgba(20,20,22,0.84)', border: `1px solid ${COLOR.line}`,
          opacity: interpolate(l, [12, 20], [0, 1], clamp), transform: `translateY(${interpolate(l, [12, 20], [22, 0], clamp)}px)`}}>
          <div style={{fontFamily: FONT, fontSize: 18, color: COLOR.ink, fontWeight: 700, marginBottom: 6}}>视频声明 · 制作人员 · 致谢名单 · 版权解释</div>
          <div style={{fontFamily: FONT, fontSize: 15, color: COLOR.dim, lineHeight: 1.6}}>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>制作人员：</span>Hugo Mou（产品设计 / 开发 / 维护）　<span style={{color: COLOR.ink, fontWeight: 600}}>致谢：</span>Modrinth 与社区作者 · Campfire 联机组件 · .NET / Windows App SDK / WinUI 3 · Mojang 官方版本与资源生态</div>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>配乐与素材：</span>“3 Strikes” — Terror Jr（Bilibili BV1tek2YsEYY，仅作展示） · 画面为 SalmonLauncher 2.0.15 真实录屏与界面截图</div>
            <div><span style={{color: COLOR.accent, fontWeight: 700}}>本软件由 AI 辅助开发制作　·　本宣传片由 AI 生成制作</span></div>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>项目许可：</span>GPL-3.0-or-later（开源免费，Windows x64）　官网 {LINKS.siteFull}　GitHub {LINKS.githubFull}</div>
            <div style={{marginTop: 4, fontSize: 13.5, color: 'rgba(134,134,139,0.92)'}}>版权与声明：Minecraft 为 Mojang Studios / Microsoft 的商标，本项目与 Mojang/Microsoft 无官方关联；Modrinth、Campfire 等名称与服务归各自权利人所有。本视频仅用于 SalmonLauncher 产品展示与介绍，不构成对第三方商标或内容的授权声明。</div>
          </div>
        </div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 时间线（小节对齐） ----------
export const Film: React.FC = () => {
  const bars = (startBar: number, bars: number) => ({from: BAR(startBar), dur: Math.round(bars * 4 * PERIOD * FILM.fps)});
  const S = {
    hook:      bars(0, 9),      // 0–9
    brand:     bars(9, 7),      // 9–16
    oneclick:  bars(16, 24),    // 16–40
    content:   bars(40, 40),    // 40–80
    character: bars(80, 10),    // 80–90
    coverage:  bars(90, 36),    // 90–126
    montage:   bars(126, 10),   // 126–136
    close:     bars(136, 12),   // 136–148
  };
  const outroFrom = BAR(148);
  const outroDur = Math.max(1, toF(FILM.duration) - outroFrom);

  const f = useCurrentFrame();
  const t = f / FILM.fps;
  let flash = 0;
  for (let i = 0; i < BEAT_COUNT; i += 1) {
    const down = i % 4 === 0;
    const d = down ? 0.26 : i % 2 === 0 ? 0.13 : 0.06;
    const w = down ? 0.1 : 0.06;
    flash = Math.max(flash, hit(t, i, w) * d);
  }
  const cuts = [S.brand.from, S.oneclick.from, S.content.from, S.character.from, S.coverage.from, S.montage.from, S.close.from, outroFrom];
  // 每个切点的甩镜方向交替，制造「连续甩动」的流动感
  const whipFrames = Math.round(FILM.fps * 0.18);

  return (
    <Stage>
      <Audio src={staticFile(AUDIO_FILE)}/>
      {/* 背景：粒子 + 扫光带，让画面始终在动 */}
      <ParticleField count={260} energy={1.1}/>
      <GlowBands speed={1.4} opacity={0.2}/>

      <Sequence from={S.hook.from}      durationInFrames={S.hook.dur}><Hook dur={S.hook.dur}/></Sequence>
      <Sequence from={S.brand.from}     durationInFrames={S.brand.dur}><Brand dur={S.brand.dur}/></Sequence>
      <Sequence from={S.oneclick.from}  durationInFrames={S.oneclick.dur}><OneClick dur={S.oneclick.dur}/></Sequence>
      <Sequence from={S.content.from}   durationInFrames={S.content.dur}><Content dur={S.content.dur}/></Sequence>
      <Sequence from={S.character.from} durationInFrames={S.character.dur}><Character dur={S.character.dur}/></Sequence>
      <Sequence from={S.coverage.from}  durationInFrames={S.coverage.dur}><Coverage dur={S.coverage.dur}/></Sequence>
      <Sequence from={S.montage.from}   durationInFrames={S.montage.dur}><Montage dur={S.montage.dur}/></Sequence>
      <Sequence from={S.close.from}     durationInFrames={S.close.dur}><Close dur={S.close.dur}/></Sequence>
      <Sequence from={outroFrom}        durationInFrames={outroDur}><Outro total={outroDur}/></Sequence>

      {/* 夸张转场：甩镜 + 推镜爆闪 + 涟漪，叠加在切点两侧 */}
      {cuts.map((c, i) => (
        <React.Fragment key={`cut-${i}`}>
          <WhipPan at={c} frames={whipFrames} dir={i % 2 === 0 ? 1 : -1} strength={1.5}/>
          <ZoomBurst at={c} frames={Math.round(FILM.fps * 0.2)} strength={1.2}/>
          <RippleGrid x={960} y={540} at={c} count={3}/>
        </React.Fragment>
      ))}

      <ChromaticEdge strength={1.2}/>
      <BokehVignette strength={1.0}/>
      {flash > 0.001 && <AbsoluteFill style={{backgroundColor: '#fff', opacity: flash * 0.3, mixBlendMode: 'screen', pointerEvents: 'none'}}/>}
      <AbsoluteFill style={{boxShadow: 'inset 0 0 220px rgba(0,0,0,0.6)', pointerEvents: 'none'}}/>
      <LegalStrip/>
    </Stage>
  );
};
