"""Check repository Markdown and registered source bytes without dependencies."""
from pathlib import Path
import argparse
import hashlib
import json
import re
from urllib.parse import unquote

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
parser.add_argument('--check-external', action='store_true', help='Also require local files outside this repository, e.g. reference projects.')
args = parser.parse_args()
root = args.root.resolve()
if not root.is_dir():
    parser.error('Repository root does not exist')
skip = {'.git', '.local', 'node_modules', '.venv', 'Library', 'Temp', 'Build', 'Builds'}
docs = [p for p in root.rglob('*.md') if not any(part in skip for part in p.relative_to(root).parts)]
errors, skipped_external = [], []
checked_links = 0
for path in docs:
    try:
        content = path.read_bytes().decode('utf-8-sig')
    except UnicodeError as exc:
        errors.append({'file': str(path.relative_to(root)), 'error': str(exc)})
        continue
    if not content.strip():
        errors.append({'file': str(path.relative_to(root)), 'error': 'empty document'})
    for match in re.finditer(r'!?\[[^\]]*\]\((<[^>]+>|[^)\n]+)\)', content):
        target = match.group(1).strip().strip('<>')
        if target.startswith('#') or re.match(r'^[A-Za-z][A-Za-z0-9+.-]*://', target):
            continue
        if re.match(r'^[A-Za-z][A-Za-z0-9+.-]*:', target) and not re.match(r'^[A-Za-z]:[/\\]', target):
            continue
        target = re.sub(r':\d+$', '', unquote(target.split('#', 1)[0]))
        windows_absolute = bool(re.match(r'^[A-Za-z]:[/\\]', target))
        destination = Path(target) if windows_absolute else (path.parent / target)
        destination = destination.resolve()
        outside = windows_absolute and not str(destination).casefold().startswith(str(root).casefold() + '\\')
        if outside and not args.check_external:
            skipped_external.append(target)
            continue
        checked_links += 1
        if not destination.exists():
            errors.append({'file': str(path.relative_to(root)), 'target': target, 'error': 'missing local reference'})

source_results = []
register = root / 'sources/chatgpt/checksums.sha256'
try:
    for line in register.read_text(encoding='utf-8-sig').splitlines():
        if not line.strip():
            continue
        expected, name = line.split('  ', 1)
        if not re.fullmatch(r'[0-9a-fA-F]{64}', expected) or not name.strip():
            raise ValueError('Invalid SHA-256 registration entry')
        source = register.parent / name
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        source_results.append({'file': str(source.relative_to(root)), 'bytes': source.stat().st_size, 'sha256': digest})
        if digest.casefold() != expected.casefold():
            errors.append({'file': str(source.relative_to(root)), 'error': 'source checksum mismatch'})
except (OSError, UnicodeError, ValueError) as exc:
    errors.append({'file': 'sources/chatgpt/checksums.sha256', 'error': str(exc)})

if not source_results:
    errors.append({'file': 'sources/chatgpt/checksums.sha256', 'error': 'no registered sources checked'})

if not docs:
    errors.append({'error': 'no Markdown documents found'})
print(json.dumps({'markdownFiles': len(docs), 'checkedLocalLinks': checked_links, 'externalLocalReferencesSkipped': len(skipped_external), 'externalCheckEnabled': args.check_external, 'sources': source_results, 'errors': errors}, ensure_ascii=False, indent=2))
raise SystemExit(bool(errors))
