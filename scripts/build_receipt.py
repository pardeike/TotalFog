"""Match a gameplay DLL to its complete source and build-input snapshot."""

from pathlib import Path
import hashlib
import json


def input_hashes(root):
    root = Path(root)
    project = root / "Source/TotalFog/TotalFog.csproj"
    if not project.is_file():
        raise RuntimeError("Gameplay project is missing")
    paths = {project, root / "global.json"}
    for folder in ["Source/TotalFog", "Source/Core"]:
        paths.update(
            p
            for p in (root / folder).rglob("*.cs")
            if not {"obj", "bin", "BridgeTools"}.intersection(p.relative_to(root).parts)
        )
    for folder in [root, root / "Source", project.parent]:
        for name in [
            "Directory.Build.props",
            "Directory.Build.targets",
            "Directory.Packages.props",
            "NuGet.Config",
            "NuGet.config",
            "nuget.config",
            "packages.lock.json",
        ]:
            if (folder / name).is_file():
                paths.add(folder / name)
    # Git may check out textual inputs with CRLF on Windows. Hash their logical
    # text so the same committed source is accepted on both CI platforms.
    return {
        p.relative_to(root).as_posix(): hashlib.sha256(
            p.read_bytes().replace(b"\r\n", b"\n")
        ).hexdigest()
        for p in sorted(paths)
    }


def receipt_path(assembly):
    return Path(assembly).with_suffix(".build.json")


def write_receipt(root, assembly):
    assembly = Path(assembly)
    record = {
        "schemaVersion": 1,
        "configuration": "Release",
        "inputs": input_hashes(root),
        "dllSha256": hashlib.sha256(assembly.read_bytes()).hexdigest(),
    }
    receipt_path(assembly).write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")


def verify_receipt(root, assembly):
    assembly = Path(assembly)
    try:
        record = json.loads(receipt_path(assembly).read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise RuntimeError("Build receipt is missing or invalid; run ./scripts/mod build") from exc
    if record.get("schemaVersion") != 1 or record.get("configuration") != "Release":
        raise RuntimeError("Unsupported build receipt; run ./scripts/mod build")
    expected = input_hashes(root)
    recorded = record.get("inputs")
    if not isinstance(recorded, dict):
        raise RuntimeError("Invalid source snapshot in build receipt")
    changed = sorted(
        name
        for name in expected.keys() | recorded.keys()
        if expected.get(name) != recorded.get(name)
    )
    if changed:
        raise RuntimeError(
            "Tracked DLL is stale for "
            + ", ".join(changed[:4])
            + "; run ./scripts/mod build and commit its DLL and receipt"
        )
    if (
        not assembly.is_file()
        or record.get("dllSha256") != hashlib.sha256(assembly.read_bytes()).hexdigest()
    ):
        raise RuntimeError("Gameplay DLL differs from its build receipt; run ./scripts/mod build")
