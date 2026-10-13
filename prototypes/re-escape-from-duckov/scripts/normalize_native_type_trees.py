"""Complete native size/version/alignment metadata absent in generated schemas.

The .NET type-tree generator uses zero for compound byte sizes and versions.
UnityPy can parse these nodes; Unity Player needs native metadata. This pass
changes headers only and verifies every persisted object body remains identical.
"""
import argparse,json,hashlib
from pathlib import Path
import UnityPy
from native_bundle import write_fs,digest

PRIMITIVES={'bool':1,'char':1,'UInt8':1,'SInt8':1,'short':2,'SInt16':2,'UInt16':2,'unsigned short':2,
    'int':4,'SInt32':4,'UInt32':4,'unsigned int':4,'float':4,'long long':8,'SInt64':8,'UInt64':8,'unsigned long long':8,'double':8,'TypelessData':-1,'string':-1}
def normalize(node):
    for c in node.m_Children:normalize(c)
    if node.m_Version==0:node.m_Version=1
    if node.m_ByteSize==0:
        if node.m_Type in PRIMITIVES:node.m_ByteSize=PRIMITIVES[node.m_Type]
        elif node.m_Type=='Array':node.m_ByteSize=-1
        elif node.m_Children:
            if any(c.m_ByteSize<0 for c in node.m_Children):node.m_ByteSize=-1
            else:
                size=0
                for c in node.m_Children:
                    size+=c.m_ByteSize
                    if c.m_MetaFlag&16384:size=(size+3)&~3
                node.m_ByteSize=size
    if any(c.m_MetaFlag&(16384|32768) for c in node.m_Children):node.m_MetaFlag|=32768

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);a=ap.parse_args()
    source=a.source.resolve();target=a.output.resolve();target.mkdir(exist_ok=True)
    asset=UnityPy.load(str(source/'duckov-native.assets')).assets[0];before={pid:hashlib.sha256(o.get_raw_data()).digest() for pid,o in asset.objects.items()};count=0
    for t in asset.types:
        if t.node is not None and any(n.m_Version==0 for n in t.node.traverse()):normalize(t.node);count+=1
    raw=target/'duckov-native.assets';raw.write_bytes(asset.save());reopened=UnityPy.load(str(raw)).assets[0]
    assert before=={pid:hashlib.sha256(o.get_raw_data()).digest() for pid,o in reopened.objects.items()}
    bundle=target/'duckov-native-assets.bundle';write_fs(bundle,[('duckov-native-assets',raw,4)],engine='2022.3.62f3c1')
    (target/'schema-normalization.json').write_text(json.dumps({'metadataOnly':True,'normalizedTypes':count,'unchangedObjectBodies':len(before),'bundle':{'bytes':bundle.stat().st_size,'sha256':digest(bundle)}},indent=2),encoding='utf-8')
    print('Normalized native type trees:',count,flush=True)

if __name__=='__main__':main()
