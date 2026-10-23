const fs=require('node:fs/promises');
const path=require('node:path');const crypto=require('node:crypto');
const {sharp,root}=require('./deps.cjs');
async function importFourRoleEmblems(){
 const {assets}=JSON.parse(await fs.readFile(path.join(root,'Art/Production/four-role-emblems-v1/selected-manifest.json'),'utf8'));
 const prepared=[];
 for(const item of assets){const bytes=await fs.readFile(path.join(root,item.source));const m=await sharp(bytes).metadata();const stats=await sharp(bytes).stats();
 if(m.width!==512||m.height!==512||!m.hasAlpha||stats.channels[3].min!==0||stats.channels[3].max!==255||crypto.createHash('sha256').update(bytes).digest('hex')!==item.sha256)throw Error(item.id);
 prepared.push({item,bytes});}
 for(const {item,bytes} of prepared)await fs.writeFile(path.join(root,item.runtime),bytes);
 console.log('Imported four subclass emblems; engine metadata untouched.');
}
module.exports={importFourRoleEmblems};if(require.main===module)importFourRoleEmblems().catch(e=>{console.error(e);process.exitCode=1;});
