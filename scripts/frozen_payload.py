"""Verify the unchanged 1.6 and historical runtime payload."""

from pathlib import Path
import hashlib
import json


def verify_frozen(root):
    root = Path(root)
    record = json.loads((root / "scripts/frozen-payload.json").read_text())
    expected = record["files"]
    actual = {
        p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
        for folder in record["roots"]
        for p in (root / folder).rglob("*")
        if p.is_file() and p.name != ".DS_Store" and not p.name.startswith("._")
    }
    changed = sorted(
        name for name in expected.keys() | actual.keys() if expected.get(name) != actual.get(name)
    )
    if changed:
        raise RuntimeError("Frozen payload changed: " + ", ".join(changed[:4]))
    return expected
