#!/bin/bash
P="$(cd "$(dirname "$0")/../.." && pwd)"
rm -f "$P/Logs/agent-tests.txt"; touch "$P/Temp/agent_tests.flag"
for i in $(seq 1 120); do sleep 2; [ -f "$P/Logs/agent-tests.txt" ] && break; done
cat "$P/Logs/agent-tests.txt" 2>/dev/null || echo "no test result (status: $(cat "$P/Logs/agent-status.txt" 2>/dev/null))"
