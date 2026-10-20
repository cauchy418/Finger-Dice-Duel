// A review contact sheet made only from actual Unity Canvas captures.
const fs = require('node:fs/promises');
const path = require('node:path');
const {sharp, root} = require('./deps.cjs');
async function main() {
  const folder = path.join(root, 'Art/Integration/front-end');
  const files = ['MainMenu.png', 'CharacterSelect.png', 'CodexOverview.png', 'TwoOnTwoSplits.png'];
  const tiles = await Promise.all(files.map(async (file, index) => ({
    input: await sharp(path.join(folder, file)).resize(960, 540).png().toBuffer(),
    left: (index % 2) * 960, top: Math.floor(index / 2) * 540
  })));
  await sharp({create:{width:1920, height:1080, channels:3, background:'#080c10'}})
    .composite(tiles).png().toFile(path.join(folder, 'overview-contact.png'));
  const lines = (await fs.readFile(path.join(folder, 'validation.txt'), 'utf8')).split(/\r?\n/);
  if (lines.some(line => line.startsWith('FAILED'))) throw Error('Unity validation failed');
  console.log(JSON.stringify({contact:'Art/Integration/front-end/overview-contact.png',sources:files,
    runtimeChecks:lines.filter(line=>line.startsWith('PASS ')).length}));
}
main().catch(error => { console.error(error); process.exitCode=1; });
