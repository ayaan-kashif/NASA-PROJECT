#!/bin/bash
# Bring this project's Unity Editor window to the front (Unity throttles its update loop in the background).
powershell -NoProfile -Command "\$p = Get-Process Unity -ErrorAction SilentlyContinue | ? { \$_.MainWindowTitle -like 'NASA PROJECT*' } | Select-Object -First 1; if (\$p) { (New-Object -ComObject WScript.Shell).AppActivate(\$p.Id) | Out-Null }" 2>/dev/null
