"""Build local searchable indexes and publish only aggregate coverage evidence."""
import collections, csv, gzip, hashlib, json, re, sqlite3, time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
LOCAL=ROOT/'local'

def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2),'utf8')

def main():
    started=time.monotonic(); db=sqlite3.connect(LOCAL/'deep-catalog.sqlite3')
    db.executescript('''DROP TABLE IF EXISTS types; DROP TABLE IF EXISTS methods; DROP TABLE IF EXISTS calls; DROP TABLE IF EXISTS assets; DROP TABLE IF EXISTS config_rows;
    CREATE TABLE types(profile TEXT, grp TEXT, assembly TEXT, token TEXT, name TEXT, base TEXT, source TEXT);
    CREATE TABLE methods(profile TEXT, grp TEXT, assembly TEXT, token TEXT, owner TEXT, name TEXT, rva INTEGER, ilbytes INTEGER, instructions INTEGER);
    CREATE TABLE calls(profile TEXT, grp TEXT, assembly TEXT, caller TEXT, offset INTEGER, opcode TEXT, target TEXT);
    CREATE TABLE assets(bundle INTEGER, file TEXT, pathid INTEGER, type TEXT, name TEXT, address TEXT, script TEXT, details TEXT);
    CREATE TABLE config_rows(tab TEXT, rowidx INTEGER, id TEXT, data TEXT);''')
    metadata=[]; ns=collections.Counter(); decomp=[]
    for f in sorted((LOCAL/'managed-metadata').rglob('*.json')):
        rel=f.relative_to(LOCAL/'managed-metadata');parts=rel.parts
        if len(parts)!=3:continue # Earlier six-file indexes kept as historical evidence.
        profile,grp=parts[:2];j=json.loads(f.read_text('utf8'));asm=j['assembly']
        assert not j['failures']
        body=0;calls=0;ilbytes=0;instructions=0;no_body=0
        for typ in j['types']:
            top=typ['name'].split('+')[0].split('`')[0]
            source=LOCAL/'source/all'/profile/grp/asm/(top.replace('.','/')+'.cs')
            db.execute('INSERT INTO types VALUES (?,?,?,?,?,?,?)',(profile,grp,asm,typ['token'],typ['name'],typ['baseType'],source.relative_to(LOCAL).as_posix() if source.exists() else ''))
            if profile=='release' and grp=='hotfix':
                parts=top.split('.');ns['.'.join(parts[:3] if parts[:2]==['GamePlay','ECS'] else parts[:2])]+=1
            for m in typ['methods']:
                body+=bool(m['ilLength']);no_body+=not bool(m['ilLength']);ilbytes+=m['ilLength'];instructions+=m['instructionCount'];calls+=len(m['calls'])
                db.execute('INSERT INTO methods VALUES (?,?,?,?,?,?,?,?,?)',(profile,grp,asm,m['token'],typ['name'],m['name'],m['rva'],m['ilLength'],m['instructionCount']))
                db.executemany('INSERT INTO calls VALUES (?,?,?,?,?,?,?)',[(profile,grp,asm,m['token'],c['offset'],c['opcode'],c['target']) for c in m['calls']])
        metadata.append(dict(profile=profile,group=grp,assembly=asm,types=j['typeCount'],methods=j['methodCount'],fields=j['fieldCount'],methodsWithIL=body,methodsWithoutIL=no_body,ILBytes=ilbytes,instructions=instructions,callSites=calls,failures=len(j['failures']),sha256=hashlib.sha256((LOCAL/'managed'/profile/grp/(asm+'.dll')).read_bytes()).hexdigest()))
        db.commit()
    for profile in ('debug','release'):
        for grp in ('hotfix','aotmeta'):
            for d in sorted((LOCAL/'source/all'/profile/grp).iterdir()):
                if not d.is_dir():continue
                files=list(d.rglob('*.cs'));issues=[];externs=0
                for f in files:
                    s=f.read_text('utf-8-sig');externs+=len(re.findall(r'\bextern\b',s))
                    for line,txt in enumerate(s.splitlines(),1):
                        if any(x in txt for x in ('Unknown result type','Error decoding','could not be decompiled','Invalid MethodBodyBlock')):issues.append({'file':f.relative_to(LOCAL).as_posix(),'line':line,'diagnostic':txt.strip()})
                decomp.append(dict(profile=profile,group=grp,assembly=d.name,files=len(files),externDeclarations=externs,diagnostics=issues))
    script=json.loads((LOCAL/'il2cpp-arm64/script.json').read_text('utf8'))
    with (LOCAL/'native-methods.csv').open('w',encoding='utf8',newline='') as f:
        writer=csv.writer(f);writer.writerow(('address','name','signature','typeSignature'))
        for m in script['ScriptMethod']:writer.writerow((hex(m['Address']),m['Name'],m['Signature'],m['TypeSignature']))
    native={'metadataVersion':31,'dummyAssemblies':len(list((LOCAL/'il2cpp-arm64/DummyDll').glob('*.dll'))),'typeDeclarations':len(re.findall(r'// TypeDefIndex: \d+', (LOCAL/'il2cpp-arm64/dump.cs').read_text('utf8'))),'methodEntries':len(script['ScriptMethod']),'uniqueMethodAddresses':len({m['Address'] for m in script['ScriptMethod']}),'strings':len(script['ScriptString']),'methodBodies':'AOT machine code in original ELF; dummy DLLs are declarations, not recovered native C# method bodies'}
    del script
    objects=json.loads((LOCAL/'assets/objects.json').read_text('utf8'));containers=json.loads((LOCAL/'assets/containers.json').read_text('utf8'))
    externals={x['file']:x['externals'] for x in containers}
    script_map={(x['file'],x['pathID']):(x.get('m_AssemblyName',''),x.get('m_Namespace',''),x.get('m_ClassName','')) for x in objects if x['type']=='MonoScript'}
    for x in json.loads((LOCAL/'mono-scripts.json').read_text('utf8')):script_map.setdefault((x['file'],x['pathID']),(x.get('m_AssemblyName',''),x.get('m_Namespace',''),x.get('m_ClassName','')))
    mb=collections.Counter();resolved=0;unresolved=[];enriched=[];textures=[];meshes=[];audio=[]
    for f in sorted((LOCAL/'assets/trees').glob('*.jsonl.gz')):
        for line in gzip.open(f,'rt',encoding='utf8'):
            j=json.loads(line);x=j['identity'];tree=j['tree'];kind=x['type'];details={};class_name=''
            if kind=='MonoBehaviour':
                p=x['script'];file=x['file'];fid=p['m_FileID']
                if fid>0:file=externals[file][fid-1].replace('\\','/').split('/')[-1]
                typ=script_map.get((file,p['m_PathID']))
                if typ:
                    resolved+=1;class_name=typ[0]+':'+('.'.join(v for v in typ[1:] if v));mb[class_name]+=1
                else:unresolved.append({'bundleID':x['bundleID'],'file':x['file'],'pathID':x['pathID'],'script':p,'externalFile':file})
            elif kind=='Texture2D':
                details={k:tree.get(k) for k in ('m_Width','m_Height','m_TextureFormat','m_MipCount','m_StreamData','m_ColorSpace')};textures.append({**x,**details})
            elif kind=='Mesh':
                details={'vertexCount':tree.get('m_VertexData',{}).get('m_VertexCount'),'subMeshes':len(tree.get('m_SubMeshes',[])),'blendShapes':len(tree.get('m_Shapes',{}).get('shapes',[])),'compressed':tree.get('m_CompressedMesh',{}).get('m_Vertices',{}).get('m_NumItems')};meshes.append({**x,**details})
            elif kind=='AudioClip':
                details={k:tree.get(k) for k in ('m_Channels','m_Frequency','m_Length','m_CompressionFormat','m_Resource','m_LoadType')};audio.append({**x,**details})
            db.execute('INSERT INTO assets VALUES (?,?,?,?,?,?,?,?)',(x['bundleID'],x['file'],x['pathID'],kind,x['name'],' | '.join(x['addresses']),class_name,json.dumps(details,separators=(',',':'))))
            enriched.append({**x,'scriptType':class_name,'properties':details})
    db.commit()
    write(LOCAL/'assets/media-properties.json',{'textures':textures,'meshes':meshes,'audio':audio})
    (LOCAL/'assets/objects-enriched.json').write_text(json.dumps(enriched,ensure_ascii=False,separators=(',',':')),'utf8')
    write(LOCAL/'assets/monobehaviour-types.json',{'resolved':resolved,'types':dict(mb.most_common()),'unresolved':unresolved})
    configs=[];schema_errors=[]
    for f in sorted((LOCAL/'config/tables').glob('*.json')):
        j=json.loads(f.read_text('utf8'));name=j['name']
        if j['kind']=='Row':
            source=LOCAL/'source/all/release/hotfix/ConfigDefine/ConfigDefine'/('data_'+name+'.cs')
            assert source.exists(),str(source)
            declared=set(re.findall(r'^\s*public [^;{}()]+?\s+(\w+);\s*$',source.read_text('utf-8-sig'),re.M))
            cols={x['name'] for x in j['columns']}
            if declared!=cols:schema_errors.append({'table':name,'onlyCode':sorted(declared-cols),'onlyData':sorted(cols-declared)})
        for i,row in enumerate(j['rows']):db.execute('INSERT INTO config_rows VALUES (?,?,?,?)',(name,i,str(row.get('id','')) if isinstance(row,dict) else str(row[0]),json.dumps(row,ensure_ascii=False,separators=(',',':'))))
        configs.append({'name':name,'kind':j['kind'],'rows':j['rowCount'],'columns':j['columns']})
    db.executescript('CREATE INDEX tname ON types(name); CREATE INDEX mname ON methods(owner,name); CREATE INDEX caller ON calls(caller); CREATE INDEX target ON calls(target); CREATE INDEX atype ON assets(type); CREATE INDEX aname ON assets(name); CREATE INDEX ctable ON config_rows(tab,id);')
    db.commit();integrity=db.execute('PRAGMA integrity_check').fetchone()[0];db.close();assert integrity=='ok'
    write(LOCAL/'config/catalog.json',configs)
    report={'managedImages':len(metadata),'managed':metadata,'native':native,'decompiledProjects':len(decomp),'decompiledFiles':sum(x['files'] for x in decomp),'decompilerDiagnostics':sum(len(x['diagnostics']) for x in decomp),'decompilation':decomp,'hotfixNamespaceTypes':dict(ns.most_common()),'assetObjectsIndexed':len(enriched),'monoBehaviourScriptsResolved':resolved,'monoBehaviourScriptsUnresolved':len(unresolved),'monoBehaviourScriptTypes':len(mb),'configSchemaCompared':sum(x['kind']=='Row' for x in configs),'configSchemaDifferences':schema_errors,'catalogIntegrity':integrity,'seconds':round(time.monotonic()-started,2)}
    write(ROOT/'evidence/deep-coverage.json',report)
    print(json.dumps({k:v for k,v in report.items() if k not in ('managed','native','decompilation','hotfixNamespaceTypes','configSchemaDifferences')},ensure_ascii=False),flush=True)

if __name__=='__main__':main()
