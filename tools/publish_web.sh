#!/bin/bash
# Publishes Builds/WebGL (made by Tools > T-Minus > Build WebGL) to the gh-pages branch:
# https://ayaan-kashif.github.io/NASA-PROJECT/  (live about a minute after the push)
set -e
P="$(cd "$(dirname "$0")/.." && pwd)"
[ -f "$P/Builds/WebGL/index.html" ] || { echo "Run Tools > T-Minus > Build WebGL first."; exit 1; }
W="$(mktemp -d)/gh-pages"
git -C "$P" fetch -q origin gh-pages
git -C "$P" worktree add -q "$W" gh-pages
find "$W" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -r "$P/Builds/WebGL/." "$W/"; touch "$W/.nojekyll"
git -C "$W" add -A
git -C "$W" commit -qm "Update playable WebGL build" || echo "No changes to publish."
git -C "$W" push -q origin gh-pages
git -C "$P" worktree remove --force "$W"
echo "Published: https://ayaan-kashif.github.io/NASA-PROJECT/"
