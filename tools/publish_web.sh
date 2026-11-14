#!/bin/bash
# Publishes Builds/WebGL (made by Tools > T-Minus > Build WebGL) to the gh-pages branch:
# https://ayaan-kashif.github.io/NASA-PROJECT/  (live about a minute after the push)
# Also adds: an about page (about.html), link-preview tags in index.html, and a notice for phones and tablets.
set -e
P="$(cd "$(dirname "$0")/.." && pwd)"
[ -f "$P/Builds/WebGL/index.html" ] || { echo "Run Tools > T-Minus > Build WebGL first."; exit 1; }
W="$(mktemp -d)/gh-pages"
git -C "$P" fetch -q origin gh-pages
git -C "$P" worktree add -q "$W" gh-pages
find "$W" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -r "$P/Builds/WebGL/." "$W/"; touch "$W/.nojekyll"
cp "$P/tools/web/about.html" "$W/about.html"

# Link previews and a description for search engines (the Unity template has none).
sed -i 's|</head>|<meta name="description" content="T-MINUS: build a CubeSat and its rocket part by part, launch it with real physics and learn from a debrief that explains every failure. Free, for students 12 and up.">\n<meta property="og:title" content="T-MINUS: Rocket \&amp; Mission Builder">\n<meta property="og:description" content="Build a satellite and its rocket bolt by bolt, launch it, and find out exactly what made it succeed or fail.">\n<meta property="og:type" content="website">\n</head>|' "$W/index.html"

# Phones and tablets: touch is experimental, so say so.
sed -i 's|</body>|<script>if (matchMedia("(pointer:coarse)").matches \&\& !matchMedia("(pointer:fine)").matches) { var d = document.createElement("div"); d.style.cssText = "position:fixed;top:0;left:0;right:0;z-index:99;background:#ff7a1a;color:#fff;padding:10px 14px;font:16px system-ui;text-align:center"; d.innerHTML = "T-MINUS is made for a keyboard and mouse. Touch is experimental: one finger clicks and drags, two fingers orbit, pinch zooms. <a style=\\"color:#fff\\" href=\\"about.html\\">About the game</a>"; document.body.appendChild(d); }</script>\n</body>|' "$W/index.html"

git -C "$W" add -A
git -C "$W" commit -qm "Update playable WebGL build" || echo "No changes to publish."
git -C "$W" push -q origin gh-pages
git -C "$P" worktree remove --force "$W"
echo "Published: https://ayaan-kashif.github.io/NASA-PROJECT/  (about page: about.html)"
