const fs=require('node:fs/promises');
const path=require('node:path');const crypto=require('node:crypto');
const {sharp,root}=require('./deps.cjs');
async function importClericMartialIcons(){
 const {assets}=JSON.parse(await fs.readFile(path.join(root,'Art/Production/cleric-martial-v1/selected-manifest.json'),'utf8'));
 for(const item of assets){const bytes=await fs.readFile(path.join(root,item.source));const m=await sharp(bytes).metadata();
 if(m.width!==512||m.height!==512||!m.hasAlpha||crypto.createHash('sha256').update(bytes).digest('hex')!==item.sha256)throw Error(item.id);
 await fs.writeFile(path.join(root,item.runtime),bytes);}
 console.log('Imported four cleric/martial icons; metadata untouched.');
}
module.exports={importClericMartialIcons};if(require.main===module)importClericMartialIcons().catch(e=>{console.error(e);process.exitCode=1;});
