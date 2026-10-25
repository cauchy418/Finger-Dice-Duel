from pathlib import Path
import heapq,sys
folder=Path(sys.argv[1]);count=int(sys.argv[2])
files=[(folder/f'shard-{i}.tsv').open(encoding='utf-8-sig') for i in range(count)]
headers=[f.readline() for f in files]
assert len(set(headers))==1 and headers[0].startswith('FDD_DUAL_V1\t')
key=lambda line:tuple(map(int,line.split('\t',4)[:4]))
rows=0
with (folder/'outcomes.tsv').open('w',encoding='utf-8',newline='') as output:
    output.write(headers[0]);previous=None
    for line in heapq.merge(*files,key=key):
        current=key(line)
        if previous is not None:assert current>previous,'duplicate or misordered match'
        output.write(line);previous=current;rows+=1
for f in files:f.close()
assert rows==1839264,rows
print(f'PASS {rows:,} unique matches from {count} isolated processes; build/roster headers match')
