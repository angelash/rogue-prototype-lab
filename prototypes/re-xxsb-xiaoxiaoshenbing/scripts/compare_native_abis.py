"""Compare declaration method names and verify address ranges for both ABI pairs."""
import collections
import json
from common import PROJECT,LOCAL,write


def main():
    maps={name:json.loads((LOCAL/f'il2cpp/{name}/script.json').read_text('utf8'))['ScriptMethod'] for name in ['arm64','armv7']}
    results={}
    for short,abi in [('arm64','arm64-v8a'),('armv7','armeabi-v7a')]:
        methods=maps[short];metadata=json.loads((LOCAL/f'elf/{abi}/libil2cpp.so.json').read_text('utf8'))
        sections=[s for s in metadata['sections'] if s['flags']&4 and s['bytes']]
        contained=lambda address:any(s['address']<=address<s['address']+s['bytes'] for s in sections)
        results[short]={'methods':len(methods),'unique_addresses':len({m['Address'] for m in methods}),
            'all_addresses_in_executable_sections':all(contained(m['Address']&~1 if short=='armv7' else m['Address']) for m in methods),
            'thumb_pointer_flags':sum(m['Address']&1 for m in methods) if short=='armv7' else 0}
    a=collections.Counter(m['Name'] for m in maps['arm64']);b=collections.Counter(m['Name'] for m in maps['armv7'])
    record={'date':'2026-10-14','ABI':results,'same_named_method_multiset':a==b,'common_unique_names':len(a.keys()&b.keys()),
        'ARM64_only_names':len(a.keys()-b.keys()),'ARMv7_only_names':len(b.keys()-a.keys()),
        'scope':'Both native binaries pair with the registered metadata; declared method names/address ranges checked. This is not a runtime equivalence or complete ARMv7 control-flow/high-level recovery claim.'}
    write(PROJECT/'evidence/native-abi-comparison.json',record);print(json.dumps(record,ensure_ascii=True))
    assert all(v['all_addresses_in_executable_sections'] for v in results.values())


if __name__=='__main__':main()
