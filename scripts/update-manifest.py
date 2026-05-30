#!/usr/bin/env python3
"""Prepends a new version entry to manifest.json using the current RELEASE_NOTES.md."""

import argparse
import datetime
import json
import pathlib
import subprocess


def detect_repo() -> str:
    remote = subprocess.check_output(
        ["git", "remote", "get-url", "origin"], text=True
    ).strip()
    # Handle both https://github.com/owner/repo and git@github.com:owner/repo
    repo = (
        remote.replace("https://github.com/", "")
        .replace("git@github.com:", "")
        .removesuffix(".git")
    )
    return repo


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Update manifest.json with a new plugin version."
    )
    parser.add_argument("--version", required=True, help="e.g. 0.8.0.0")
    parser.add_argument("--checksum", required=True, help="MD5 hex of the zip")
    parser.add_argument("--tag", required=True, help="e.g. v0.8.0")
    args = parser.parse_args()

    repo = detect_repo()
    notes = pathlib.Path("RELEASE_NOTES.md").read_text(encoding="utf-8").strip()

    manifest_path = pathlib.Path("manifest.json")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    new_entry = {
        "version": args.version,
        "changelog": notes,
        "targetAbi": "10.11.9.0",
        "sourceUrl": (
            f"https://github.com/{repo}/releases/download/{args.tag}"
            f"/jellyfin-plugin-localrecs-{args.tag}.zip"
        ),
        "checksum": args.checksum,
        "timestamp": datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ"),
    }

    manifest[0]["versions"].insert(0, new_entry)
    manifest_path.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )

    print(f"manifest.json updated: {args.version} ({args.tag})")


if __name__ == "__main__":
    main()
