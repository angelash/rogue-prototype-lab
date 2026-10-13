"""Inventory directly readable Unity files and raw media, without wrapper parsing."""
import collections
import hashlib
import importlib.metadata
import json
import subprocess
from pathlib import Path
import UnityPy
from PIL import Image
from UnityPy.helpers.Tpk import get_typetree_node
from intake import PROJECT, write

def compact(v):
    if isinstance(v,bytes):return {'byteCount':len(v),'sha256':hashlib.sha256(v).hexdigest()}
    if isinstance(v,dict):return {str(k):compact(x) for k,x in v.items()}
    if isinstance(v,(list,tuple)):return [compact(x) for x in v]
    return v

def main():
    apk=PROJECT/'local/apk';data=apk/'assets/bin/Data';containers=[];objects=[];errors=[]
    candidates=[x for x in data.iterdir() if x.is_file() and x.name not in ['boot.config','unity_app_guid']
        and x.suffix not in ['.json'] and not x.name.endswith(tuple(f'.split{i}' for i in range(1,10)))]
    candidates.extend([apk/'assets/firstbundle_27f169ab75c7002eab21b01d7140b84f.bundle',data/'Resources/unity_builtin_extra'])
    for path in candidates:
        relative=path.relative_to(apk).as_posix()
        try:
            env=UnityPy.load(str(path))
            if not list(env.assets):continue
            for file in env.assets:
                containers.append({'input':relative,'name':file.name,'unityVersion':file.unity_version,
                    'objects':len(file.objects),'externals':[x.path for x in file.externals]})
                for obj in file.objects.values():
                    row={'source':relative,'file':file.name,'pathID':obj.path_id,'type':obj.type.name,
                        'bytes':obj.byte_size,'treeStatus':'pending','name':''}
                    try:
                        tree=obj.read_typetree();row['treeStatus']='ok';row['name']=tree.get('m_Name','')
                        if row['type']=='MonoScript':row.update({k:tree.get(k) for k in ['m_ClassName','m_Namespace','m_AssemblyName']})
                        row['treeFile']=f"unity-trees/{path.name}/{obj.path_id}_{obj.type.name}.json"
                        write(PROJECT/'local'/row['treeFile'],compact(tree))
                    except Exception as ex:
                        row['treeStatus']='failed';row['error']=str(ex);errors.append({'source':relative,'pathID':obj.path_id,'type':obj.type.name,'error':str(ex)})
                        if row['type']=='MonoBehaviour':
                            try:
                                base=obj.read_typetree(nodes=get_typetree_node(114,obj.version),check_read=False)
                                row['baseOnly']=True;row['base']=compact(base)
                            except Exception as base_error: row['baseError']=str(base_error)
                    objects.append(row)
        except Exception as ex:errors.append({'source':relative,'stage':'container','error':str(ex)})
    images=[]
    for path in apk.rglob('*'):
        if path.suffix.lower() not in ['.png','.jpg','.jpeg','.webp']:continue
        r={'file':path.relative_to(apk).as_posix()}
        try:
            with Image.open(path) as im:r.update(width=im.width,height=im.height,format=im.format);im.verify()
            r['status']='ok'
        except Exception as ex:r.update(status='failed',error=str(ex))
        images.append(r)
    videos=[]
    for path in sorted((apk/'assets').glob('*.mp4')):
        r={'file':path.relative_to(apk).as_posix()}
        try:
            proc=subprocess.run(['ffprobe','-v','error','-show_format','-show_streams','-of','json',str(path)],capture_output=True,check=True)
            d=json.loads(proc.stdout);r.update(status='ok',duration=d['format'].get('duration'),
                streams=[{k:s.get(k) for k in ['codec_type','codec_name','width','height','sample_rate','channels','r_frame_rate']} for s in d['streams']])
        except Exception as ex:r.update(status='failed',error=str(ex))
        videos.append(r)
    scripts=[x for x in objects if x['type']=='MonoScript']
    summary={'unityPyVersion':UnityPy.__version__,'containers':len(containers),'objects':len(objects),
        'types':dict(collections.Counter(x['type'] for x in objects).most_common()),
        'treeSuccess':sum(x['treeStatus']=='ok' for x in objects),'treeFailures':sum(x['treeStatus']=='failed' for x in objects),
        'errors':len(errors),'monoScripts':len(scripts),'scriptAssemblies':dict(collections.Counter(x.get('m_AssemblyName','') for x in scripts)),
        'monoBehaviourBaseOnly':sum(bool(x.get('baseOnly')) for x in objects),
        'rawImages':len(images),'rawImagesOK':sum(x['status']=='ok' for x in images),
        'rawVideos':len(videos),'rawVideosOK':sum(x['status']=='ok' for x in videos),
        'scope':'Only directly readable base Unity files, one plain firstbundle, and APK raw images/videos; protected resource bundles excluded.'}
    write(PROJECT/'evidence/readable-asset-summary.json',summary)
    write(PROJECT/'evidence/readable-containers.json',containers)
    write(PROJECT/'local/readable-objects.json',objects);write(PROJECT/'local/readable-errors.json',errors)
    write(PROJECT/'local/mono-scripts.json',scripts);write(PROJECT/'evidence/raw-images.json',images)
    write(PROJECT/'evidence/raw-videos.json',videos)
    print(json.dumps(summary,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
