const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { sharp, root } = require('./deps.cjs');
async function importMovementStatusIcons() {
  const assets = JSON.parse(await fs.readFile(path.join(root, 'Art/Production/status-icons-v4/manifest.json'), 'utf8'));
  for (const item of assets) {
    const bytes = await fs.readFile(path.join(root, item.selected));
    const info = await sharp(bytes).metadata();
    if (info.width !== 512 || info.height !== 512 || !info.hasAlpha || crypto.createHash('sha256').update(bytes).digest('hex') !== item.sha256)
      throw Error(`Invalid movement status icon: ${item.id}`);
    await fs.writeFile(path.join(root, item.runtime), bytes);
  }
  console.log('Imported vine restraint and grease; engine metadata untouched.');
}
module.exports = { importMovementStatusIcons };
if (require.main === module) importMovementStatusIcons().catch(error => { console.error(error); process.exitCode = 1; });
