"""Export verified current sampling reports; never simulate or estimate wins."""
from pathlib import Path
import argparse, csv, hashlib, json, re, subprocess

ROOT=Path(__file__).resolve().parents[2]
FILES=['1v1-report.txt','2v2-report.txt','dual-report.txt','8p-report.txt','identity-report-random.txt','identity-report.txt','focus-report.txt']
FIXTURES=['CharacterBalanceTests','TwoVersusTwoBalanceTests','EightPlayerFactionTests','IdentityBalanceTests','FocusPolicyExperimentTests']

def read(path):
    b=Path(path).read_bytes()
    return b.decode('utf-16' if b.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def role_rows(text,names):
    rows={}
    for line in text.splitlines():
        for name in sorted(names,key=len,reverse=True):
            if line.startswith(name) and name not in rows:
                values=re.findall(r'(\d+\.\d+)%',line[len(name):])
                if values:rows[name]={'rate_percent':float(values[0]),'report_row':line}
                break
        if len(rows)==len(names):break
    assert set(rows)==set(names),set(names)-set(rows)
    return rows

def group_rows(text,names):
    rows={}
    for name in names:
        line=next(line for line in text.splitlines() if line.startswith(name+' '))
        m=re.search(r'(\d+)\s*/\s*(\d+)',line)
        rows[name]={'wins':int(m[1]),'appearances':int(m[2]),'rate_percent':float(re.search(r'(\d+\.\d+)%',line)[1])}
    return rows

def main():
    p=argparse.ArgumentParser(__doc__)
    p.add_argument('--date',required=True,help='Client date YYYY-MM-DD')
    p.add_argument('--log-prefix',required=True)
    p.add_argument('--dual-log',required=True)
    p.add_argument('--dual-cache',required=True)
    a=p.parse_args()
    for fixture in FIXTURES:
        text=read(ROOT/(a.log_prefix+fixture+'.txt'))
        assert re.search(r'通过 \d+ / 失败 0',text) and '   FAIL ' not in text,fixture
    text=read(ROOT/a.dual_log)
    assert '通过 4 / 失败 0' in text and 'unique matches' in text
    with (ROOT/a.dual_cache/'outcomes.tsv').open(encoding='utf-8-sig') as f:
        version,mvid,rounds,duals=f.readline().rstrip().split('\t')
    assert version=='FDD_DUAL_V1'
    dual_count=len(duals.split('|'))
    with (ROOT/'Tools/ai-strategy-profiles.tsv').open(encoding='utf-8-sig') as f:
        roster=list(csv.DictReader(f,delimiter='\t'))
    names=[r['角色'] for r in roster];n=len(names)
    assert len(set(names))==n
    reports={file:read(ROOT/'Tools'/file) for file in FILES}
    expected=[n*(n-1)//2*80,n*(n-1)//2*128,dual_count*(dual_count-1)//2*int(rounds)*2,2000,2000,2000,n*3600]
    for file,count in zip(FILES,expected):
        assert int(re.search(r'总对局\s*(\d+)',reports[file])[1])==count,file
        assert not re.search(r'异常\s*[1-9]',reports[file]),file
        assert not re.search(r'模拟器强停\s*[1-9]',reports[file]),file
    rates={mode:role_rows(reports[file],names) for mode,file in zip(['1v1','2v2','dual'],FILES)}
    for name in names:
        row=rates['1v1'][name]['report_row'][len(name):]
        assert sum(map(int,re.findall(r'\d+',row)[:3]))==(n-1)*80
        row=rates['2v2'][name]['report_row'][len(name):]
        assert int(re.findall(r'\d+',row)[1])==(n-1)*256
    sources=sorted((ROOT/'Assets/Scripts').rglob('*.cs'))+sorted((ROOT/'Assets/Tests').rglob('*.cs'))
    h=hashlib.sha256()
    for f in sources:
        h.update(f.relative_to(ROOT).as_posix().encode());h.update(f.read_bytes())
    data={'date':a.date,'source_base_commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
          'source_sha256':h.hexdigest(),'source_scope':'Assets/Scripts/**/*.cs + Assets/Tests/**/*.cs; sorted paths and raw bytes','dual_build_mvid':mvid,'characters':n,'duals':dual_count,
          'total_games':sum(expected),'reports':{f:{'games':c,'sha256':digest(ROOT/'Tools'/f)} for f,c in zip(FILES,expected)},
          'roles':{r['ID']:{'name':r['角色'],**{mode:rates[mode][r['角色']] for mode in rates}} for r in roster},
          'factions':group_rows(reports[FILES[3]],['守序','中立','混乱']),
          'identity':{mode:group_rows(reports[f],['主公','忠臣','反贼','内奸']) for mode,f in zip(['random','biased'],FILES[4:6])},
          'dual_axis':group_rows(reports[FILES[2]].split('--- 阵营胜率',1)[1],['守序','中立','混乱'])}
    (ROOT/'Tools/Statistics/results.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    table='| 角色 | 1v1胜率 | 2v2队伍胜率 | 双将决胜胜率 |\n|---|---:|---:|---:|\n'
    for row in roster:
        name=row['角色'];values=' | '.join(f"{rates[m][name]['rate_percent']:.1f}%" for m in rates)
        table+=f'| {name} | {values} |\n'
    (ROOT/'Tools/Statistics/role-rates.md').write_text(table,encoding='utf-8')
    print(f'PASS {n} roles, {dual_count} duals, {sum(expected):,} games; seven report hashes verified')

if __name__=='__main__':main()
