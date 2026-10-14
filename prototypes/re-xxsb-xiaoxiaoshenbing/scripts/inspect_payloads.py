"""Validate every TextAsset payload and classify binary map/bank layouts."""
import collections
import hashlib
import json
import struct
from common import PROJECT, LOCAL, write


def main():
    inputs=json.loads((LOCAL/'assets/text-assets.json').read_text('utf8'));records=[];errors=[]
    for item in inputs:
        data=(LOCAL/item['payload']).read_bytes();assert hashlib.sha256(data).hexdigest()==item['sha256']
        row={k:item[k] for k in ['name','payload','bytes','sha256','source_id']}
        try:
            if data.startswith(b'MZ'):
                row.update(kind='managed-PE',status='indexed separately by extract_hotfixes')
            elif data.startswith(b'RIFF'):
                assert struct.unpack_from('<I',data,4)[0]+8==len(data)
                pos=12;chunks=[]
                while pos<len(data):
                    tag=data[pos:pos+4].decode('ascii');size=struct.unpack_from('<I',data,pos+4)[0]
                    assert pos+8+size<=len(data);chunks.append({'tag':tag,'offset':pos+8,'bytes':size});pos+=8+size+(size&1)
                assert pos==len(data)
                row.update(kind='FMOD-RIFF',chunks=chunks,embedded_FSB5_offsets=[i for i in range(len(data)) if data.startswith(b'FSB5',i)],status='structure-validated')
            else:
                try:
                    value=json.loads(data.decode('utf-8-sig'))
                except (UnicodeDecodeError,json.JSONDecodeError):
                    value=None
                if value is not None:
                    row.update(kind='JSON',root_type=type(value).__name__,items=len(value) if isinstance(value,(dict,list)) else None,status='parsed')
                    if item['name']=='a1_battle_consistency_manifest':
                        assert value['entryCount']==len(value['entries'])
                        assert len({(r['category'],r['path']) for r in value['entries']})==len(value['entries'])
                        row.update(consistency_entries=value['entryCount'],categories=dict(collections.Counter(r['category'] for r in value['entries'])),
                            scope='Client-declared content hashes; original authoring files are not all present in APK, so not independently treated as source verification.')
                    elif '_astar' in item['name'] and isinstance(value,list):
                        row['header']=value[:6];row['grid_values']=len(value)-6
                elif item['source_id'] in ['b0266','b0267']:
                    index,startx,starty,endx,endy=struct.unpack_from('<5h',data)
                    assert endx>startx and endy>starty and len(data)==10+2*(endx-startx)*(endy-starty)
                    row.update(kind='SDF-grid-block',status='byte-layout-validated',block_index=index,bounds=[startx,starty,endx,endy],
                        signed16_samples=(len(data)-10)//2,scope='Header and 16-bit grid layout; scale/physical semantics depend on native reader, not assumed.')
                elif item['source_id']=='b0264':
                    bounds=list(struct.unpack_from('<4h',data));row.update(kind='astar-grid-block',status='preserved-layout',bounds=bounds,
                        int16_samples=(len(data)-8)//2);assert (len(data)-8)%2==0
                elif item['source_id']=='b0263':
                    values=list(struct.unpack_from('<6h',data));row.update(kind='astar-packed-or-index',status='header-inventoried',header=values,payload_bytes=len(data)-12,
                        scope='Original packed/index bytes retained; not interpreted as independent pathfinding rules.')
                else:
                    row.update(kind='other-binary',status='preserved',header=data[:32].hex())
            records.append(row)
        except Exception as error:
            row.update(kind=row.get('kind','unclassified'),status='failed',error=type(error).__name__+': '+str(error));records.append(row);errors.append(row)
    write(LOCAL/'content/payload-index.json',records)
    summary={'text_assets':len(inputs),'classified':len(records),'kinds':dict(collections.Counter(x['kind'] for x in records)),
        'failures':errors,'all_original_payload_hashes_match':True,
        'SDF_blocks':sum(x['kind']=='SDF-grid-block' for x in records),'scope':'JSON parses and byte-layout/bank validation; native grid semantics and package-independent authoring consistency remain distinct.'}
    write(PROJECT/'evidence/payload-summary.json',summary);print(json.dumps(summary,ensure_ascii=True))


if __name__=='__main__':main()
