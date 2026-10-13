"""Read the plain YooAsset 1.4.6 manifest and local bundle availability."""
import collections
import json
import struct
from pathlib import Path
from intake import PROJECT, write

class Reader:
    def __init__(self, data): self.data=data; self.pos=0
    def number(self, fmt):
        n=struct.calcsize('<'+fmt); assert self.pos+n<=len(self.data)
        v=struct.unpack_from('<'+fmt,self.data,self.pos)[0];self.pos+=n;return v
    def text(self):
        n=self.number('H');assert self.pos+n<=len(self.data)
        v=self.data[self.pos:self.pos+n].decode('utf-8',errors='strict');self.pos+=n;return v
    def strings(self): return [self.text() for _ in range(self.number('H'))]
    def ints(self): return [self.number('i') for _ in range(self.number('H'))]

def main():
    apk=PROJECT/'local/apk';source=apk/'assets/PackageManifest_DefaultPackage_73317.bytes'
    r=Reader(source.read_bytes());assert r.number('I')==0x594f4f
    header={'fileVersion':r.text(),'enableAddressable':r.number('B'),'outputNameStyle':r.number('i'),
        'packageName':r.text(),'packageVersion':r.text()}
    assets=[]
    for i in range(r.number('i')):
        assets.append({'id':i,'address':r.text(),'path':r.text(),'tags':r.strings(),
            'bundleID':r.number('i'),'dependencies':r.ints()})
    bundles=[]
    for i in range(r.number('i')):
        b={'id':i,'name':r.text(),'hash':r.text(),'crc':r.text(),'bytes':r.number('q'),
            'raw':r.number('B'),'loadMethod':r.number('B'),'tags':r.strings(),'references':r.ints()}
        stem,ext=b['name'].rsplit('.',1)
        b['filename']=f"{stem}_{b['hash']}.{ext}"
        f=apk/'assets'/b['filename'];b['inAPK']=f.is_file()
        b['sizeMatches']=f.stat().st_size==b['bytes'] if f.is_file() else None
        b['header']=f.open('rb').read(8).hex() if f.is_file() else None
        b['assetIDs']=[];bundles.append(b)
    assert r.pos==len(r.data),f'Trailing manifest bytes: {len(r.data)-r.pos}'
    for a in assets:
        assert 0<=a['bundleID']<len(bundles)
        assert all(0<=x<len(bundles) for x in a['dependencies'])
        bundles[a['bundleID']]['assetIDs'].append(a['id'])
        a['inAPK']=bundles[a['bundleID']]['inAPK']
        a['dependenciesInAPK']=all(bundles[x]['inAPK'] for x in a['dependencies'])
        a['extension']=Path(a['path']).suffix.lower()
    # MXET extension is independently aligned against address sequence.
    ext=Reader((apk/'assets/PackageExtManifest_DefaultPackage_73317.bytes').read_bytes())
    assert ext.number('I')==0x5445584d
    ext_version=ext.number('H');n=ext.number('i');assert n==len(assets)
    ext_fields=[]
    for a in assets:
        assert ext.text()==a['address'];ext_fields.append(ext.number('i'))
    ext_trailing=len(ext.data)-ext.pos
    write(PROJECT/'local/manifest-extension.json',{'version':ext_version,'assetValues':ext_fields,
        'unparsedTrailingBytes':ext_trailing,'trailingHex':ext.data[ext.pos:].hex()})
    counts=collections.Counter(a['extension'] for a in assets)
    groups=collections.Counter('/'.join(a['path'].split('/')[2:4]) for a in assets)
    local_b=[b for b in bundles if b['inAPK']];missing=[b for b in bundles if not b['inAPK']]
    summary={'header':header,'assets':len(assets),'bundles':len(bundles),'manifestConsumed':r.pos,
        'manifestBytes':len(r.data),'extAddressAlignment':n,'extTrailingBytes':ext_trailing,
        'assetsInAPK':sum(a['inAPK'] for a in assets),'assetsWithAllDependenciesInAPK':sum(a['inAPK'] and a['dependenciesInAPK'] for a in assets),
        'bundlesInAPK':len(local_b),'bundlesAbsentFromAPK':len(missing),
        'localBundleBytes':sum(b['bytes'] for b in local_b),'missingBundleBytes':sum(b['bytes'] for b in missing),
        'localSizeMismatches':[b['id'] for b in local_b if not b['sizeMatches']],
        'rawLocal':sum(b['raw'] for b in local_b),'rawAbsent':sum(b['raw'] for b in missing),
        'localStandardUnityFS':sum(b['header']=='556e697479465300' for b in local_b),
        'extensions':dict(counts.most_common()),'pathGroups':dict(groups.most_common()),
        'loadMethods':dict(collections.Counter(b['loadMethod'] for b in bundles)),
        'uniqueAddresses':len(set(a['address'] for a in assets)), 'uniquePaths':len(set(a['path'] for a in assets))}
    write(PROJECT/'evidence/manifest-summary.json',summary)
    write(PROJECT/'local/manifest-assets.json',assets)
    write(PROJECT/'local/manifest-bundles.json',bundles)
    print(json.dumps(summary,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
