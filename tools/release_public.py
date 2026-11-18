"""Publish a release on the public repo (thomasjteachey/ivalice-remixed-public), whose release starts
.github/workflows/publish-nexus.yml (the Nexus upload, mods 187 and 186).

    python -I tools/release_public.py 0.9.14 notes.md [--dry-run]

The public repo's history is one snapshot commit a release, not this repo's: the commit gets HEAD's tree (so the
code/eiu gitlink and the scripts' exec bits come along unchanged) on top of the public main, and only that commit and
its tag are pushed, by explicit refspecs. Nothing of this repo's history or tags goes there. The tree is checked first
for the game's files (the disc, FFT_enhanced.exe, the ReMixed .ppf), which must never be public. The release is made
as a draft, given mod/release/IvaliceReMixed-VERSION.zip, then published, so the workflow finds its file.
The GitHub token comes from `git credential fill` and goes only into request headers.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REPO = "thomasjteachey/ivalice-remixed-public"
URL = f"https://github.com/{REPO}.git"
# The game's files and anything like them: never in a public tree.
FORBIDDEN = re.compile(r"(\.(exe|ppf|iso|bin|cue|img|ccd|sub|mdf|mds|chd|pbp|ecm|7z|rar|pac)$)", re.I)
LARGEST = 20_000_000


def git(*args: str, text: bool = True) -> str:
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True, text=text).stdout.strip()


def token() -> str:
    out = subprocess.run(["git", "credential", "fill"], input="protocol=https\nhost=github.com\n\n", cwd=ROOT,
                         check=True, capture_output=True, text=True).stdout
    for line in out.splitlines():
        if line.startswith("password="):
            return line[len("password="):]
    sys.exit("no GitHub credential")


def api(method: str, url: str, key: str, body: dict | None = None, data: bytes | None = None,
        kind: str = "application/json") -> dict:
    payload = json.dumps(body).encode() if body is not None else data
    request = urllib.request.Request(url, data=payload, method=method, headers={
        "Authorization": f"Bearer {key}", "Accept": "application/vnd.github+json", "Content-Type": kind,
        "User-Agent": "ivalice-remixed-release"})
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read() or b"{}")


def check_tree(tree: str) -> None:
    bad, big = [], []
    for line in git("ls-tree", "-r", "-l", tree).splitlines():
        meta, path = line.split("\t", 1)
        size = meta.split()[3]
        if FORBIDDEN.search(path):
            bad.append(path)
        if size.isdigit() and int(size) > LARGEST:
            big.append(f"{path} ({int(size):,} bytes)")
    if bad or big:
        sys.exit("refusing to publish: " + ", ".join(bad + big))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("version")
    parser.add_argument("notes", type=Path, help="the release's notes (Markdown)")
    parser.add_argument("--dry-run", action="store_true", help="make the commit and check it, push nothing")
    args = parser.parse_args()
    tag = f"v{args.version}"
    asset = ROOT / "mod" / "release" / f"IvaliceReMixed-{args.version}.zip"
    if not asset.is_file():
        sys.exit(f"no {asset.relative_to(ROOT)}: build the release first (docs/release/RELEASING.md)")
    if git("status", "--porcelain", "--untracked-files=no"):
        sys.exit("commit the release first: the working tree has changes")
    notes = args.notes.read_text(encoding="utf-8").strip()

    tree = git("rev-parse", "HEAD^{tree}")
    check_tree(tree)
    if git("ls-remote", "--tags", URL, f"refs/tags/{tag}"):
        sys.exit(f"the public repo already has {tag}")
    # Only the public main, no tags: its tags must not follow into this repo, nor this repo's out to it.
    git("fetch", "--no-tags", "--quiet", URL, "refs/heads/main")
    parent = git("rev-parse", "FETCH_HEAD")
    if git("rev-parse", f"{parent}^{{tree}}") == tree:
        sys.exit("the public main already has this tree")
    commit = git("commit-tree", tree, "-p", parent, "-m", f"Ivalice ReMixed {args.version}")
    if git("rev-list", "--count", f"{parent}..{commit}") != "1" or git("rev-parse", f"{commit}^{{tree}}") != tree:
        sys.exit("the snapshot commit isn't one commit with HEAD's tree")
    print(f"snapshot {commit[:10]} on public main {parent[:10]}, tree {tree[:10]}")
    if args.dry_run:
        print(f"dry run: would push it as main and {tag}, then release {asset.name}")
        return

    subprocess.run(["git", "push", "--quiet", URL, f"{commit}:refs/heads/main", f"{commit}:refs/tags/{tag}"],
                   cwd=ROOT, check=True)
    key = token()
    release = api("POST", f"https://api.github.com/repos/{REPO}/releases", key,
                  {"tag_name": tag, "name": f"Ivalice ReMixed {args.version}", "body": notes, "draft": True})
    upload = release["upload_url"].split("{")[0] + f"?name={asset.name}"
    api("POST", upload, key, data=asset.read_bytes(), kind="application/zip")
    published = api("PATCH", f"https://api.github.com/repos/{REPO}/releases/{release['id']}", key, {"draft": False})
    print(f"released {published['html_url']} (the Nexus workflow starts from it)")


if __name__ == "__main__":
    main()
