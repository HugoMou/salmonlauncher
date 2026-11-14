// Film.tsx — SalmonLauncher promo, Apple-keynote energy, 177.6s synced to "3 Strikes" (108 BPM).
// Fast beat-cuts, heavy 3D motion, metal/glass, real clean UI screenshots (default, no Mica).
import React from 'react';
import {
  AbsoluteFill, Audio, Easing, Img, Sequence, interpolate, spring, staticFile,
  useCurrentFrame, useVideoConfig,
} from 'remotion';
import {AUDIO_FILE, COLOR, CREDITS, FILM, FONT, LINKS, SHOTS, toF} from './spec';
import {BEAT_COUNT, PERIOD, PHASE, barFrame, beatFrame, beatTime, hit} from './lib/beats';
import {Big, Camera, Cut, LegalStrip, MetalText, QRCard, ScanLine, Shot3D, Slam, Stage, Sub, Tag, clamp, easeOut} from './lib/fx';

// ---------- beat utilities ----------
const BF = (i: number) => beatFrame(i, FILM.fps);
const BAR = (b: number) => barFrame(b, FILM.fps);
const B = (frames: number) => frames; // frames helper
const stepDur = (beats: number) => Math.round(beats * PERIOD * FILM.fps);

// downbeat/flash arrays for energy
const everyBeat = Array.from({length: BEAT_COUNT}, (_, i) => i);
const downbeats = everyBeat.filter((i) => i % 4 === 0);
const strongHits = everyBeat.filter((i) => i % 8 === 0);

// ---------- reusable usage overlays ----------
const ClickPulse: React.FC<{x: number; y: number; at: number; color?: string}> = ({x, y, at, color = COLOR.accent}) => {
  const f = useCurrentFrame();
  const local = f - at;
  if (local < 0 || local > 20) return null;
  const s = interpolate(local, [0, 18], [0.2, 1.8], {extrapolateRight: 'clamp', easing: easeOut});
  const o = interpolate(local, [0, 4, 18], [0, 0.9, 0], clamp);
  return (
    <div style={{position:'absolute', left: x - 30, top: y - 30, width: 60, height: 60, borderRadius: 999,
      border: `3px solid ${color}`, opacity: o, transform: `scale(${s})`, boxShadow: `0 0 30px ${color}`}} />
  );
};

const ProgressBar: React.FC<{x: number; y: number; w: number; at: number; dur: number; label?: string}> = ({x, y, w, at, dur, label}) => {
  const f = useCurrentFrame();
  const p = interpolate(f, [at, at + dur], [0, 1], clamp);
  if (f < at - 2) return null;
  return (
    <div style={{position:'absolute', left:x, top:y, width:w, opacity: interpolate(f,[at-2,at+4],[0,1],clamp)}}>
      {label && <div style={{fontFamily:FONT, fontSize:16, color:COLOR.dim, marginBottom:6}}>{label}</div>}
      <div style={{height:10, borderRadius:999, background:'rgba(255,255,255,0.12)', overflow:'hidden', border:`1px solid ${COLOR.line}`}}>
        <div style={{height:'100%', width:`${p*100}%`, borderRadius:999, background:`linear-gradient(90deg, ${COLOR.accent}, ${COLOR.cyan})`, boxShadow:`0 0 20px ${COLOR.accent}`}} />
      </div>
    </div>
  );
};

const StepBadge: React.FC<{n: number; text: string; active?: boolean; x: number; y: number; delay?: number}> = ({n, text, active, x, y, delay=0}) => {
  const f = useCurrentFrame(); const {fps}=useVideoConfig();
  const p = spring({frame:f-delay,fps,config:{damping:18,stiffness:140}});
  return (
    <div style={{position:'absolute', left:x, top:y, display:'flex', alignItems:'center', gap:10, opacity: interpolate(f,[delay,delay+6],[0,1],clamp), transform:`translateX(${(1-p)*30}px)`}}>
      <div style={{width:30, height:30, borderRadius:999, display:'flex', alignItems:'center', justifyContent:'center',
        background: active ? COLOR.accent : 'rgba(255,255,255,0.12)', color: active ? '#000' : COLOR.ink, fontFamily:FONT, fontWeight:700, fontSize:16,
        boxShadow: active ? `0 0 20px ${COLOR.accent}` : 'none'}}>{n}</div>
      <div style={{fontFamily:FONT, fontSize:20, fontWeight:600, color: active ? COLOR.ink : COLOR.dim}}>{text}</div>
    </div>
  );
};

const CursorDot: React.FC<{x: number; y: number; at: number; toX: number; toY: number; dur: number}> = ({x,y,at,toX,toY,dur}) => {
  const f = useCurrentFrame();
  const p = interpolate(f, [at, at+dur], [0, 1], {...clamp, easing: easeOut});
  const cx = x + (toX-x)*p; const cy = y + (toY-y)*p;
  const o = interpolate(f,[at-2,at,at+dur,at+dur+6],[0,1,1,0],clamp);
  return <div style={{position:'absolute', left:cx-10, top:cy-10, width:20, height:20, borderRadius:999, background:COLOR.accent, opacity:o, boxShadow:`0 0 24px ${COLOR.accent}, 0 0 0 8px rgba(126,217,87,0.2)`}}/>;
};

// ---------- 1) IMPACT INTRO (0 - 4 bars ~8.9s) ----------
const Impact: React.FC<{local: number}> = ({local}) => {
  const f = local; const {fps} = useVideoConfig();
  const slams = [0, 2, 4, 6, 8, 12].map(b => BF(b)); // relative to segment start 0
  return (
    <AbsoluteFill>
      {/* beat flashes */}
      {slams.map((bf, i) => {
        const o = interpolate(f, [bf, bf+1, bf+5], [0, i===2||i===4?0.35:0.18, 0], clamp);
        return <AbsoluteFill key={i} style={{backgroundColor: i%2?COLOR.accent:'#fff', opacity:o, mixBlendMode:'screen'}} />;
      })}
      <Camera tz={interpolate(f,[0, BF(16)],[ -200, 0], clamp)} rx={interpolate(f,[0,BF(8)],[8,0],clamp)}>
        {f >= BF(0) && f < BF(4) && <Slam delay={0} size={160} tint="silver">启动。</Slam>}
        {f >= BF(4) && f < BF(8) && <Slam delay={BF(4)} size={160} tint="green">安装。</Slam>}
        {f >= BF(8) && f < BF(12) && <Slam delay={BF(8)} size={160} tint="blue">畅玩。</Slam>}
        {f >= BF(12) && (
          <AbsoluteFill style={{justifyContent:'center', alignItems:'center'}}>
            <MetalText size={128} delay={BF(12)} from="scale" tint="silver">SalmonLauncher</MetalText>
            <div style={{position:'absolute', top: 660}}>
              <Sub delay={BF(14)} size={34}>一个真正好用的 Minecraft Java 启动器</Sub>
            </div>
            <ScanLine delay={BF(13)} y={620} />
          </AbsoluteFill>
        )}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 2) HERO (bars 4-8 ~8.9s) ----------
const Hero: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame();
  const l = f - base;
  const camZ = interpolate(l, [0, stepDur(16)], [-300, 80], {extrapolateRight:'clamp', easing:easeOut});
  const camRy = interpolate(l, [0, stepDur(16)], [10, -6], clamp);
  return (
    <AbsoluteFill>
      <Camera tz={camZ} ry={camRy} rx={4}>
        <div style={{position:'absolute', left:120, top:180}}>
          <Tag text="SALMONLAUNCHER 2.0.13" delay={0} accent />
          <div style={{marginTop:30}}><MetalText size={96} delay={2} from="left" tint="silver" align="left">{'从进入启动器\n到畅玩 Minecraft'}</MetalText></div>
          <div style={{marginTop:26}}><Big size={46} delay={10} align="left" color={COLOR.dim}>每一步都清楚。每个功能都讲到。</Big></div>
          <div style={{marginTop:30, display:'flex', gap:12, flexWrap:'wrap', maxWidth:620}}>
            {['Windows 原生 WinUI 3','开源免费 GPL-3.0','真实界面 · 全新状态采集'].map((t,i)=><Tag key={t} text={t} delay={14+i*3} accent={i===2} />)}
          </div>
        </div>
        <Shot3D file={SHOTS.home} x={820} y={120} w={1000} delay={4} ry={-16} rx={6} z={60} label="启动面板 · 使用画面" floatAmp={6} />
        <Shot3D file={SHOTS.tasks} x={660} y={560} w={420} delay={12} ry={10} rx={-4} z={120} label="任务进度" />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 3) ONBOARDING USAGE FLOW (bars 8-16 ~17.8s) step by step ----------
const Onboarding: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame();
  const l = f - base;
  const steps = [
    {file: SHOTS.wLang, label: '1. 选择语言', note: '简中 / 繁中 / English / 日本語'},
    {file: SHOTS.wTerms, label: '2. 阅读并同意条款', note: '协议与免责声明清晰展示'},
    {file: SHOTS.wMigrate, label: '3. 迁移数据（可选）', note: '可手动选择旧启动器或 .minecraft 文件夹'},
    {file: SHOTS.wAccount, label: '4. 添加账户', note: '离线 / 微软账号，稍后可改'},
    {file: SHOTS.wTheme, label: '5. 外观与配色', note: '默认普通背景，云母可选'},
    {file: SHOTS.wDone, label: '6. 完成引导', note: '直接开始使用'},
  ];
  const seg = stepDur(3); // 3 beats per step
  const idx = Math.min(steps.length - 1, Math.floor(l / seg));
  const localSeg = l - idx * seg;
  const s = steps[idx];
  const camRy = interpolate(localSeg, [0, seg], [18, -18], clamp);
  const camZ = interpolate(localSeg, [0, seg*0.5, seg], [-120, 80, -40], clamp);
  return (
    <AbsoluteFill>
      <div style={{position:'absolute', left:100, top:110}}>
        <Tag text="首次启动 · 使用流程" accent />
        <div style={{marginTop:18}}><MetalText size={72} delay={0} from="left" tint="silver" align="left">一步步带你上手。</MetalText></div>
      </div>
      {/* step rail */}
      <div style={{position:'absolute', left:100, top:320, display:'flex', flexDirection:'column', gap:18}}>
        {steps.map((st,i)=>(<StepBadge key={st.label} n={i+1} text={st.label.replace(/^\d+\.\s*/, '')} active={i===idx} x={0} y={0} delay={i*2} />))}
      </div>
      <Camera ry={camRy} rx={5} tz={camZ}>
        <Shot3D key={s.file+idx} file={s.file} x={620} y={160} w={1180} delay={0} ry={-8} rx={4} z={40} label={s.label} />
      </Camera>
      <div style={{position:'absolute', right:120, bottom:150, textAlign:'right'}}>
        <Big size={44} delay={2} align="right">{s.note}</Big>
      </div>
      {/* simulated cursor tap on beat */}
      <ClickPulse x={1420} y={760} at={Math.round(seg*0.7)} color={COLOR.accent} />
      <CursorDot x={1100} y={500} at={Math.round(seg*0.15)} toX={1420} toY={760} dur={Math.round(seg*0.45)} />
    </AbsoluteFill>
  );
};

// ---------- 4) HOME LAUNCH USAGE (4 bars) ----------
const LaunchUse: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame(); const l = f - base;
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(l,[0,stepDur(16)],[-12,8],clamp)} rx={5} tz={interpolate(l,[0,stepDur(8),stepDur(16)],[-80,120,40],clamp)}>
        <div style={{position:'absolute', left:100, top:140}}>
          <Tag text="启动使用" accent />
          <div style={{marginTop:18}}><MetalText size={84} delay={0} from="left" tint="green" align="left">{'选好实例\n点一下启动。'}</MetalText></div>
          <div style={{marginTop:24}}>
            <Sub delay={8} align="left">账户、版本、Java 状态一目了然</Sub>
            <Sub delay={12} align="left">点击启动 → 自动准备环境与依赖</Sub>
            <Sub delay={16} align="left">任务进度实时可见，完成后进入游戏</Sub>
          </div>
        </div>
        <Shot3D file={SHOTS.home} x={760} y={100} w={1060} delay={2} ry={-14} rx={5} z={60} label="启动面板 · 实际使用" />
        <ClickPulse x={1500} y={720} at={stepDur(6)} />
        <CursorDot x={1200} y={520} at={stepDur(4)} toX={1500} toY={720} dur={stepDur(2)} />
        <Shot3D file={SHOTS.tasks} x={620} y={560} w={460} delay={stepDur(7)} ry={12} rx={-3} z={140} label="任务实时进度" />
        <ProgressBar x={660} y={860} w={380} at={stepDur(8)} dur={stepDur(6)} label="准备资源 / 补全依赖…" />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 5) INSTALL FLOW (8 bars) detailed usage ----------
const InstallFlow: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame(); const l = f - base;
  const seg = stepDur(4);
  const stages = [
    {file: SHOTS.versions, title: '选择游戏版本', note: '官方版本列表，直接选择想玩的版本', label:'版本列表'},
    {file: SHOTS.loaders, title: '选择加载器', note: 'Fabric / Quilt / Forge / NeoForge 一键选择', label:'加载器'},
    {file: SHOTS.install, title: '确认安装选项', note: '实例名、版本组合、安装参数清晰可见', label:'安装选项'},
    {file: SHOTS.tasks, title: '下载与安装', note: '多线程分段下载、镜像回退、断点续传、实时速度', label:'任务队列'},
  ];
  const idx = Math.min(stages.length-1, Math.floor(l/seg));
  const st = stages[idx];
  const ls = l - idx*seg;
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(ls,[0,seg], [14-idx*8, -14-idx*4], clamp)} rx={6} tz={interpolate(ls,[0,seg/2,seg],[-60,100,20],clamp)}>
        <div style={{position:'absolute', left:90, top:120}}>
          <Tag text="安装与下载 · 使用流程" accent />
          <div style={{marginTop:16}}><MetalText size={68} delay={0} from="left" tint="blue" align="left">{st.title}</MetalText></div>
          <div style={{marginTop:16}}><Sub delay={4} align="left" size={30}>{st.note}</Sub></div>
        </div>
        <Shot3D key={st.file+idx} file={st.file} x={700} y={140} w={1100} delay={0} ry={-10+idx*4} rx={5} z={40} label={st.label} />
        {idx===3 && <ProgressBar x={760} y={780} w={900} at={4} dur={seg-6} label="下载资源 · 多连接加速 · 镜像自动回退" />}
        <ClickPulse x={1200} y={700} at={Math.round(seg*0.55)} />
      </Camera>
      <div style={{position:'absolute', left:90, bottom:120, display:'flex', gap:12}}>
        {stages.map((s,i)=>(<Tag key={s.title} text={`${i+1}. ${s.title}`} delay={0} accent={i===idx} color={i===idx?COLOR.accent:COLOR.dim} />))}
      </div>
    </AbsoluteFill>
  );
};

// ---------- 6) INSTANCE MANAGEMENT USAGE (8 bars) ----------
const InstanceFlow: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame(); const l = f - base;
  const seg = stepDur(4);
  const stages = [
    {file: SHOTS.overview, title: '实例概览', note: '版本信息、快速操作、健康检查、复制实例、打开文件夹', label:'概览'},
    {file: SHOTS.mods, title: '模组管理', note: '启用/禁用、安装与移除，按实例独立管理', label:'模组'},
    {file: SHOTS.saves, title: '存档管理', note: '备份存档、从历史备份还原，数据安全可回退', label:'存档'},
    {file: SHOTS.instCfg, title: '实例设置与日志', note: '独立配置与运行日志，排查问题有据可查', label:'实例设置', alt: SHOTS.instLogs},
  ];
  const idx = Math.min(stages.length-1, Math.floor(l/seg));
  const st = stages[idx];
  const ls = l - idx*seg;
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(ls,[0,seg], [16, -16], clamp)} rx={5} tz={interpolate(ls,[0,seg/2,seg],[-40,120,10],clamp)}>
        <div style={{position:'absolute', left:90, top:120}}>
          <Tag text="实例管理 · 实际使用" accent />
          <div style={{marginTop:16}}><MetalText size={70} delay={0} from="left" tint="silver" align="left">{'每个世界\n独立一间房。'}</MetalText></div>
          <div style={{marginTop:18}}><Big size={40} delay={4} align="left" color={COLOR.accent}>{st.title}</Big></div>
          <div style={{marginTop:10}}><Sub delay={8} align="left" size={28}>{st.note}</Sub></div>
        </div>
        <Shot3D key={st.file+idx} file={st.file} x={720} y={120} w={1080} delay={0} ry={-12} rx={5} z={30} label={st.label} />
        {st.alt && <Shot3D file={st.alt} x={560} y={560} w={460} delay={Math.round(seg*0.4)} ry={14} rx={-4} z={120} label="实例日志" />}
        <ClickPulse x={1280} y={620} at={Math.round(seg*0.5)} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 7) CONTENT ECOSYSTEM (8 bars) ----------
const ContentFlow: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame(); const l = f - base;
  const seg = stepDur(4);
  const stages = [
    {file: SHOTS.repoMods, title: '模组仓库', note: '对接 Modrinth，空搜索显示热门推荐，可“换一批”', label:'模组 · 热门推荐', accent:'一眼看到大家在用什么'},
    {file: SHOTS.repoShaders, title: '光影仓库', note: '搜索与浏览光影包，按实例安装', label:'光影', accent:'搜得到，装得上'},
    {file: SHOTS.repoPacks, title: '资源包仓库', note: '资源包浏览与安装，丰富画面风格', label:'资源包', accent:'按实例管理'},
    {file: SHOTS.instPacks, title: '实例内资源/光影', note: '已安装内容在实例内独立管理，开关自如', label:'实例资源', alt: SHOTS.instShaders, accent:'每个实例互不影响'},
  ];
  const idx = Math.min(stages.length-1, Math.floor(l/seg));
  const st = stages[idx];
  const ls = l - idx*seg;
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(ls,[0,seg],[18,-18],clamp)} rx={4} tz={interpolate(ls,[0,seg/2,seg],[-80,80,-20],clamp)}>
        <div style={{position:'absolute', left:0, right:0, top:90, textAlign:'center'}}>
          <Tag text="内容生态 · Modrinth" accent />
          <div style={{marginTop:16}}><MetalText size={72} delay={0} tint="green">{st.title}</MetalText></div>
          <Sub delay={6} size={30}>{st.note}</Sub>
        </div>
        <Shot3D key={st.file+idx} file={st.file} x={380} y={300} w={1160} delay={0} ry={idx%2? -10:10} rx={5} z={40} label={st.label} />
        {st.alt && <Shot3D file={st.alt} x={80} y={520} w={420} delay={Math.round(seg*0.45)} ry={18} rx={-4} z={120} label="实例光影" />}
        <div style={{position:'absolute', left:0, right:0, bottom:110, textAlign:'center'}}>
          <Big size={38} delay={10} color={COLOR.accent}>{st.accent}</Big>
        </div>
        <ClickPulse x={1200} y={420} at={Math.round(seg*0.5)} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 8) ONLINE + SKINS (5 bars) ----------
const OnlineFlow: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame(); const l = f - base;
  const seg = stepDur(5);
  return (
    <AbsoluteFill>
      <Camera ry={interpolate(l,[0,seg*2],[-14,14],clamp)} rx={5} tz={interpolate(l,[0,seg,seg*2],[-60,100,40],clamp)}>
        <div style={{position:'absolute', left:90, top:130}}>
          <Tag text="联机与皮肤" accent />
          <div style={{marginTop:16}}><MetalText size={76} delay={0} from="left" tint="blue" align="left">{'把世界\n开给朋友。'}</MetalText></div>
          <div style={{marginTop:20}}>
            <Sub delay={6} align="left">随附 Campfire 联机组件，一键开房</Sub>
            <Sub delay={10} align="left">生成邀请码，复制分享，支持检查与更新</Sub>
            <Sub delay={14} align="left">皮肤站与 3D 预览</Sub>
          </div>
        </div>
        <Shot3D file={SHOTS.online} x={720} y={110} w={1080} delay={2} ry={-12} rx={5} z={30} label="联机中心 · Campfire" />
        <Shot3D file={SHOTS.skins} x={560} y={580} w={440} delay={stepDur(3)} ry={14} rx={-4} z={140} label="皮肤 3D 预览" />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 9) TOOLS + SETTINGS + ABOUT (10 bars, fast detailed tour) ----------
const DeepTour: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame(); const l = f - base;
  const seg = stepDur(2); // 2 beats per screen, fast
  const items = [
    {file: SHOTS.diag, title: '崩溃诊断', note: '解析崩溃报告、给出建议，启动失败可直达', tint:'blue' as const},
    {file: SHOTS.appLog, title: '应用日志', note: '日志尾部读取、关键词过滤，问题可追踪', tint:'silver' as const},
    {file: SHOTS.global, title: '全局设置', note: '内存、Java、下载源与镜像，即时生效', tint:'green' as const},
    {file: SHOTS.launchSet, title: '启动设置', note: '窗口、JVM 参数与启动行为按需配置', tint:'silver' as const},
    {file: SHOTS.theme, title: '主题工坊与外观', note: '深浅色/强调色/配色，云母默认关闭可选开启', tint:'gold' as const},
    {file: SHOTS.about, title: '关于与更新', note: '版本信息、开源许可、检查更新', tint:'silver' as const},
    {file: SHOTS.terms, title: '协议与条款', note: '用户协议与免责声明清晰可查', tint:'silver' as const},
  ];
  const idx = Math.min(items.length-1, Math.floor(l/seg));
  const it = items[idx];
  const ls = l - idx*seg;
  const ry = ((idx % 2 === 0) ? 1 : -1) * interpolate(ls,[0,seg],[18,-18],clamp);
  return (
    <AbsoluteFill>
      <Camera ry={ry} rx={5} tz={interpolate(ls,[0,seg/2,seg],[-120,80,-40],clamp)}>
        <div style={{position:'absolute', left:100, top:130, zIndex:5}}>
          <Tag text="诊断 · 日志 · 设置 · 关于" accent />
          <div style={{marginTop:16}}><MetalText size={72} delay={0} from="left" tint={it.tint} align="left">{it.title}</MetalText></div>
          <div style={{marginTop:14}}><Sub delay={3} align="left" size={30}>{it.note}</Sub></div>
        </div>
        <Shot3D key={it.file+idx} file={it.file} x={700} y={140} w={1120} delay={0} ry={idx%2?12:-12} rx={5} z={40} label={it.title} />
      </Camera>
      <div style={{position:'absolute', right:80, bottom:120, fontFamily:FONT, fontSize:22, color:COLOR.dim}}>
        {idx+1} / {items.length}
      </div>
    </AbsoluteFill>
  );
};

// ---------- 10) BLOG + LINKS highlight (4 bars) ----------
const BlogLinks: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame(); const l = f - base;
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(l,[0,stepDur(16)],[-200,60],clamp)} rx={4}>
        <AbsoluteFill style={{justifyContent:'center', alignItems:'center'}}>
          <Tag text="作者博客与社区" accent />
          <div style={{marginTop:24}}><MetalText size={88} delay={2} tint="gold">www.hugomou.com</MetalText></div>
          <div style={{marginTop:30, textAlign:'center'}}>
            <Sub delay={8} size={34} color={COLOR.ink}>阅读 SalmonLauncher 相关文章</Sub>
            <Sub delay={12} size={34} color={COLOR.ink}>在文章下留言提建议</Sub>
            <Sub delay={16} size={34} color={COLOR.ink}>也可以通过博客联系作者</Sub>
          </div>
          <div style={{marginTop:40, display:'flex', gap:18}}>
            <Tag text={LINKS.siteFull} delay={18} accent />
            <Tag text={LINKS.githubFull} delay={20} accent />
            <Tag text={LINKS.blogFull} delay={22} accent />
          </div>
        </AbsoluteFill>
        <Shot3D file={SHOTS.about} x={80} y={260} w={420} delay={6} ry={22} rx={6} z={-40} floatAmp={5} />
        <Shot3D file={SHOTS.home} x={1420} y={260} w={420} delay={8} ry={-22} rx={6} z={-40} floatAmp={5} />
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 11) MONTAGE WALL (4 bars, hyper fast) ----------
const Montage: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame(); const l = f - base;
  const all = Object.values(SHOTS);
  const cols = 6; const cardW=270; const cardH=180; const gap=16;
  const totalW = cols*cardW + (cols-1)*gap; const sx = (1920-totalW)/2;
  const camZ = interpolate(l,[0,stepDur(16)],[-400,200],clamp);
  const camRy = interpolate(l,[0,stepDur(16)],[-18,18],clamp);
  return (
    <AbsoluteFill>
      <Camera tz={camZ} ry={camRy} rx={6}>
        <div style={{position:'absolute', left:0, right:0, top:60, textAlign:'center', zIndex:10}}>
          <MetalText size={74} delay={0} tint="silver">从启动页到畅玩，功能一次讲全。</MetalText>
        </div>
        <div style={{position:'absolute', left:sx, top:190, width:totalW, transformStyle:'preserve-3d'}}>
          {all.map((file,i)=>{
            const col=i%cols, row=Math.floor(i/cols);
            const d = i; // frames stagger
            const p = spring({frame:l-d,fps:30,config:{damping:16,stiffness:140}});
            const wave = Math.sin((l+i*4)/10)*8;
            return (
              <div key={file+i} style={{position:'absolute', left:col*(cardW+gap), top:row*(cardH+gap), width:cardW, height:cardH,
                transformStyle:'preserve-3d', transform:`translateZ(${(1-p)*-500 + wave}px) rotateY(${(col-2.5)*3}deg) rotateX(${(1-p)*30}deg) scale(${0.8+p*0.2})`,
                opacity: interpolate(l,[d,d+4],[0,1],clamp)}}>
                <div style={{width:'100%',height:'100%',borderRadius:12,overflow:'hidden',border:`1px solid ${COLOR.line}`,boxShadow:'0 18px 50px rgba(0,0,0,0.7)'}}>
                  <Img src={staticFile(`shots/${file}`)} style={{width:'100%',height:'100%',objectFit:'cover',objectPosition:'top left'}} />
                </div>
              </div>
            );
          })}
        </div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 12) OUTRO + CREDITS (remaining ~13.6s, 6 bars) ----------
const Outro: React.FC<{base: number}> = ({base}) => {
  const f = useCurrentFrame(); const l = f - base;
  const {fps} = useVideoConfig();
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{background:'radial-gradient(ellipse 70% 50% at 50% 40%, rgba(126,217,87,0.16), transparent 70%)'}} />
      <Camera tz={interpolate(l,[0,stepDur(24)],[-120,40],clamp)}>
        <div style={{position:'absolute', left:0, right:0, top:70, textAlign:'center'}}>
          <MetalText size={96} delay={0} tint="silver">SalmonLauncher</MetalText>
          <div style={{marginTop:14}}><Sub delay={6} size={32} color={COLOR.dim}>把复杂留给启动器，把时间留给游戏。</Sub></div>
        </div>

        <QRCard file="site.png" title="官方网站" url={LINKS.site} x={260} y={300} delay={8} size={170} />
        <QRCard file="github.png" title="GitHub 仓库" url={LINKS.github} x={760} y={300} delay={12} size={170} />
        <QRCard file="blog.png" title="作者博客" url={LINKS.blog} x={1260} y={300} delay={16} size={170} />

        <div style={{position:'absolute', left:120, right:120, bottom:150, padding:'22px 28px', borderRadius:18, background:'rgba(20,20,22,0.72)', border:`1px solid ${COLOR.line}`, backdropFilter:'blur(20px)', opacity: interpolate(l,[24,36],[0,1],clamp)}}>
          <div style={{fontFamily:FONT, fontSize:22, color:COLOR.ink, fontWeight:700, marginBottom:10}}>制作人员 · 致谢名单 · 版权与声明</div>
          <div style={{fontFamily:FONT, fontSize:17.5, color:COLOR.dim, lineHeight:1.75}}>
            <div>{CREDITS.maker}</div>
            <div>{CREDITS.thanks}</div>
            <div>{CREDITS.license}　·　{CREDITS.music}</div>
            <div>{CREDITS.assets}</div>
            <div style={{color:COLOR.accent}}>{CREDITS.blog}</div>
            <div style={{marginTop:6, fontSize:15.5, color:'rgba(134,134,139,0.95)'}}>{CREDITS.disclaimer}</div>
          </div>
        </div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- main timeline (beat-aligned, fast cuts) ----------
export const Film: React.FC = () => {
  // segment plan in beats (startBeat, durBeats)
  const seg = (startBeat: number, durBeats: number) => ({from: BF(startBeat), dur: stepDur(durBeats)});
  // Timeline: 320 beats ~177.8s, phase offset applied in BF
  // 0-16 impact, 16-32 hero, 32-68 onboarding (36 beats=6 steps*6), 68-84 launch, 84-116 install, 116-148 instance, 148-180 content, 180-200 online, 200-228 deep tour, 228-244 blog, 244-260 montage, 260-end outro
  const S = {
    impact: seg(0, 16),
    hero: seg(16, 16),
    onboard: seg(32, 36),
    launch: seg(68, 16),
    install: seg(84, 32),
    instance: seg(116, 32),
    content: seg(148, 32),
    online: seg(180, 20),
    tour: seg(200, 28), // 7 items * 4 beats? 28/7=4 beats each
    blog: seg(228, 16),
    montage: seg(244, 16),
    outro: {from: BF(260), dur: toF(FILM.duration) - BF(260)},
  };

  // global beat flash layer
  const f = useCurrentFrame();
  const t = f / FILM.fps;
  let flash = 0;
  for (let i=0;i<BEAT_COUNT;i+=1){ // every beat subtle, downbeats stronger
    const d = i%4===0 ? 0.22 : (i%2===0 ? 0.1 : 0.05);
    const w = i%4===0 ? 0.12 : 0.06;
    flash = Math.max(flash, hit(t,i,w) * d);
  }

  return (
    <Stage>
      <Audio src={staticFile(AUDIO_FILE)} />

      <Sequence from={S.impact.from} durationInFrames={S.impact.dur}><Impact local={0} /></Sequence>
      {/* For components using absolute frame we pass base; easier: wrap with local by subtracting in render via useCurrentFrame-base? but useCurrentFrame is global. So pass base and read global inside with offset. */}
      <Sequence from={S.hero.from} durationInFrames={S.hero.dur}><Hero base={S.hero.from} /></Sequence>
      <Sequence from={S.onboard.from} durationInFrames={S.onboard.dur}><Onboarding base={S.onboard.from} /></Sequence>
      <Sequence from={S.launch.from} durationInFrames={S.launch.dur}><LaunchUse base={S.launch.from} /></Sequence>
      <Sequence from={S.install.from} durationInFrames={S.install.dur}><InstallFlow base={S.install.from} /></Sequence>
      <Sequence from={S.instance.from} durationInFrames={S.instance.dur}><InstanceFlow base={S.instance.from} /></Sequence>
      <Sequence from={S.content.from} durationInFrames={S.content.dur}><ContentFlow base={S.content.from} /></Sequence>
      <Sequence from={S.online.from} durationInFrames={S.online.dur}><OnlineFlow base={S.online.from} /></Sequence>
      <Sequence from={S.tour.from} durationInFrames={S.tour.dur}><DeepTour base={S.tour.from} /></Sequence>
      <Sequence from={S.blog.from} durationInFrames={S.blog.dur}><BlogLinks base={S.blog.from} /></Sequence>
      <Sequence from={S.montage.from} durationInFrames={S.montage.dur}><Montage base={S.montage.from} /></Sequence>
      <Sequence from={S.outro.from} durationInFrames={Math.max(1,S.outro.dur)}><Outro base={S.outro.from} /></Sequence>

      {/* global beat punch */}
      {flash > 0.001 && <AbsoluteFill style={{backgroundColor:'#fff', opacity: flash*0.18, mixBlendMode:'screen', pointerEvents:'none'}} />}
      <LegalStrip />
    </Stage>
  );
};
