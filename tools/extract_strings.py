#!/usr/bin/env python3
"""
Moves plain on-screen text out of the C# UI scripts into Assets/_TMinus/Resources/Data/strings.json (audit A-24).

  python tools/extract_strings.py            # rewrite the scripts and the table
  python tools/extract_strings.py --check    # report only; change nothing

It only touches text that is passed straight to a UI call as one plain quoted piece of text:
  new Label("..."), L("...", ...), Btn("...", ...), Toast("..."), AddButton(x, "...", ...), { text = "..." }
Interpolated text ($"..."), text built with +, constants, switch cases and CSS class names are left alone, so the script never
changes what the game does: `Strings.Get(key, original)` returns the original text when the key is missing.
Run it again after adding new UI; it skips text it has already moved. It does not need Unity.
"""
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
SCRIPTS = os.path.join(ROOT, 'Assets', '_TMinus', 'Scripts')
TABLE = os.path.join(ROOT, 'Assets', '_TMinus', 'Resources', 'Data', 'strings.json')

# file (relative to Scripts) -> key prefix
TARGETS = {
    'UI/MenuPanels.cs': 'menu',
    'UI/MainMenuController.cs': 'menu',
    'Mission/BriefingUI.cs': 'briefing',
    'Mission/BriefingUI.Missions.cs': 'briefing',
    'Mission/BriefingUI.Pad.cs': 'pad',
    'Mission/DebriefUI.cs': 'debrief',
    'Mission/FailureLab.cs': 'failure',
    'Mission/FailureJobTracker.cs': 'failure',
    'Houston/HoustonPanel.Actions.cs': 'houston',
    'Houston/HoustonPanel.cs': 'houston',
    'Flight/FlightHud.cs': 'flight',
    'Spacecraft/BayUI.cs': 'bay',
    'Assembly/VabUI.cs': 'vab',
}

# One plain "..." literal right after one of these openings. A '$' before the quote (an interpolated string) never matches.
PATTERNS = [
    re.compile(r'(new Label\()"((?:[^"\\\n])+)"(\))'),
    re.compile(r'(\bL\()"((?:[^"\\\n])+)"(,)'),
    re.compile(r'(\bBtn\()"((?:[^"\\\n])+)"(,)'),
    re.compile(r'(\bToast\()"((?:[^"\\\n])+)"([,)])'),
    re.compile(r'(\bAddButton\(\w+, )"((?:[^"\\\n])+)"(,)'),
    re.compile(r'(\{ text = )"((?:[^"\\\n])+)"( \})'),
]
SKIP_LINE = re.compile(r'\bconst\b|static readonly|\bcase\b|\[SerializeField\]|Debug\.|\bname = "')


def looks_like_text(s):
    return len(s) >= 3 and re.search(r'[A-Za-z]', s) and (' ' in s or s[0].isupper())


def slug(s):
    words = re.findall(r'[A-Za-z0-9]+', s.lower())[:5]
    return '_'.join(words) or 'text'


def main():
    check = '--check' in sys.argv
    table = json.load(open(TABLE, encoding='utf-8'))
    en = table['en']
    by_text = {v: k for k, v in en.items()}
    added = changed_files = 0

    for rel, prefix in TARGETS.items():
        path = os.path.join(SCRIPTS, rel.replace('/', os.sep))
        if not os.path.exists(path):
            continue
        raw = open(path, encoding='utf-8', newline='').read()
        nl = '\r\n' if '\r\n' in raw else '\n'
        lines = raw.replace('\r\n', '\n').split('\n')
        out, file_changed = [], 0
        for line in lines:
            if SKIP_LINE.search(line) or line.lstrip().startswith('//') or line.lstrip().startswith('///'):
                out.append(line)
                continue
            for pat in PATTERNS:
                def sub(m):
                    nonlocal added, file_changed
                    text = m.group(2)
                    if not looks_like_text(text) or '\\' in text:
                        return m.group(0)
                    key = by_text.get(text)
                    if key is None:
                        base = f'{prefix}.{slug(text)}'
                        key, n = base, 2
                        while key in en:
                            key = f'{base}_{n}'
                            n += 1
                        en[key] = text
                        by_text[text] = key
                        added += 1
                    file_changed += 1
                    esc = text.replace('"', '\\"')
                    return f'{m.group(1)}Strings.Get("{key}", "{esc}"){m.group(3)}'
                line = pat.sub(sub, line)
            out.append(line)
        if file_changed:
            changed_files += 1
            print(f'{rel}: {file_changed} texts moved')
            if not check:
                text = nl.join(out)
                if 'using TMinus.Core;' not in text:
                    text = 'using TMinus.Core;' + nl + text
                open(path, 'w', encoding='utf-8', newline='').write(text)

    print(f'{added} new table entries, {changed_files} files{" (check only: nothing written)" if check else ""}')
    if not check:
        with open(TABLE, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(table, f, ensure_ascii=False, indent=2)
            f.write('\n')


if __name__ == '__main__':
    main()
