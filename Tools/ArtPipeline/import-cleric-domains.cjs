const fs=require('node:fs/promises');
const path=require('node:path');const crypto=require('node:crypto');
const {sharp,root}=require('./deps.cjs');
async function importClericDomains(){
 const {assets}=JSON.parse(await fs.readFile(path.join(root,'Art/Production/cleric-domains-v1/selected-manifest.json'),'utf8'));
 for(const asset of assets){
  const bytes=await fs.readFile(path.join(root,asset.source));const meta=await sharp(bytes).metadata();
  if(meta.width!==512||meta.height!==512||!meta.hasAlpha||crypto.createHash('sha256').update(bytes).digest('hex')!==asset.sha256)throw Error(asset.id);
  await fs.writeFile(path.join(root,asset.runtime),bytes);
 }
 console.log('Imported three cleric-domain assets; metadata untouched.');
}
module.exports={importClericDomains};if(require.main===module)importClericDomains().catch(error=>{console.error(error);process.exitCode=1;});
