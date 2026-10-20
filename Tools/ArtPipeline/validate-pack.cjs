const fs=require('node:fs/promises');
const path=require('node:path');
const crypto=require('node:crypto');
const {sharp,root}=require('./deps.cjs');
async function main(){
 const art=path.join(root,'Art/Production/battle-v1');
 const dst=path.join(root,'Assets/Resources/Art/Battle/v1');
 const vector=JSON.parse(await fs.readFile(path.join(art,'vector-manifest.json'),'utf8'));
 const hands=JSON.parse(await fs.readFile(path.join(art,'hand-manifest.json'),'utf8'));
 const assets=[...vector.assets];
 for(const hand of hands.hands)for(const side of ['left','right'])assets.push({key:`hand_${side}_${hand.value}`,group:'Hands',path:hand[side],width:512,height:640,alpha:true,value:hand.value,side,pivot:[.5,30/640],source:hand.source});
 const bg=await sharp(path.join(dst,'Backgrounds/battle_table.png')).metadata();
 assets.push({key:'battle_table',group:'Backgrounds',path:'Backgrounds/battle_table.png',width:bg.width,height:bg.height,source:'Art/Production/battle-v1/prompts/background.txt'});
 const counts={},hashes=new Set();
 for(const a of assets){
   const filename=path.join(dst,a.path),bytes=await fs.readFile(filename),meta=await sharp(bytes).metadata();
   if(meta.format!=='png'||meta.width!==a.width||meta.height!==a.height)throw new Error(`Invalid PNG dimensions: ${a.path}`);
   const stats=await sharp(bytes).stats();
   if(stats.isOpaque===false&&stats.channels[3]?.max===0)throw new Error(`Empty alpha: ${a.path}`);
   a.alpha=!!meta.hasAlpha;a.bytes=bytes.length;a.sha256=crypto.createHash('sha256').update(bytes).digest('hex');
   a.resourcesKey=`Art/Battle/v1/${a.path.slice(0,-4)}`;
   if(hashes.has(a.sha256))throw new Error(`Duplicate asset: ${a.path}`);
   hashes.add(a.sha256);counts[a.group]=(counts[a.group]||0)+1;
 }
 const expected={UI:19,Damage:13,Status:9,Classes:24,Overlay:11,Hands:20,Backgrounds:1};
 for(const [group,n]of Object.entries(expected))if(counts[group]!==n)throw new Error(`Missing ${group} assets`);
 for(let i=0;i<10;i++){
   const left=await sharp(path.join(dst,`Hands/hand_left_${i}.png`)).flop().ensureAlpha().raw().toBuffer();
   const right=await sharp(path.join(dst,`Hands/hand_right_${i}.png`)).ensureAlpha().raw().toBuffer();
   if(!left.equals(right))throw new Error(`Mirror mismatch: ${i}`);
   const raw=await sharp(path.join(dst,`Hands/hand_left_${i}.png`)).ensureAlpha().raw().toBuffer();
   if(raw[3]!==0||raw[raw.length-1]!==0)throw new Error(`Opaque hand corners: ${i}`);
 }
 const manifest={version:1,date:'2026-10-20',status:'Exported first production asset pack; runtime not wired; visual review and engine import still required.',resourceRoot:'Assets/Resources/Art/Battle/v1',spriteBorderOrder:'left,bottom,right,top',counts,assets};
 await fs.writeFile(path.join(art,'asset-manifest.json'),JSON.stringify(manifest,null,2));
 const report={pngFiles:assets.length,counts,checks:['PNG decoding and dimensions','No empty alpha masks','All asset content hashes distinct','10 left/right pixel-perfect mirror pairs','Transparent hand corners'],manual:['Corrected 4 has four extended long fingers and one folded thumb','Corrected 6 has extended pinky/thumb and three folded fingers','0 and 9 canonicalized to anatomical left before mirroring','Contact sheets and assembly preview visually inspected'],runtimeVerified:false};
 await fs.writeFile(path.join(art,'validation-report.json'),JSON.stringify(report,null,2));
 console.log(JSON.stringify(report));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
