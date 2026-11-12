#!/bin/bash
# Usage: umenu.sh "Tools/T-Minus/Build VAB Scene" — waits for compile to settle, runs the menu item.
P="$(cd "$(dirname "$0")/../.." && pwd)"
for i in $(seq 1 60); do grep -q "loaded\|menu\|tests done" "$P/Logs/agent-status.txt" && break; sleep 2; done
printf '%s' "$1" > "$P/Temp/agent_menu.flag"
for i in $(seq 1 60); do sleep 2; [ -f "$P/Temp/agent_menu.flag" ] || break; done
sleep 3; cat "$P/Logs/agent-status.txt"
