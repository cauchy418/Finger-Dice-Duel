const fs=require('node:fs/promises'),path=require('node:path'),crypto=require('node:crypto');
const {sharp,root}=require('./deps.cjs');
async function importLoreStormIllusion(){
  const {assets}=JSON.parse(await fs.readFile(path.join(root,'Art/Production/lore-storm-illusion-v1/selected-manifest.json'),'utf8'));
  for(const item of assets){const bytes=await fs.readFile(path.join(root,item.source));const m=await sharp(bytes).metadata();
    if(m.width!==512||m.height!==512||!m.hasAlpha||crypto.createHash('sha256').update(bytes).digest('hex')!==item.sha256)throw Error(item.id);
    await fs.writeFile(path.join(root,item.runtime),bytes);}
  console.log('Imported Lore, Storm Sorcery and Illusion emblems; metadata untouched.');
}
module.exports={importLoreStormIllusion};if(require.main===module)importLoreStormIllusion().catch(e=>{console.error(e);process.exitCode=1;});
