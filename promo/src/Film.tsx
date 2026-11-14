// Film.tsx — SalmonLauncher promo, Apple-keynote energy, 355.2s tightly synced to "3 Strikes" played twice (108 BPM).
// Apple keynote grammar: black stage with layered ambient glow, huge metal headline, ONE centered floating rotating 3D product card per chapter,
// surround deco cards forming a product matrix, camera fly-in entry & define-then-punch hold, minimal copy, generous negative space.
import React from 'react';
import {AbsoluteFill, Audio, Img, Sequence, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {AUDIO_FILE, COLOR, CREDITS, FILM, FONT, LINKS, SHOTS, toF} from './spec';
import {BEAT_COUNT, PERIOD, beatFrame, hit} from './lib/beats';
import {Big, Camera, ClickFx, LegalStrip, MetalText, Panel, QRCard, ScanLine, Shot3D, Slam, Stage, Sub, Tag, beatPunch, clamp, easeOut, snap} from './lib/fx';

const BF = (i: number) => beatFrame(i, FILM.fps);
const stepDur = (beats: number) => Math.round(beats * PERIOD * FILM.fps);

// --- micro overlays ---
const Cursor: React.FC<{x1: number; y1: number; x2: number; y2: number; at: number; dur: number}> = ({x1, y1, x2, y2, at, dur}) => {
  const l = useCurrentFrame();
  const p = interpolate(l, [at, at + dur], [0, 1], {...clamp, easing: easeOut});
  const cx = x1 + (x2 - x1) * p;
  const cy = y1 + (y2 - y1) * p;
  const o = interpolate(l, [at - 2, at, at + dur, at + dur + 10], [0, 1, 1, 0], clamp);
  return (
    <div style={{position: 'absolute', left: cx, top: cy, opacity: o, transform: `translate(-2px,-2px)`, filter: `drop-shadow(0 0 10px ${COLOR.accent})`}}>
      <svg width="38" height="46" viewBox="0 0 38 46" fill="none">
        <path d="M2 2L2 38L11 29L17 43L22 41L16 27L28 27L2 2Z" fill={COLOR.accent} stroke="#000" strokeWidth="2" strokeLinejoin="round"/>
      </svg>
    </div>
  );
};

const Pulse: React.FC<{x: number; y: number; at: number; c?: string}> = ({x, y, at, c = COLOR.accent}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 26) return null;
  const s = interpolate(t, [0, 18], [0.2, 2.6], {extrapolateRight: 'clamp', easing: easeOut});
  const o = interpolate(t, [0, 2, 18], [0, 1, 0], clamp);
  return (
    <div style={{position: 'absolute', left: x - 34, top: y - 34, width: 68, height: 68, borderRadius: 999, border: `4px solid ${c}`, opacity: o, transform: `scale(${s})`, boxShadow: `0 0 40px ${c}`}} />
  );
};

// 用户点击按钮：光标移动 → 点击光环 → 标签
const ClickPoint: React.FC<{x:number;y:number;at:number;label?:string}> = ({x,y,at,label}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 40) return null;
  const flash = interpolate(t, [0, 8, 16], [0, 0.3, 0], clamp);
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{position:'absolute',left:0,top:0,width:'100%',height:'100%',backgroundColor:'#fff',opacity:flash,mixBlendMode:'screen',pointerEvents:'none'}} />
      <Cursor x1={x-120} y1={y-60} x2={x} y2={y} at={at} dur={stepDur(1)} />
      <ClickFx x={x} y={y} at={at+stepDur(1)} label={label} size={1.15} />
      <Pulse x={x} y={y} at={at+stepDur(2)} />
    </AbsoluteFill>
  );
};

// 浮动旋转 3D 剪贴图：作为环绕装饰，随拍点翻转与上下浮动
const FloatCard: React.FC<{file:string;x:number;y:number;w:number;baseBeat:number;ryBase?:number;rxBase?:number;z?:number}> = ({file,x,y,w,baseBeat,ryBase=0,rxBase=5,z=0}) => {
  const l = useCurrentFrame();
  const b = Math.floor(l / stepDur(1)) - baseBeat;
  const ry = ryBase + (b % 2 === 0 ? 14 : -14) * (0.6 + 0.4 * Math.sin(b));
  const rx = rxBase + (b % 3 === 0 ? 4 : -2);
  const tz = z + (b % 2 === 0 ? 60 : 0);
  const floatY = Math.sin((l + baseBeat * 7) / 22) * 16;
  const punch = 1 + 0.03 * Math.max(0, 1 - ((l % stepDur(1)) / stepDur(1)) * 2) ** 2;
  return <Shot3D file={file} x={x} y={y + floatY} w={w} delay={0} ry={ry} rx={rx} z={tz} scale={punch} label={undefined} />;
};

// 章节入场动画：卡片从远处飞入并旋转定格，主标题随之弹出
// entryMs 毫秒内完成飞入，之后进入静止旋转状态
const Chapter: React.FC<{
  base: number; dur: number; head: string; sub?: string; file?: string;
  cardX?: number; cardY?: number; cardW?: number; cardRy?: number; cardRx?: number; cardZ?: number;
  camRy?: number; camRx?: number; camZ?: number; click?: {x:number;y:number;label:string;atRel:number};
  punch?: number; deco?: boolean; closeup?: boolean;
}> = ({base, dur, head, sub, file, cardX=960, cardY=520, cardW=900, cardRy=-10, cardRx=6, cardZ=0, camRy=0, camRx=4, camZ=140, click, punch=0.04, deco=false, closeup=false}) => {
  const l = useCurrentFrame();
  const ls = l - base;
  const entry = stepDur(8); // 8 beats entry fly-in
  const entryP = interpolate(ls, [0, entry], [0, 1], clamp);
  // 相机：从远处拉近并环绕
  const camRyT = interpolate(ls, [0, entry, dur], [camRy - 20, camRy, camRy + 10], clamp);
  const camRxT = interpolate(ls, [0, entry, dur], [camRx + 8, camRx, camRx - 2], clamp);
  const camZT = interpolate(ls, [0, entry, dur], [camZ - 300, camZ + 20, camZ - 10], clamp);
  // 主卡片：从远处飞入，旋转定格，随后轻微脉冲
  const cardEntryZ = interpolate(ls, [0, entry], [cardZ - 500, cardZ], clamp);
  const cardEntryRy = interpolate(ls, [0, entry], [cardRy + 40, cardRy], clamp);
  const cardEntryRx = interpolate(ls, [0, entry], [cardRx + 20, cardRx], clamp);
  const cardEntryScale = interpolate(ls, [0, entry], [0.55, 1.0], clamp);
  const cardRyT = interpolate(ls, [entry, dur], [cardRy - 12, cardRy + 12], clamp);
  const cardRxT = interpolate(ls, [entry, dur], [cardRx - 3, cardRx + 3], clamp);
  const cardFlyScale = ls < entry ? cardEntryScale : 1 + punch * Math.max(0, 1 - ((ls - entry) % stepDur(1)) / (stepDur(1) * 0.6)) ** 2;
  const punchScale = beatPunch(ls, FILM.fps, PERIOD, punch);
  // 产品特写：后半段放大主卡片
  const isCloseup = closeup && ls > dur / 2;
  const closeupW = isCloseup ? cardW * 1.35 : cardW;
  const closeupY = isCloseup ? cardY - 40 : cardY;
  const closeupZ = isCloseup ? cardZ - 120 : cardZ;
  const entryOpacity = interpolate(ls, [0, entry], [0, 1], clamp);
  return (
    <AbsoluteFill style={{opacity: entryOpacity}}>
      <Camera ry={camRyT} rx={camRxT} tz={camZT} scale={punchScale}>
        <div style={{position: 'absolute', left: 0, right: 0, top: 130, textAlign: 'center'}}>
          <MetalText size={106} delay={0} from="scale" tint="silver">{head}</MetalText>
          {sub && <div style={{marginTop: 14}}><Sub delay={4} size={30} color={COLOR.dim} maxWidth={700}>{sub}</Sub></div>}
        </div>
        {file && (
          <>
            <Shot3D key={file + base + '-main'} file={file} x={cardX} y={closeupY} w={closeupW} delay={0} ry={ls < entry ? cardEntryRy : cardRyT} rx={ls < entry ? cardEntryRx : cardRxT} z={ls < entry ? cardEntryZ : closeupZ} floatAmp={14} scale={cardFlyScale * 1.03} label={undefined} />
            {deco && (
              <>
                <FloatCard file={file} x={cardX - 460} y={cardY - 240} w={320} baseBeat={Math.floor(ls / stepDur(1))} ryBase={-24} rxBase={10} z={-140} />
                <FloatCard file={file} x={cardX + 460} y={cardY - 240} w={300} baseBeat={Math.floor(ls / stepDur(1)) + 3} ryBase={24} rxBase={-8} z={-120} />
                <FloatCard file={file} x={cardX - 300} y={cardY + 280} w={340} baseBeat={Math.floor(ls / stepDur(1)) + 6} ryBase={-12} rxBase={14} z={-160} />
                <FloatCard file={file} x={cardX + 300} y={cardY + 280} w={320} baseBeat={Math.floor(ls / stepDur(1)) + 9} ryBase={16} rxBase={-10} z={-130} />
              </>
            )}
          </>
        )}
        {click && <ClickPoint x={click.x} y={click.y} at={base + click.atRel} label={click.label} />}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 1) IMPACT INTRO (beats 0-20) ----------
const Impact: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const hits = [0, 2, 4, 6, 8, 10, 12, 14, 16, 18];
  return (
    <AbsoluteFill>
      {hits.map((b, i) => {
        const o = interpolate(l, [BF(b), BF(b) + 1, BF(b) + 4], [0, i % 4 === 0 ? 0.45 : 0.22, 0], clamp);
        return <AbsoluteFill key={b} style={{backgroundColor: i % 2 ? COLOR.accent : '#fff', opacity: o, mixBlendMode: 'screen'}} />;
      })}
      <Camera tz={interpolate(l, [0, stepDur(20)], [-400, 140], clamp)} rx={interpolate(l, [0, stepDur(10)], [10, 0], clamp)} ry={interpolate(l, [0, stepDur(20)], [-8, 8], clamp)} scale={beatPunch(l, 30, PERIOD, 0.06)}>
        {l >= BF(0) && l < BF(4) && (
          <AbsoluteFill style={{position:'absolute',left:0,right:0,top:0,bottom:0,display:'flex',flexDirection:'column',justifyContent:'center',alignItems:'center'}}>
            <MetalText size={120} delay={0} from="scale" tint="silver">启动。</MetalText>
          </AbsoluteFill>
        )}
        {l >= BF(6) && l < BF(10) && (
          <AbsoluteFill style={{position:'absolute',left:0,right:0,top:0,bottom:0,display:'flex',flexDirection:'column',justifyContent:'center',alignItems:'center'}}>
            <MetalText size={100} delay={0} from="scale" tint="gold">极简。</MetalText>
          </AbsoluteFill>
        )}
        {l >= BF(12) && l < BF(18) && (
          <AbsoluteFill style={{position:'absolute',left:0,right:0,top:0,bottom:0,display:'flex',flexDirection:'column',justifyContent:'center',alignItems:'center'}}>
            <MetalText size={90} delay={0} from="scale" tint="blue">无痕迹。</MetalText>
          </AbsoluteFill>
        )}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 2) HERO (beats 20-50) ----------
const Hero: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const ls = l - base;
  return (
    <Chapter base={base} dur={stepDur(30)} head="SalmonLauncher" sub="把复杂留给启动器，把时间留给游戏。"
      file={SHOTS.home} cardX={960} cardY={560} cardW={1000} cardRy={-14} cardRx={8} cardZ={-60}
      camRy={0} camRx={5} camZ={140} deco={true} />
  );
};

// ---------- 3) ONBOARDING (beats 50-110, 60 beats ~ 10 beats per step for 6 steps) ----------
const Onboarding: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(10);
  const steps = [
    {file: SHOTS.wLang, head: '选择语言', sub: '简体中文 / 繁體中文 / English / 日本語，进入即选'},
    {file: SHOTS.wTerms, head: '阅读并同意条款', sub: '用户协议与免责声明清晰展示，勾选后继续'},
    {file: SHOTS.wMigrate, head: '迁移数据（可选）', sub: '可一键识别，也可手动选择旧启动器或 .minecraft 数据文件夹'},
    {file: SHOTS.wAccount, head: '添加账户', sub: '离线账户 / 微软账号登录，之后可随时管理'},
    {file: SHOTS.wTheme, head: '外观与配色', sub: '主题与强调色；默认普通背景，云母材质按需可选开启'},
    {file: SHOTS.wDone, head: '完成引导', sub: '初始化完成，直接进入启动器开始使用'},
  ];
  const idx = Math.min(steps.length - 1, Math.floor(l / seg));
  const s = steps[idx];
  return (
    <Chapter base={base + idx * seg} dur={seg} head={s.head} sub={s.sub}
      file={s.file} cardX={960} cardY={560} cardW={950} cardRy={idx % 2 ? 14 : -14} cardRx={6} cardZ={40}
      camRy={idx % 2 ? 12 : -12} camRx={6} camZ={120} click={{x:1500,y:720,label:s.head,atRel:stepDur(3)}} deco={true} />
  );
};

// ---------- 4) LAUNCH / PLAY (beats 110-170, 60 beats ~ 15 beats per phase for 4 phases) ----------
const LaunchPlay: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(15);
  const phases = [
    {file: SHOTS.home, head: '选择实例与账户', sub: '确认游戏版本与内存分配，点击“启动”'},
    {file: SHOTS.tasks, head: '点击启动', sub: '启动器开始读取并校验游戏文件', click: {x:1500,y:720,label:'一键启动',atRel:stepDur(3)}},
    {file: SHOTS.tasks, head: '资源校验中，请稍候…', sub: '启动器后台自动补全缺失文件，进度实时可见'},
    {file: SHOTS.instLogs, head: '进入游戏 · 开始畅玩', sub: '游戏窗口打开，进入 Minecraft 主菜单'},
  ];
  const idx = Math.min(phases.length - 1, Math.floor(l / seg));
  const p = phases[idx];
  return (
    <Chapter base={base + idx * seg} dur={seg} head={p.head} sub={p.sub}
      file={p.file} cardX={960} cardY={540} cardW={1000} cardRy={idx % 2 ? 16 : -16} cardRx={6} cardZ={60}
      camRy={idx % 2 ? 10 : -10} camRx={5} camZ={100} click={p.click} deco={true} />
  );
};

// ---------- 5) INSTALL (beats 170-250, 80 beats ~ 20 beats per stage for 4 stages) ----------
const Install: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(20);
  const stages = [
    {file: SHOTS.versions, head: '选择游戏版本', sub: '正式版 / 快照，版本清单完整，一键选择'},
    {file: SHOTS.loaders, head: '选择加载器', sub: 'Fabric / Quilt / Forge / NeoForge，适配模组生态'},
    {file: SHOTS.install, head: '确认安装选项', sub: '实例名称、版本、加载器、参数一屏确认后开始安装', click: {x:1500,y:720,label:'一键安装',atRel:stepDur(5)}},
    {file: SHOTS.tasks, head: '下载与安装任务', sub: '多线程分段下载、镜像自动回退、断点续传，实时速度与进度可见'},
  ];
  const idx = Math.min(stages.length - 1, Math.floor(l / seg));
  const s = stages[idx];
  return (
    <Chapter base={base + idx * seg} dur={seg} head={s.head} sub={s.sub}
      file={s.file} cardX={960} cardY={560} cardW={1000} cardRy={idx % 2 ? 14 : -14} cardRx={6} cardZ={40}
      camRy={idx % 2 ? 12 : -12} camRx={6} camZ={120} click={s.click} deco={true} />
  );
};

// ---------- 6) INSTANCES (beats 250-330, 80 beats ~ 16 beats per stage for 5 stages) ----------
const Instances: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(16);
  const stages = [
    {file: SHOTS.overview, head: '实例概览', sub: '版本、加载器、状态一目了然，支持快速切换与启动'},
    {file: SHOTS.mods, head: '模组管理', sub: 'Modrinth 浏览、搜索、安装模组，支持版本筛选与依赖检查'},
    {file: SHOTS.saves, head: '存档管理', sub: '备份、恢复、重命名存档，自定义存档路径，数据安全无忧'},
    {file: SHOTS.instCfg, head: '实例设置', sub: '实例名称、游戏目录、Java 路径、启动参数等详细配置'},
    {file: SHOTS.instLogs, head: '实例日志', sub: '实时读取启动日志与崩溃报告，关键词过滤，问题定位更精准'},
  ];
  const idx = Math.min(stages.length - 1, Math.floor(l / seg));
  const s = stages[idx];
  return (
    <Chapter base={base + idx * seg} dur={seg} head={s.head} sub={s.sub}
      file={s.file} cardX={960} cardY={540} cardW={1000} cardRy={idx % 2 ? 14 : -14} cardRx={6} cardZ={40}
      camRy={idx % 2 ? 12 : -12} camRx={6} camZ={120} click={{x:1500,y:660,label:s.head,atRel:stepDur(4)}} deco={true} />
  );
};

// ---------- 7) CONTENT (beats 330-410, 80 beats ~ 20 beats per stage for 4 stages) ----------
const Content: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(20);
  const stages = [
    {file: SHOTS.repoMods, head: '模组仓库', sub: 'Modrinth 模组仓库集成，搜索、筛选、安装模组，支持版本与依赖管理'},
    {file: SHOTS.repoShaders, head: '光影与材质', sub: '光影包与材质包预览与安装，让游戏画面更出彩'},
    {file: SHOTS.repoPacks, head: '资源包管理', sub: '资源包浏览与切换，自定义游戏纹理与音效'},
    {file: SHOTS.instPacks, head: '实例内容', sub: '在实例内统一管理已安装的模组、光影、资源包与数据包'},
  ];
  const idx = Math.min(stages.length - 1, Math.floor(l / seg));
  const s = stages[idx];
  return (
    <Chapter base={base + idx * seg} dur={seg} head={s.head} sub={s.sub}
      file={s.file} cardX={960} cardY={560} cardW={1050} cardRy={idx % 2 ? -16 : 16} cardRx={6} cardZ={50}
      camRy={idx % 2 ? -14 : 14} camRx={6} camZ={130} click={{x:1500,y:680,label:s.head,atRel:stepDur(5)}} deco={true} closeup={idx === 2} />
  );
};

// ---------- 8) ONLINE + SKINS (beats 410-470, 60 beats ~ 30 beats per half for 2 halves) ----------
const Online: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const half = stepDur(30);
  const p2 = l >= base + half;
  if (!p2) {
    return (
      <Chapter base={base} dur={half} head="联机 · Campfire" sub="把世界开给朋友。集成在启动器内"
        file={SHOTS.online} cardX={960} cardY={560} cardW={1050} cardRy={-14} cardRx={6} cardZ={50}
        camRy={0} camRx={5} camZ={130} click={{x:1500,y:680,label:'创建房间',atRel:stepDur(8)}} deco={true} />
    );
  }
  return (
    <Chapter base={base + half} dur={half} head="皮肤站与 3D 预览" sub="皮肤站集成与 3D 皮肤预览，外观所见即所得"
      file={SHOTS.skins} cardX={960} cardY={560} cardW={1050} cardRy={14} cardRx={6} cardZ={50}
      camRy={0} camRx={5} camZ={130} click={{x:1500,y:680,label:'更换皮肤',atRel:stepDur(8)}} deco={true} closeup={true} />
  );
};

// ---------- 9) TOOLS & SETTINGS (beats 470-550, 80 beats ~ 10 beats per item for 8 items) ----------
const ToolsSettings: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(10);
  const items = [
    {file: SHOTS.diag, head: '崩溃诊断', sub: '解析崩溃报告并给出建议，启动失败可一键直达诊断'},
    {file: SHOTS.appLog, head: '应用日志', sub: '日志尾部实时读取、关键词过滤，问题可追踪可定位'},
    {file: SHOTS.global, head: '全局设置', sub: '内存分配、Java、下载源与镜像等全局参数，即时生效'},
    {file: SHOTS.launchSet, head: '启动设置', sub: '游戏窗口大小、JVM 参数与启动行为按需配置'},
    {file: SHOTS.theme, head: '主题与外观', sub: '深浅色/强调色/主题工坊配色；云母材质默认关闭，可选开启'},
    {file: SHOTS.about, head: '关于与更新', sub: '版本信息、开源许可、检查启动器更新'},
    {file: SHOTS.terms, head: '协议与条款', sub: '用户协议与免责声明完整可查'},
    {file: SHOTS.wLang, head: '语言设置', sub: '多语言界面切换，支持简体/繁体中文、英文、日文'},
  ];
  const idx = Math.min(items.length - 1, Math.floor(l / seg));
  const it = items[idx];
  return (
    <Chapter base={base + idx * seg} dur={seg} head={it.head} sub={it.sub}
      file={it.file} cardX={960} cardY={560} cardW={1000} cardRy={idx % 2 ? 14 : -14} cardRx={6} cardZ={40}
      camRy={idx % 2 ? 12 : -12} camRx={6} camZ={120} click={{x:1500,y:680,label:it.head,atRel:stepDur(3)}} deco={true} />
  );
};

// ---------- 10) MONTAGE (beats 550-590, 40 beats rapid fire) ----------
const Montage: React.FC<{base: number}> = ({base}) => {
  const l = useCurrentFrame();
  const ls = l - base;
  const files = [SHOTS.home, SHOTS.tasks, SHOTS.versions, SHOTS.loaders, SHOTS.install, SHOTS.overview, SHOTS.mods, SHOTS.saves, SHOTS.instCfg, SHOTS.instLogs, SHOTS.repoMods, SHOTS.repoShaders, SHOTS.repoPacks, SHOTS.instPacks, SHOTS.online, SHOTS.skins, SHOTS.diag, SHOTS.appLog, SHOTS.global, SHOTS.launchSet, SHOTS.theme, SHOTS.about, SHOTS.terms];
  const perCard = Math.floor(stepDur(40) / files.length);
  const cardW = 340, gap = 20, cols = 3;
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(ls, [0, stepDur(40)], [-500, 200], clamp)} ry={interpolate(ls, [0, stepDur(40)], [-22, 22], clamp)} rx={8} scale={beatPunch(ls, 30, PERIOD, 0.08)}>
        <div style={{position: 'absolute', left: 90, top: 90}}>
          <Tag text="功能全景 · 快速剪辑" accent />
          <div style={{marginTop: 14}}><MetalText size={80} delay={1} from="bottom" tint="silver" align="left">全部功能，一应俱全。</MetalText></div>
        </div>
        {files.map((file, i) => {
          const d = i * perCard;
          const row = Math.floor(i / cols);
          const col = i % cols;
          const p = interpolate(ls, [d, d + 18], [0, 1], clamp);
          return (
            <div key={file + i} style={{position: 'absolute', left: col * (cardW + gap), top: 200 + row * (260 + gap), width: cardW, height: 260, transformStyle: 'preserve-3d', transform: `translateZ(${(1 - p) * -600}px) rotateY(${(col - 2.5) * 5}deg) rotateX(${(1 - p) * 30}deg) scale(${(0.75 + p * 0.25) * beatPunch(ls, 30, PERIOD, 0.05)})`, opacity: interpolate(ls, [d, d + 3], [0, 1], clamp)}}>
              <div style={{width: '100%', height: '100%', borderRadius: 14, overflow: 'hidden', border: `1px solid ${COLOR.line}`, boxShadow: '0 20px 60px rgba(0,0,0,0.75)'}}>
                <Img src={staticFile(`shots/${file}`)} style={{width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top left'}} />
              </div>
            </div>
          );
        })}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 11) OUTRO + CREDITS (beat 590 - end) ----------
const Outro: React.FC<{base: number; total: number}> = ({base, total}) => {
  const l = useCurrentFrame();
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{background: 'radial-gradient(ellipse 75% 55% at 50% 38%, rgba(126,217,87,0.18), transparent 70%)'}} />
      <Camera tz={interpolate(l, [0, Math.min(total, stepDur(24))], [-160, 50], clamp)} rx={interpolate(l, [0, total], [4, 0], clamp)} scale={beatPunch(l, 30, PERIOD, 0.04)}>
        <div style={{position: 'absolute', left: 0, right: 0, top: 55, textAlign: 'center'}}>
          <MetalText size={100} delay={0} from="scale" tint="silver">SalmonLauncher</MetalText>
          <div style={{marginTop: 12}}><Sub delay={4} size={32} color={COLOR.dim}>把复杂留给启动器，把时间留给游戏。</Sub></div>
        </div>
        <QRCard file="site.png" title="官方网站" url={LINKS.site} x={260} y={285} delay={6} size={170} />
        <QRCard file="github.png" title="GitHub 仓库" url={LINKS.github} x={660} y={285} delay={10} size={170} />
        <QRCard file="blog.png" title="作者博客" url={LINKS.blog} x={1060} y={285} delay={14} size={170} />
        <div style={{position: 'absolute', left: 100, right: 100, bottom: 138, padding: '20px 26px', borderRadius: 18, background: 'rgba(20,20,22,0.76)', border: `1px solid ${COLOR.line}`, opacity: interpolate(l, [18, 28], [0, 1], clamp), transform: `translateY(${interpolate(l, [18, 28], [30, 0], clamp)}px)`}}>
          <div style={{fontFamily: FONT, fontSize: 21, color: COLOR.ink, fontWeight: 700, marginBottom: 8}}>视频声明 · 制作人员 · 致谢名单 · 版权解释</div>
          <div style={{fontFamily: FONT, fontSize: 17, color: COLOR.dim, lineHeight: 1.7}}>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>制作人员：</span>Hugo Mou（产品设计 / 开发 / 维护）</div>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>致谢名单：</span>Modrinth 内容平台与社区作者 · Campfire 联机组件 · .NET / Windows App SDK / WinUI 3 · Mojang 官方版本与资源生态</div>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>配乐与素材：</span>“3 Strikes” — Terror Jr（来源：Bilibili BV1tek2YsEYY，本片仅作展示） · 画面为 SalmonLauncher 2.0.13 真实界面截图（全新默认状态，云母默认关闭，无个人使用痕迹）</div>
            <div style={{color: COLOR.accent, fontWeight: 600, marginTop: 2}}>本软件由 AI 辅助开发制作</div>
            <div style={{color: COLOR.accent, fontWeight: 600}}>本宣传片由 AI 生成制作</div>
            <div><span style={{color: COLOR.ink, fontWeight: 600}}>项目许可：</span>GPL-3.0-or-later（开源免费，Windows x64）　官网 {LINKS.siteFull}　GitHub {LINKS.githubFull}</div>
            <div style={{marginTop: 6, fontSize: 15, color: 'rgba(134,134,139,0.95)'}}>版权与声明：Minecraft 为 Mojang Studios / Microsoft 的商标，本项目与 Mojang/Microsoft 无官方关联；Modrinth、Campfire 等名称与服务归各自权利人所有。本视频仅用于 SalmonLauncher 产品展示与介绍，不构成对第三方商标或内容的授权声明。</div>
          </div>
        </div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- timeline ----------
export const Film: React.FC = () => {
  const seg = (startBeat: number, durBeats: number) => ({from: BF(startBeat), dur: stepDur(durBeats)});
  // 0-20 impact(20), 20-50 hero(30), 50-110 onboarding(60), 110-170 launch(60),
  // 170-250 install(80), 250-330 instances(80), 330-410 content(80),
  // 410-470 online(60), 470-550 tools/settings(80), 550-590 montage(40), 590-end outro
  const S = {
    impact: seg(0, 20),
    hero: seg(20, 30),
    onboard: seg(50, 60),
    launch: seg(110, 60),
    install: seg(170, 80),
    instance: seg(250, 80),
    content: seg(330, 80),
    online: seg(410, 60),
    tools: seg(470, 80),
    montage: seg(550, 40),
  };
  const outroFrom = BF(590);
  const outroDur = Math.max(1, toF(FILM.duration) - outroFrom);

  const f = useCurrentFrame();
  const t = f / FILM.fps;
  let flash = 0;
  for (let i = 0; i < BEAT_COUNT; i += 1) {
    const down = i % 4 === 0;
    const d = down ? 0.22 : i % 2 === 0 ? 0.12 : 0.06;
    const w = down ? 0.1 : 0.06;
    flash = Math.max(flash, hit(t, i, w) * d);
  }

  return (
    <Stage>
      <Audio src={staticFile(AUDIO_FILE)} />
      <Sequence from={S.impact.from} durationInFrames={S.impact.dur}><Impact base={S.impact.from} /></Sequence>
      <Sequence from={S.hero.from} durationInFrames={S.hero.dur}><Hero base={S.hero.from} /></Sequence>
      <Sequence from={S.onboard.from} durationInFrames={S.onboard.dur}><Onboarding base={S.onboard.from} /></Sequence>
      <Sequence from={S.launch.from} durationInFrames={S.launch.dur}><LaunchPlay base={S.launch.from} /></Sequence>
      <Sequence from={S.install.from} durationInFrames={S.install.dur}><Install base={S.install.from} /></Sequence>
      <Sequence from={S.instance.from} durationInFrames={S.instance.dur}><Instances base={S.instance.from} /></Sequence>
      <Sequence from={S.content.from} durationInFrames={S.content.dur}><Content base={S.content.from} /></Sequence>
      <Sequence from={S.online.from} durationInFrames={S.online.dur}><Online base={S.online.from} /></Sequence>
      <Sequence from={S.tools.from} durationInFrames={S.tools.dur}><ToolsSettings base={S.tools.from} /></Sequence>
      <Sequence from={S.montage.from} durationInFrames={S.montage.dur}><Montage base={S.montage.from} /></Sequence>
      <Sequence from={outroFrom} durationInFrames={outroDur}><Outro base={outroFrom} total={outroDur} /></Sequence>

      {flash > 0.001 && <AbsoluteFill style={{backgroundColor: '#fff', opacity: flash * 0.28, mixBlendMode: 'screen', pointerEvents:'none'}} />}
      <AbsoluteFill style={{boxShadow: 'inset 0 0 180px rgba(0,0,0,0.45)', pointerEvents:'none'}} />
      <LegalStrip />
    </Stage>
  );
};
