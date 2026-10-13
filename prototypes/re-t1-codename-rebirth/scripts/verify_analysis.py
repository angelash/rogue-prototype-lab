"""Validate actual input bytes, standard parsers, catalogs and scope accounting."""
import hashlib
import json
import re
import subprocess
import zlib
from pathlib import Path
from intake import PROJECT, digest, write

def read(path):return json.loads((PROJECT/path).read_text('utf-8'))
def main():
    tests=[]
    def check(name, passed, details=None):
        tests.append({'name':name,'passed':bool(passed),'details':details})
        print(name,'PASS' if passed else 'FAIL',flush=True)
    intake=read('evidence/intake.json');apk=PROJECT/'local/apk'
    check('original_and_copy_sha256',digest(Path(intake['source']))==intake['sourceSHA256']==digest(Path(intake['copy'])))
    files=read('evidence/apk-files.json');bad=[]
    for row in files:
        file=apk/row['path']
        if not file.is_file() or file.stat().st_size!=row['bytes'] or digest(file)!=row['sha256']:bad.append(row['path'])
    check('all_extracted_file_bytes',not bad,{'files':len(files),'mismatches':bad})
    check('zip_crc_intake',not intake['failures'] and all(x['status']=='ok' for x in files))
    manifest=read('evidence/manifest-summary.json');assets=read('local/manifest-assets.json');bundles=read('local/manifest-bundles.json')
    check('manifest_exact_consumption',manifest['manifestConsumed']==manifest['manifestBytes'] and manifest['extTrailingBytes']==0)
    check('unique_addresses_and_paths',len(assets)==len(set(x['address'] for x in assets))==len(set(x['path'] for x in assets)))
    check('all_manifest_dependencies_in_apk',len(assets)==manifest['assetsWithAllDependenciesInAPK'] and not manifest['bundlesAbsentFromAPK'])
    check('all_bundle_file_sizes',all(x['sizeMatches'] for x in bundles))
    md5_bad=[];crc_bad=[]
    for b in bundles:
        file=apk/'assets'/b['filename'];m=hashlib.md5();crc=0
        with file.open('rb') as f:
            for data in iter(lambda:f.read(1024*1024),b''):m.update(data);crc=zlib.crc32(data,crc)
        if m.hexdigest()!=b['hash']:md5_bad.append(b['id'])
        if (crc&0xffffffff).to_bytes(4,'little').hex()!=b['crc']:crc_bad.append(b['id'])
    check('manifest_bundle_md5',not md5_bad,{'files':len(bundles),'mismatches':md5_bad})
    check('manifest_bundle_crc_little_endian_hex',not crc_bad,{'mismatches':crc_bad})
    mfile=apk/'assets/PackageManifest_DefaultPackage_73317.bytes'
    check('manifest_md5_sidecar',hashlib.md5(mfile.read_bytes()).hexdigest()==mfile.with_suffix('.hash').read_text('ascii').strip())
    dex=read('evidence/dex-summary.json');sdk_results=[]
    for path in sorted(apk.glob('classes*.dex')):
        p=subprocess.run(['D:/androidSdk/build-tools/34.0.0/dexdump.exe','-c',str(path)],capture_output=True)
        sdk_results.append({'file':path.name,'exitCode':p.returncode,'output':(p.stdout+p.stderr).decode('utf-8',errors='replace')[:2000]})
    write(PROJECT/'local/dex-sdk-verification.json',sdk_results)
    check('independent_android_sdk_dex_verification',all(x['exitCode']==0 for x in sdk_results),{'files':len(sdk_results)})
    check('dex_checksum_and_class_accounting',len(dex)==28 and all(x['sha1Matches'] and x['adler32Matches'] for x in dex)
        and sum(x['header']['classCount'] for x in dex)==len(read('local/dex-class-list.json')))
    native=read('evidence/native-summary.json')
    check('native_all_libraries_accounted',len(native)==len(list(apk.glob('lib/*/*.so')))==50)
    obj=read('local/readable-objects.json');summary=read('evidence/readable-asset-summary.json')
    check('unity_object_scope_closes',len(obj)==summary['objects']==summary['treeSuccess']+summary['treeFailures'])
    check('unity_full_and_base_only_distinguished',summary['treeFailures']==summary['monoBehaviourBaseOnly']+1==971)
    check('unity_objects_unique',len(obj)==len({(x['source'],x['file'],x['pathID']) for x in obj}))
    scripts=read('local/code-symbols.json');code=read('evidence/code-summary.json')
    check('script_identity_accounting',len(scripts)==code['unityScriptDescriptors'] and len({(x['assembly'],x['name']) for x in scripts})==code['uniqueUnityScriptTypes'])
    video=read('evidence/raw-videos.json');images=read('evidence/raw-images.json')
    check('raw_media_status_accounting',len(video)==36 and all(x['status']=='ok' for x in video) and len(images)==487
        and sum(x['status']=='ok' for x in images)==483 and sum(x['status']=='failed' for x in images)==4)
    qa=read('evidence/explorer-qa.json');check('explorer_functional_checks',qa['passed']==qa['total']==14 and all(x['pass'] for x in qa['checks']))
    links=[]
    for file in PROJECT.rglob('*.md'):
        if (PROJECT/'local') in file.parents:continue
        for link in re.findall(r'\]\(([^)]+)\)',file.read_text('utf-8')):
            if link.startswith(('http','app:','#')):continue
            target=link.split('#')[0]
            if target.endswith('verification.json'):continue # written after checks
            if not (file.parent/target).exists():links.append({'file':str(file.relative_to(PROJECT)),'target':target})
    check('report_local_links',not links,links)
    result={'date':'2026-10-13','scope':'Input bytes, clear manifests, DEX/ELF metadata, readable Unity scope and index UI; not complete game code/assets recovery.',
        'passed':sum(x['passed'] for x in tests),'total':len(tests),'checks':tests,
        'notValidated':['custom resource wrappers','QXGM metadata','hotfix method bodies','configuration rows and rules','game runtime and online/server content']}
    write(PROJECT/'evidence/verification.json',result)
    if result['passed']!=result['total']:raise SystemExit(1)
if __name__=='__main__':main()
