"""Full container/object/reference inventory in a compressed local SQLite index."""
from pathlib import Path
import argparse, collections, gc, hashlib, json, sqlite3, sys, zlib
import UnityPy
from UnityPy.helpers.TypeTreeNode import TypeTreeNode
from UnityPy.helpers.Tpk import get_typetree_node
from prepare import write

def compact(v):
    if isinstance(v,bytes):return {'binaryBytes':len(v),'sha256':hashlib.sha256(v).hexdigest()}
    if isinstance(v,dict):return {k:('[redacted]' if 'token' in k.lower() else compact(x)) for k,x in v.items()}
    if isinstance(v,(tuple,list)):
        if len(v)>2048 and isinstance(v[0],(int,float)):
            return {'numericElements':len(v),'sha256':hashlib.sha256(json.dumps(v,separators=(',',':')).encode()).hexdigest()}
        return [compact(x) for x in v]
    return v

def pointers(v,field=''):
    if isinstance(v,dict):
        if 'm_FileID' in v and 'm_PathID' in v:
            if v['m_PathID']:yield field,v['m_FileID'],v['m_PathID']
        else:
            for k,x in v.items():yield from pointers(x,field+'.'+k)
    elif isinstance(v,(tuple,list)):
        if v and isinstance(v[0],(str,int,float,bool)):return
        for i,x in enumerate(v):yield from pointers(x,f'{field}[{i}]')

def target(asset,fileid,pid):
    name=asset.name if fileid==0 else Path(asset.externals[fileid-1].path.replace('\\','/')).name if 0<fileid<=len(asset.externals) else f'unknown-file-{fileid}'
    return f'{name}:{pid}'

def corrected_tree(obj,cache):
    node=obj._get_typetree_node()
    if obj.type.name!='MonoBehaviour':return obj.read_typetree(nodes=node),None
    base=get_typetree_node(114,obj.version)
    key=(obj.assets_file.name,obj.type_id)
    if key not in cache:
        assert [c.m_Name for c in node.m_Children[:len(base.m_Children)]]==[c.m_Name for c in base.m_Children]
        def normalized(n):
            children=[normalized(c) for c in n.m_Children]
            kind=n.m_Type
            if len(children)==1 and children[0].m_Type=='Array':
                element=children[0].m_Children[1]
                # Generator encodes List<string> with an outer string type.
                # UnityPy must distinguish a string (char array) from a list.
                if not (kind=='string' and element.m_Type=='char'):kind='vector'
            return TypeTreeNode(m_Level=n.m_Level,m_Type=kind,m_Name=n.m_Name,
                m_ByteSize=n.m_ByteSize,m_Version=n.m_Version,m_MetaFlag=n.m_MetaFlag,m_Children=children)
        cache[key]=TypeTreeNode(m_Level=0,m_Type=node.m_Type,m_Name=node.m_Name,
            m_ByteSize=node.m_ByteSize,m_Version=node.m_Version,m_MetaFlag=node.m_MetaFlag,
            m_Children=base.m_Children+[normalized(c) for c in node.m_Children[len(base.m_Children):]])
    tree=obj.read_typetree(nodes=cache[key])
    original_base=obj.read_typetree(nodes=base,check_read=False)
    assert tree['m_Script']==original_base['m_Script'],'Independent script pointer mismatch'
    return tree,original_base

def candidates(data):
    return sorted((p for p in data.rglob('*') if p.is_file() and
       (p.suffix in ('.assets','.bundle','.unity3d') or p.name=='globalgamemanagers' or
        (p.name.startswith('level') and p.name[5:].isdigit()) or p.name in ('unity default resources','unity_builtin_extra'))),
        key=lambda p:(p.name!='globalgamemanagers',p.name))

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);ap.add_argument('--generator',required=True)
    args=ap.parse_args();project=Path(args.project).resolve();local=project/'local';data=local/'build-copy/Duckov_Data'
    sys.path.insert(0,str(Path(args.generator).resolve()))
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    gen=TypeTreeGenerator('2022.3.62f2');gen.load_local_game(str(local/'build-copy'))
    db=sqlite3.connect(local/'assets.sqlite');db.executescript('''
    PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;
    CREATE TABLE IF NOT EXISTS containers(name TEXT PRIMARY KEY,path TEXT,version TEXT,objects INTEGER,externals TEXT,complete INTEGER DEFAULT 0);
    CREATE TABLE IF NOT EXISTS objects(id TEXT PRIMARY KEY,container TEXT,pid INTEGER,type TEXT,name TEXT,bytes INTEGER,offset INTEGER,sha256 TEXT,status TEXT,error TEXT,script_object TEXT,script TEXT,tree BLOB);
    CREATE TABLE IF NOT EXISTS edges(src TEXT,field TEXT,dst TEXT,resolved INTEGER DEFAULT 0);
    CREATE TABLE IF NOT EXISTS resources(key TEXT,object_id TEXT);
    CREATE TABLE IF NOT EXISTS exports(object_id TEXT,path TEXT,bytes INTEGER,sha256 TEXT);
    CREATE INDEX IF NOT EXISTS ix_objects_type ON objects(type);
    CREATE INDEX IF NOT EXISTS ix_objects_script ON objects(script);
    CREATE INDEX IF NOT EXISTS ix_edges_src ON edges(src);
    CREATE INDEX IF NOT EXISTS ix_edges_dst ON edges(dst);
    ''')
    files=candidates(data);containers=[];fatal=[]
    for i,path in enumerate(files):
        if db.execute('SELECT complete FROM containers WHERE name=?',(path.name,)).fetchone()==(1,):
            print(f'Skip indexed {path.name}',flush=True);continue
        try:
            env=UnityPy.load(str(path));env.typetree_generator=gen
            # Dependencies loaded during parsing are indexed in their own pass.
            assets=list(env.assets)
            for asset in assets:
                name=asset.name;cache={};count=0;errors=0
                db.execute('DELETE FROM edges WHERE src IN (SELECT id FROM objects WHERE container=?)',(name,))
                db.execute('DELETE FROM resources WHERE object_id IN (SELECT id FROM objects WHERE container=?)',(name,))
                db.execute('DELETE FROM objects WHERE container=?',(name,))
                externals=[dict(path=x.path,type=x.type) for x in asset.externals]
                db.execute('INSERT OR REPLACE INTO containers VALUES(?,?,?,?,?,0)',(name,path.relative_to(data).as_posix(),asset.unity_version,len(asset.objects),json.dumps(externals)))
                for obj in list(asset.objects.values()):
                    oid=f'{name}:{obj.path_id}';typ=obj.type.name;nm='';err=None;scriptid=None;script=None;packed=None
                    raw=obj.get_raw_data();digest=hashlib.sha256(raw).hexdigest()
                    try:
                        tree,base=corrected_tree(obj,cache)
                        nm=tree.get('m_Name','')
                        if not isinstance(nm,str):nm=str(nm)
                        if typ=='MonoBehaviour':
                            ptr=tree['m_Script'];scriptid=target(asset,ptr['m_FileID'],ptr['m_PathID'])
                        if typ=='MonoScript':script='.'.join(filter(None,[tree.get('m_Namespace',''),tree.get('m_ClassName','')]))
                        for field,fileid,pid in pointers(tree):
                            db.execute('INSERT INTO edges(src,field,dst) VALUES(?,?,?)',(oid,field,target(asset,fileid,pid)))
                        if typ=='ResourceManager':
                            for key,ptr in tree.get('m_Container',[]):
                                db.execute('INSERT INTO resources VALUES(?,?)',(key,target(asset,ptr['m_FileID'],ptr['m_PathID'])))
                        packed=zlib.compress(json.dumps(compact(tree),ensure_ascii=True,separators=(',',':')).encode(),3)
                        status='ok'
                    except Exception as ex:status='failed';err=f'{type(ex).__name__}: {ex}';errors+=1
                    db.execute('INSERT OR REPLACE INTO objects VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)',
                        (oid,name,obj.path_id,typ,nm,obj.byte_size,obj.byte_start,digest,status,err,scriptid,script,packed))
                    count+=1
                    if count%10000==0:
                        db.commit();print(f'{name}: {count}/{len(asset.objects)}, failures={errors}',flush=True)
                db.execute('UPDATE containers SET complete=1 WHERE name=?',(name,));db.commit()
                print(f'Container {i+1}/{len(files)} {name}: {count} objects, {errors} failures',flush=True)
            del env;gc.collect()
        except Exception as ex:
            fatal.append(dict(path=path.relative_to(data).as_posix(),error=str(ex)))
            print(f'Container failed {path.name}: {ex}',flush=True)
    db.execute('UPDATE objects SET script=(SELECT s.script FROM objects s WHERE s.id=objects.script_object) WHERE type="MonoBehaviour"')
    db.execute('UPDATE edges SET resolved=1 WHERE dst IN (SELECT id FROM objects)');db.commit()
    summary=dict(containers=db.execute('SELECT count(*) FROM containers').fetchone()[0],
        candidateContainers=len(files),objects=db.execute('SELECT count(*) FROM objects').fetchone()[0],
        types=dict(db.execute('SELECT type,count(*) FROM objects GROUP BY type ORDER BY count(*) DESC').fetchall()),
        treeFailures=[dict(id=x[0],type=x[1],error=x[2]) for x in db.execute('SELECT id,type,error FROM objects WHERE status!="ok"')],
        edges=db.execute('SELECT count(*) FROM edges').fetchone()[0],
        unresolvedEdges=db.execute('SELECT count(*) FROM edges WHERE resolved=0').fetchone()[0],
        resourceKeys=db.execute('SELECT count(*) FROM resources').fetchone()[0],containerFailures=fatal,
        monoScriptPointerChecks=db.execute('SELECT count(*) FROM objects WHERE type="MonoBehaviour" AND status="ok"').fetchone()[0])
    write(project/'evidence/asset-summary.json',summary)
    write(project/'evidence/containers.json',[dict(name=r[0],path=r[1],unityVersion=r[2],objects=r[3],externals=json.loads(r[4])) for r in db.execute('SELECT name,path,version,objects,externals FROM containers')])
    write(project/'evidence/script-usage.json',[dict(script=r[0],objects=r[1]) for r in db.execute('SELECT script,count(*) FROM objects WHERE type="MonoBehaviour" GROUP BY script ORDER BY count(*) DESC')])
    db.execute('PRAGMA wal_checkpoint(TRUNCATE)');db.close()
    print(json.dumps({k:v for k,v in summary.items() if k not in ('types','treeFailures')},ensure_ascii=False),flush=True)

if __name__=='__main__':main()
