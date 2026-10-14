"""Validate the planning inputs and confirm canonical Unity source is untouched.

This checks a plan, never claims that its runtime acceptance cases have passed.
"""
from pathlib import Path
import json
from audit_commercial_migration import ROOT, PLAN, PROJECT, inventory, read, save

def main():
    actual=inventory();summary=read(PLAN/'audit-summary.json');assets=read(PLAN/'asset-plan.json')
    symbols=read(PLAN/'symbol-plan.json');data=read(PLAN/'data-plan.json')
    plan=read(PLAN/'execution-plan.json');tests=read(PLAN/'test-plan.json')
    checks=[]
    def check(name,condition): checks.append(dict(name=name,passed=bool(condition)))
    check('canonical_967_files_and_frozen_bytes',actual['sourceFiles']==967 and actual['sourceSHA256']==plan['baseline']['sourceSHA256']==summary['baselineSourceSHA256'])
    check('inventory_counts_current',summary['counts']==actual['counts'])
    check('all_82_texture_inputs_and_dimensions',assets['textures']==actual['textures'] and len(assets['textures'])==82 and all(t['dimensions']==t['actualDimensions'] for t in assets['textures']))
    check('all_91_original_audio_slots_and_specs',assets['audio']==actual['audio'] and len(assets['audio'])==91 and all(a['originalByteMatch'] and a['serializedUses'] and a['spec']['frames']>0 for a in assets['audio']))
    check('fonts_and_other_assets_exhaustive',assets['fonts']==actual['fonts'] and assets['otherAssets']==actual['otherAssets'] and len(assets['textures'])+len(assets['audio'])+len(assets['fonts'])+len(assets['otherAssets'])==actual['counts']['assetFiles'])
    check('unique_asset_proposals',len({a['proposedName'] for a in assets['textures']+assets['audio']})==173)
    check('type_member_property_parameter_coverage',symbols['types']==actual['types'] and symbols['members']==actual['symbols'] and symbols['properties']==actual['properties'] and symbols['parameters']==actual['parameters'])
    check('no_known_proposed_signature_collisions',symbols['collisions']==[] and actual['renameCollisions']==[])
    check('draft_names_not_mislabeled_approved',all(s['manuallyApproved'] is False for s in symbols['members']+symbols['properties']+symbols['parameters']))
    check('data_mapping_matches_effective_source',data==actual['dataMap'] and len(data['cards'])==95 and len(data['commissions'])==28 and len(data['texts'])==420)
    check('data_ids_unique',all(len({r['newId' if key in ('cards','commissions') else 'new'] for r in data[key]})==len(data[key]) for key in ('cards','commissions','texts','districts')))
    check('ward_default_and_language_slots_preserved',all(c['requirements']['Wards']==2 for c in data['commissions']) and data['localization']['oldSlots']=={'ENG':'zh-Hans','SPA':'en'})
    positions={p['id']:i for i,p in enumerate(plan['phases'])}
    check('eight_phases_with_valid_dependencies_rollback_and_exits',len(positions)==8 and all(p['exit'] and p['rollback'] and all(dep in positions and positions[dep]<positions[p['id']] for dep in p['dependsOn']) for p in plan['phases']))
    check('analysis_only_status_consistent',plan['status']=='analysis_complete_execution_not_started' and not plan['target']['newProjectCreated'] and all(p['status']=='not_started' for p in plan['phases']) and tests['status']=='definitions_only_not_executed')
    check('acceptance_definitions_cover_all_risk_groups',len(tests['cases'])==31 and len({c['id'] for c in tests['cases']})==31 and {c['group'] for c in tests['cases']}=={'source','binding','data','rules','flow','visual','native','save','audio','release'})
    check('four_render_combinations_explicit',len(tests['matrix']['languages'])*len(tests['matrix']['aspects'])==4 and tests['matrix']['renderScenarioTarget']==748)
    product=read(ROOT/'release/product.json');rights=read(ROOT/'release/rights-register.json')
    check('authorization_recorded_without_false_clearance',product['commercialRoute']=='licensed' and product['originalGameAssetReuseAllowed'] is False and rights['authorization']['state']=='user_declared_authorized' and rights['authorization']['documentReviewed'] is False and not any(g['status']=='cleared' for g in rights['groups']))
    out=dict(passed=all(c['passed'] for c in checks),scope='Plan consistency, coverage and untouched canonical source only; no game migration/runtime QA executed',checks=checks,sourceSHA256=actual['sourceSHA256'])
    save(ROOT/'local/commercial-migration-v3/plan-verification.json',out)
    print(json.dumps(out,ensure_ascii=False));return 0 if out['passed'] else 2
if __name__=='__main__':raise SystemExit(main())
