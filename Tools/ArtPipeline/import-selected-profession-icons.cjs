const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { sharp, root } = require('./deps.cjs');
async function importSelectedProfessionIcons() {
  const manifest = JSON.parse(await fs.readFile(path.join(root, 'Art/Production/profession-subclass-v1/selected-manifest.json'), 'utf8'));
  const prepared = [];
  for (const item of manifest.assets) {
    const bytes = await fs.readFile(path.join(root, item.source));
    const meta = await sharp(bytes).metadata();
    const hash = crypto.createHash('sha256').update(bytes).digest('hex');
    if (meta.width !== 512 || meta.height !== 512 || !meta.hasAlpha || hash !== item.sha256)
      throw new Error(`Invalid selected profession asset: ${item.id}`);
    prepared.push({ item, bytes });
  }
  for (const { item, bytes } of prepared) {
    const target = path.join(root, item.runtime);
    await fs.mkdir(path.dirname(target), { recursive: true });
    await fs.writeFile(target, bytes);
  }
  console.log(`Imported ${prepared.length} selected profession/subclass emblems; metadata untouched.`);
}
module.exports = { importSelectedProfessionIcons };
if (require.main === module) importSelectedProfessionIcons().catch(e => { console.error(e); process.exitCode = 1; });
