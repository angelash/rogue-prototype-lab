"""Preserve floating-point data textures and classify runtime-empty atlases."""
import collections, hashlib, json, re
from pathlib import Path
import numpy as np
import UnityPy
from PIL import Image
from UnityPy.helpers.ResourceReader import get_resource_data

ROOT=Path(__file__).resolve().parents[1];LOCAL=ROOT/'local';OUT=LOCAL/'assets/export'

def main():
    rows=json.loads((OUT/'index.json').read_text('utf8'));index={x['id']:x for x in json.loads((LOCAL/'decoded/bundle-index.json').read_text('utf8'))}
    failures=[x for x in rows if x['status']=='failed'];fixed=[]
    if not (OUT/'initial-export-errors.json').exists():(OUT/'initial-export-errors.json').write_text(json.dumps(failures,ensure_ascii=False,indent=2),'utf8')
    for row in failures:
        env=UnityPy.load(str(LOCAL/'decoded/bundles'/index[row['bundleID']]['filename']));obj=next(o for o in env.objects if o.path_id==row['pathID']);a=obj.read();k=int(a.m_TextureFormat)
        row.update(width=a.m_Width,height=a.m_Height,format=k);prefix=f"{row['bundleID']:04}_{row['pathID']}_"+re.sub(r'[^\w.-]','_',a.m_Name)[:80]
        raw=bytes(a.image_data) if a.image_data else get_resource_data(a.m_StreamData.path,obj.assets_file,a.m_StreamData.offset,a.m_StreamData.size) if a.m_StreamData.size else b''
        if a.m_Width==0 and a.m_Height==0 and not raw:
            row.update(status='runtime-empty',reason='Zero-sized font atlas, no serialized pixels');row.pop('error',None);fixed.append(row);continue
        assert k in (17,20),(k,a.m_Name)
        dtype='<f2' if k==17 else '<f4';pixels=np.frombuffer(raw,dtype=dtype);n=a.m_Width*a.m_Height*4;assert len(pixels)>=n
        targets=[];native=OUT/'Texture2D'/(prefix+'.raw');native.write_bytes(raw);targets.append(native)
        full=OUT/'Texture2D'/(prefix+'.npy');np.save(full,pixels,allow_pickle=False);targets.append(full)
        base=pixels[:n].astype(np.float32).reshape(a.m_Height,a.m_Width,4)
        finite=np.isfinite(base);finite_values=base[finite];lo=float(finite_values.min()) if finite_values.size else 0;hi=float(finite_values.max()) if finite_values.size else 0
        preview=np.nan_to_num(base,nan=lo,posinf=hi,neginf=lo);preview=(np.clip((preview-lo)/(hi-lo),0,1)*255).astype(np.uint8) if hi>lo else np.zeros_like(preview,dtype=np.uint8)
        im=Image.fromarray(preview[:,:,:3][::-1],mode='RGB');png=OUT/'Texture2D'/(prefix+'_data-preview.png');im.save(png);targets.append(png)
        row.update(status='ok',dataTexture=True,previewTransform='global finite min/max normalization, RGB, vertical flip; inspection only',min=lo,max=hi,sourceValueCount=len(pixels),dtype=dtype,files=[{'path':p.relative_to(LOCAL).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in targets]);row.pop('error',None);fixed.append(row)
        assert np.load(full,allow_pickle=False).tobytes()==raw
    (OUT/'index.json').write_text(json.dumps(rows,ensure_ascii=False,separators=(',',':')),'utf8');(OUT/'errors.json').write_text(json.dumps([x for x in rows if x['status']=='failed'],ensure_ascii=False,indent=2),'utf8')
    evidence=json.loads((ROOT/'evidence/media-export.json').read_text('utf8'));evidence['passed']=dict(collections.Counter(x['type'] for x in rows if x['status']=='ok'));evidence['runtimeEmptyTextures']=sum(x['status']=='runtime-empty' for x in rows);evidence['failed']=sum(x['status']=='failed' for x in rows);evidence['exportedBytes']=sum(f['bytes'] for x in rows for f in x['files']);evidence['floatDataTexturesLossless']=sum(x.get('dataTexture',False) for x in rows);evidence['textureRepair']='17 floating-point data textures retained as exact raw + NPY; PNG is normalized preview. Three zero-sized font atlases classified as runtime-empty.'
    (ROOT/'evidence/media-export.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),'utf8');print(json.dumps(evidence,ensure_ascii=False))

if __name__=='__main__':main()
