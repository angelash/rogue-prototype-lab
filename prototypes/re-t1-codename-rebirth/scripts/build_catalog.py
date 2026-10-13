"""Create metadata catalogs, identity evidence and an offline searchable explorer."""
import collections
import json
import re
import subprocess
from pathlib import Path
from intake import PROJECT, write

def read(path):return json.loads((PROJECT/path).read_text('utf-8'))
def main():
    local=PROJECT/'local';apk=local/'apk';input_file=next((local/'input').glob('*.apk'))
    aapt=Path('D:/androidSdk/build-tools/34.0.0/aapt.exe')
    for args,name in [(['dump','badging',str(input_file)],'badging.txt'),
            (['dump','xmltree',str(input_file),'AndroidManifest.xml'],'AndroidManifest.txt')]:
        result=subprocess.run([str(aapt),*args],capture_output=True,check=True)
        (local/name).write_bytes(result.stdout)
    badging=(local/'badging.txt').read_text('utf-8');manifest=(local/'AndroidManifest.txt').read_text('utf-8')
    package=re.search(r"package: name='([^']+)' versionCode='([^']+)' versionName='([^']+)'",badging)
    components=[];permissions=[];current=None
    for line in manifest.splitlines():
        match=re.match(r'(\s*)E: (\S+)',line)
        if match:
            element=match.group(2)
            if element in ['activity','service','receiver','provider','application','uses-permission']:
                current={'type':element,'indent':len(match.group(1)),'attributes':{}}
                if element=='uses-permission':permissions.append(current)
                else:components.append(current)
            elif current and len(match.group(1))<=current['indent']:current=None
        match=re.search(r'A: android:([\w]+)[^=]*=(.*)',line)
        if match and current and len(line)-len(line.lstrip())==current['indent']+2:
            if match.group(1) in ['name','exported','enabled','screenOrientation','debuggable','usesCleartextTraffic']:
                value=match.group(2).split(' (Raw:')[0].strip('"')
                current['attributes'][match.group(1)]=value
    identity={'label':re.search(r"application-label:'([^']+)'",badging).group(1),
        'package':package[1],'versionCode':int(package[2]),'versionName':package[3],
        'minSDK':int(re.search(r"sdkVersion:'(\d+)'",badging)[1]),
        'targetSDK':int(re.search(r"targetSdkVersion:'(\d+)'",badging)[1]),
        'androidDebuggable':'application-debuggable' in badging,
        'launchActivity':re.search(r"launchable-activity: name='([^']+)'",badging)[1],
        'unityVersion':'2022.3.62f3c1','unityVersionEvidence':'globalgamemanagers and level0 serialized-file headers',
        'components':[{k:v for k,v in x.items() if k!='indent'} for x in components],
        'permissions':[x['attributes'].get('name','') for x in permissions]}
    write(PROJECT/'evidence/identity.json',identity)
    scripts=read('local/mono-scripts.json');assets=read('local/manifest-assets.json');bundles=read('local/manifest-bundles.json')
    symbols=[]
    for x in scripts:
        ns=x.get('m_Namespace') or '';cls=x.get('m_ClassName') or ''
        symbols.append({'assembly':x.get('m_AssemblyName') or '', 'name':(ns+'.' if ns else '')+cls,
            'class':cls,'namespace':ns,'source':x['source'],'file':x['file'],'pathID':x['pathID']})
    unique={(x['assembly'],x['name']) for x in symbols}
    cfg=sorted({x['class'] for x in symbols if x['assembly']=='ConfigDefine' and x['class'].startswith('data_')})
    registrations=json.loads((apk/'assets/bin/Data/ScriptingAssemblies.json').read_text('utf-8'))
    initializers=json.loads((apk/'assets/bin/Data/RuntimeInitializeOnLoads.json').read_text('utf-8'))['root']
    dex_classes=[]
    for file in sorted((local/'dex').glob('*.json')):
        data=json.loads(file.read_text('utf-8'))
        for c in data['classes']:
            dex_classes.append({'dex':file.stem,'name':c['name'],'base':c['base'],
                'methods':len(c['methods']),'fields':len(c['fields'])})
    duplicates=[{'name':name,'count':n} for name,n in collections.Counter(x['name'] for x in dex_classes).items() if n>1]
    core=sorted({x['name'] for x in symbols if x['assembly'] in ['GameCore','GamePlay','Launcher']
        and x['class'].endswith(('System','Control','Mgr','Manager'))})
    ui=collections.Counter(x['path'].split('/')[5] for x in assets if x['path'].startswith('Assets/res/UI/MainAsset/Prefabs/'))
    maps=sorted({m.group(1) for x in assets if (m:=re.search(r'Assets/res/collision/client/collision_c_(\d+)\.bytes',x['path']))})
    code_summary={'registeredAssemblies':len(registrations['names']),'registeredAssemblyNames':registrations['names'],
        'unityScriptDescriptors':len(symbols),'uniqueUnityScriptTypes':len(unique),
        'scriptAssemblyCount':len({x['assembly'] for x in symbols}),'configTableTypeNames':len(cfg),
        'runtimeInitializers':initializers,'dexDuplicatedClassNames':len(duplicates),
        'scope':'Unity MonoScript identity metadata and standard DEX class declarations; game managed method bodies and native mapping not recovered.'}
    write(PROJECT/'evidence/code-summary.json',code_summary)
    write(local/'code-symbols.json',symbols);write(local/'dex-class-list.json',dex_classes)
    write(local/'dex-duplicates.json',duplicates);write(local/'config-table-types.json',cfg)
    write(local/'system-symbols.json',core)
    content={'uiPrefabDirectoryCounts':dict(ui.most_common()),'collisionMapIDs':maps,
        'configTableTypeNames':len(cfg),'renderDrawKeyAddressCount':sum('/HybridRenderSystem/DrawKeys/' in x['path'] for x in assets),
        'configBinaryAddresses':[x['address'] for x in assets if '/address1/config_byte_' in x['path']],
        'hotfixPaths':{branch:{'addresses':len(rows:=[x for x in assets if f'/Hotfixes/{branch}/' in x['path']]),
            'aotmetaCandidates':sum('.aotmeta.' in x['path'] for x in rows),
            'qxpsCandidates':sum('.qxps.' in x['path'] for x in rows)} for branch in ['debug','release']},
        'scope':'Address/type names reveal content organization, not table rows, playable counts, numerical rules or runtime activation.'}
    write(PROJECT/'evidence/content-summary.json',content)
    explorer={'identity':identity,'assets':assets,'bundles':bundles,'scripts':symbols,'dex':dex_classes,
        'summary':read('evidence/manifest-summary.json'),'coverage':read('evidence/readable-asset-summary.json'),
        'code':code_summary,'tables':cfg}
    payload=json.dumps(explorer,ensure_ascii=True,separators=(',',':')).replace('<','\\u003c')
    html=(PROJECT/'scripts/explorer.html').read_text('utf-8').replace('__DATA__',payload)
    (local/'explorer.html').write_text(html,encoding='utf-8')
    print(json.dumps({'identity':identity['label'],'registeredAssemblies':code_summary['registeredAssemblies'],
        'uniqueScriptTypes':len(unique),'configTableTypeNames':len(cfg),'dexDuplicateNames':len(duplicates),
        'explorerBytes':(local/'explorer.html').stat().st_size},ensure_ascii=False))
if __name__=='__main__':main()
