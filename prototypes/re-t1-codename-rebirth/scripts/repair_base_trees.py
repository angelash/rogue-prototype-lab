"""Reuse same-build embedded script type trees to read stripped base components."""
import collections, json, sys, time
from pathlib import Path
import UnityPy
from inspect_bundles import compact

ROOT=Path(__file__).resolve().parents[1]

def main():
    t=time.monotonic();local=ROOT/'local'
    bundle_rows=json.loads((local/'assets/objects.json').read_text('utf8'))
    base=json.loads((local/'readable-objects.json').read_text('utf8'))
    cons=json.loads((local/'assets/containers.json').read_text('utf8'))+json.loads((ROOT/'evidence/readable-containers.json').read_text('utf8'))
    ext={x.get('file',x.get('name')):x['externals'] for x in cons}
    scripts={(x['file'],x['pathID']):(x.get('m_AssemblyName',''),x.get('m_Namespace',''),x.get('m_ClassName','')) for x in bundle_rows+base if x['type']=='MonoScript'}
    def identity(file,p):
        if p['m_FileID']>0:file=ext[file][p['m_FileID']-1].replace('\\','/').split('/')[-1]
        return scripts.get((file,p['m_PathID']))
    needed={identity(x['file'],x['base']['m_Script']) for x in base if x.get('baseOnly')};needed.discard(None)
    nodes={};bundle_map=collections.defaultdict(list)
    for x in bundle_rows:
        if x['type']=='MonoBehaviour':
            key=identity(x['file'],x['script'])
            if key in needed:bundle_map[x['bundleID']].append((x,key))
    index={x['id']:x for x in json.loads((local/'decoded/bundle-index.json').read_text('utf8'))}
    for bid,rows in bundle_map.items():
        if all(k in nodes for _,k in rows):continue
        env=UnityPy.load(str(local/'decoded/bundles'/index[bid]['filename']));objects={(o.assets_file.name,o.path_id):o for o in env.objects}
        for x,key in rows:
            if key not in nodes:nodes[key]=objects[x['file'],x['pathID']]._get_typetree_node()
    # Only assembly metadata is read; no game constructors or methods execute.
    sys.path.insert(0,str(local/'tools/python'))
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    generator=TypeTreeGenerator('2022.3.62f3')
    # AOT supplemental images prune fields; full native declarations preserve
    # serialization attributes more faithfully than those partial images.
    generator.load_local_dll_folder(str(local/'il2cpp-arm64/DummyDll'))
    generated={};generation_errors={}
    for key in needed-nodes.keys():
        asm,namespace,name=key
        try:generated[key]=generator.get_nodes_up(asm,'.'.join(x for x in (namespace,name) if x))
        except Exception as ex:generation_errors[key]=type(ex).__name__+': '+str(ex)
    embedded_count=len(nodes);nodes.update(generated)
    result=[];failed=[];inputs=collections.defaultdict(list)
    for x in base:
        if x.get('baseOnly'):inputs[x['source']].append(x)
    out=local/'base-recovered-trees';out.mkdir(exist_ok=True)
    for source,rows in inputs.items():
        env=UnityPy.load(str(local/'apk'/source));objects={(o.assets_file.name,o.path_id):o for o in env.objects}
        for x in rows:
            key=identity(x['file'],x['base']['m_Script']);r={'source':source,'file':x['file'],'pathID':x['pathID'],'scriptIdentity':key}
            try:
                assert key in nodes,'No same-build embedded type tree'
                tree=objects[x['file'],x['pathID']].read_typetree(nodes=nodes[key])
                target=out/(Path(source).name+'_'+str(x['pathID'])+'.json');target.write_text(json.dumps(compact(tree),ensure_ascii=False,separators=(',',':')),'utf8')
                r.update(status='ok',path=target.relative_to(local).as_posix())
            except Exception as ex:r.update(status='failed',error=type(ex).__name__+': '+str(ex));failed.append(r)
            result.append(r)
    (local/'base-recovered-index.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf8')
    report={'attempted':len(result),'passed':len(result)-len(failed),'failed':len(failed),'neededScriptTypes':len(needed),'sameBuildTypeTrees':embedded_count,'metadataGeneratedTypeTrees':len(generated),'typeTreeGeneratorVersion':'0.0.10','failures':failed,'seconds':round(time.monotonic()-t,2),'originalFirstLayerResultsPreserved':True}
    (ROOT/'evidence/base-recovery.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf8')
    print(json.dumps({k:v for k,v in report.items() if k!='failures'},ensure_ascii=False),flush=True)

if __name__=='__main__':main()
