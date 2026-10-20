const fs=require('node:fs/promises');
const path=require('node:path');
const crypto=require('node:crypto');
const {sharp,root}=require('./deps.cjs');
const art=path.join(root,'Art/Production/front-end-v1');
const dst=path.join(root,'Assets/Resources/Art/FrontEnd/v1');
async function main(){
 const manifest=JSON.parse(await fs.readFile(path.join(art,'asset-manifest.json'),'utf8'));
 const seen=new Set(),counts={};
 for(const a of manifest.assets){
  const bytes=await fs.readFile(path.join(dst,a.path)),meta=await sharp(bytes).metadata(),stats=await sharp(bytes).stats();
  if(meta.format!=='png'||meta.width!==a.width||meta.height!==a.height)throw Error('Invalid image '+a.path);
  const hash=crypto.createHash('sha256').update(bytes).digest('hex');
  if(hash!==a.sha256)throw Error('Hash mismatch '+a.path);
  if(seen.has(hash))throw Error('Duplicate asset '+a.path);
  seen.add(hash);counts[a.group]=(counts[a.group]||0)+1;
  if(a.group==='Backgrounds'){
   if(meta.width!==1920||meta.height!==1080||!stats.isOpaque)throw Error('Invalid cover background '+a.path);
   if(!a.textSafe||a.textSafe.length!==4)throw Error('Missing text-safe bounds '+a.path);
  }else{
   if(!meta.hasAlpha||stats.channels[3].min!==0||stats.channels[3].max<1)throw Error('Invalid alpha '+a.path);
   if(a.group==='Illustrations'){
    const corner=await sharp(bytes).extract({left:0,top:0,width:8,height:8}).ensureAlpha().raw().toBuffer();
    for(let i=3;i<corner.length;i+=4)if(corner[i]>5)throw Error('Nontransparent illustration corner '+a.path);
   }
  }
  if(a.border){const [l,b,r,t]=a.border;if(l<0||r<0||b<0||t<0||l+r>=a.width||b+t>=a.height)throw Error('Invalid nine-slice '+a.path);}
  await fs.access(path.join(root,a.source));
 }
 const expected={Backgrounds:4,Illustrations:5,UI:46,Icons:24};
 for(const [k,n]of Object.entries(expected))if(counts[k]!==n)throw Error('Missing group '+k);
 for(const type of ['mode_button','tab','list_row','roster_tile','icon_button'])
  for(const state of ['normal','hover','pressed','selected','disabled'])
   if(!manifest.assets.some(a=>a.key===type+'_'+state))throw Error('Missing UI state '+type+'_'+state);
 for(const a of manifest.reused)await fs.access(path.join(root,a.path));
 if(manifest.reused.filter(a=>a.group==='Classes').length!==24)throw Error('Must preserve all 24 classes');
 if(manifest.reused.filter(a=>a.group==='Damage').length!==13)throw Error('Must preserve all 13 damage types');
 if(manifest.reused.filter(a=>a.group==='Status').length!==9)throw Error('Must preserve all 9 project statuses');
 for(const p of ['main-menu-assembly.png','main-menu-alternate-assembly.png','selection-assembly.png','codex-detail-assembly.png','codex-overview-assembly.png']){
  const m=await sharp(path.join(art,'previews',p)).metadata();if(m.width!==1920||m.height!==1080)throw Error('Invalid assembly '+p);
 }
 const result={date:'2026-10-21',assets:manifest.assets.length,counts,reused:manifest.reused.length,
  automatedChecks:['PNG dimensions and SHA-256','Unique asset content','Four opaque 16:9 backgrounds','Preserved transparent alpha and illustration corners','Nine-slice border bounds','Five interaction states for five widget families','All 24 class / 13 damage / 9 status reusable resources exist','Five full-size static screen assemblies'],
  visualChecks:['No text or controls baked into painted backgrounds','Peripheral background props leave central UI space','Mode illustrations readable at menu size','Parchment and vignette alpha checked on dark surface','All 24 class emblems present in selection and overview','Selected tile label moved clear of bottom frame','Long codex content fits above pager'],
  runtimeIntegrated:false,previewType:'Static compositions of exported assets, not Unity runtime screenshots',
  fontReuse:['Fonts/SourceHanSerifCN-SemiBold','Fonts/NotoSansCJKsc-Medium','Fonts/NotoSansCJKsc-Bold'],
  notes:['Background sources are 1672x941; 1920x1080 exports use uniform resampling, not native 4K.','The settings and role-category glyphs are reserved artwork; this kit does not add those features.']};
 await fs.writeFile(path.join(art,'validation-report.json'),JSON.stringify(result,null,2));
 console.log(JSON.stringify(result));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
