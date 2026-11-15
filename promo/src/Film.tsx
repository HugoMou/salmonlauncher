// Film.tsx — SalmonLauncher 宣传片
// 355.2s，108 BPM《3 Strikes》播放两遍，全程按拍点卡点。
// 素材：真实操作录屏（光标移动、按钮点击、下载进度、资源校验、游戏画面）+ 干净页面截图。
// 风格：Apple keynote — 黑底舞台、金属标题、3D 飞入定格、环绕卡片矩阵、相机环绕。
import React from 'react';
import {AbsoluteFill, Audio, Img, Sequence, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {AUDIO_FILE, CLIP_MARKS, CLIPS, COLOR, FILM, FONT, LINKS, SHOTS, toF} from './spec';
import {BEAT_COUNT, PERIOD, beatFrame, hit} from './lib/beats';
import {Big, Camera, ClickFx, LegalStrip, MetalText, Panel, QRCard, RealProgress, Shot3D, Stage, Sub, Tag, VideoClip, beatPunch, clamp, easeOut} from './lib/fx';

const BF = (i: number) => beatFrame(i, FILM.fps);
// 注意：<Sequence> 内 useCurrentFrame() 返回的是「序列内相对帧」，因此章节时间一律用相对拍点。
const LB = (i: number) => Math.round(i * PERIOD * FILM.fps);
const stepDur = (beats: number) => Math.round(beats * PERIOD * FILM.fps);

// ---------- 通用叠加层 ----------
// 光标（用于在真实录屏上再强调一次点击位置）
const Cursor: React.FC<{x1:number;y1:number;x2:number;y2:number;at:number;dur:number}> = ({x1,y1,x2,y2,at,dur}) => {
  const l = useCurrentFrame();
  const p = interpolate(l,[at,at+dur],[0,1],{...clamp, easing:easeOut});
  const cx = x1 + (x2-x1)*p, cy = y1 + (y2-y1)*p;
  const o = interpolate(l,[at-2,at,at+dur,at+dur+10],[0,1,1,0],clamp);
  return (
    <div style={{position:'absolute',left:cx,top:cy,opacity:o,transform:'translate(-2px,-2px)',filter:`drop-shadow(0 0 12px ${COLOR.accent})`,zIndex:60}}>
      <svg width="44" height="54" viewBox="0 0 38 46" fill="none">
        <path d="M2 2L2 38L11 29L17 43L22 41L16 27L28 27L2 2Z" fill={COLOR.accent} stroke="#000" strokeWidth="2" strokeLinejoin="round"/>
      </svg>
    </div>
  );
};

const Pulse: React.FC<{x:number;y:number;at:number;c?:string}> = ({x,y,at,c=COLOR.accent}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 26) return null;
  const s = interpolate(t,[0,18],[0.2,2.6],{extrapolateRight:'clamp', easing:easeOut});
  const o = interpolate(t,[0,2,18],[0,1,0],clamp);
  return <div style={{position:'absolute',left:x-34,top:y-34,width:68,height:68,borderRadius:999,border:`4px solid ${c}`,opacity:o,transform:`scale(${s})`,boxShadow:`0 0 40px ${c}`,zIndex:59}}/>;
};

// 完整的真实点击演示：光标移动 → 点击光环 → 脉冲 + 标签
const ClickPoint: React.FC<{x:number;y:number;at:number;label?:string}> = ({x,y,at,label}) => {
  const l = useCurrentFrame();
  const t = l - at;
  if (t < 0 || t > 46) return null;
  const flash = interpolate(t,[0,8,16],[0,0.28,0],clamp);
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{backgroundColor:'#fff',opacity:flash,mixBlendMode:'screen',pointerEvents:'none'}}/>
      <Cursor x1={x-160} y1={y-90} x2={x} y2={y} at={at} dur={stepDur(1.2)}/>
      <ClickFx x={x} y={y} at={at+stepDur(1.2)} label={label} size={1.2}/>
      <Pulse x={x} y={y} at={at+stepDur(2.2)}/>
    </AbsoluteFill>
  );
};

// 要点逐行飞入（每拍一行）
const Bullet: React.FC<{text:string;x:number;y:number;delay:number;accent?:boolean}> = ({text,x,y,delay,accent}) => {
  const l = useCurrentFrame(); const {fps}=useVideoConfig();
  const p = spring({frame:l-delay,fps,config:{damping:16,stiffness:150}});
  const o = interpolate(l,[delay,delay+4],[0,1],clamp);
  return (
    <div style={{position:'absolute',left:x,top:y,opacity:o,transform:`translateX(${(1-p)*40}px)`,zIndex:20}}>
      <span style={{color:accent?COLOR.accent:COLOR.dim,fontWeight:accent?700:500,marginRight:10}}>▸</span>
      <span style={{fontFamily:FONT,fontSize:22,color:COLOR.ink,fontWeight:accent?700:500}}>{text}</span>
    </div>
  );
};

// 章节入场：4 拍快速飞入定格，相机环绕，主标题弹出；要点与右侧辅助卡随后出现
// base 参数保留为「章节内部相对起点」，Sequence 内各章节统一传 0。
const Chapter: React.FC<{
  base:number; dur:number; head:string; sub?:string;
  // 主视觉：截图或录屏
  file?:string; clip?:{src:string; start?:number};
  cardX?:number; cardY?:number; cardW?:number; cardH?:number; cardRy?:number; cardRx?:number; cardZ?:number;
  camRy?:number; camRx?:number; camZ?:number;
  click?:{x:number;y:number;label:string;atRel:number};
  bullets?:string[]; sideFile?:string; sideClip?:{src:string;start?:number};
  sideX?:number; sideY?:number; sideW?:number; sideH?:number;
  progress?:{pct:number;label:string;detail?:string;atRel:number;x:number;y:number;w:number};
  deco?:boolean; closeup?:boolean; mute?:boolean;
}> = ({base,dur,head,sub,file,clip,cardX=960,cardY=520,cardW=900,cardH,cardRy=-10,cardRx=6,cardZ=0,
      camRy=0,camRx=4,camZ=140,click,bullets,sideFile,sideClip,sideX=70,sideY=380,sideW=430,sideH,
      progress,deco=false,closeup=false,mute=true}) => {
  const l = useCurrentFrame();
  const ls = l - base;
  const entry = stepDur(4);
  const camRyT = interpolate(ls,[0,entry,dur],[camRy-20,camRy,camRy+10],clamp);
  const camRxT = interpolate(ls,[0,entry,dur],[camRx+8,camRx,camRx-2],clamp);
  const camZT = interpolate(ls,[0,entry,dur],[camZ-300,camZ+20,camZ-10],clamp);
  const cardEntryZ = interpolate(ls,[0,entry],[cardZ-520,cardZ],clamp);
  const cardEntryRy = interpolate(ls,[0,entry],[cardRy+40,cardRy],clamp);
  const cardEntryRx = interpolate(ls,[0,entry],[cardRx+20,cardRx],clamp);
  const cardEntryScale = interpolate(ls,[0,entry],[0.55,1],clamp);
  const cardRyT = interpolate(ls,[entry,dur],[cardRy-10,cardRy+10],clamp);
  const cardRxT = interpolate(ls,[entry,dur],[cardRx-3,cardRx+3],clamp);
  const flyScale = ls < entry ? cardEntryScale : 1 + 0.035*Math.max(0,1-((ls-entry)%stepDur(1))/(stepDur(1)*0.6))**2;
  const punchScale = beatPunch(ls, FILM.fps, PERIOD, 0.04);
  const isClose = closeup && ls > dur*0.55;
  const cw = isClose ? cardW*1.28 : cardW;
  const cy = isClose ? cardY-36 : cardY;
  const cz = isClose ? cardZ-120 : cardZ;
  const opacity = interpolate(ls,[0,entry],[0,1],clamp);
  const bulletStart = entry + stepDur(1.5);
  const usingClip = !!clip;
  return (
    <AbsoluteFill style={{opacity}}>
      <Camera ry={camRyT} rx={camRxT} tz={camZT} scale={punchScale}>
        <div style={{position:'absolute',left:0,right:0,top:120,textAlign:'center',zIndex:25}}>
          <MetalText size={104} delay={0} from="scale" tint="silver">{head}</MetalText>
          {sub && <div style={{marginTop:12}}><Sub delay={3} size={29} color={COLOR.dim} maxWidth={760}>{sub}</Sub></div>}
        </div>

        {usingClip ? (
          <VideoClip src={clip!.src} start={clip!.start ?? 0} x={cardX} y={cy} w={cw} h={cardH}
            delay={0} ry={ls<entry?cardEntryRy:cardRyT} rx={ls<entry?cardEntryRx:cardRxT} z={ls<entry?cardEntryZ:cz}
            floatAmp={12} scale={flyScale*1.03} mute={mute}/>
        ) : file ? (
          <Shot3D file={file} x={cardX} y={cy} w={cw} h={cardH}
            delay={0} ry={ls<entry?cardEntryRy:cardRyT} rx={ls<entry?cardEntryRx:cardRxT} z={ls<entry?cardEntryZ:cz}
            floatAmp={14} scale={flyScale*1.03}/>
        ) : null}

        {deco && (file || usingClip) && (
          <>
            {sideFile && <Shot3D file={sideFile} x={sideX} y={sideY} w={sideW} delay={bulletStart} ry={12} rx={7} z={-90} floatAmp={8}/>}
            {sideClip && <VideoClip src={sideClip.src} start={sideClip.start ?? 0} x={sideX} y={sideY} w={sideW} h={sideH}
              delay={bulletStart} ry={12} rx={7} z={-90} floatAmp={6} mute/>}
          </>
        )}

        {bullets && bullets.map((b,i)=>(
          <Bullet key={i} text={b} x={sideX} y={sideY + (sideFile||sideClip ? (sideH ?? Math.round(sideW*0.72)) + 44 : 0) + i*54}
            delay={bulletStart + i*stepDur(1)} accent={i===0}/>
        ))}

        {progress && <RealProgress x={progress.x} y={progress.y} w={progress.w} pct={progress.pct}
          label={progress.label} detail={progress.detail} delay={bulletStart + stepDur(progress.atRel)}/>}

        {click && <ClickPoint x={click.x} y={click.y} at={base + click.atRel} label={click.label}/>}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 1) 冲击开场（0-24 拍）----------
const Impact: React.FC<{base:number}> = ({base}) => {
  const l = useCurrentFrame();
  const hits = [0,2,4,6,8,10,12,14,16,18,20,22];
  return (
    <AbsoluteFill>
      {hits.map((b,i)=>{
        const o = interpolate(l,[BF(b),BF(b)+1,BF(b)+4],[0, i%4===0?0.45:0.22, 0],clamp);
        return <AbsoluteFill key={b} style={{backgroundColor:i%2?COLOR.accent:'#fff',opacity:o,mixBlendMode:'screen'}}/>;
      })}
      <Camera tz={interpolate(l,[0,stepDur(24)],[-400,150],clamp)}
        rx={interpolate(l,[0,stepDur(12)],[10,0],clamp)}
        ry={interpolate(l,[0,stepDur(24)],[-8,8],clamp)}
        scale={beatPunch(l,30,PERIOD,0.06)}>
        {l>=BF(0) && l<BF(5) && (
          <AbsoluteFill style={{display:'flex',flexDirection:'column',justifyContent:'center',alignItems:'center'}}>
            <MetalText size={128} delay={0} from="scale" tint="silver">启动。</MetalText>
          </AbsoluteFill>
        )}
        {l>=BF(7) && l<BF(12) && (
          <AbsoluteFill style={{display:'flex',flexDirection:'column',justifyContent:'center',alignItems:'center'}}>
            <MetalText size={106} delay={0} from="scale" tint="gold">极简。</MetalText>
          </AbsoluteFill>
        )}
        {l>=BF(14) && l<BF(19) && (
          <AbsoluteFill style={{display:'flex',flexDirection:'column',justifyContent:'center',alignItems:'center'}}>
            <MetalText size={128} delay={0} from="scale" tint="blue">无痕迹。</MetalText>
          </AbsoluteFill>
        )}
        {l>=BF(20) && (
          <AbsoluteFill style={{display:'flex',flexDirection:'column',justifyContent:'center',alignItems:'center'}}>
            <MetalText size={72} delay={0} from="scale" tint="green">SalmonLauncher</MetalText>
          </AbsoluteFill>
        )}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 2) 首次启动引导（真实录屏，6 个片段）----------
const Onboarding: React.FC<{base:number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(10);
  const m = CLIP_MARKS.onboarding;
  const steps = [
    {start:m.lang,    head:'选择语言',       sub:'简体中文 / 繁體中文 / English / 日本語，选择后立即生效', bullets:['多语言界面一键切换','选择后立即生效','随时可在设置中更改']},
    {start:m.terms,   head:'阅读并同意条款', sub:'用户协议与免责声明完整展示，勾选后继续',              bullets:['条款全文清晰可读','勾选同意后继续','无隐藏条款']},
    {start:m.migrate, head:'迁移数据（可选）', sub:'可一键识别旧启动器，也可手动选择 .minecraft 目录',   bullets:['一键识别旧启动器','手动指定数据文件夹','本步可随时跳过']},
    {start:m.account, head:'添加账户',       sub:'离线账户与微软账号登录，之后可随时管理',              bullets:['离线账户即开即用','微软账号正版登录','随时切换管理']},
    {start:m.theme,   head:'外观与配色',     sub:'内置 Minecraft 场景壁纸，配色自动从画面提取',        bullets:['内置场景壁纸','配色自动取色','云母材质默认关闭']},
    {start:m.done,    head:'完成引导',       sub:'初始化完成，直接进入启动器开始使用',                  bullets:['初始化完成','直接进入启动器','开始使用']},
  ];
  const idx = Math.min(steps.length-1, Math.floor(l/seg));
  const s = steps[idx];
  return (
    <Chapter base={base+idx*seg} dur={seg} head={s.head} sub={s.sub}
      clip={{src:CLIPS.onboarding, start:s.start}}
      cardX={960} cardY={520} cardW={950} cardRy={idx%2?12:-12} cardRx={6} cardZ={40}
      camRy={idx%2?10:-10} camRx={6} camZ={120}
      click={{x:1000,y:930,label:s.head,atRel:stepDur(3.2)}}
      bullets={s.bullets} deco/>
  );
};

// ---------- 3) 安装与真实下载进度（录屏 + 进度条强调）----------
const Install: React.FC<{base:number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(20);
  const m = CLIP_MARKS.install;
  const stages = [
    {start:m.versions, head:'选择游戏版本', sub:'103 个版本完整清单，正式版 / 快照 / 远古版本分类筛选', bullets:['103 个版本完整清单','稳定版 / 快照 / 远古筛选','一键选定目标版本']},
    {start:m.loaders,  head:'选择加载器',   sub:'原版 / Fabric / Quilt / Forge / NeoForge 自由组合',   bullets:['原版纯净安装','Fabric / Quilt 轻量','Forge / NeoForge 生态']},
    {start:m.plan,     head:'确认安装概览', sub:'逐项列出下载任务与体积，含逐文件 SHA-1 校验说明',     bullets:['70 个下载任务逐项列出','逐文件 SHA-1 校验','安装位置清晰可见']},
    {start:m.downloading, head:'真实下载过程', sub:'多线程分段下载、镜像回退、断点续传，速度与进度实时可见', bullets:['多线程分段下载','镜像自动回退','断点续传','逐文件 SHA-1 校验'],
     progress:{pct:1, label:'下载与安装进度', detail:'真实录屏 · 进度与速度实时刷新', atRel:2, x:70, y:640, w:1320}},
  ];
  const idx = Math.min(stages.length-1, Math.floor(l/seg));
  const s = stages[idx];
  const local = l - idx*seg;
  // 最后一段进度条按录制内的真实推进节奏增长
  const pct = idx===3 ? interpolate(local,[stepDur(4),seg-8],[0.06,1],clamp) : 0;
  return (
    <Chapter base={base+idx*seg} dur={seg} head={s.head} sub={s.sub}
      clip={{src:CLIPS.install, start:s.start}}
      cardX={960} cardY={470} cardW={1080} cardRy={idx%2?12:-12} cardRx={6} cardZ={40}
      camRy={idx%2?10:-10} camRx={6} camZ={120}
      click={{x:1180,y:184,label:s.head,atRel:stepDur(3)}}
      bullets={s.bullets}
      progress={(s as any).progress ? {...(s as any).progress, pct} : undefined} deco/>
  );
};

// ---------- 4) 启动与进入游戏（真实下载 → 校验 → 游戏画面）----------
const LaunchPlay: React.FC<{base:number}> = ({base}) => {
  const l = useCurrentFrame();
  const m = CLIP_MARKS.launch;
  const g = CLIP_MARKS.game;
  const half = stepDur(20);
  const half2 = stepDur(36);
  const third = stepDur(56);
  // 三段：点击启动 / 资源下载与校验 / 进入游戏
  if (l < base+half) {
    const local = l-base;
    return (
      <Chapter base={base} dur={half} head="点击启动" sub="选择实例与账户，一键进入世界"
        clip={{src:CLIPS.launch, start:m.click}}
        cardX={960} cardY={480} cardW={1060} cardRy={-12} cardRx={6} cardZ={40}
        camRy={0} camRx={6} camZ={120}
        click={{x:1200,y:470,label:'启动 Minecraft',atRel:stepDur(4)}}
        bullets={['实例与账户一目了然','点击「启动 Minecraft」','状态实时反馈']}
        progress={{pct:interpolate(local,[stepDur(5),half-6],[0.05,0.32],clamp),label:'定位 Java 与准备资源',detail:'真实录屏 · 启动准备阶段',atRel:3,x:70,y:660,w:1320}} deco/>
    );
  }
  if (l < base+half2) {
    const local = l-(base+half);
    return (
      <Chapter base={base+half} dur={half2-half} head="下载与校验资源" sub="首次启动自动补齐游戏资源，全程进度可见"
        clip={{src:CLIPS.launch, start:m.downloading}}
        cardX={960} cardY={470} cardW={1080} cardRy={12} cardRx={6} cardZ={40}
        camRy={0} camRx={6} camZ={120}
        bullets={['首次启动补齐资源对象','分段下载 · 失败自动重试','逐文件校验完整性','无需任何手动干预']}
        progress={{pct:interpolate(local,[stepDur(3),half2-half-6],[0.3,1],clamp),label:'资源下载与校验',detail:'真实录屏 · 进度与速度实时刷新',atRel:2,x:70,y:640,w:1320}} deco/>
    );
  }
  const local = l-(base+half2);
  return (
    <Chapter base={base+half2} dur={third-half2} head="进入游戏 · 开始畅玩" sub="游戏窗口打开，直接进入 Minecraft 主菜单"
      clip={{src:CLIPS.game, start:g.menu}}
      cardX={960} cardY={470} cardW={1180} cardH={650} cardRy={-10} cardRx={5} cardZ={60}
      camRy={0} camRx={5} camZ={110}
      click={{x:1120,y:520,label:'游戏主菜单',atRel:stepDur(6)}}
      bullets={['游戏窗口自动打开','进入 Minecraft 主菜单','启动器同步显示运行状态']}
      progress={{pct:1,label:'启动完成',detail:'真实录屏 · Minecraft 1.21.11 主菜单',atRel:2,x:70,y:670,w:1320}} deco/>
  );
};

// ---------- 5) 实例管理（真实录屏）----------
const Instances: React.FC<{base:number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(16);
  const t = CLIP_MARKS.tour;
  const stages = [
    {start:t.overview, head:'实例概览', sub:'版本、加载器、状态一目了然，支持快速切换与启动', bullets:['所有实例一览','版本 / 加载器 / 状态','快速切换与启动']},
    {start:t.mods,     head:'模组管理', sub:'实例内模组统一管理，启用停用即时生效',              bullets:['模组统一管理','启用 / 停用','即时生效']},
    {start:t.saves,    head:'存档管理', sub:'备份、恢复、重命名存档，自定义存档路径',            bullets:['一键备份存档','随时恢复','自定义存档路径']},
    {start:t.cfg,      head:'实例设置', sub:'实例名称、游戏目录、Java 路径、启动参数详细配置',    bullets:['实例名称与目录','Java 路径设置','启动参数配置']},
    {start:t.logs,     head:'实例日志', sub:'实时读取启动日志与崩溃报告，关键词过滤定位问题',    bullets:['日志实时读取','崩溃报告分析','关键词过滤']},
  ];
  const idx = Math.min(stages.length-1, Math.floor(l/seg));
  const s = stages[idx];
  return (
    <Chapter base={base+idx*seg} dur={seg} head={s.head} sub={s.sub}
      clip={{src:CLIPS.tour, start:s.start}}
      cardX={960} cardY={480} cardW={1060} cardRy={idx%2?12:-12} cardRx={6} cardZ={40}
      camRy={idx%2?10:-10} camRx={6} camZ={120}
      click={{x:188,y:idx<5?[454,514,574,634,694][idx]:480,label:s.head,atRel:stepDur(3)}}
      bullets={s.bullets} deco/>
  );
};

// ---------- 6) 内容仓库（真实录屏）----------
const Content: React.FC<{base:number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(18);
  const t2 = CLIP_MARKS.tour2;
  const t = CLIP_MARKS.tour;
  const stages = [
    {start:t2.repoMods, head:'模组仓库', sub:'集成 Modrinth 模组仓库，搜索、筛选、一键安装', bullets:['Modrinth 仓库集成','搜索与筛选','一键安装']},
    {start:t2.shaders,  head:'光影仓库', sub:'光影包浏览与安装，让画面更出彩',                 bullets:['光影包浏览','一键安装','画质提升']},
    {start:t2.packs,    head:'资源包仓库', sub:'资源包浏览与切换，自定义纹理与音效',           bullets:['资源包浏览','一键切换','自定义纹理']},
    {start:t2.instPacks,head:'实例资源包', sub:'实例内统一管理已安装的模组、光影与资源包',      bullets:['实例内容统一管理','启用 / 禁用','版本控制']},
    {start:t2.instShaders, head:'实例光影', sub:'实例内光影统一管理，随时切换',               bullets:['实例光影管理','随时切换','状态可见']},
  ];
  const idx = Math.min(stages.length-1, Math.floor(l/seg));
  const s = stages[idx];
  return (
    <Chapter base={base+idx*seg} dur={seg} head={s.head} sub={s.sub}
      clip={{src:CLIPS.tour2, start:s.start}}
      cardX={960} cardY={480} cardW={1080} cardRy={idx%2?-14:14} cardRx={6} cardZ={50}
      camRy={idx%2?-12:12} camRx={6} camZ={130}
      click={{x:188,y:480,label:s.head,atRel:stepDur(3)}}
      bullets={s.bullets} closeup={idx===2} deco/>
  );
};

// ---------- 7) 联机 / 皮肤 / 系统与设置（真实录屏）----------
const Tools: React.FC<{base:number}> = ({base}) => {
  const l = useCurrentFrame();
  const seg = stepDur(14);
  const t = CLIP_MARKS.tour;
  const items = [
    {start:t.online, head:'联机 · Campfire', sub:'随附 Campfire：进入世界即对局域网开放并生成邀请码', bullets:['一键开放世界','生成邀请码','好友粘贴即可加入']},
    {start:t.skins,  head:'皮肤站',          sub:'皮肤站集成与 3D 皮肤预览，外观所见即所得',           bullets:['皮肤站集成','3D 皮肤预览','一键更换']},
    {start:t.global, head:'全局偏好',        sub:'内存分配、下载源与镜像等全局参数',                   bullets:['内存分配','下载源与镜像','即时生效']},
    {start:t.theme,  head:'外观与设置',      sub:'主题、强调色与云母材质开关（默认关闭）',             bullets:['深浅色主题','强调色自定义','云母默认关闭']},
    {start:t.diag,   head:'崩溃诊断',        sub:'解析崩溃报告并给出建议，启动失败可直达诊断',         bullets:['解析崩溃报告','给出修复建议','启动失败直达']},
    {start:t.applog, head:'应用日志',        sub:'日志尾部实时读取、关键词过滤，问题可定位',           bullets:['日志实时读取','关键词过滤','问题可定位']},
    {start:t.about,  head:'关于与更新',      sub:'版本信息、开源许可、检查启动器更新',                 bullets:['版本信息','开源许可 GPL-3.0','检查更新']},
    {start:t.terms,  head:'用户协议',        sub:'用户协议与免责声明完整可查',                         bullets:['用户协议全文','免责声明','透明公开']},
  ];
  const idx = Math.min(items.length-1, Math.floor(l/seg));
  const it = items[idx];
  return (
    <Chapter base={base+idx*seg} dur={seg} head={it.head} sub={it.sub}
      clip={{src:CLIPS.tour, start:it.start}}
      cardX={960} cardY={480} cardW={1060} cardRy={idx%2?12:-12} cardRx={6} cardZ={40}
      camRy={idx%2?10:-10} camRx={6} camZ={120}
      click={{x:188,y:480,label:it.head,atRel:stepDur(2.6)}}
      bullets={it.bullets} deco/>
  );
};

// ---------- 8) 功能全景快剪（真实截图矩阵 + 卡点）----------
const Montage: React.FC<{base:number}> = ({base}) => {
  const l = useCurrentFrame();
  const ls = l - base;
  const files = [SHOTS.home,SHOTS.tasks,SHOTS.versions,SHOTS.loaders,SHOTS.install,SHOTS.overview,SHOTS.mods,SHOTS.saves,SHOTS.instCfg,SHOTS.instLogs,SHOTS.repoMods,SHOTS.repoShaders,SHOTS.repoPacks,SHOTS.instPacks,SHOTS.instShaders,SHOTS.online,SHOTS.skins,SHOTS.diag,SHOTS.appLog,SHOTS.global,SHOTS.launchSet,SHOTS.theme,SHOTS.about,SHOTS.terms];
  const span = stepDur(44);
  const perCard = Math.floor(span / files.length);
  const cardW = 300, gap = 16, cols = 4;
  return (
    <AbsoluteFill>
      <Camera tz={interpolate(ls,[0,span],[-520,210],clamp)} ry={interpolate(ls,[0,span],[-24,24],clamp)} rx={8}
        scale={beatPunch(ls,30,PERIOD,0.09)}>
        <div style={{position:'absolute',left:90,top:82,zIndex:30}}>
          <Tag text="功能全景 · 快速剪辑" accent/>
          <div style={{marginTop:14}}><MetalText size={76} delay={1} from="bottom" tint="silver" align="left">全部功能，一应俱全。</MetalText></div>
        </div>
        {files.map((file,i)=>{
          const d = i*perCard;
          const row = Math.floor(i/cols), col = i%cols;
          const p = interpolate(ls,[d,d+16],[0,1],clamp);
          const punch = beatPunch(ls-d,30,PERIOD,0.06);
          return (
            <div key={file+i} style={{position:'absolute',left:col*(cardW+gap),top:250+row*(216+gap),width:cardW,height:216,
              transformStyle:'preserve-3d',
              transform:`translateZ(${(1-p)*-620}px) rotateY(${(col-1.5)*6}deg) rotateX(${(1-p)*32}deg) scale(${(0.74+p*0.26)*punch})`,
              opacity:interpolate(ls,[d,d+3],[0,1],clamp)}}>
              <div style={{width:'100%',height:'100%',borderRadius:12,overflow:'hidden',border:`1px solid ${COLOR.line}`,boxShadow:'0 22px 60px rgba(0,0,0,0.78)'}}>
                <Img src={staticFile(`shots/${file}`)} style={{width:'100%',height:'100%',objectFit:'cover',objectPosition:'top left'}}/>
              </div>
            </div>
          );
        })}
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 9) 片尾：二维码 + 声明（含 AI 制作声明）----------
const Outro: React.FC<{base:number; total:number}> = ({base, total}) => {
  const l = useCurrentFrame();
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{background:'radial-gradient(ellipse 75% 55% at 50% 36%, rgba(126,217,87,0.18), transparent 70%)'}}/>
      <Camera tz={interpolate(l,[0,Math.min(total,stepDur(26))],[-170,50],clamp)}
        rx={interpolate(l,[0,total],[4,0],clamp)} scale={beatPunch(l,30,PERIOD,0.04)}>
        <div style={{position:'absolute',left:0,right:0,top:48,textAlign:'center'}}>
          <MetalText size={98} delay={0} from="scale" tint="silver">SalmonLauncher</MetalText>
          <div style={{marginTop:10}}><Sub delay={4} size={31} color={COLOR.dim}>把复杂留给启动器，把时间留给游戏。</Sub></div>
        </div>
        <QRCard file="site.png" title="官方网站" url={LINKS.site} x={250} y={272} delay={6} size={168}/>
        <QRCard file="github.png" title="GitHub 仓库" url={LINKS.github} x={650} y={272} delay={10} size={168}/>
        <QRCard file="blog.png" title="作者博客" url={LINKS.blog} x={1050} y={272} delay={14} size={168}/>
        <div style={{position:'absolute',left:96,right:96,bottom:126,padding:'20px 26px',borderRadius:18,
          background:'rgba(20,20,22,0.78)',border:`1px solid ${COLOR.line}`,
          opacity:interpolate(l,[18,28],[0,1],clamp),transform:`translateY(${interpolate(l,[18,28],[30,0],clamp)}px)`}}>
          <div style={{fontFamily:FONT,fontSize:21,color:COLOR.ink,fontWeight:700,marginBottom:8}}>视频声明 · 制作人员 · 致谢名单 · 版权解释</div>
          <div style={{fontFamily:FONT,fontSize:17,color:COLOR.dim,lineHeight:1.7}}>
            <div><span style={{color:COLOR.ink,fontWeight:600}}>制作人员：</span>Hugo Mou（产品设计 / 开发 / 维护）</div>
            <div><span style={{color:COLOR.ink,fontWeight:600}}>致谢名单：</span>Modrinth 内容平台与社区作者 · Campfire 联机组件 · .NET / Windows App SDK / WinUI 3 · Mojang 官方版本与资源生态</div>
            <div><span style={{color:COLOR.ink,fontWeight:600}}>配乐与素材：</span>“3 Strikes” — Terror Jr（来源：Bilibili BV1tek2YsEYY，本片仅作展示） · 画面为 SalmonLauncher 2.0.13 真实录屏与真实界面截图（全新默认状态，云母默认关闭，无个人使用痕迹）</div>
            <div style={{color:COLOR.accent,fontWeight:600,marginTop:2}}>本软件由 AI 辅助开发制作</div>
            <div style={{color:COLOR.accent,fontWeight:600}}>本宣传片由 AI 生成制作</div>
            <div><span style={{color:COLOR.ink,fontWeight:600}}>项目许可：</span>GPL-3.0-or-later（开源免费，Windows x64）　官网 {LINKS.siteFull}　GitHub {LINKS.githubFull}</div>
            <div style={{marginTop:6,fontSize:15,color:'rgba(134,134,139,0.95)'}}>版权与声明：Minecraft 为 Mojang Studios / Microsoft 的商标，本项目与 Mojang/Microsoft 无官方关联；Modrinth、Campfire 等名称与服务归各自权利人所有。本视频仅用于 SalmonLauncher 产品展示与介绍，不构成对第三方商标或内容的授权声明。</div>
          </div>
        </div>
      </Camera>
    </AbsoluteFill>
  );
};

// ---------- 时间线（拍为单位，共约 660 拍）----------
export const Film: React.FC = () => {
  const seg = (startBeat:number, durBeats:number) => ({from:BF(startBeat), dur:stepDur(durBeats)});
  const S = {
    impact:   seg(0,24),     // 24
    onboard:  seg(24,60),    // 60  引导 6 步
    install:  seg(84,80),    // 80  安装 4 段（含真实下载）
    launch:   seg(164,56),   // 56  启动 3 段（准备 / 下载校验 / 游戏）
    instance: seg(220,80),   // 80  实例 5 段
    content:  seg(300,90),   // 90  内容 5 段
    tools:    seg(390,112),  // 112 工具与设置 8 项
    montage:  seg(502,44),   // 44  功能全景快剪
  };
  const outroFrom = BF(546);
  const outroDur = Math.max(1, toF(FILM.duration) - outroFrom);

  const f = useCurrentFrame();
  const t = f / FILM.fps;
  let flash = 0;
  for (let i=0;i<BEAT_COUNT;i+=1) {
    const down = i%4===0;
    const d = down ? 0.22 : i%2===0 ? 0.12 : 0.06;
    const w = down ? 0.1 : 0.06;
    flash = Math.max(flash, hit(t,i,w)*d);
  }

  return (
    <Stage>
      <Audio src={staticFile(AUDIO_FILE)}/>
      <Sequence from={S.impact.from}   durationInFrames={S.impact.dur}><Impact base={0}/></Sequence>
      <Sequence from={S.onboard.from}  durationInFrames={S.onboard.dur}><Onboarding base={0}/></Sequence>
      <Sequence from={S.install.from}  durationInFrames={S.install.dur}><Install base={0}/></Sequence>
      <Sequence from={S.launch.from}   durationInFrames={S.launch.dur}><LaunchPlay base={0}/></Sequence>
      <Sequence from={S.instance.from} durationInFrames={S.instance.dur}><Instances base={0}/></Sequence>
      <Sequence from={S.content.from}  durationInFrames={S.content.dur}><Content base={0}/></Sequence>
      <Sequence from={S.tools.from}    durationInFrames={S.tools.dur}><Tools base={0}/></Sequence>
      <Sequence from={S.montage.from}  durationInFrames={S.montage.dur}><Montage base={0}/></Sequence>
      <Sequence from={outroFrom}       durationInFrames={outroDur}><Outro base={0} total={outroDur}/></Sequence>

      {flash > 0.001 && <AbsoluteFill style={{backgroundColor:'#fff',opacity:flash*0.28,mixBlendMode:'screen',pointerEvents:'none'}}/>}
      <AbsoluteFill style={{boxShadow:'inset 0 0 180px rgba(0,0,0,0.45)',pointerEvents:'none'}}/>
      <LegalStrip/>
    </Stage>
  );
};
