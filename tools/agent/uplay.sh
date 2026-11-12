#!/bin/bash
# Usage: uplay.sh <script-file>. Runs an AgentPlayScript with Unity focused; prints log + errors.
P="$(cd "$(dirname "$0")/../.." && pwd)"; L="$P/Logs/Editor.log"
start=$(wc -l < "$L"); rm -f "$P/Logs/agent-play.txt"; rm -rf "$P/Logs/agent-shots"
cp "$1" "$P/Temp/agent_play.flag"
for i in $(seq 1 150); do
  powershell -NoProfile -Command "(New-Object -ComObject WScript.Shell).AppActivate((Get-Process Unity | ? {\$_.MainWindowTitle -like 'NASA PROJECT*'}).Id) | Out-Null" 2>/dev/null
  sleep 2; grep -q "finished" "$P/Logs/agent-play.txt" 2>/dev/null && break
done
cat "$P/Logs/agent-play.txt"; ls "$P/Logs/agent-shots" 2>/dev/null
tail -n +$start "$L" | grep -E "Exception|rror" | grep -vE "^UnityEngine\.|^Unity\.AI|^  at |Licensing|ErrorMode|ScriptCompilation|^\(Filename" | sort | uniq -c | head -20
