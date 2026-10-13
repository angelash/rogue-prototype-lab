"""Reject remote pushes whose reachable history contains local recovery inputs.

Used by the local Git pre-push hook. Never rewrites history or contacts a remote.
"""
from pathlib import Path
import argparse
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
PRIVATE = ["prototypes/proto-yokai-night-market/local/", "prototypes/re-crunch-time/local/",
           "prototypes/re-escape-from-duckov/local/", "prototypes/re-t1-codename-rebirth/local/"]


def contains_private_history(ref):
    return bool(subprocess.check_output(
        ["git", "rev-list", "-1", ref, "--", *PRIVATE], cwd=ROOT, text=True).strip())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check-ref")
    parser.add_argument("remote", nargs="?")
    parser.add_argument("url", nargs="?")
    args = parser.parse_args()
    refs = [args.check_ref] if args.check_ref else [
        line.split()[1] for line in sys.stdin if line.strip() and set(line.split()[1]) != {"0"}]
    blocked = [ref for ref in refs if contains_private_history(ref)]
    if blocked:
        print("Push stopped: reachable history contains recovered game code or reference media. "
              "The full source branch is local-only; use its Git bundle backup. "
              "Public main may receive original tools/data only.", file=sys.stderr)
        return 2
    print("Push history check passed: no local recovery inputs.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
