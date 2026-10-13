"""Read QCFG v2 using the recovered reader layout; keep typed maps losslessly.

This never instantiates game assemblies. Byte coverage, object boundaries,
key indexes and column extents are checked independently of the game reader.
"""
import hashlib, json, math, struct, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

class Reader:
    def __init__(self, data, strings=None, start=0, end=None):
        self.data, self.strings, self.p, self.end = data, strings, start, len(data) if end is None else end
    def take(self, n):
        assert n >= 0 and self.p+n <= self.end, (self.p, n, self.end)
        r=self.data[self.p:self.p+n]; self.p+=n; return r
    def number(self, fmt): return struct.unpack('<'+fmt, self.take(struct.calcsize('<'+fmt)))[0]
    def i(self): return self.number('i')
    def string(self):
        ix=self.i()
        assert -1 <= ix < len(self.strings), ix
        return None if ix < 0 else self.strings[ix]
    def value(self, depth=0):
        assert depth < 64
        tag=self.number('B')
        if tag in (0,1,2): return (None,False,True)[tag]
        if tag in (16,17,18,19):
            n=self.number({16:'i',17:'q',18:'f',19:'d'}[tag])
            if isinstance(n,float) and not math.isfinite(n): return {'$float':repr(n)}
            return n
        if tag==32: return self.string()
        assert tag in (48,49,50,51), (tag, self.p)
        count=self.i(); assert 0 <= count <= self.end-self.p
        if tag==48: return [self.value(depth+1) for _ in range(count)]
        entries=[]
        for _ in range(count):
            key=self.string() if tag==49 else self.number('i' if tag==50 else 'q')
            entries.append([key,self.value(depth+1)])
        return {'$map':{49:'string',50:'int32',51:'int64'}[tag], 'entries':entries}

def decode(path, output):
    d=path.read_bytes(); magic,version,ss,os,ts,header_count=struct.unpack_from('<6i',d)
    assert (magic,version,ss)==(1195787089,2,24)
    def pool(p, end, decode_string=False):
        r=Reader(d,start=p,end=end); count=r.i(); assert 0 <= count < len(d)//4
        offsets=[r.i() for _ in range(count+1)]
        assert offsets[0]==0 and offsets==sorted(offsets) and r.p+offsets[-1]==end
        parts=[d[r.p+a:r.p+b] for a,b in zip(offsets,offsets[1:])]
        return [x.decode('utf-8') for x in parts] if decode_string else parts
    strings=pool(ss,os,True); object_bytes=pool(os,ts)
    objects=[]
    for b in object_bytes:
        r=Reader(b,strings); objects.append(r.value()); assert r.p==r.end
    directory=Reader(d,strings,ts); count=directory.i(); assert count==header_count
    entries=[(directory.string(),directory.i(),directory.i(),directory.i()) for _ in range(count)]
    boundaries=[(directory.p, directory.p)]+[(o,o+n) for _,_,o,n in entries]
    assert all(a[1]==b[0] for a,b in zip(boundaries,boundaries[1:])) and boundaries[-1][1]==len(d)
    results=[]
    def object_at(ix):
        assert -1 <= ix < len(objects),ix
        return None if ix<0 else objects[ix]
    for name,kind,offset,length in entries:
        r=Reader(d,strings,offset,offset+length)
        assert name and '/' not in name and '\\' not in name
        if kind==2:
            n=r.i(); values=[[r.string(),object_at(r.i())] for _ in range(n)]
            table={'name':name,'kind':'KV','rows':values,'rowCount':n,'columns':[]}
        else:
            assert kind==1
            cols,rows,stride,keycol,keykind=[r.i() for _ in range(5)]
            columns=[dict(zip(('name','type','kind','offset','width'),(r.string(),r.string(),r.i(),r.i(),r.i()))) for _ in range(cols)]
            assert len({c['name'] for c in columns})==cols
            spans=sorted((c['offset'],c['offset']+c['width']) for c in columns)
            assert spans[0][0]==0 and spans[-1][1]==stride and all(a[1]==b[0] for a,b in zip(spans,spans[1:]))
            for c in columns: assert c['width']=={1:4,2:8,3:4,4:8,5:1,6:4,7:4}[c['kind']]
            key_count=r.i(); assert keykind in (0,1,2,3)
            keys=[(r.i() if keykind==1 else r.string(),r.i()) for _ in range(key_count)] if keykind else []
            assert keykind or key_count==0
            block_length=r.i(); assert block_length==rows*stride and r.p+block_length==r.end
            block_start=r.p; values=[]
            for ri in range(rows):
                row={}
                for c in columns:
                    vr=Reader(d,strings,block_start+ri*stride+c['offset'])
                    k=c['kind']
                    v=vr.string() if k==6 else object_at(vr.i()) if k==7 else vr.number({1:'i',2:'q',3:'f',4:'d',5:'B'}[k])
                    if k==5: assert v in (0,1); v=bool(v)
                    if isinstance(v,float) and not math.isfinite(v): v={'$float':repr(v)}
                    row[c['name']]=v
                values.append(row)
            if keykind:
                assert key_count==rows and len({k for k,_ in keys})==rows and len({ri for _,ri in keys})==rows
                assert [k for k,_ in keys]==sorted(k for k,_ in keys)
                for k,ri in keys:
                    assert 0 <= ri < rows
                    v=values[ri][columns[keycol]['name']]
                    assert k==(str(v) if keykind==2 else v),(name,k,v)
            r.p+=block_length
            table={'name':name,'kind':'Row','rowCount':rows,'stride':stride,'keyColumn':keycol,'keyKind':keykind,'columns':columns,'keys':keys,'rows':values}
        assert r.p==r.end
        output.mkdir(parents=True,exist_ok=True)
        target=output/(name+'.json'); target.write_text(json.dumps(table,ensure_ascii=False,separators=(',',':'),allow_nan=False),encoding='utf-8')
        results.append({'name':name,'kind':table['kind'],'rows':table['rowCount'],'columns':len(table['columns']),'path':str(target.relative_to(ROOT/'local')).replace('\\','/'),'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
    return {'input':str(path.relative_to(ROOT/'local')).replace('\\','/'),'sha256':hashlib.sha256(d).hexdigest(),'bytes':len(d),'stringCount':len(strings),'objectCount':len(objects),'objectValuesConsumedExactly':True,'sectionCoverageExact':True,'tables':results}

if __name__=='__main__':
    t=time.monotonic()
    parts=[decode(p,ROOT/'local/config/tables') for p in sorted((ROOT/'local/assets/text/assets/res/address1').glob('config_byte_*.bytes'))]
    tables=[x for p in parts for x in p['tables']]
    assert len({x['name'] for x in tables})==len(tables)
    (ROOT/'local/config/index.json').write_text(json.dumps(parts,ensure_ascii=False,indent=2),encoding='utf-8')
    report={'containers':len(parts),'tableCount':len(tables),'rowCount':sum(x['rows'] for x in tables),'columnCount':sum(x['columns'] for x in tables),'stringCount':sum(x['stringCount'] for x in parts),'objectCount':sum(x['objectCount'] for x in parts),'allPoolsSectionsKeysValidated':True,'seconds':round(time.monotonic()-t,2),'tables':[{'name':x['name'],'kind':x['kind'],'rows':x['rows'],'columns':x['columns']} for x in tables]}
    (ROOT/'evidence/config-decoding.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='tables'},ensure_ascii=False))
