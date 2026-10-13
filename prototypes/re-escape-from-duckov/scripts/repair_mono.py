"""Recheck every managed object with normalized list nodes and independent base pointers."""
import argparse,gc,hashlib,json,sqlite3,sys,zlib
from pathlib import Path
import UnityPy
from UnityPy.helpers.Tpk import get_typetree_node
from analyze_assets import corrected_tree,compact,pointers,target
from prepare import write

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);ap.add_argument('--generator',required=True);a=ap.parse_args()
    p=Path(a.project).resolve();local=p/'local';sys.path.insert(0,str(Path(a.generator).resolve()))
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    gen=TypeTreeGenerator('2022.3.62f2');gen.load_local_game(str(local/'build-copy'))
    db=sqlite3.connect(local/'assets.sqlite',timeout=60);results=[]
    for container,path in db.execute('SELECT name,path FROM containers WHERE name IN (SELECT DISTINCT container FROM objects WHERE type="MonoBehaviour")').fetchall():
        env=UnityPy.load(str(local/'build-copy/Duckov_Data'/path));env.typetree_generator=gen
        asset=next(x for x in env.assets if x.name==container);cache={};count=0;errors=0
        for oid,pid,previous in db.execute('SELECT id,pid,status FROM objects WHERE container=? AND type="MonoBehaviour"',(container,)).fetchall():
            obj=asset.objects[pid];base=obj.read_typetree(nodes=get_typetree_node(114,obj.version),check_read=False)
            ptr=base['m_Script'];scriptid=target(asset,ptr['m_FileID'],ptr['m_PathID'])
            error=None
            try:tree,_=corrected_tree(obj,cache);status='ok'
            except Exception as ex:tree=base;status='base-only';error=f'{type(ex).__name__}: {ex}';errors+=1
            db.execute('DELETE FROM edges WHERE src=?',(oid,))
            for field,fileid,pathid in pointers(tree):db.execute('INSERT INTO edges(src,field,dst) VALUES(?,?,?)',(oid,field,target(asset,fileid,pathid)))
            packed=zlib.compress(json.dumps(compact(tree),ensure_ascii=True,separators=(',',':')).encode(),3)
            db.execute('UPDATE objects SET name=?,script_object=?,status=?,error=?,tree=? WHERE id=?',(base['m_Name'],scriptid,status,error,packed,oid))
            if previous!='ok' or status!='ok':results.append(dict(id=oid,previous=previous,current=status,error=error))
            count+=1
            if count%1000==0:db.commit()
        db.commit();del env;gc.collect()
        print(f'Mono recheck {container}: {count}, base-only={errors}',flush=True)
    db.execute('UPDATE objects SET script=(SELECT s.script FROM objects s WHERE s.id=objects.script_object) WHERE type="MonoBehaviour"')
    db.execute('UPDATE edges SET resolved=CASE WHEN dst IN (SELECT id FROM objects) THEN 1 ELSE 0 END');db.commit()
    write(p/'evidence/mono-repair.json',results)
    summary=json.loads((p/'evidence/asset-summary.json').read_text())
    summary.update(treeFailures=[dict(id=r[0],script=r[1],error=r[2]) for r in db.execute('SELECT id,script,error FROM objects WHERE status!="ok"')],
        edges=db.execute('SELECT count(*) FROM edges').fetchone()[0],unresolvedEdges=db.execute('SELECT count(*) FROM edges WHERE resolved=0').fetchone()[0],
        monoScriptPointerChecks=db.execute('SELECT count(*) FROM objects WHERE type="MonoBehaviour"').fetchone()[0])
    write(p/'evidence/asset-summary.json',summary)
    write(p/'evidence/script-usage.json',[dict(script=r[0],objects=r[1]) for r in db.execute('SELECT script,count(*) FROM objects WHERE type="MonoBehaviour" GROUP BY script ORDER BY count(*) DESC')])
    db.commit();db.close();print(f'Remaining full-tree failures: {len(summary["treeFailures"])}',flush=True)

if __name__=='__main__':main()
