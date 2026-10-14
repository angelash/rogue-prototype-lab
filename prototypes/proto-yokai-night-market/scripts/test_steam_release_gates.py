"""Regression tests for real failure modes: stale proof, changed bytes, wrong AppID."""
import copy,hashlib,json,tempfile,unittest
from pathlib import Path
from audit_steam_release import evaluate,private_proof,files_match,fingerprint,sha,valid_app_id,windows_layout
from prepare_steam_preview import render

class ReleaseGates(unittest.TestCase):
    def test_consistent_complete_fixture_can_pass(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);build=root/'candidate';build.mkdir();p=build/'game.exe';p.write_bytes(b'synthetic fixture, never a game')
            for name in ['UnityPlayer.dll','game_Data/globalgamemanagers','game_Data/Managed/Assembly-CSharp.dll','MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll']:
                extra=build/name;extra.parent.mkdir(parents=True,exist_ok=True);extra.write_bytes(b'synthetic fixture')
            entries=[dict(path=p.relative_to(build).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in build.rglob('*') if p.is_file()];source=hashlib.sha256(b'synthetic source').hexdigest()
            q=root/'qa.json';q.write_text(json.dumps(dict(passed=True,sourceSHA256=source,buildManifestSHA256=fingerprint(entries))))
            proof=dict(path='qa.json',sha256=sha(q));quality={k:proof for k in ('complete-native','audio-listening','performance-minimum','clean-install','steam-installed-build')}
            product=dict(candidateBuild=str(build),executable='game.exe',candidateSourceSHA256=source,appId=987654321,windowsDepotId=987654322,commercialRoute='independent',publisherDisplayName='Fixture',supportEmail='fixture@example.invalid')
            result=evaluate(product,{'groups':[dict(id='code',status='cleared')]},{'rights':{'code':proof},'candidate':dict(sourceSHA256=source,files=entries),'quality':quality},root)
            self.assertTrue(result['readyForBuildReview'])
            (build/'UnityPlayer.dll').unlink();self.assertFalse(windows_layout(build,'game.exe'))
    def test_original_and_demo_appids_rejected(self):
        for value in (None,0,480,340550,True,'123456'):
            self.assertFalse(valid_app_id(value))
        self.assertTrue(valid_app_id(987654321))
    def test_evidence_must_exist_and_match_hash(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);p=root/'proof.json';p.write_text('{}')
            proof=dict(path='proof.json',sha256=sha(p));self.assertTrue(private_proof(proof,root))
            p.write_text('{"changed":true}');self.assertFalse(private_proof(proof,root))
            self.assertFalse(private_proof(dict(path='../outside.json',sha256='x'),root))
    def test_build_change_extra_or_missing_file_invalidates_manifest(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);p=root/'game.bin';p.write_bytes(b'approved')
            entries=[dict(path='game.bin',bytes=p.stat().st_size,sha256=sha(p))]
            self.assertTrue(files_match(root,entries))
            extra=root/'extra';extra.write_text('unreviewed');self.assertFalse(files_match(root,entries));extra.unlink()
            p.write_bytes(b'changed');self.assertFalse(files_match(root,entries))
    def test_quality_report_from_another_source_cannot_pass(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);build=root/'candidate';build.mkdir();p=build/'game.exe';p.write_bytes(b'fixture')
            entries=[dict(path='game.exe',bytes=p.stat().st_size,sha256=sha(p))]
            q=root/'qa.json';q.write_text(json.dumps(dict(passed=True,sourceSHA256='old',buildManifestSHA256=fingerprint(entries))))
            proof=dict(path='qa.json',sha256=sha(q))
            product=dict(candidateBuild=str(build),candidateSourceSHA256='new',appId=987654321,windowsDepotId=987654322,commercialRoute='independent',publisherDisplayName='Fixture',supportEmail='test@example.invalid')
            result=evaluate(product,{'groups':[dict(id='code',status='unresolved')]},{'candidate':dict(sourceSHA256='new',files=entries),'quality':{'complete-native':proof}},root)
            self.assertIn('complete-native',result['failedGates']);self.assertIn('rights-and-dependency-clearance',result['failedGates'])
    def test_templates_cannot_activate_a_branch(self):
        from audit_steam_release import ROOT
        text=(ROOT/'release/steam/app_build.vdf.template').read_text('utf-8')
        self.assertIn('"Preview" "1"',text);self.assertNotIn('SetLive',text)
    def test_vdf_values_cannot_inject_new_fields(self):
        with self.assertRaises(ValueError):render('"AppID" "@APP_ID@"',{'APP_ID':'5"\n"SetLive" "default'})
        with self.assertRaises(ValueError):render('@UNKNOWN@',{})
if __name__=='__main__':unittest.main()
