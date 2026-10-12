#!/usr/bin/env python3
"""
PreInvocation hook: heuristic hot-path linter for VR Volley.

Scans C# files under Assets/_Project that changed since the last run and warns the agent about
patterns that violate AGENTS.md (allocations / slow calls inside Update, FixedUpdate, LateUpdate,
OnTriggerStay, OnCollisionStay, OnAnimatorIK, OnBeforeRender).

Contract: reads hook JSON from stdin (ignored except for safety), writes JSON to stdout.
Must stay fast (<200 ms) and must never crash the agent loop. Any error → prints {}.

Run manually:  python .agents/hooks/hotpath_lint.py --all
"""
import json
import os
import re
import sys

HOOK_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(HOOK_DIR, "..", ".."))
SCAN_ROOT = os.path.join(PROJECT_ROOT, "Assets", "_Project")
CACHE_FILE = os.path.join(HOOK_DIR, ".hotpath_lint_cache.json")

HOT_METHODS = r"(?:Update|FixedUpdate|LateUpdate|OnTriggerStay|OnCollisionStay|OnAnimatorIK|OnBeforeRender)"
METHOD_RE = re.compile(r"\b(?:void)\s+(" + HOT_METHODS + r")\s*\([^)]*\)\s*\{")

RULES = [
    (re.compile(r"\bGetComponent(?:InChildren|InParent|s)?\s*[<(]"), "GetComponent* in hot path: cache it in Awake"),
    (re.compile(r"\bFind(?:ObjectOfType|ObjectsOfType|FirstObjectByType|AnyObjectByType|ObjectsByType|WithTag)?\s*\("), "Find* in hot path: cache the reference"),
    (re.compile(r"\bnew\s+(?:List|Dictionary|HashSet|Queue|Stack)\s*<"), "collection allocation in hot path: pre-allocate in Awake"),
    (re.compile(r"\bnew\s+[A-Za-z_][\w.]*\s*\[\s*[^\]\s]"), "array allocation in hot path: pre-allocate in Awake"),
    (re.compile(r"\.(?:Where|Select|Any|All|First|FirstOrDefault|OrderBy|ToList|ToArray|Count)\s*\(\s*[a-zA-Z_]\w*\s*=>"), "LINQ in hot path"),
    (re.compile(r"\.(?:ToList|ToArray)\s*\(\s*\)"), "ToList/ToArray allocates"),
    (re.compile(r"\$\"|\"\s*\+|\+\s*\"|string\.Format\s*\(|\.ToString\s*\(\s*\)"), "string building in hot path allocates (use TMP SetText or cache)"),
    (re.compile(r"\bDebug\.Log(?:Warning|Error|Format)?\s*\("), "Debug.Log in hot path allocates; use a [Conditional] logger"),
    (re.compile(r"\bPhysics\.(?:RaycastAll|SphereCastAll|CapsuleCastAll|BoxCastAll|OverlapSphere|OverlapBox|OverlapCapsule)\s*\("), "allocating physics query: use the *NonAlloc variant"),
    (re.compile(r"\b(?:Instantiate|Destroy)\s*\("), "Instantiate/Destroy during play: use a pool"),
    (re.compile(r"\.material\b(?!s)\s*(?:\.|=)"), "renderer.material clones the material: use sharedMaterial / MaterialPropertyBlock"),
    (re.compile(r"\bnew\s+WaitFor(?:Seconds|EndOfFrame|FixedUpdate)\s*\("), "new WaitFor* allocates: cache it"),
    (re.compile(r"\bCamera\.main\b"), "Camera.main in gameplay hot path: use XR Origin / cached camera transform"),
    (re.compile(r"=>\s*[{\w]"), "lambda in hot path may allocate a closure"),
    (re.compile(r"\bGetComponent\b|\bSendMessage\s*\("), "SendMessage/GetComponent reflection-style call in hot path"),
]

FILE_RULES = [
    (re.compile(r"^\s*using\s+System\.Linq\s*;", re.M), "uses System.Linq in a runtime script; make sure it never runs per frame"),
    (re.compile(r"PhysicMaterial|PhysicsMaterial"), "references PhysicMaterial; ball/hand contacts must use ContactMath, not PhysX bounciness"),
]


def strip_comments_and_strings_keep_quotes(src: str) -> str:
    # Remove // and /* */ comments. Keep string literals (the string rules need them).
    src = re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), src, flags=re.S)
    src = re.sub(r"//[^\n]*", "", src)
    return src


def extract_body(src: str, open_brace_idx: int) -> tuple[str, int]:
    depth = 0
    for i in range(open_brace_idx, len(src)):
        c = src[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return src[open_brace_idx:i + 1], i
    return src[open_brace_idx:], len(src)


def lint_file(path: str) -> list[str]:
    try:
        with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
            raw = f.read()
    except OSError:
        return []

    if "/Editor/" in path.replace("\\", "/") or "/Tests/" in path.replace("\\", "/"):
        return []

    src = strip_comments_and_strings_keep_quotes(raw)
    rel = os.path.relpath(path, PROJECT_ROOT).replace("\\", "/")
    findings = []

    for rx, msg in FILE_RULES:
        if rx.search(src):
            findings.append(f"{rel}: {msg}")

    seen = set()
    for m in METHOD_RE.finditer(src):
        method = m.group(1)
        body, _ = extract_body(src, m.end() - 1)
        body_start_line = src.count("\n", 0, m.end()) + 1
        for line_off, line in enumerate(body.split("\n")):
            if "hotpath-ok" in raw.split("\n")[min(body_start_line - 1 + line_off, len(raw.split("\n")) - 1)]:
                continue
            for rx, msg in RULES:
                if rx.search(line):
                    key = (body_start_line + line_off, msg)
                    if key in seen:
                        continue
                    seen.add(key)
                    findings.append(f"{rel}:{body_start_line + line_off} in {method}(): {msg}")
    return findings


def load_cache() -> dict:
    try:
        with open(CACHE_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def save_cache(cache: dict) -> None:
    try:
        with open(CACHE_FILE, "w", encoding="utf-8") as f:
            json.dump(cache, f)
    except OSError:
        pass


def main() -> None:
    scan_all = "--all" in sys.argv
    if not scan_all:
        try:
            sys.stdin.read()
        except Exception:
            pass

    if not os.path.isdir(SCAN_ROOT):
        print("{}")
        return

    cache = {} if scan_all else load_cache()
    new_cache = {}
    findings = []
    for dirpath, _, files in os.walk(SCAN_ROOT):
        for name in files:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            try:
                mtime = os.path.getmtime(path)
            except OSError:
                continue
            new_cache[path] = mtime
            if cache.get(path) == mtime:
                continue
            findings.extend(lint_file(path))

    save_cache(new_cache)

    if scan_all:
        print("\n".join(findings) if findings else "No hot-path findings.")
        return

    if not findings:
        print("{}")
        return

    shown = findings[:15]
    more = len(findings) - len(shown)
    msg = (
        "HOT-PATH LINT (AGENTS.md zero-allocation rules) found issues in recently changed C# files. "
        "Fix them, or add a `// hotpath-ok: <reason>` comment on the line if it is a deliberate exception:\n- "
        + "\n- ".join(shown)
        + (f"\n- ...and {more} more" if more > 0 else "")
    )
    print(json.dumps({"injectSteps": [{"ephemeralMessage": msg}]}))


if __name__ == "__main__":
    try:
        main()
    except Exception:
        print("{}")
