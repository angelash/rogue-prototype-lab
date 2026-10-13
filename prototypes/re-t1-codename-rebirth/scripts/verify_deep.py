"""Independent byte/format/accounting checks for the recovered local corpus."""
import collections, gzip, hashlib, json, re, sqlite3, struct, sys, time
from pathlib import Path
from PIL import Image
import numpy as np

ROOT=Path(__file__).resolve().parents[1];LOCAL=ROOT/'local'
sys.path.insert(0,str(LOCAL/'tools/python'))
from elftools.elf.elffile import ELFFile

def read(p):return json.loads((ROOT/p).read_text('utf8'))
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(2**20),b''):h.update(b)
    return h.hexdigest()
def wav_check(d):
    assert d[:4]==b'RIFF' and d[8:12]==b'WAVE' and struct.unpack_from('<I',d,4)[0]+8==len(d)
    p=12;fmt=None;size=None
    while p<len(d):
        tag=d[p:p+4];n=struct.unpack_from('<I',d,p+4)[0];assert p+8+n<=len(d)
        if tag==b'fmt ':fmt=struct.unpack_from('<HHIIHH',d,p+8)
        if tag==b'data':size=n
        p+=8+n+(n%2)
    assert p==len(d) and fmt and size is not None
    kind,ch,hz,bps,align,bits=fmt;assert kind in (1,3) and ch>0 and hz>0 and align==ch*bits//8 and bps==hz*align and size%align==0
    return size//align

def main():
    t=time.monotonic();checks=[]
    def check(name,ok,details=None):
        checks.append({'name':name,'passed':bool(ok),'details':details});print(name+(' PASS' if ok else ' FAIL'),flush=True)
    intake=read('evidence/intake.json');check('original_copy_unchanged',digest(Path(intake['source']))==intake['sourceSHA256']==digest(Path(intake['copy'])))
    q=read('evidence/qxgm-decoding.json');d=(ROOT/q['output']).read_bytes();check('standard_metadata_hash_header',digest(ROOT/q['output'])==q['outputSHA256'] and struct.unpack_from('<II',d)==(0xfab11baf,31))
    check('metadata_page_HMAC_digest_accounting',q['pagesDigestPassed']==q['pages']==3145 and q['headerHMACPassed'] and q['hashTableSHA256Passed'])
    bi=read('local/decoded/bundle-index.json');bad=[]
    for x in bi:
        p=LOCAL/'decoded/bundles'/x['filename']
        if not p.exists() or p.stat().st_size!=x['bytes'] or digest(p)!=x['SHA256'] or p.read_bytes()[:8]!=b'UnityFS\0':bad.append(x['id'])
    check('all_decoded_bundle_bytes',len(bi)==981 and not bad,bad)
    mi=read('local/managed/index.json');bad=[]
    for x in mi:
        p=LOCAL/x['file'];d=p.read_bytes();pe=struct.unpack_from('<I',d,0x3c)[0]
        if len(d)!=x['bytes'] or digest(p)!=x['SHA256'] or d[:2]!=b'MZ' or d[pe:pe+4]!=b'PE\0\0':bad.append(x['file'])
    check('all_managed_image_bytes_PE',len(mi)==38 and not bad,bad)
    check('all_shards_blocks_authenticated',sum(x['shards'] for x in mi)==sum(x['shardHashesPassed'] for x in mi)==208 and sum(x['blocks'] for x in mi)==sum(x['blockCipherHashesPassed'] for x in mi)==sum(x['GCMBlockTagsPassed'] for x in mi)==163613 and all(x['GCMKeyWrapPassed'] and x['GCMIndexTagsPassed']==x['indexGroups'] and x['allBytesCovered'] for x in mi))
    deep=read('evidence/deep-coverage.json');check('38_IL_indexes_no_parse_failures',len(deep['managed'])==38 and all(x['failures']==0 for x in deep['managed']))
    check('decompiled_corpus_and_diagnostics',len(list((LOCAL/'source/all').rglob('*.cs')))==deep['decompiledFiles']==17872 and deep['decompilerDiagnostics']==8)
    scripts=read('local/il2cpp-arm64/script.json')['ScriptMethod']
    with (LOCAL/'apk/lib/arm64-v8a/libil2cpp.so').open('rb') as f:segments=[(s['p_vaddr'],s['p_vaddr']+s['p_filesz']) for s in ELFFile(f).iter_segments() if s['p_type']=='PT_LOAD' and s['p_flags']&1]
    check('native_method_addresses_executable',len(scripts)==199560 and all(any(lo<=x['Address']<hi for lo,hi in segments) for x in scripts))
    rt=read('local/hotfix-runtime-config.json');check('runtime_release_profile',rt['currentHotfixBuildProfile']==1 and 'public const HotfixRuntimeBuildProfile ReleaseHotfix = 1;' in (LOCAL/'il2cpp-arm64/dump.cs').read_text('utf8'))
    objects=read('local/assets/objects.json');ob=read('evidence/bundle-objects.json');check('all_object_identities_and_trees',len(objects)==ob['objects']==191092 and len({(x['bundleID'],x['file'],x['pathID']) for x in objects})==len(objects) and all(x['treeStatus']=='ok' for x in objects))
    text=read('local/assets/text-assets.json');bad=[x['payload'] for x in text if (LOCAL/x['payload']).stat().st_size!=x['payloadBytes'] or digest(LOCAL/x['payload'])!=x['payloadSHA256']]
    check('all_TextAsset_bytes',len(text)==1077 and not bad,bad)
    cfg=read('evidence/config-decoding.json');tables=[json.loads(f.read_text('utf8')) for f in (LOCAL/'config/tables').glob('*.json')]
    check('configuration_rows_and_schema',len(tables)==cfg['tableCount']==208 and sum(x['rowCount'] for x in tables)==cfg['rowCount']==30676 and all(x['rowCount']==len(x['rows']) for x in tables))
    check('known_schema_difference_explicit',deep['configSchemaDifferences']==[{'table':'task_action','onlyCode':['format'],'onlyData':[]}])
    mb=read('local/assets/monobehaviour-types.json');check('all_component_script_references_accounted',mb['resolved']==39030 and len(mb['unresolved'])==1 and mb['unresolved'][0]['script']=={'m_FileID':0,'m_PathID':0})
    media=read('local/assets/export/index.json');files=[f for x in media for f in x['files']];bad=[];png=0;wavs=0;mesh=0;floats=0
    for row in media:
        for x in row['files']:
            p=LOCAL/x['path']
            if p.stat().st_size!=x['bytes'] or digest(p)!=x['sha256']:bad.append(x['path'])
            if p.suffix=='.png':
                with Image.open(p) as im:assert (im.width,im.height)==(row['width'],row['height']);im.verify()
                png+=1
            if p.suffix=='.wav':wav_check(p.read_bytes());wavs+=1
            if p.name.endswith('.obj.gz'):
                vs=fs=0;minv=None;maxv=0
                for line in gzip.open(p,'rt',encoding='utf8'):
                    if line.startswith('v '):vs+=1
                    elif line.startswith('f '):
                        fs+=1;ids=[int(v.split('/')[0]) for v in line.split()[1:]];assert len(ids)>=3
                        minv=min(minv or ids[0],min(ids));maxv=max(maxv,max(ids))
                assert vs==row['vertices'] and fs==row['faces'] and (not fs or 1<=minv<=maxv<=vs);mesh+=1
        if row.get('dataTexture'):
            raw=next(LOCAL/x['path'] for x in row['files'] if x['path'].endswith('.raw'));npy=next(LOCAL/x['path'] for x in row['files'] if x['path'].endswith('.npy'));assert np.load(npy,allow_pickle=False).tobytes()==raw.read_bytes();floats+=1
    check('all_exported_media_hashes',not bad,{'files':len(files),'bad':bad})
    check('all_texture_formats',png==3201 and floats==17 and sum(x['status']=='runtime-empty' for x in media)==3 and all(x['status'] in ('ok','runtime-empty') for x in media))
    check('all_OBJ_vertex_face_bounds',mesh==6867)
    check('all_AudioClip_WAVE_formats',wavs==728)
    fm=read('local/assets/export/FMOD/index.json');fm_bad=[];fm_wav=0
    for x in fm:
        for f in x['files']:
            p=LOCAL/f['path']
            if digest(p)!=f['sha256'] or p.stat().st_size!=f['bytes']:fm_bad.append(f['path'])
            if p.suffix=='.wav':wav_check(p.read_bytes());fm_wav+=1
    check('all_FMOD_sample_bytes_and_formats',len(fm)==27 and fm_wav==333 and not fm_bad and all(x['status']=='ok' for x in fm))
    payload=read('evidence/payload-inspection.json');check('structured_payload_coverage',payload['textAssets']==1077 and not payload['validationFailures'] and payload['collisionShapes']==49987)
    base=read('evidence/base-recovery.json');check('base_scope_and_remaining_failures',base['attempted']==970 and base['passed']==967 and base['failed']==3)
    android=read('evidence/android-recovery.json');check('SDK_full_DEX_instruction_coverage',android['disassemblyDEX']==28 and android['disassemblyClasses']==11505 and android['disassemblyMethodBodies']==71190 and android['disassemblyInstructionUnits']==2059980 and android['allSDKCountsMatchIndependentDEXParser'])
    with sqlite3.connect(f'file:{(LOCAL/"deep-catalog.sqlite3").as_posix()}?mode=ro',uri=True) as db:
        integrity=db.execute('PRAGMA integrity_check').fetchone()[0];counts={k:db.execute('SELECT COUNT(*) FROM '+k).fetchone()[0] for k in ('types','methods','assets','config_rows','config_labels')}
    check('catalog_integrity_counts',integrity=='ok' and counts['types']==sum(x['types'] for x in deep['managed']) and counts['methods']==sum(x['methods'] for x in deep['managed']) and counts['assets']==191092 and counts['config_rows']==30676 and counts['config_labels']==8030,counts)
    links=[]
    for f in ROOT.rglob('*.md'):
        if LOCAL in f.parents:continue
        for link in re.findall(r'\]\(([^)]+)\)',f.read_text('utf8')):
            if link.startswith(('http','app:','#')):continue
            target=link.split('#')[0]
            if target.endswith(('deep-verification.json','deep-backup.json')):continue
            if not (f.parent/target).exists():links.append({'file':str(f.relative_to(ROOT)),'link':target})
    check('all_report_local_links',not links,links)
    result={'date':'2026-10-13','passed':sum(x['passed'] for x in checks),'total':len(checks),'checks':checks,'seconds':round(time.monotonic()-t,2),'scope':'Static recovered bytes, media/container formats, instruction/accounting and report links. Known conversion/decompiler exceptions remain explicit.','notValidated':['APK execution','server/online data','original-source compilation','native methods as high-level pseudocode','QGSG/QXTL outer signatures','listening/performance']}
    (ROOT/'evidence/deep-verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf8');print(json.dumps({k:v for k,v in result.items() if k!='checks'},ensure_ascii=False))
    if result['passed']!=result['total']:raise SystemExit(1)

if __name__=='__main__':main()
