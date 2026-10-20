// User previously authorized sheet slicing and resizing. Keep engine .meta files intact.
const fs=require('node:fs/promises');
const path=require('node:path');
const {sharp,root}=require('./deps.cjs');
const {findBounds}=require('./painted-bounds.cjs');
const art=path.join(root,'Art/Production/painted-icons-v2');
const dst=path.join(root,'Assets/Resources/Art/Battle/v1');
const plans=[
 {group:'Damage',file:'damage-sheet.png',cols:5,rows:3,keys:['slashing','bludgeoning','piercing','thunder','acid','poison','radiant','necrotic','fire','cold','lightning','force','psychic']},
 {group:'Status',file:'status-sheet.png',cols:3,rows:3,keys:['resonance','acid','poison','blind','decay','burn','freeze','shock','stun']},
 {group:'Classes',file:'classes-a-sheet.png',cols:4,rows:2,keys:['soldier','fighter','curseweaver','barbarian','priest','ranger','werewolf','swordsman']},
 {group:'Classes',file:'classes-b-sheet.png',cols:4,rows:2,keys:['ninja','assassin','paladin','hexer','arcane_archer','gambler','knight','monk']},
 {group:'Classes',file:'classes-c-sheet.png',cols:4,rows:2,keys:['giant','thief','hunter','magician','wizard','sorcerer','warlock','bard']}
];
async function main(){
 const assets=[],prepared=[];
 for(const plan of plans){
   const source=path.join(art,'source',plan.file),meta=await sharp(source).metadata();
   if(!meta.hasAlpha)throw new Error(`Missing transparency: ${plan.file}`);
   const detected=await findBounds(source,plan.cols,plan.rows,plan.keys.length);
   prepared.push({plan,source,detected});
 }
 for(const {plan,source,detected}of prepared){
   const native=path.join(art,'native',plan.group);await fs.mkdir(native,{recursive:true});
   for(let i=0;i<plan.keys.length;i++){
     const key=plan.keys[i],{left,top,width,height}=detected.boxes[i];
     const crop=await sharp(source).extract({left,top,width,height}).png().toBuffer();
     await fs.writeFile(path.join(native,`${key}.png`),crop);
     let finalCrop=crop,specklesRemoved=0;
     if(plan.group==='Classes'){
       const {data,info}=await sharp(crop).ensureAlpha().raw().toBuffer({resolveWithObject:true});
       const rr=Math.pow(Math.min(width,height)*.47,2);
       for(let y=0;y<height;y++)for(let x=0;x<width;x++){
         const p=(y*width+x)*4;
         if(data[p]>240&&data[p+1]<35&&data[p+2]<20&&Math.pow(x-width/2,2)+Math.pow(y-height/2,2)>rr&&data[p+3]){data[p+3]=0;specklesRemoved++;}
       }
       finalCrop=await sharp(data,{raw:{width:info.width,height:info.height,channels:4}}).png().toBuffer();
     }
     const rel=`${plan.group}/${key}.png`;
     await sharp(finalCrop).resize(448,448,{fit:'contain',background:'#00000000'}).extend({top:32,bottom:32,left:32,right:32,background:'#00000000'}).png().toFile(path.join(dst,rel));
     assets.push({key,group:plan.group,path:rel,width:512,height:512,alpha:true,style:'painted-old-gold-v2',source:`Art/Production/painted-icons-v2/source/${plan.file}`,nativeCrop:`Art/Production/painted-icons-v2/native/${rel}`,nativeWidth:width,nativeHeight:height,crop:{left,top,width,height},exteriorMaskSpecklesRemoved:specklesRemoved,frameIncluded:true,usage:'Simple Image, white tint; do not add another badge ring or multiply by a semantic color.'});
   }
 }
 const manifest={version:2,tool:'Built-in image_gen; authorized grid slicing and sizing with sharp.',assets};
 await fs.writeFile(path.join(art,'painted-manifest.json'),JSON.stringify(manifest,null,2));
 console.log(`Imported ${assets.length} painted icons at stable resource paths; .meta untouched.`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
