from pathlib import Path
import heapq,sys
folder=Path(sys.argv[1]);count=int(sys.argv[2])
files=[(folder/f'shard-{i}.tsv').open(encoding='utf-8-sig') for i in range(count)]
headers=[f.readline() for f in files]
assert len(set(headers))==1 and headers[0].startswith('FDD_DUAL_V1\t')
version,build,games,roster=headers[0].rstrip('\r\n').split('\t')
games=int(games);duals=roster.split('|')
assert games>0 and len(duals)==len(set(duals)) and len(duals)>1
expected=len(duals)*(len(duals)-1)//2*games*2
key=lambda line:tuple(map(int,line.split('\t',4)[:4]))
rows=0
with (folder/'outcomes.tsv').open('w',encoding='utf-8',newline='') as output:
    output.write(headers[0]);previous=None
    for line in heapq.merge(*files,key=key):
        current=key(line)
        i,j,g,swap=current
        assert 0<=i<j<len(duals) and 0<=g<games and swap in (0,1),'out-of-range match'
        if previous is not None:assert current>previous,'duplicate or misordered match'
        output.write(line);previous=current;rows+=1
for f in files:f.close()
assert rows==expected,(rows,expected)
print(f'PASS {rows:,} unique matches from {count} isolated processes; build/roster headers match')
