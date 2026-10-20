// Original editable vector masters; PNG export requires sharp (no network).
const fs = require('node:fs/promises');
const path = require('node:path');
const {sharp, root} = require('./deps.cjs');
const src = path.join(root,'Art/Production/battle-v1/svg');
const dst = path.join(root,'Assets/Resources/Art/Battle/v1');
const manifest=[];
const gold='#AD8B50', ivory='#E5D6B3';
const line=(d,s=gold,w=4)=>`<path d="${d}" fill="none" stroke="${s}" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round"/>`;
const poly=(d,c='url(#metal)')=>`<path d="${d}" fill="${c}" stroke="#D6BC83" stroke-width="1.6" stroke-linejoin="round"/>`;
const circ=(x,y,r,s=gold,w=3,fill='none')=>`<circle cx="${x}" cy="${y}" r="${r}" fill="${fill}" stroke="${s}" stroke-width="${w}"/>`;
const defs=`<defs><linearGradient id="plate" x2="0" y2="1"><stop stop-color="#23262B"/><stop offset=".55" stop-color="#11151A"/><stop offset="1" stop-color="#090C10"/></linearGradient><linearGradient id="metal" x1="0" y1="0" x2=".75" y2="1"><stop stop-color="#F0DBA4"/><stop offset=".28" stop-color="#C2A36B"/><stop offset=".55" stop-color="#8C6C39"/><stop offset=".72" stop-color="#D6B77B"/><stop offset="1" stop-color="#72522C"/></linearGradient><linearGradient id="steel" x2="0" y2="1"><stop stop-color="#ECEAE1"/><stop offset=".5" stop-color="#96A0B0"/><stop offset="1" stop-color="#586272"/></linearGradient><linearGradient id="red" x2="0" y2="1"><stop stop-color="#D04442"/><stop offset=".4" stop-color="#A8232F"/><stop offset="1" stop-color="#571421"/></linearGradient><radialGradient id="button"><stop stop-color="#51402B"/><stop offset="1" stop-color="#18191C"/></radialGradient></defs>`;
function svg(w,h,body){return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${defs}${body}</svg>`;}
async function emit(group,key,w,h,body,extra={}){
  const rel=`${group}/${key}.png`;
  const text=svg(w,h,body);
  await fs.mkdir(path.join(src,group),{recursive:true});
  await fs.mkdir(path.join(dst,group),{recursive:true});
  await fs.writeFile(path.join(src,group,`${key}.svg`),text);
  await sharp(Buffer.from(text)).png().toFile(path.join(dst,rel));
  manifest.push({key,group,path:rel,width:w,height:h,alpha:true,source:`Art/Production/battle-v1/svg/${group}/${key}.svg`,...extra});
}
function corners(w,h,c=gold){return [[0,0,1,1],[w,0,-1,1],[0,h,1,-1],[w,h,-1,-1]].map(([x,y,sx,sy])=>`<g transform="translate(${x} ${y}) scale(${sx} ${sy})">${line('M10 42V10H42 M17 35V17H35 M17 28L28 17 M27 34L34 27',c,1.7)}<path d="M30 10L37 6 44 10 37 14Z" fill="${c}"/></g>`).join('');}
function frame(w,h,c=gold,fill='none'){return `<rect x="7" y="7" width="${w-14}" height="${h-14}" rx="5" fill="${fill}" stroke="#0A0C0F" stroke-width="9"/><rect x="8" y="8" width="${w-16}" height="${h-16}" rx="4" fill="none" stroke="${c}" stroke-width="2"/><rect x="14" y="14" width="${w-28}" height="${h-28}" rx="2" fill="none" stroke="${c}" stroke-opacity=".38" stroke-width="1"/>${corners(w,h,c)}`;}
const sword=poly('M61 17L68 9 75 17 72 74H64Z')+line('M55 78H81 M68 78V105 M60 106H76');
const shield=poly('M64 18L101 31 95 70Q88 95 64 110 40 95 33 70L27 31Z')+line('M64 29V95 M38 39L64 30 90 39', '#6C502C',3);
const skull=`<path d="M39 76C16 44 39 19 64 19S113 44 89 76L86 101H42Z" fill="currentColor"/><path d="M41 53L55 57 51 70 39 67Z M87 53L73 57 77 70 89 67Z M64 71L58 82H70Z" fill="#11151A"/>${line('M53 94V103 M64 94V106 M75 94V103','#11151A',4)}`;
const eye=line('M16 64Q64 19 112 64 64 109 16 64Z','currentColor',6)+circ(64,64,15,'currentColor',5);
const flame=poly('M65 12C74 36 97 38 99 65Q102 105 64 114 24 109 27 76 29 55 45 42 42 65 53 66 69 54 65 12Z','currentColor');
const bolt=poly('M67 11L35 70H60L51 117 96 52H70L82 11Z','currentColor');
const book=poly('M64 35Q42 21 19 30V99Q43 91 64 105 85 91 109 99V30Q86 21 64 35Z')+line('M64 39V99 M29 42L52 47 M29 56L52 61 M76 47L99 42 M76 61L99 56','#5E4727',3);
const bow=line('M37 17Q109 64 37 111 M37 17L59 64 37 111 M17 64H110 M97 51L111 64 97 77');
const palm=line('M39 104L29 72Q24 58 33 57L46 75V36Q46 24 54 28V60 19Q54 9 63 14V59 26Q65 15 73 23V62 35Q77 26 83 33V82Q82 104 69 113H45Z','currentColor',5);
async function ui(){
  await emit('UI','hand_plate',320,448,`<rect x="11" y="11" width="298" height="426" rx="5" fill="url(#plate)"/>${line('M32 370H288','#6B583B',1)}<path d="M160 36L244 191 160 331 76 191Z" fill="none" stroke="#A68A58" stroke-opacity=".075" stroke-width="2"/>`,{usage:'Simple Image; preserve aspect ratio.'});
  for(const [key,c] of [['normal',gold],['available','#E1E6EC'],['selected','#F4C766'],['disabled','#555761']]) await emit('UI',`hand_frame_${key}`,320,448,frame(320,448,c),{border:[48,48,48,48]});
  for(const [key,c,fill] of [['normal',gold,'url(#button)'],['hover','#E7CD92','#443B2B'],['pressed','#D2AA64','#211C16'],['disabled','#53545A','#15181D']]) await emit('UI',`button_${key}`,320,88,frame(320,88,c,fill),{border:[48,32,48,32]});
  await emit('UI','panel_log',512,640,frame(512,640,gold,'url(#plate)'),{border:[48,48,48,48]});
  await emit('UI','panel_tooltip',384,256,frame(384,256,'#827657','url(#plate)'),{border:[48,48,48,48]});
  await emit('UI','label_strip',320,64,`<path d="M15 10H305L296 54H24Z" fill="#0D1015" stroke="#77613E" stroke-width="1.4"/>${line('M38 15H282','#AD8B50',1)}`,{border:[40,12,40,12]});
  await emit('UI','health_track',512,48,`<path d="M16 5H497L507 24 497 43H16L5 24Z" fill="#131318" stroke="url(#metal)" stroke-width="3"/><path d="M22 11H491L498 24 491 37H22L13 24Z" fill="#2D1420"/>`,{border:[24,16,24,16]});
  await emit('UI','health_fill',256,32,`<rect width="256" height="32" fill="url(#red)"/>${line('M0 2H256','#E38270',1)}`,{border:[2,8,2,8],usage:'Horizontal filled image; render track above fill.'});
  await emit('UI','badge_ring',256,256,circ(128,128,117,'#392F22',13,'#11151A')+circ(128,128,117,gold,3)+circ(128,128,105,'#D7BA7D',1)+[0,90,180,270].map(a=>`<path transform="rotate(${a} 128 128)" d="M128 3L137 17 128 31 119 17Z" fill="url(#metal)"/>`).join(''));
  await emit('UI','divider',512,32,line('M10 16H241 M271 16H502',gold,1)+`<path d="M256 7L265 16 256 25 247 16Z" fill="none" stroke="${gold}" stroke-width="1.5"/>`,{border:[32,8,32,8]});
  await emit('UI','status_counter',64,64,`<path d="M12 4H52L60 12V52L52 60H12L4 52V12Z" fill="#10131A" stroke="${gold}" stroke-width="2"/>`);
  await emit('UI','turn_marker',128,128,poly('M64 8L105 49 64 112 23 49Z')+line('M64 22V80 M40 49H88','#513D22',3));
  await emit('UI','d20_sigil',512,512,`<g transform="translate(128 128) scale(2)" opacity=".5">${circ(64,64,57,gold,1)}${line('M64 8L116 38 116 90 64 120 12 90 12 38Z M64 8L35 45 93 45Z M35 45L12 90 64 83 116 90 93 45 M35 45L64 83 93 45 M64 83V120 M12 38L35 45 M116 38L93 45',gold,1)}</g>`);
}
const damage=[
 ['slashing','挥砍','#9AA0AC',`<g transform="rotate(35 64 64)">${sword}</g>`+line('M15 63Q23 96 58 112','currentColor',4)],
 ['bludgeoning','钝击','#9AA0AC',poly('M52 46L80 59 58 112 43 105Z','currentColor')+poly('M25 25L39 10 102 42 96 62 77 67Z','currentColor')],
 ['piercing','穿刺','#9AA0AC',line('M24 105L93 36','currentColor',7)+poly('M77 26L112 14 100 49Z','currentColor')+line('M24 82V105H47 M14 91V115H38','currentColor',4)],
 ['thunder','雷鸣','#9B4FD0',circ(64,64,12,'currentColor',7)+[27,43].map(r=>line(`M${64-r*.6} ${64-r*.8}A${r} ${r} 0 0 0 ${64-r*.6} ${64+r*.8} M${64+r*.6} ${64-r*.8}A${r} ${r} 0 0 1 ${64+r*.6} ${64+r*.8}`,'currentColor',6)).join('')],
 ['acid','强酸','#B6CC2E',poly('M65 12C65 12 32 51 32 72A32 32 0 0 0 96 72C96 51 65 12 65 12Z','currentColor')+line('M47 77Q46 91 61 93','#11151A',5)],
 ['poison','毒素','#3FA34D',poly('M50 13H78V38L98 91Q104 112 83 115H45Q23 112 30 91L50 38Z','currentColor')+line('M44 64H84 M58 27H71','#11151A',5)+circ(53,86,4,'#11151A',3,'#11151A')+circ(76,96,5,'#11151A',3,'#11151A')],
 ['radiant','光耀','#F2C72E',circ(64,64,23,'currentColor',6)+Array.from({length:8},(_,i)=>`<path transform="rotate(${i*45} 64 64)" d="M64 10V28" stroke="currentColor" stroke-width="6"/>`).join('')],
 ['necrotic','暗蚀','#1E5B2E',poly('M60 110C14 79 29 34 99 15 107 73 97 97 60 110Z','currentColor')+line('M49 91L87 37 M64 68L46 58 M64 68L84 71','#97AB86',4)],
 ['fire','火焰','#E8701A',flame],
 ['cold','寒冷','#4C9EE6',Array.from({length:6},(_,i)=>`<g transform="rotate(${i*60} 64 64)">${line('M64 64V12 M64 33L49 23 M64 33L79 23','currentColor',5)}</g>`).join('')],
 ['lightning','闪电','#1A4FA0',bolt],
 ['force','力场','#8B1A1A',line('M64 13L115 107H13Z','currentColor',7)+circ(64,71,17,'currentColor',5)+line('M64 50V35 M44 81L31 89 M84 81L97 89','currentColor',4)],
 ['psychic','心灵','#F08CBB',eye+line('M45 23L53 34 M64 15V29 M83 23L75 34 M45 105L53 94 M64 113V99 M83 105L75 94','currentColor',4)]
];
const statuses=[
 ['resonance','残响','#9B4FD0',line('M43 101Q28 98 35 82L40 71C23 48 38 19 63 20 86 20 91 43 80 57L64 74Q61 91 51 88 M53 53Q49 36 64 36 78 40 66 51','currentColor',6)+line('M98 36Q115 64 98 90','currentColor',5)],
 ['acid','强酸','#B6CC2E',`<g>${line('M64 18L103 30 96 77Q83 99 64 111 45 99 32 77L25 30Z','currentColor',6)}${line('M67 30L52 55 71 65 53 90','currentColor',5)}${circ(84,45,5,'currentColor',3)}${circ(41,72,5,'currentColor',3)}</g>`],
 ['poison','中毒','#3FA34D',skull],
 ['blind','目盲','#F2C72E',eye+line('M25 108L105 20','#11151A',14)+line('M25 108L105 20','currentColor',6)],
 ['decay','腐朽','#1E5B2E',line('M64 111V45Q46 20 31 49 52 63 64 48 M64 66Q83 38 102 61 84 81 64 66','currentColor',7)+line('M31 104L103 26','#B1BF9C',5)],
 ['burn','燃烧','#E8701A',`<g transform="translate(7 18) scale(.75)">${palm}</g>`+`<g transform="translate(62 0) scale(.46)">${flame}</g>`],
 ['freeze','冰冻','#4C9EE6',`<g transform="translate(12 16) scale(.73)">${palm}</g>`+line('M88 14L114 35 103 77 88 65 74 35Z M88 14V65 M74 35H114','currentColor',4)],
 ['shock','感电','#1A4FA0',circ(64,28,13,'currentColor',5)+line('M64 44V96 M38 114L64 84 90 114 M27 60L46 50H82L101 60 M15 38L25 48 15 58 28 67 M113 38L103 48 113 58 100 67','currentColor',6)],
 ['stun','震慑','#F08CBB',line('M43 114V95C18 78 23 41 42 32 65 18 94 31 96 58L108 76 92 79V99H74V114','currentColor',5)+line('M64 12L69 27 85 29 73 40 75 55 62 47 48 54 51 39 40 29 56 27Z','currentColor',4)]
];
function iconBody(body,c,kind){return `<g style="color:${c}">${kind==='class'?circ(64,64,59,'#6A5635',1,'#10141A'):''}${body}</g>`;}
async function icons(){
  for(const [key,name,c,body] of damage)await emit('Damage',key,256,256,`<g transform="scale(2)">${iconBody(body.replaceAll('url(#metal)','currentColor').replaceAll('#D6BC83','currentColor').replaceAll(gold,'currentColor'),c)}</g>`,{label:name,color:c});
  for(const [key,name,c,body] of statuses)await emit('Status',key,256,256,`<g transform="scale(2)">${iconBody(body,c)}</g>`,{label:name,color:c});
  const helmet=poly('M38 91L33 54Q32 20 64 17 97 20 96 54L91 91 74 108V70H54V108Z')+line('M41 53L56 58 M87 53L72 58 M64 20V51','#3A2A18',5);
  const cls=[
   ['soldier','士兵',helmet],
   ['fighter','战士',`<g transform="translate(17 20) scale(.7)">${shield}</g>`+sword],
   ['curseweaver','咒术师',line('M64 13L113 106H15Z',gold,4)+line('M38 75Q37 37 68 47 101 61 75 84 51 106 42 77 33 52 61 49 86 44 88 71',ivory,5)],
   ['barbarian','野蛮人',poly('M57 13H71V114H57Z')+poly('M56 27Q32 30 20 15L15 66Q39 55 56 57Z')+poly('M72 27Q96 30 108 15L113 66Q89 55 72 57Z')],
   ['priest','牧师',line('M40 20L30 67H98L88 20 M64 10V37 M50 23H78 M35 67Q39 106 64 112 89 106 93 67 M21 78L13 85 M107 78L115 85',gold,5)+circ(64,80,10,gold,3)],
   ['ranger','游侠',bow+poly('M15 92Q12 71 35 66 37 87 15 92Z')],
   ['werewolf','狼人',poly('M30 16L49 31 80 30 99 14 95 58 103 79 76 110H52L25 79 33 58Z')+line('M40 54L54 61 M88 54L74 61 M54 79L64 88 74 79 M64 88V98','#3E2C1B',5)],
   ['swordsman','剑客',`<g transform="translate(-10 3) rotate(-15 64 64)">${sword}</g><g transform="translate(24 -4) rotate(15 64 64) scale(.85)">${sword}</g>`],
   ['ninja','忍者',poly('M64 10L76 47 113 64 76 77 64 115 51 77 14 64 51 47Z')+circ(64,64,11,'#241D14',5)],
   ['assassin','刺客',line('M21 88Q16 40 64 14 112 40 107 88 M36 76L50 63 M92 76L78 63',gold,5)+poly('M64 45L75 60 68 97 64 115 60 97 53 60Z')],
   ['paladin','圣武士',`<g transform="translate(13 21) scale(.8)">${shield}</g>`+circ(64,27,13,gold,4)+line('M64 3V8 M40 10L46 16 M88 10L82 16',gold,4)],
   ['hexer','妖术师',line('M29 95Q7 67 31 45 M99 95Q121 67 97 45 M31 45L45 54 43 35 M97 45L83 54 85 35 M50 25Q67 15 83 29 M54 106Q70 113 81 103',gold,5)+line('M33 95L96 23 M59 49L78 64',ivory,5)],
   ['arcane_archer','魔射手',circ(64,64,39,gold,2)+circ(64,64,25,gold,2)+line('M25 106L99 22 M82 25L105 16 102 40 M25 84V106H47',ivory,5)],
   ['gambler','赌徒',poly('M64 13L110 41 98 96 44 113 15 64Z')+line('M64 13L44 113 M64 13L15 64 98 96 64 13 M15 64L110 41 44 113','#50391E',3)+circ(70,62,7,'#4E361E',3)],
   ['knight','骑士',poly('M31 110L35 80 57 69 46 57 24 57 30 40 51 26 61 11 76 27Q111 43 95 81L105 110Z')+line('M41 45H49 M62 35Q91 42 78 78L58 90','#3E2C1B',5)],
   ['monk','武僧',circ(64,64,46,gold,4)+line('M64 18C105 35 24 88 64 110',gold,6)+circ(64,42,7,gold,3,gold)+circ(64,88,7,gold,3)],
   ['giant','巨人',poly('M34 83L23 59 24 31 36 27 44 39V20H57V37 15H72V39 22H85V44 32H99V71L87 95H41Z')+line('M15 114L39 90 57 108 76 92 113 114',gold,5)],
   ['thief','盗贼',line('M46 55V35Q46 14 67 14 89 14 89 37',gold,7)+poly('M30 55H98V111H30Z')+line('M65 72V91 M17 100L99 41 114 47 103 59',ivory,5)],
   ['hunter','猎人',line('M16 52Q64 15 112 52 64 92 16 52Z',gold,5)+circ(64,52,12,gold,4)+line('M29 102Q64 76 99 102 M40 114Q64 95 88 114',gold,4)],
   ['magician','魔术师',poly('M64 17L88 54 64 91 40 54Z')+line('M33 110L18 75Q12 49 24 51L33 68 30 37Q34 24 41 39L45 69 M95 110L110 75Q116 49 104 51L95 68 98 37Q94 24 87 39L83 69',gold,5)],
   ['wizard','法师',`<g transform="translate(0 16) scale(1 .83)">${book}</g>`+line('M64 8L68 23 84 28 68 33 64 48 60 33 44 28 60 23Z',ivory,3)],
   ['sorcerer','术士',`<g style="color:${gold}" transform="translate(12 -2) scale(.8)">${flame}</g>`+poly('M64 57L92 86 64 116 36 86Z')+line('M64 57V116 M36 86H92','#4C351C',3)],
   ['warlock','邪术师',eye+line('M33 23L20 10 M95 23L108 10 M22 92L15 109 32 117 42 101 M86 101L96 117 113 109 106 92',gold,6)],
   ['bard','吟游诗人',line('M25 24Q20 75 46 85V111H82V85Q108 75 103 24 M25 24L44 36H84L103 24 M45 37V76 M58 37V82 M71 37V82 M84 37V76 M44 93H84',gold,5)]
  ];
  for(const [key,name,body] of cls)await emit('Classes',key,256,256,`<g transform="scale(2)" style="color:${gold}">${iconBody(body,gold,'class')}</g>`,{label:name,color:gold});
  const overlayIcons=[['shield',shield,'#6FADDD'],['shield_broken',line('M64 18L101 31 95 70Q88 95 64 110 40 95 33 70L27 31Z','currentColor',5)+line('M70 23L54 49 73 61 52 87 66 108','currentColor',6),'#8595AA'],['locked',line('M42 55V35Q42 14 64 14 86 14 86 35V55','currentColor',7)+poly('M29 55H99V112H29Z','currentColor')+circ(64,78,7,'#10141A',4,'#10141A'),'#A6A0BA'],['taichi',circ(64,64,46,'currentColor',5)+line('M64 18C106 37 22 91 64 110','currentColor',6)+circ(64,42,7,'currentColor',3,'currentColor')+circ(64,88,7,'currentColor',3),'#6AC2A4'],['concealed',eye+line('M20 106L108 18','currentColor',6),'#5B9F9B'],['stealth',line('M23 96Q17 41 64 15 111 41 105 96 M40 68L53 73 M88 68L75 73','currentColor',6),'#9383AE'],['attack',sword,'#C88570'],['defense',shield,'#9AABC2'],['range',bow,'#C3AE80'],['heal',line('M51 21H77V51H107V77H77V107H51V77H21V51H51Z','currentColor',6),'#68B485'],['dead',skull,'#AAA5A1']];
  for(const [key,body,c] of overlayIcons)await emit('Overlay',key,256,256,`<g transform="scale(2)" style="color:${c}">${body.replaceAll('url(#metal)','currentColor').replaceAll(gold,'currentColor').replaceAll('#D6BC83','currentColor')}</g>`);
}
async function main(){
  await ui(); await icons();
  await fs.mkdir(path.join(root,'Art/Production/battle-v1'),{recursive:true});
  await fs.writeFile(path.join(root,'Art/Production/battle-v1/vector-manifest.json'),JSON.stringify({version:1,assets:manifest},null,2));
  console.log(`Exported ${manifest.length} original SVG masters and transparent PNG assets.`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
