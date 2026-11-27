"""Exercise 1.7 staging and frozen-payload guards without a game installation."""

from pathlib import Path
import ast
import hashlib
import importlib.util
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def load_helper(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


frozen_payload = load_helper("frozen_payload")
receipt = load_helper("build_receipt")
published = load_helper("published_payload")
# Load the production functions without running the command dispatcher.
workflow = ast.parse((ROOT / "scripts/mod").read_text())
functions = ast.Module(
    body=[
        node
        for node in workflow.body
        if isinstance(node, ast.FunctionDef)
        and node.name in {"gameplay_assembly", "stage", "deploy_pair_locked"}
    ],
    type_ignores=[],
)


class WorkflowSeparationTests(unittest.TestCase):
    def setUp(self):
        workspace = tempfile.TemporaryDirectory()
        self.addCleanup(workspace.cleanup)
        self.root = Path(workspace.name) / "repo"
        self.root.mkdir()
        files = {
            "Current/Assemblies/TotalFog.dll": b"frozen 1.6 DLL",
            "Current/Defs/Fog.xml": b"frozen 1.6 definition",
            "LegacyAssets/Textures/Fog.png": b"frozen texture",
            "Languages/English/Keyed/Fog.xml": b"frozen language",
            "1.5/Assemblies/VersionFromManifest.dll": b"frozen historical DLL",
            "1.7/Assemblies/TotalFog.dll": b"new 1.7 DLL",
            "1.7/Defs/Fog.xml": b"independent 1.7 definition",
            "1.7/Languages/English/Keyed/Fog.xml": b"local 1.7 language",
            "About/About.xml": b"<ModMetaData><modVersion>1.0.5</modVersion></ModMetaData>",
            "About/Manifest.xml": b"<Manifest><version>1.0.5</version></Manifest>",
            "Directory.Build.props": b"<Project><PropertyGroup><ModVersion>1.0.5</ModVersion></PropertyGroup></Project>",
            "LoadFolders.xml": (ROOT / "LoadFolders.xml").read_bytes(),
            "Source/TotalFog/TotalFog.csproj": b"<Project/>",
            "global.json": b"{}",
        }
        for name, content in files.items():
            self.write(name, content)
        self.published = {
            name: data
            for name, data in files.items()
            if name.startswith(("Current/", "LegacyAssets/", "Languages/", "About/"))
            or name == "LoadFolders.xml"
        }
        self.published["Current/Assemblies/TotalFog.dll"] = b"published 1.6 DLL"
        self.published["Languages/English/Keyed/Fog.xml"] = b"published language"
        for name in published.VERSION_METADATA:
            self.published[name] = self.published[name].replace(b"1.0.5", b"1.0.4")
        self.frozen = {
            name: hashlib.sha256(content).hexdigest()
            for name, content in files.items()
            if name.startswith(("Current/", "LegacyAssets/", "Languages/", "1.5/"))
        }
        self.write(
            "scripts/frozen-payload.json",
            json.dumps(
                {"roots": ["Current", "LegacyAssets", "Languages", "1.5"], "files": self.frozen}
            ).encode(),
        )
        self.mods = Path(workspace.name) / "game/Mods"
        self.mods.mkdir(parents=True)
        self.companion = self.root / "artifacts/BridgeTools/TotalFog/TotalFog.BridgeTools.dll"
        self.write(str(self.companion.relative_to(self.root)), b"matching companion")
        self.candidate = self.root / "1.7/Assemblies/TotalFog.dll"
        receipt.write_receipt(self.root, self.candidate)
        self.companion.with_suffix(".pair.json").write_text(
            json.dumps(
                {
                    "gameplaySha256": hashlib.sha256(self.candidate.read_bytes()).hexdigest(),
                    "companionSha256": hashlib.sha256(self.companion.read_bytes()).hexdigest(),
                }
            )
        )
        self.ns = {
            "ROOT": self.root,
            "PROJECT": self.root / "Source/TotalFog/TotalFog.csproj",
            "MODS": self.mods,
            "published_payload": lambda root: self.published,
            "read_payload": published.read_payload,
            "verify_installed_legacy": published.verify_installed_legacy,
            "verify_legacy_mapping": published.verify_legacy_mapping,
            "write_package": published.write_package,
            "VERSION_METADATA": published.VERSION_METADATA,
            "tempfile": tempfile,
            "Path": Path,
            "os": os,
            "shutil": shutil,
            "subprocess": subprocess,
            "ET": ET,
            "hashlib": hashlib,
            "json": json,
            "verify_frozen": frozen_payload.verify_frozen,
            "verify_receipt": receipt.verify_receipt,
            "stopped": lambda: None,
        }
        exec(compile(functions, str(ROOT / "scripts/mod"), "exec"), self.ns)
        environment = patch.dict(os.environ, {"RIMWORLD_MOD_DIR": str(self.mods)})
        environment.start()
        self.addCleanup(environment.stop)
        os.environ.pop("TotalFogBuildOutput", None)

    def write(self, name, content):
        target = self.root / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)

    def test_build_output_uses_17_and_rejects_frozen_or_symlink_aliases(self):
        self.assertEqual(self.ns["gameplay_assembly"](), self.candidate)
        for output in ["../../Current/Assemblies", str(self.root / "LegacyAssets")]:
            with patch.dict(os.environ, {"TotalFogBuildOutput": output}):
                with self.assertRaisesRegex(RuntimeError, "frozen"):
                    self.ns["gameplay_assembly"]()
        if os.name != "nt":
            alias = self.root / "1.7/frozen-alias"
            alias.symlink_to(self.root / "Current", target_is_directory=True)
            with patch.dict(os.environ, {"TotalFogBuildOutput": str(alias)}):
                with self.assertRaisesRegex(RuntimeError, "frozen"):
                    self.ns["gameplay_assembly"]()

    def test_frozen_guard_rejects_changed_removed_and_added_payload(self):
        target = self.root / "Current/Defs/Fog.xml"
        original = target.read_bytes()
        for action in ["change", "remove", "add"]:
            with self.subTest(action=action):
                if action == "remove":
                    target.unlink()
                elif action == "change":
                    target.write_bytes(b"changed")
                else:
                    self.write("Current/Defs/Extra.xml", b"unexpected")
                with self.assertRaisesRegex(RuntimeError, "Frozen payload changed"):
                    frozen_payload.verify_frozen(self.root)
                target.write_bytes(original)
                (self.root / "Current/Defs/Extra.xml").unlink(missing_ok=True)

    def test_staging_retains_every_frozen_file_and_adds_independent_17_payload(self):
        destination = self.root / "artifacts/package/TotalFog"
        self.ns["stage"](destination)
        for name, data in self.published.items():
            if name not in published.VERSION_METADATA:
                self.assertEqual((destination / name).read_bytes(), data)
        self.assertNotEqual(
            (destination / "Current/Assemblies/TotalFog.dll").read_bytes(),
            (self.root / "Current/Assemblies/TotalFog.dll").read_bytes(),
        )
        self.assertFalse((destination / "1.5").exists())
        frozen_payload.verify_frozen(self.root)
        self.assertEqual((destination / "1.7/Assemblies/TotalFog.dll").read_bytes(), b"new 1.7 DLL")
        self.assertFalse(any(destination.rglob("*.BridgeTools.dll")))
        self.assertFalse(any(destination.rglob("RimBridgeServer.Sdk.dll")))

    def test_installed_frozen_mismatch_refuses_before_any_copy(self):
        destination = self.mods / "TotalFog"
        installed = destination / "Current/Assemblies/TotalFog.dll"
        installed.parent.mkdir(parents=True)
        installed.write_bytes(b"a different installed 1.6 DLL")
        with self.assertRaisesRegex(RuntimeError, "Installed published payload differs"):
            self.ns["deploy_pair_locked"]()
        self.assertFalse((destination / "1.7").exists())
        self.assertEqual(installed.read_bytes(), b"a different installed 1.6 DLL")

    def test_pair_copy_preserves_published_mtimes_and_creates_identical_zip(self):
        destination = self.mods / "TotalFog"
        self.ns["stage"](destination)
        before = {
            name: (destination / name).stat().st_mtime_ns
            for name in self.published
            if name != "LoadFolders.xml" and name not in published.VERSION_METADATA
        }
        (destination / "1.7/obsolete.xml").write_bytes(b"remove")
        self.ns["deploy_pair_locked"]()
        self.assertEqual(
            before,
            {
                name: (destination / name).stat().st_mtime_ns
                for name in self.published
                if name != "LoadFolders.xml" and name not in published.VERSION_METADATA
            },
        )
        self.assertFalse((destination / "1.7/obsolete.xml").exists())
        self.assertEqual(
            (self.mods.parent / "BridgeTools/TotalFog/TotalFog.BridgeTools.dll").read_bytes(),
            self.companion.read_bytes(),
        )

    def test_missing_published_file_refuses_before_copy(self):
        destination = self.mods / "TotalFog"
        self.ns["stage"](destination)
        (destination / "Languages/English/Keyed/Fog.xml").unlink()
        before = published.read_payload(destination)
        with self.assertRaisesRegex(RuntimeError, "missing"):
            self.ns["deploy_pair_locked"]()
        self.assertEqual(published.read_payload(destination), before)

    def test_added_legacy_resource_refuses_before_copy(self):
        destination = self.mods / "TotalFog"
        self.ns["stage"](destination)
        (destination / "Languages/English/Keyed/Extra.xml").write_bytes(b"keep")
        before = published.read_payload(destination)
        with self.assertRaisesRegex(RuntimeError, "Additional installed legacy"):
            self.ns["deploy_pair_locked"]()
        self.assertEqual(published.read_payload(destination), before)

    def test_every_published_file_is_guarded_before_mutation(self):
        destination = self.mods / "TotalFog"
        self.ns["stage"](destination)
        for name in self.published:
            with self.subTest(name=name):
                target = destination / name
                original = target.read_bytes()
                target.write_bytes(b"<loadFolders/>" if name == "LoadFolders.xml" else b"different")
                before = published.read_payload(destination)
                with self.assertRaises(RuntimeError):
                    self.ns["deploy_pair_locked"]()
                self.assertEqual(published.read_payload(destination), before)
                target.write_bytes(original)

    def test_package_contains_exact_published_bytes_and_only_17_updates(self):
        destination = self.root / "artifacts/package/TotalFog"
        self.ns["stage"](destination)
        archive = published.write_package(
            destination, self.root / "dist/TotalFog.zip", self.published
        )
        with published.ZipFile(archive) as output:
            data = {n.removeprefix("TotalFog/"): output.read(n) for n in output.namelist()}
        self.assertEqual(data, published.read_payload(destination))
        for name, content in self.published.items():
            if name not in published.VERSION_METADATA:
                self.assertEqual(data[name], content)
        self.assertEqual(ET.fromstring(data["About/About.xml"]).findtext("modVersion"), "1.0.5")
        self.assertEqual(data["1.7/Languages/English/Keyed/Fog.xml"], b"local 1.7 language")

    def test_changed_companion_refuses_before_copy(self):
        self.companion.write_bytes(b"stale or unrelated companion")
        with self.assertRaisesRegex(RuntimeError, "pair differs"):
            self.ns["deploy_pair_locked"]()
        self.assertFalse((self.mods / "TotalFog").exists())

    def test_tampered_published_cache_refuses_without_network_or_staging(self):
        archive = self.root / "artifacts/published/TotalFog-v1.0.4.zip"
        archive.parent.mkdir(parents=True)
        archive.write_bytes(b"tampered cache")
        with self.assertRaisesRegex(RuntimeError, "checksum mismatch"):
            published.published_payload(self.root)

    def test_version_mapping_preserves_16_and_isolates_17(self):
        folders = ET.parse(ROOT / "LoadFolders.xml")
        self.assertEqual(
            [n.text for n in folders.findall("v1.6/li")], ["/", "Current", "LegacyAssets"]
        )
        self.assertEqual([n.text for n in folders.findall("v1.7/li")], ["1.7"])


if __name__ == "__main__":
    unittest.main()
