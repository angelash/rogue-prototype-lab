"""Read-only verification of candidate bytes, registered resources and optional Git source bytes."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
import uuid
from create_first_batch_config import PROJECTS

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
selection = parser.add_mutually_exclusive_group(required=True)
selection.add_argument('--project', choices=PROJECTS)
selection.add_argument('--all', action='store_true')
parser.add_argument('--git-ref', help='Compare projectSources to this Git revision or : for the staged index')
args = parser.parse_args()
numbers = list(PROJECTS) if args.all else [args.project]
errors = []
guids = {}

def sha(data):
    return hashlib.sha256(data).hexdigest()

def path_inside(base, value):
    path = (base / value).resolve()
    if not path.is_relative_to(base.resolve()):
        raise ValueError('Path escapes its declared base: ' + value)
    return path

for number in numbers:
    folder, product, _ = PROJECTS[number]
    project = ROOT / 'prototypes' / folder / 'game' / product
    evidence = ROOT / 'docs/development' / ('proto-' + number) / 'evidence'
    manifest = json.loads((evidence / 'build-manifest.json').read_text(encoding='utf-8-sig'))
    run = ET.parse(evidence / 'test-results.xml').getroot()
    if run.get('result') != 'Passed' or run.get('failed') != '0' or int(run.get('passed', '0')) < 1:
        errors.append(number + ': failed or empty test report')
    for key in ('total', 'passed', 'failed', 'result', 'start-time', 'end-time'):
        if run.get(key) != manifest['tests'][key]:
            errors.append(number + ': test metadata mismatch ' + key)
    if manifest['summary']['result'] != 'Succeeded' or manifest['summary']['errors'] != 0:
        errors.append(number + ': unsuccessful build report')
    build = project / 'Builds' / 'Windows64'
    for group, base in (('projectSources', project), ('files', build)):
        for entry in manifest[group]:
            path = path_inside(base, entry['path'])
            if not path.is_file():
                errors.append(number + ': missing ' + str(path.relative_to(ROOT)))
                continue
            data = path.read_bytes()
            if len(data) != entry['bytes'] or sha(data) != entry['sha256']:
                errors.append(number + ': byte mismatch ' + str(path.relative_to(ROOT)))
    if args.git_ref:
        paths = [(project / e['path']).relative_to(ROOT).as_posix() for e in manifest['projectSources']]
        refs = [((':' + p) if args.git_ref == ':' else args.git_ref + ':' + p) for p in paths]
        reply = subprocess.run(['git', 'cat-file', '--batch'], cwd=ROOT,
                               input=('\n'.join(refs) + '\n').encode('utf-8'), stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE, check=True).stdout
        stream = io.BytesIO(reply)
        for entry, git_path in zip(manifest['projectSources'], paths):
            header = stream.readline().decode('utf-8').strip().split(' ')
            if len(header) != 3 or header[1] != 'blob':
                errors.append(number + ': missing Git blob ' + git_path)
                continue
            data = stream.read(int(header[2])); stream.read(1)
            if sha(data) != entry['sha256']:
                errors.append(number + ': Git byte mismatch ' + git_path)
    register = json.loads((ROOT / 'sources/art' / folder / 'runtime-resource-register.json').read_text(encoding='utf-8-sig'))
    for entry in register['copies']:
        source = path_inside(ROOT, entry['source']).read_bytes()
        runtime = path_inside(ROOT, entry['runtime']).read_bytes()
        if source != runtime or sha(runtime) != entry['sha256'] or len(runtime) != entry['bytes']:
            errors.append(number + ': registered media byte mismatch ' + entry['runtime'])
    art = json.loads((ROOT / 'sources/art' / folder / 'asset-register.json').read_text(encoding='utf-8-sig'))
    if sha(path_inside(ROOT, art['generator']['path']).read_bytes()) != art['generator']['sha256']:
        errors.append(number + ': art generator hash mismatch')
    if 'cameraEvidence' in manifest:
        if sha((evidence / manifest['cameraEvidence']['path']).read_bytes()) != manifest['cameraEvidence']['sha256']:
            errors.append(number + ': camera evidence hash mismatch')
    settings = (project / 'ProjectSettings/ProjectSettings.asset').read_text(encoding='utf-8')
    guid = re.search(r'^  productGUID: (\w+)$', settings, re.MULTILINE).group(1)
    expected_guid = uuid.uuid5(uuid.NAMESPACE_URL, 'https://github.com/angelash/rogue-prototype-lab/tree/main/prototypes/' + folder).hex
    if guid != expected_guid:
        errors.append(number + ': productGUID differs from the independent project identity')
    if guid in guids:
        errors.append(number + ': duplicate productGUID with ' + guids[guid])
    guids[guid] = number
    print(number, 'tests', run.get('passed'), 'sources', len(manifest['projectSources']),
          'build files', len(manifest['files']), 'guid', guid)

if errors:
    print('\n'.join(errors))
    raise SystemExit(1)
print('Verified candidate bytes/resources/source identity. Native input, hearing and player hypotheses require separate actual evidence.')
