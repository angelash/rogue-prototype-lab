"""Read-only static inventory for the BPH mobile/WeChat technical report.

Reads Assets, Packages and ProjectSettings; does not launch Unity or modify inputs.
GUID reachability is an approximation of serialized references, not BuildReport.
Detailed asset/source inventories default to a local ignored output directory.
"""
from __future__ import annotations
import argparse
from collections import Counter, defaultdict
import datetime
import hashlib
import json
from pathlib import Path
import re
import struct

PATTERNS = {
    'mouse_direct': r'Mouse\.current|Input\.mousePosition|Input\.GetMouseButton',
    'keyboard_direct': r'Keyboard\.current|Input\.GetKey',
    'touch_direct': r'Touchscreen\.current|Input\.touch|InputType\.Touch',
    'pointer_callbacks': r'void OnPointer(?:Enter|Exit|Down|Up|Click)\(',
    'right_middle': r'rightButton|middleButton|InputButton\.Right|InputButton\.Middle',
    'pointer_consumer': r'GetPointerPosition\(',
    'resource_load': r'Resources\.Load(?:All|Async)?',
    'filesystem': r'File\.(?:Read|Write|Move|Copy|Delete|Exists)|Directory\.(?:Create|Exists|Get)|persistentDataPath',
    'playerprefs': r'PlayerPrefs\.',
    'native_interop': r'DllImport\(|SteamAPI\.|SteamInput\.|Microsoft\.Win32',
    'reflection': r'GetField\(|GetMethod\(|GetProperty\(|MakeGenericType|Activator\.CreateInstance',
    'lifecycle': r'OnApplication(?:Pause|Focus|Quit)\(',
    'managed_thread_or_blocking_candidates': r'Task\.Run\(|Thread\.(?:Sleep|Yield|CurrentThread)|ThreadPool\.|new Thread\(|\.GetAwaiter\(\)\.GetResult\(\)',
    'gpu_readback': r'ReadPixels\(|GetPixels32\(|AsyncGPUReadback|Graphics\.Blit',
    'allocation_hotspot': r'FindObjectsByType|FindFirstObjectByType|OverlapPointAll|new HashSet<',
}
GUID = re.compile(r'guid: ([a-f0-9]{32})')
SERIAL = {'.unity', '.prefab', '.asset', '.mat', '.controller', '.overridecontroller', '.anim', '.playable', '.rendertexture'}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def value(text: str, field: str):
    m = re.search(r'^\s*' + re.escape(field) + r':\s*(.*?)\s*$', text, re.M)
    return m[1] if m else None


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    project = args.project.resolve()
    output = args.output.resolve()
    assert output != project and not output.is_relative_to(project), 'Output must be outside the audited project'
    output.mkdir(parents=True, exist_ok=True)
    assets = project / 'Assets'
    files = sorted(p for p in assets.rglob('*') if p.is_file())
    ext = defaultdict(lambda: {'count': 0, 'bytes': 0})
    folders = defaultdict(lambda: {'count': 0, 'bytes': 0})
    asset_rows, textures, guid_map, guid_duplicates = [], [], {}, []
    code_hits = defaultdict(list)
    hashes = {}
    source_rows, text_cache = [], {}
    for path in files:
        rel = path.relative_to(project).as_posix()
        size = path.stat().st_size
        ext[path.suffix.lower()]['count'] += 1
        ext[path.suffix.lower()]['bytes'] += size
        folder = path.relative_to(assets).parts[0]
        folders[folder]['count'] += 1
        folders[folder]['bytes'] += size
        if path.suffix != '.meta':
            asset_rows.append({'path': rel, 'bytes': size})
        if path.suffix == '.meta':
            text = path.read_text(encoding='utf-8-sig', errors='replace')
            m = re.search(r'^guid: ([a-f0-9]{32})', text, re.M)
            if m:
                source = rel[:-5]
                if m[1] in guid_map:
                    guid_duplicates.append({'guid': m[1], 'paths': [guid_map[m[1]], source]})
                guid_map[m[1]] = source
        if path.suffix == '.cs':
            data = path.read_bytes()
            hashes[rel] = sha(data)
            text = data.decode('utf-8-sig', errors='replace')
            lines = text.splitlines()
            source_rows.append({'path': rel, 'bytes': len(data), 'lines': len(lines), 'sha256': hashes[rel]})
            for kind, pattern in PATTERNS.items():
                matches = [i+1 for i, line in enumerate(lines) if re.search(pattern, line)]
                if matches:
                    code_hits[kind].append({'path': rel, 'lines': matches})
        if path.suffix in SERIAL:
            data = path.read_bytes()
            if data.startswith(b'%YAML'):
                text_cache[rel] = data.decode('utf-8-sig', errors='replace')
        if path.suffix.lower() == '.png':
            data = path.read_bytes()
            if data[:8] == b'\x89PNG\r\n\x1a\n':
                width, height = struct.unpack('>II', data[16:24])
                meta_path = Path(str(path) + '.meta')
                meta = meta_path.read_text(encoding='utf-8-sig') if meta_path.exists() else ''
                textures.append({'path': rel, 'bytes': len(data), 'width': width, 'height': height,
                                 'rgba32BaseBytesEstimate': width*height*4,
                                 'readable': value(meta, 'isReadable'), 'mipmaps': value(meta, 'enableMipMap'),
                                 'textureCompression': value(meta, 'textureCompression'),
                                 'spriteMode': value(meta, 'spriteMode'), 'filterMode': value(meta, 'filterMode')})
    edges = {path: sorted(set(GUID.findall(text))) for path, text in text_cache.items()}

    def closure(roots):
        seen, missing, pending = set(), set(), list(roots)
        while pending:
            path = pending.pop()
            if path in seen:
                continue
            seen.add(path)
            for g in edges.get(path, []):
                if g in guid_map:
                    pending.append(guid_map[g])
                elif g not in {'0000000000000000e000000000000000', '0000000000000000f000000000000000'}:
                    missing.add(g)
        size_map = {r['path']: r['bytes'] for r in asset_rows}
        result = {'assetPaths': sorted(seen), 'fileCount': sum(x in size_map for x in seen),
                  'sourceFileBytes': sum(size_map.get(x, 0) for x in seen),
                  'unresolvedGuidCount': len(missing), 'unresolvedGuids': sorted(missing)}
        return result

    scenes = {}
    for rel in text_cache:
        if rel.endswith('.unity'):
            scenes[rel] = closure([rel])
    resources = [r for r in asset_rows if '/Resources/' in r['path']]
    stream = [r for r in asset_rows if '/StreamingAssets/' in r['path']]
    shader_files = [r['path'] for r in asset_rows if r['path'].endswith('.shader')]
    materials = [r for r in asset_rows if r['path'].endswith('.mat')]
    fonts = [r for r in asset_rows if Path(r['path']).suffix in {'.ttf', '.otf', '.woff', '.woff2'}]
    tmp_fonts = []
    for rel, text in text_cache.items():
        if 'm_AtlasTextures:' in text and 'm_CharacterTable:' in text:
            tmp_fonts.append({'path': rel, 'bytes': (project/rel).stat().st_size,
                              'atlasWidth': value(text, 'm_AtlasWidth'), 'atlasHeight': value(text, 'm_AtlasHeight'),
                              'populationMode': value(text, 'm_AtlasPopulationMode'),
                              'characterEntries': len(re.findall(r'^\s+m_Unicode:', text, re.M))})
    scripts = {}
    names = ['SafeAreaFitter','SkillTreeUI','InputHandler','CursorDamagePerSecond','SlashDetector','GuideStep','CrtScreenFilter','CastSpellManager','InputSystemUIInputModule']
    for name in names:
        meta_files = [f for f in files if f.name == name + '.cs.meta']
        if meta_files:
            text = meta_files[0].read_text(encoding='utf-8-sig')
            match = re.search(r'^guid: ([a-f0-9]{32})', text, re.M)
            if match:
                g = match[1]
                scripts[name] = {'guid':g,'serializedReferences':[{'path':p,'lines':[i+1 for i,l in enumerate(t.splitlines()) if g in l]} for p,t in text_cache.items() if g in t]}
    ui_summaries = []
    for rel,text in text_cache.items():
        if not rel.endswith(('.unity','.prefab')):
            continue
        canvas = []
        for m in re.finditer(r'm_UiScaleMode:\s*(\d+)',text):
            block=text[max(0,m.start()-180):m.start()+480]
            canvas.append({'line':text[:m.start()].count('\n')+1,'mode':m[1],
                           'referenceResolution':value(block,'m_ReferenceResolution'),
                           'matchWidthOrHeight':value(block,'m_MatchWidthOrHeight')})
        if canvas:
            ui_summaries.append({'path':rel,'canvasScalers':canvas})
    settings_files=sorted(p for folder in ['ProjectSettings','Packages'] for p in (project/folder).glob('*') if p.is_file() and p.suffix in {'.asset','.json','.txt'})
    for p in settings_files:hashes[p.relative_to(project).as_posix()]=sha(p.read_bytes())
    settings=(project/'ProjectSettings/ProjectSettings.asset').read_text(encoding='utf-8-sig')
    keys=['defaultScreenWidth','defaultScreenHeight','defaultScreenWidthWeb','defaultScreenHeightWeb','activeInputHandler','m_ActiveColorSpace','webGLThreadsSupport','webGLInitialMemorySize','webGLMaximumMemorySize','webGLCompressionFormat','webGLDecompressionFallback','stripEngineCode','allowUnsafeCode']
    extensions={k:v for k,v in sorted(ext.items(),key=lambda x:x[1]['bytes'],reverse=True)}
    summary={'schemaVersion':1,'observedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
             'project':'prototypes/re-booster-pack-heroes/local/UnityProject','method':'read_only_static_scan',
             'assetFilesIncludingMeta':len(files),'assetFilesExcludingMeta':len(asset_rows),
             'assetSourceBytesExcludingMeta':sum(r['bytes'] for r in asset_rows),
             'extensions':extensions,'topFolders':dict(sorted(folders.items(),key=lambda x:x[1]['bytes'],reverse=True)),
             'sourceFiles':len(source_rows),'mainGameSourceFiles':sum('/Assembly-CSharp/' in r['path'] for r in source_rows),
             'codePatternStats':{k:{'fileCount':len(v),'lineMatches':sum(len(r['lines']) for r in v),'mainGameFileCount':sum('/Assembly-CSharp/' in r['path'] for r in v)} for k,v in code_hits.items()},
             'textures':{'pngCount':len(textures),'sourceBytes':sum(t['bytes'] for t in textures),'rgba32BaseBytesEstimate':sum(t['rgba32BaseBytesEstimate'] for t in textures),
                         'readableCount':sum(t['readable']=='1' for t in textures),'mipmappedCount':sum(t['mipmaps']=='1' for t in textures),
                         'dimensions':dict(Counter(f"{t['width']}x{t['height']}" for t in textures)),
                         'largestByPixels':sorted(textures,key=lambda t:t['rgba32BaseBytesEstimate'],reverse=True)[:12]},
             'audio':{'fileCount':sum(v['count'] for k,v in ext.items() if k in {'.ogg','.wav','.mp3','.aiff','.aif'}),'sourceBytes':sum(v['bytes'] for k,v in ext.items() if k in {'.ogg','.wav','.mp3','.aiff','.aif'})},
             'fonts':fonts,'tmpFontAssets':tmp_fonts,'shaderCount':len(shader_files),'materialCount':len(materials),
             'resources':{'count':len(resources),'sourceBytes':sum(r['bytes'] for r in resources),'largest':sorted(resources,key=lambda r:r['bytes'],reverse=True)[:8]},
             'streamingAssets':{'count':len(stream),'sourceBytes':sum(r['bytes'] for r in stream),'paths':[r['path'] for r in stream]},
             'sceneSerializedGuidClosure':{p:{k:v for k,v in c.items() if k not in ['assetPaths','unresolvedGuids']} for p,c in scenes.items()},
             'scriptReferences':scripts,'canvasScalers':ui_summaries,'playerSettings':{k:value(settings,k) for k in keys},
             'guidDuplicates':guid_duplicates,'largestSourceAssets':sorted(asset_rows,key=lambda r:r['bytes'],reverse=True)[:15],
             'sdkIndicators':{'wechatNamedFiles':[r['path'] for r in asset_rows if any(s in r['path'].lower() for s in ['wx-wasm','wechat','weixin','wx-sdk','minigameconfig','wxwebgl'])],
                              'addressableNamedFiles':[r['path'] for r in asset_rows if 'addressable' in r['path'].lower()]},
             'boundaries':['Asset-source sizes are not player/package/download sizes.','RGBA32 estimate excludes mips and is not runtime memory; assets may be unloaded, compressed or duplicated.','GUID closure follows text serialized references, not Unity BuildReport; runtime loads, code inclusion, built-ins and package assets not fully covered.','Code regex matches are candidate locations, not confirmed reachable defects.','No Unity build, game input, ad SDK or phone tests performed.']}
    (output/'inventory.json').write_bytes((json.dumps(summary,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
    (output/'private-details.json').write_bytes((json.dumps({'assets':asset_rows,'textures':textures,'codePatternHits':dict(code_hits),'sources':source_rows,'serializedGuidClosures':scenes},ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
    (output/'input-hashes.json').write_bytes((json.dumps(hashes,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
    print(json.dumps({k:summary[k] for k in ['assetFilesExcludingMeta','assetSourceBytesExcludingMeta','sourceFiles','mainGameSourceFiles','textures','audio','resources','streamingAssets','sceneSerializedGuidClosure','playerSettings','sdkIndicators']},ensure_ascii=False))


if __name__ == '__main__':
    main()
