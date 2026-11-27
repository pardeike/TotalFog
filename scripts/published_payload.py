"""Use the published 1.6 payload without rewriting the local checkout."""

from pathlib import Path
import hashlib
import urllib.request
import xml.etree.ElementTree as ET
from zipfile import ZIP_DEFLATED, ZipFile

PUBLISHED_URL = "https://github.com/pardeike/TotalFog/releases/download/v1.0.4/TotalFog.zip"
PUBLISHED_SHA256 = "21ea6e3dfdfd118b4c5e571540c194e41076e451c512324d069f8e4630e49158"
# The archive matches all 277 files in the accepted Steam upload manifest.
STEAM_ITEM = "3820003593"
STEAM_UPDATED = 1792980417
STEAM_CONTENT_DIGEST = "50f3cbe1448d406a83c00fbd293f417357f9d23646039682baef02065ffedb6f"
VERSION_METADATA = {"About/About.xml": "modVersion", "About/Manifest.xml": "version"}


def published_payload(root):
    archive = Path(root) / "artifacts/published/TotalFog-v1.0.4.zip"
    if not archive.exists():
        with urllib.request.urlopen(PUBLISHED_URL, timeout=30) as response:
            data = response.read()
        if hashlib.sha256(data).hexdigest() != PUBLISHED_SHA256:
            raise RuntimeError("Published legacy archive checksum mismatch")
        archive.parent.mkdir(parents=True, exist_ok=True)
        archive.write_bytes(data)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != PUBLISHED_SHA256:
        raise RuntimeError("Published legacy archive checksum mismatch: " + str(archive))
    with ZipFile(archive) as release:
        return {
            name.removeprefix("TotalFog/"): release.read(name)
            for name in release.namelist()
            if not name.endswith("/")
        }


def read_payload(root):
    root = Path(root)
    files = {}
    if root.is_symlink():
        raise RuntimeError("Refusing symlinked payload: " + str(root))
    for path in root.rglob("*"):
        if path.is_symlink():
            raise RuntimeError("Refusing symlink in payload: " + str(path))
        if path.is_file() and path.name != ".DS_Store" and not path.name.startswith("._"):
            files[path.relative_to(root).as_posix()] = path.read_bytes()
    return files


def verify_legacy_mapping(data, published):
    def mappings(content):
        return {
            version.tag: [(folder.tag, folder.text, folder.attrib) for folder in version]
            for version in ET.fromstring(content)
            if version.tag != "v1.7"
        }

    if mappings(data) != mappings(published["LoadFolders.xml"]):
        raise RuntimeError("Published legacy LoadFolders mappings differ")


def verify_version_metadata(name, data, published):
    def without_version(content):
        try:
            root = ET.fromstring(content)
        except ET.ParseError as exc:
            raise RuntimeError("Invalid published metadata: " + name) from exc
        version = root.find(VERSION_METADATA[name])
        if version is None:
            raise RuntimeError("Missing version in " + name)
        for node in root.iter():
            node.text = (node.text or "").strip()
            node.tail = None
        version.text = ""
        return ET.tostring(root)

    if without_version(data) != without_version(published[name]):
        raise RuntimeError("Published metadata differs beyond its version: " + name)


def verify_installed_legacy(destination, published):
    destination = Path(destination)
    if not destination.exists():
        return {}
    installed = read_payload(destination)
    for name, expected in published.items():
        if name in VERSION_METADATA:
            if name not in installed:
                raise RuntimeError("Installed published metadata is missing: " + name)
            verify_version_metadata(name, installed[name], published)
        elif name == "LoadFolders.xml":
            if name not in installed:
                raise RuntimeError("Installed published payload is missing: " + name)
            verify_legacy_mapping(installed[name], published)
        elif installed.get(name) != expected:
            raise RuntimeError("Installed published payload differs or is missing: " + name)
    additional = sorted(
        name for name in installed if name not in published and not name.startswith("1.7/")
    )
    if additional:
        raise RuntimeError("Additional installed legacy payload: " + ", ".join(additional[:4]))
    return installed


def write_package(staged, archive, published):
    payload = read_payload(staged)
    for name, expected in published.items():
        if name in VERSION_METADATA:
            verify_version_metadata(name, payload[name], published)
        elif name == "LoadFolders.xml":
            verify_legacy_mapping(payload[name], published)
        elif payload.get(name) != expected:
            raise RuntimeError("Packaged published payload differs: " + name)
    if ET.fromstring(payload["About/About.xml"]).findtext("modVersion") != ET.fromstring(
        payload["About/Manifest.xml"]
    ).findtext("version"):
        raise RuntimeError("About.xml and Manifest.xml versions differ")
    if [n.text for n in ET.fromstring(payload["LoadFolders.xml"]).findall("v1.7/li")] != ["1.7"]:
        raise RuntimeError("RimWorld 1.7 must load only 1.7/")
    for name in payload:
        if name not in published and not name.startswith("1.7/"):
            raise RuntimeError("Unexpected package payload: " + name)
        if any(p in {"Source", "BridgeTools", "artifacts"} for p in Path(name).parts):
            raise RuntimeError("Development files in package: " + name)
        if (
            name.endswith(".dll")
            and name not in published
            and name != "1.7/Assemblies/TotalFog.dll"
        ):
            raise RuntimeError("Unexpected runtime DLL: " + name)
    archive = Path(archive)
    if archive.is_symlink():
        raise RuntimeError("Refusing symlinked ZIP destination: " + str(archive))
    archive.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(archive, "w", ZIP_DEFLATED) as output:
        for name, data in sorted(payload.items()):
            output.writestr("TotalFog/" + name, data)
    with ZipFile(archive) as output:
        actual = {n.removeprefix("TotalFog/"): output.read(n) for n in output.namelist()}
        if output.testzip() is not None or actual != payload:
            raise RuntimeError("ZIP differs from the staged payload")
    archive.with_suffix(".zip.sha256").write_text(
        hashlib.sha256(archive.read_bytes()).hexdigest() + "  " + archive.name + "\n"
    )
    return archive
