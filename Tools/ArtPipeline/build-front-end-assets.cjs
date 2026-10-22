// Front-end kit: new painted sources + editable extensions of the existing gold/iron SVG UI.
const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { sharp, root } = require('./deps.cjs');
const art = path.join(root, 'Art/Production/front-end-v1');
const dst = path.join(root, 'Assets/Resources/Art/FrontEnd/v1');
const assets = [];
const gold = '#BE9B5C';
const defs = `<defs>
 <linearGradient id="metal" x1="0" y1="0" x2=".65" y2="1"><stop stop-color="#F0D79B"/><stop offset=".24" stop-color="#B68E48"/><stop offset=".5" stop-color="#70512C"/><stop offset=".72" stop-color="#D6B576"/><stop offset="1" stop-color="#75562C"/></linearGradient>
 <linearGradient id="iron" x2=".1" y2="1"><stop stop-color="#222C31"/><stop offset=".45" stop-color="#10181D"/><stop offset="1" stop-color="#080D11"/></linearGradient>
 <linearGradient id="blue" x2=".2" y2="1"><stop stop-color="#244A64"/><stop offset=".5" stop-color="#142C43"/><stop offset="1" stop-color="#0C182B"/></linearGradient>
 <linearGradient id="hover" x2=".2" y2="1"><stop stop-color="#334C5A"/><stop offset="1" stop-color="#15232F"/></linearGradient>
 <radialGradient id="halo"><stop stop-color="#527EAC" stop-opacity="0"/><stop offset=".58" stop-color="#527EAC" stop-opacity="0"/><stop offset=".8" stop-color="#6B9FCD" stop-opacity=".26"/><stop offset="1" stop-color="#4673AB" stop-opacity="0"/></radialGradient>
</defs>`;
const line = (d,c=gold,w=2) => `<path d="${d}" fill="none" stroke="${c}" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round"/>`;
const states = ['normal','hover','pressed','selected','disabled'];
const colors = { normal:gold, hover:'#E4CA8E', pressed:'#A88446', selected:'#F0CD7B', disabled:'#555A5E' };
function svg(w,h,body) { return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${defs}${body}</svg>`; }
function corners(w,h,c) {
 const scale = Math.min(1, Math.min(w,h)/224);
 const body = line('M14 72V14H72 M21 54V21H54 M25 36C41 44 47 24 33 25C30 31 37 35 43 28 M27 52L52 27',c,1.8)
   + `<path d="M50 14L59 8 68 14 59 20Z" fill="${c}"/>`;
 return [[0,0,1,1],[w,0,-1,1],[0,h,1,-1],[w,h,-1,-1]].map(([x,y,sx,sy])=>`<g transform="translate(${x} ${y}) scale(${sx*scale} ${sy*scale})">${body}</g>`).join('');
}
function frame(w,h,state='normal',fill='url(#iron)',transparent=false) {
 const c=colors[state], hot=state==='selected';
 return `<rect x="6" y="6" width="${w-12}" height="${h-12}" rx="3" fill="${transparent?'none':fill}" stroke="#05090C" stroke-width="10"/>
 <rect x="8" y="8" width="${w-16}" height="${h-16}" rx="2" fill="none" stroke="${hot?'#466B89':'#373A35'}" stroke-width="${hot?6:4}"/>
 <rect x="8" y="8" width="${w-16}" height="${h-16}" rx="2" fill="none" stroke="${c}" stroke-width="${hot?2.8:1.8}"/>
 <rect x="14" y="14" width="${w-28}" height="${h-28}" rx="1" fill="none" stroke="${c}" stroke-opacity=".3" stroke-width="1"/>
 ${corners(w,h,c)}`;
}
async function record(group,key,w,h,extra={}) {
 const file=path.join(dst,group,key+'.png');
 const bytes=await fs.readFile(file), meta=await sharp(bytes).metadata();
 assets.push({key,group,path:`${group}/${key}.png`,resourcesKey:`Art/FrontEnd/v1/${group}/${key}`,
  width:w,height:h,nativeAlpha:!!meta.hasAlpha,bytes:bytes.length,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),...extra});
}
async function emit(group,key,w,h,body,extra={}) {
 const source=`Art/Production/front-end-v1/svg/${group}/${key}.svg`;
 await fs.mkdir(path.dirname(path.join(root,source)),{recursive:true});
 await fs.mkdir(path.join(dst,group),{recursive:true});
 const text=svg(w,h,body);
 await fs.writeFile(path.join(root,source),text);
 await sharp(Buffer.from(text)).png().toFile(path.join(dst,group,key+'.png'));
 await record(group,key,w,h,{source,kind:'editable-svg',...extra});
}
async function painted() {
 const spec=JSON.parse(await fs.readFile(path.join(art,'prompts/image-prompts.json'),'utf8'));
 for(const a of spec.assets) {
  const source=`Art/Production/front-end-v1/source/${a.key}.png`;
  const meta=await sharp(path.join(root,source)).metadata();
  if(a.transparent&&!meta.hasAlpha) throw Error('Transparent source has no alpha: '+a.key);
  await fs.mkdir(path.join(dst,a.group),{recursive:true});
  const pad=a.key.startsWith('mode_')?16:0;
  let image=sharp(path.join(root,source)).resize(a.width-2*pad,a.height-2*pad,{fit:a.transparent?'contain':'cover',background:'#00000000',position:'centre'});
  if(pad)image=image.extend({top:pad,bottom:pad,left:pad,right:pad,background:'#00000000'});
  await image.png().toFile(path.join(dst,a.group,a.file));
  await record(a.group,a.key,a.width,a.height,{source,kind:'imagegen',nativeWidth:meta.width,nativeHeight:meta.height,
   resampling:a.transparent?`uniform contain; ${pad}px transparent canvas padding; preserved alpha`:'uniform cover; centered crop',upscaled:a.width>meta.width||a.height>meta.height,
   textSafe:a.group==='Backgrounds'?(a.key.startsWith('menu_')?[.30,.08,.40,.74]:[.12,.08,.76,.84]):a.key==='codex_parchment'?[.12,.10,.76,.80]:null,
   usage:a.group==='Backgrounds'?'Opaque cover background':'Simple Image, preserve aspect, white tint'});
 }
}
async function ui() {
 await emit('UI','screen_frame',1280,768,frame(1280,768,'normal','none',true),{border:[96,96,96,96],usage:'Sliced transparent full-screen trim; no raycast'});
 for(const [key,w,h]of [['panel_content',960,640],['panel_sidebar',384,768],['panel_detail',480,768],['panel_tooltip',512,256]])
  await emit('UI',key,w,h,frame(w,h),{border:[80,80,80,80],usage:'Sliced panel; dark text area'});
 for(const state of states) {
  const fill=state==='selected'?'url(#blue)':state==='hover'?'url(#hover)':state==='pressed'?'#0D1824':state==='disabled'?'#101519':'url(#iron)';
  for(const [kind,w,h,border]of [['mode_button',640,144,[64,48,64,48]],['tab',256,80,[40,32,40,32]],['list_row',384,96,[40,32,40,32]],['roster_tile',240,240,[80,80,80,80]],['icon_button',96,96,[32,32,32,32]]])
   await emit('UI',`${kind}_${state}`,w,h,frame(w,h,state,fill),{border,state,usage:'Sliced; sprite state rather than tint the gold border'});
 }
 await emit('UI','selection_halo',256,256,`<circle cx="128" cy="128" r="124" fill="url(#halo)"/>`,{usage:'Simple; behind selected class emblem; not another gold ring'});
 await emit('UI','title_banner',576,96,`<path d="M8 48L39 13H537L568 48 537 83H39Z" fill="url(#iron)" stroke="url(#metal)" stroke-width="2"/>${line('M64 22H512 M64 74H512','#6B5738',1)}`,{border:[80,32,80,32]});
 await emit('UI','divider_horizontal',512,32,line('M4 16H224 M288 16H508',gold,1)+line('M232 16L246 10 256 16 266 22 280 16 M246 10L256 4 266 10 256 16Z',gold,1.3),{border:[80,8,80,8]});
 await emit('UI','divider_vertical',32,512,line('M16 4V224 M16 288V508',gold,1)+line('M16 232L10 246 16 256 22 266 16 280 M10 246L4 256 10 266 16 256Z',gold,1.3),{border:[8,80,8,80]});
 await emit('UI','scroll_track',24,256,`<rect x="7" y="4" width="10" height="248" rx="4" fill="#060B0F" stroke="#4E4736"/>`,{border:[8,24,8,24]});
 for(const state of ['normal','hover','pressed'])
  await emit('UI',`scroll_thumb_${state}`,24,80,`<path d="M12 3L20 12V68L12 77 4 68V12Z" fill="${state==='pressed'?'#5E492C':'url(#metal)'}" stroke="${colors[state]}"/>${line('M9 31H15 M9 39H15 M9 47H15','#483822',1.3)}`,{border:[8,24,8,24],state});
 for(const state of ['normal','hover','disabled'])for(const side of ['left','right'])
  await emit('UI',`page_${side}_${state}`,80,64,frame(80,64,state)+`<g transform="${side==='right'?'translate(80 0) scale(-1 1)':''}">${line('M47 19L32 32 47 45',colors[state],3)}</g>`,{usage:'Simple, preserve aspect',state});
 for(const state of ['normal','selected'])
  await emit('UI',`filter_ribbon_${state}`,128,384,`<path d="M8 8H120V374L64 345 8 374Z" fill="${state==='selected'?'url(#blue)':'url(#iron)'}" stroke="${colors[state]}" stroke-width="2"/>${line('M16 16H112V358L64 332 16 358Z',colors[state],1)}`,{border:[32,64,32,48],usage:'Sliced vertical filter strip; reserve for optional category navigation'});
}
async function icons() {
 const sword=line('M30 102L88 35 M77 20L108 10 99 42 M76 25L92 39 M24 87L42 103',gold,5);
 const book=line('M64 35Q42 21 17 30V105Q43 94 64 109 85 94 111 105V30Q86 21 64 35Z M64 35V109 M28 44L52 49 M76 49L100 44 M28 60L52 65 M76 65L100 60',gold,3.5);
 const shield=line('M64 12L106 28 100 73Q90 103 64 117 38 103 28 73L22 28Z M64 28V96',gold,4);
 const circle=(x,y,r,c=gold,width=3)=>`<circle cx="${x}" cy="${y}" r="${r}" fill="none" stroke="${c}" stroke-width="${width}"/>`;
 const star=line('M64 10L72 43 103 32 83 57 116 64 83 72 103 101 72 83 64 116 56 83 25 101 45 72 12 64 45 57 25 32 56 43Z',gold,3);
 const paths={
  codex:book,
  rules:line('M32 20H104Q117 20 114 35H100V105H24Q9 105 13 89H28V33Q28 20 42 20 M39 47H86 M39 63H86 M39 79H74',gold,3.5),
  settings:circle(64,64,31)+circle(64,64,11)+Array.from({length:8},(_,i)=>`<path transform="rotate(${i*45} 64 64)" d="M56 33V17H72V33" fill="none" stroke="${gold}" stroke-width="4"/>`).join(''),
  quit:line('M77 110H25V17H77V38 M67 64H114 M99 49L114 64 99 79 M37 99V29H66',gold,4),
  back:line('M110 64H20 M49 34L19 64 49 94',gold,4),
  help:circle(64,64,47)+line('M48 48Q47 27 68 29 91 31 81 51L65 67V78 M64 94V96',gold,5),
  all:[24,72].flatMap(x=>[24,72].map(y=>`<rect x="${x}" y="${y}" width="32" height="32" rx="2" fill="${gold}"/>`)).join(''),
  melee:sword+`<g transform="translate(128 0) scale(-1 1)">${sword}</g>`,
  ranged:line('M38 12Q118 64 38 116 M38 12L63 64 38 116 M13 64H113 M96 48L113 64 96 80',gold,4),
  magic:line('M20 108L85 43 M73 26L90 9 98 32 120 40 98 49 90 72 82 49 61 40Z M26 30V49 M16 40H36',gold,4),
  support:line('M24 105L18 78Q19 62 30 73L43 87V52Q43 41 51 45V78 35Q56 23 64 35V77 45Q71 32 79 44V79 57Q89 44 96 59V91L82 113H42Z M64 9V19 M40 15L46 24 M88 15L82 24',gold,3.5),
  special:line('M64 9L112 37V91L64 119 16 91V37Z M64 9L38 48 90 48Z M38 48L16 91 64 86 112 91 90 48 M38 48L64 86 90 48 M64 86V119',gold,3),
  health:line('M64 110C37 86 11 63 19 38Q30 10 64 34 98 10 109 38 117 63 91 86 64 110Z',gold,4),
  skill:star+circle(64,64,14),
  detail:book+circle(90,90,19,'#E3CD9B',4)+line('M104 104L117 117','#E3CD9B',5),
  guide:line('M20 111L26 78 96 8 119 31 49 101Z M26 78L49 101 M38 89L105 20',gold,3.5),
  counter:shield+line('M42 47L86 91 M86 47L42 91','#D6B47C',5),
  synergy:line('M53 34L69 18Q91 3 110 22 127 41 109 60L87 81Q71 95 53 79 M75 94L59 110Q37 125 18 106 1 87 19 68L41 47Q57 33 75 49 M42 86L86 42',gold,4),
  lawful:line('M64 12V108 M32 111H96 M23 36H105 M36 35L16 76H56L36 35Z M92 35L72 76H112L92 35Z',gold,3.5),
  neutral:circle(64,64,45)+line('M64 19V109 M19 64H109',gold,2),
  chaotic:line('M17 108L48 77 31 61 80 12 M17 108L16 87 M17 108L38 108 M111 18L80 49 97 65 48 114 M111 18V39 M111 18H90',gold,4),
  good:circle(64,64,22)+Array.from({length:8},(_,i)=>`<path transform="rotate(${i*45} 64 64)" d="M64 9V30" stroke="${gold}" stroke-width="4"/>`).join(''),
  evil:line('M40 35L21 12 24 60 M88 35L107 12 104 60 M35 88Q9 51 39 31 64 15 89 31 119 51 93 88L88 111H40Z M41 57L55 63 49 77 36 72 M87 57L73 63 79 77 92 72 M64 77L57 90H71 M54 104V113 M74 104V113',gold,3.5),
  arcane_orb:circle(64,56,37)+circle(64,56,24)+line('M64 20V92 M28 56H100 M43 30L85 82 M85 30L43 82 M46 97L36 113H92L82 97',gold,3)
 };
 for(const [key,body]of Object.entries(paths))await emit('Icons',key,128,128,body,{usage:'Simple glyph, color white to retain aged gold; recommended 24–40 UI pixels'});
}
async function main() {
 await painted();
 const dieSource='Art/Production/initiative-v1/d20_obsidian-gold.source.png';
 const dieMeta=await sharp(path.join(root,dieSource)).metadata();
 await sharp(path.join(root,dieSource)).resize(464,464,{fit:'inside'}).extend({top:24,bottom:24,left:24,right:24,background:'#00000000'}).png().toFile(path.join(dst,'Illustrations/d20_initiative.png'));
 await record('Illustrations','d20_initiative',512,512,{source:dieSource,kind:'imagegen',nativeWidth:dieMeta.width,nativeHeight:dieMeta.height,resampling:'uniform contain; 24px transparent padding; preserved alpha',upscaled:false,usage:'Simple Image, white tint; live numeral overlay, never a baked dice result'});
 await ui(); await icons();
 const battle=JSON.parse(await fs.readFile(path.join(root,'Art/Production/battle-v1/asset-manifest.json'),'utf8'));
 const reused=battle.assets.filter(a=>['Subclasses','Damage','Status'].includes(a.group)).map(a=>({key:a.key,group:a.group,resourcesKey:a.resourcesKey,path:'Assets/Resources/Art/Battle/v1/'+a.path}));
 for(const key of ['attack','defense','range','heal','d20_sigil']) {
  const a=battle.assets.find(a=>a.key===key);if(!a)throw Error('Missing reusable '+key);
  reused.push({key:a.key,group:a.group,resourcesKey:a.resourcesKey,path:'Assets/Resources/Art/Battle/v1/'+a.path});
 }
 const counts={};for(const a of assets)counts[a.group]=(counts[a.group]||0)+1;
 await fs.writeFile(path.join(art,'asset-manifest.json'),JSON.stringify({version:1,date:'2026-10-21',status:'Runtime front-end and D20 wiring implemented; verification in Art/Integration/front-end/validation.txt',resourceRoot:'Assets/Resources/Art/FrontEnd/v1',spriteBorderOrder:'left,bottom,right,top',counts,assets,reused},null,2));
 console.log(JSON.stringify({exported:assets.length,counts,reused:reused.length}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
