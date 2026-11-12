#!/bin/bash
# Trigger Unity import/compile and report CS errors from new log lines.
P="$(cd "$(dirname "$0")/../.." && pwd)"; L="$P/Logs/Editor.log"
start=$(wc -l < "$L")
touch "$P/Temp/agent_refresh.flag"
if [ "$1" = "focus" ]; then powershell -NoProfile -Command "(New-Object -ComObject WScript.Shell).AppActivate((Get-Process Unity | ? {\$_.MainWindowTitle -like 'NASA PROJECT*'}).Id) | Out-Null"; fi
for i in $(seq 1 90); do
  sleep 2
  n=$(wc -l < "$L")
  if [ ! -f "$P/Temp/agent_refresh.flag" ] && tail -n +$start "$L" | grep -qE "Reloading assemblies after|Finished compiling graph|Asset Pipeline Refresh .*Total:|Compilation failed|error CS"; then
    sleep 6; break
  fi
done
echo "flag still present: $([ -f "$P/Temp/agent_refresh.flag" ] && echo yes || echo no)"
tail -n +$start "$L" | grep -E "error CS|warning CS[0-9]+.*_TMinus|Exception:|\[T-Minus\]|\[DataRegistry\]" | sort -u | head -40
echo "--- status: $(cat "$P/Logs/agent-status.txt" 2>/dev/null)"
