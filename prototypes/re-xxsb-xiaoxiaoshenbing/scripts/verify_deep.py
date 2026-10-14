"""Independent byte/hash and cross-layer coverage checks for the static recovery."""
import collections
import gzip
import json
from pathlib import Path
import re
from common import PROJECT,LOCAL,APK,write,sha256


def read(name):return json.loads((PROJECT/'evidence'/name).read_text('utf8'))


def main():
    checks={};issues=[]
    original=next((LOCAL/'input').glob('*.apk'));prep=read('preparation.json');extraction=read('extraction.json')
    checks['input_unchanged']=sha256(original)==prep['apk_sha256']
    checks['user_source_unchanged']=sha256(Path(prep['source']))==prep['apk_sha256']
    checks['all_APK_bytes_accounted']=extraction['files']==1488 and extraction['bytes']==370964058 and extraction['all_sha256_and_sizes_match']
    manifest=read('manifest-summary.json');link=read('content-linking.json')
    checks['manifest_full_consumption']=manifest['manifestConsumed']==manifest['manifestBytes'] and manifest['extTrailingBytes']==0
    checks['all_bundles_local']=manifest['bundles']==manifest['bundlesInAPK']==484 and manifest['bundlesAbsentFromAPK']==0 and not manifest['localSizeMismatches']
    checks['manifest_bundle_MD5_CRC_size']=link['manifest_bundle_MD5_size_CRC_all_pass']
    checks['logical_asset_object_coverage']=link['manifest_assets']==link['manifest_assets_linked']==1438 and not link['link_failures']
    managed=read('managed-recovery.json');decompiled=read('managed-decompilation.json')
    checks['managed_all_payloads']=managed['payloads']==managed['standard_PE']==managed['successful_metadata']==21
    checks['managed_split']=managed['hotfix_DLLs']==10 and managed['AOT_supplements']==11
    checks['managed_IL']=managed['IL_method_bodies']==53427 and managed['IL_decode_failures']==0
    checks['all_decompiler_exits']=decompiled['assemblies']==21 and decompiled['failed_exit_codes']==0 and decompiled['source_files']==7319
    for record in json.loads((LOCAL/'code/hotfix-index.json').read_text('utf8')):
        if sha256(LOCAL/record['input'])!=record['sha256'] or sha256(LOCAL/record['canonical'])!=record['sha256']:
            issues.append(record['assembly'])
    checks['managed_original_and_canonical_hashes']=not issues
    native=read('native-disassembly.json')
    checks['native_all_ARM64_libraries']=native['libraries']==20 and native['executable_sections']==43
    checks['native_byte_coverage']=all(s['bytes']==s['disassembled_bytes'] for r in native['files'] for s in r['sections'])
    checks['native_all_method_addresses']=native['IL2CPP_method_names']==native['unique_IL2CPP_addresses']==native['labeled_IL2CPP_method_names']==133186
    android=read('android-recovery.json');dex=read('structure-summary.json')
    checks['DEX_independent_coverage']=android['DEX_disassembled']==dex['dexFiles']==35 and android['all_sdk_counts_match'] and dex['dexChecksumsAllMatch']
    checks['DEX_method_units']=android['method_bodies']==dex['dexMethodBodies']==113830 and android['instruction_units']==dex['dexInstructionUnits']==3157749
    checks['fallback_nonempty_exits']=all(r['fallback_exit']==0 or r['fallback_status']=='empty-DEX' for r in android['files'])
    checks['embedded_nonstandard_DEX_kept']=len(dex['nonstandardDEX'])==1 and (APK/dex['nonstandardDEX'][0]['file']).stat().st_size==71500
    embedded=read('embedded-dex-recovery.json')
    checks['embedded_DEX_recovered']=embedded['sha1_match'] and embedded['adler32_match'] and embedded['SDK_counts_match'] and embedded['normal_exit']==embedded['fallback_exit']==0
    checks['all_36_DEX_methods']=android['method_bodies']+embedded['method_bodies']==114259 and android['instruction_units']+embedded['instruction_units']==3169066
    abi=read('native-abi-comparison.json')
    checks['both_native_ABI_maps']=abi['same_named_method_multiset'] and all(r['all_addresses_in_executable_sections'] and r['methods']==133186 for r in abi['ABI'].values())
    objects=json.loads((LOCAL/'assets/objects.json').read_text('utf8'));containers=json.loads((LOCAL/'assets/containers.json').read_text('utf8'))
    repaired=read('asset-repair.json');edges=json.loads((LOCAL/'assets/edges.json').read_text('utf8'))
    checks['object_count_closure']=len(objects)==sum(c['objects'] for c in containers)==59312 and len({r['id'] for r in objects})==len(objects)
    checks['type_tree_limits_accounted']=sum(r['tree_status']=='ok' for r in objects)==repaired['tree_success']==59311 and repaired['remaining_tree_failures']==1
    checks['object_reference_coverage']=len(edges)==235902 and all(e['resolved_in_inventory'] for e in edges)
    unparsed=[r for r in objects if r['tree_status']!='ok']
    checks['unparsed_original_object_preserved']=len(unparsed)==1 and (LOCAL/unparsed[0]['raw']).stat().st_size==8488 and sha256(LOCAL/unparsed[0]['raw'])==unparsed[0]['raw_sha256']
    content=read('content-summary.json')
    checks['all_configuration_entries']=content['merged_entries']==content['decoded_tables']==150 and not content['failures']
    checks['configuration_records']=content['row_records']==10495 and content['KV_entries']==130 and content['all_primary_keys_unique']
    checks['empty_tables_not_faked']=len(content['empty_header_placeholders'])==2 and all(s['row_count']==0 for s in content['empty_header_placeholders'])
    payload=read('payload-summary.json')
    checks['all_TextAsset_payloads']=payload['text_assets']==payload['classified']==92 and not payload['failures'] and payload['all_original_payload_hashes_match']
    media=read('media-export.json');exports=json.loads((LOCAL/'media/index.json').read_text('utf8'))
    media_types={'Texture2D','Sprite','Mesh','AudioClip','AnimationClip','Avatar','Shader','ComputeShader','Font'}
    checks['media_object_coverage']=media['attempted_objects']==len(exports)==sum(r['type'] in media_types for r in objects)==3388
    checks['media_success_and_empty']=media['failed']==0 and sum(media['passed'].values())+media['empty_runtime_textures']==3388
    checks['media_files_present']=all((LOCAL/f['path']).is_file() for r in exports for f in r['files'])
    checks['media_export_hashes']=all((LOCAL/f['path']).stat().st_size==f['bytes'] and sha256(LOCAL/f['path'])==f['sha256'] for r in exports for f in r['files'])
    backup=read('deep-backup.json')
    checks['complete_private_backup']=backup['remotes']==[] and backup['git_fsck_pass'] and backup['input_matches'] and backup['working_tree_clean'] and backup['input_blob_sha256']==prep['apk_sha256']
    missing=[]
    for path in list(PROJECT.glob('*.md'))+list((PROJECT/'reports').glob('*.md'))+list((PROJECT/'scripts').glob('*.md')):
        for link_value in re.findall(r'\]\(([^)]+)\)',path.read_text('utf8')):
            if '://' in link_value or link_value.startswith('#'):continue
            if not (path.parent/link_value.split('#')[0]).resolve().exists():missing.append({'file':path.name,'link':link_value})
    checks['document_links']=not missing
    result={'date':'2026-10-14','checks':checks,'passed':sum(checks.values()),'total':len(checks),'issues':issues,'missing_document_links':missing,
        'scope':'Static bytes, all 36 independent DEX counts, both native address maps/ARM64 executable coverage, configuration/object/media closure and artifact links. One builtin layout and Java/native/rebuild/runtime boundaries remain explicit.'}
    write(PROJECT/'evidence/deep-verification.json',result);print(json.dumps(result,ensure_ascii=True))
    if not all(checks.values()):raise RuntimeError('Deep recovery verification failed')


if __name__=='__main__':main()
