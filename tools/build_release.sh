#!/bin/bash
# Release build WITHOUT the Unity AI packages (audit A-11).
#
# The development project keeps com.unity.ai.assistant and com.unity.ai.inference because they are the Unity MCP tooling, and they drag an editor-only
# tool into the player (the web build then needs an extra Emscripten flag and a stub). This script builds from a temporary copy of the project whose
# Packages/manifest.json has those two packages removed, so your own project, scenes and settings are never touched.
#
#   UNITY_PATH="/c/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Unity.exe" bash tools/build_release.sh webgl
#   UNITY_PATH=... bash tools/build_release.sh windows
#
# Output: Builds/release/WebGL (or Builds/release/Windows). To publish the web one: WEB_BUILD_DIR=Builds/release/WebGL bash tools/publish_web.sh
# Needs Unity 6000.6.8f1 installed. The first build of the copy imports every asset again, which takes a while. Not run in the session that wrote it.
set -e
TARGET="${1:-webgl}"
case "$TARGET" in
  webgl) METHOD="TMinus.EditorTools.WebGLBuilder.Build"; OUT="WebGL" ;;
  windows) METHOD="TMinus.EditorTools.WindowsBuilder.Build"; OUT="Windows" ;;
  *) echo "Usage: build_release.sh webgl|windows"; exit 1 ;;
esac
[ -n "$UNITY_PATH" ] && [ -f "$UNITY_PATH" ] || { echo "Set UNITY_PATH to the Unity 6000.6.8f1 editor (Unity.exe)."; exit 1; }

P="$(cd "$(dirname "$0")/.." && pwd)"
W="$(mktemp -d)/T-MINUS-release"
mkdir -p "$W"
echo "Copying the project to $W (without caches, builds and git history)..."
tar -C "$P" --exclude=./Library --exclude=./Temp --exclude=./Logs --exclude=./Builds --exclude=./.git --exclude=./obj -cf - . | tar -C "$W" -xf -

echo "Removing the AI packages from the copy's manifest..."
sed -i '/"com\.unity\.ai\./d' "$W/Packages/manifest.json"
rm -f "$W/Packages/packages-lock.json"
rm -rf "$W/ProjectSettings/Packages/com.unity.ai.assistant"
if grep -q '"com\.unity\.ai\.' "$W/Packages/manifest.json"; then echo "The AI packages are still in the manifest; stopping."; exit 1; fi

echo "Building $TARGET with Unity in batch mode..."
"$UNITY_PATH" -batchmode -quit -projectPath "$W" -executeMethod "$METHOD" -logFile "$W/build.log" || { echo "Unity failed. See $W/build.log"; exit 1; }
[ -d "$W/Builds/$OUT" ] || { echo "No build was produced. See $W/build.log and $W/Logs"; exit 1; }

mkdir -p "$P/Builds/release"
rm -rf "$P/Builds/release/$OUT"
cp -r "$W/Builds/$OUT" "$P/Builds/release/$OUT"
echo "Done: $P/Builds/release/$OUT"
du -sh "$P/Builds/release/$OUT" 2>/dev/null || true
