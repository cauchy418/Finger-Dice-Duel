const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { sharp, root } = require('./deps.cjs');
const base = 'Art/Production/wizard-schools-v1';
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
async function importWizardSchoolIcons() {
  const { assets } = JSON.parse(await fs.readFile(path.join(root,base,'selected-manifest.json'),'utf8'));
  for(const item of assets) {
    const bytes=await fs.readFile(path.join(root,item.source));
    const meta=await sharp(bytes).metadata();
    const stats=await sharp(bytes).stats();
    if(meta.width!==512 || meta.height!==512 || !meta.hasAlpha || stats.channels[3].min!==0 || hash(bytes)!==item.sha256)
      throw new Error(`Invalid wizard icon ${item.id}`);
    const target=path.join(root,item.runtime);await fs.mkdir(path.dirname(target),{recursive:true});await fs.writeFile(target,bytes);
  }
  console.log(`Imported ${assets.length} wizard/resistance/ward icons; metadata untouched.`);
}
module.exports={importWizardSchoolIcons};
if(require.main===module) importWizardSchoolIcons().catch(e=>{console.error(e);process.exitCode=1;});
