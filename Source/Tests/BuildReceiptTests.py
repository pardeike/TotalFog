"""Pure checks for the tracked gameplay artifact gate; no game or build required."""

from pathlib import Path
import hashlib
import importlib.util
import json
import tempfile
import unittest

spec = importlib.util.spec_from_file_location(
    "build_receipt", Path(__file__).resolve().parents[2] / "scripts/build_receipt.py"
)
receipt = importlib.util.module_from_spec(spec)
spec.loader.exec_module(receipt)


class BuildReceiptTests(unittest.TestCase):
    def setUp(self):
        self.workspace = tempfile.TemporaryDirectory()
        self.addCleanup(self.workspace.cleanup)
        self.root = Path(self.workspace.name)
        self.source = self.root / "Source/TotalFog"
        self.source.mkdir(parents=True)
        (self.source / "TotalFog.csproj").write_bytes(b"<Project/>\n")
        (self.source / "Fog.cs").write_bytes(b"class Fog {}\n")
        (self.root / "global.json").write_bytes(b"{}\n")
        (self.root / "Directory.Build.props").write_bytes(b"<Project/>\n")
        self.assembly = self.root / "TotalFog.dll"
        self.assembly.write_bytes(b"fixture DLL")
        receipt.write_receipt(self.root, self.assembly)

    def test_current_source_and_dll_pass(self):
        receipt.verify_receipt(self.root, self.assembly)

    def test_native_build_records_the_exact_gameplay_and_companion_pair(self):
        assembly = self.root / "1.7/Assemblies/TotalFog.dll"
        companion = self.root / "artifacts/BridgeTools/TotalFog/TotalFog.BridgeTools.dll"
        for target in [assembly, companion]:
            target.parent.mkdir(parents=True)
            target.write_bytes(target.name.encode())
        receipt.record_build(self.root, assembly, companion)
        receipt.verify_receipt(self.root, assembly)
        pair = json.loads(companion.with_suffix(".pair.json").read_text())
        self.assertEqual(pair["gameplaySha256"], hashlib.sha256(assembly.read_bytes()).hexdigest())
        self.assertEqual(
            pair["companionSha256"], hashlib.sha256(companion.read_bytes()).hexdigest()
        )

    def test_native_receipt_writer_refuses_frozen_output_before_writing(self):
        assembly = self.root / "Current/Assemblies/TotalFog.dll"
        assembly.parent.mkdir(parents=True)
        assembly.write_bytes(b"frozen DLL")
        with self.assertRaisesRegex(RuntimeError, "frozen"):
            receipt.record_build(self.root, assembly)
        self.assertEqual(assembly.read_bytes(), b"frozen DLL")
        self.assertFalse(receipt.receipt_path(assembly).exists())

    def test_source_edit_addition_and_removal_fail(self):
        for action in ["edit", "add", "remove"]:
            with self.subTest(action=action):
                receipt.write_receipt(self.root, self.assembly)
                target = self.source / ("New.cs" if action == "add" else "Fog.cs")
                previous = target.read_bytes() if target.exists() else None
                if action == "remove":
                    target.unlink()
                else:
                    target.write_bytes(b"class Changed {}\n")
                with self.assertRaisesRegex(RuntimeError, "stale"):
                    receipt.verify_receipt(self.root, self.assembly)
                if previous is None:
                    target.unlink()
                else:
                    target.write_bytes(previous)

    def test_build_inputs_fail_when_changed(self):
        (self.root / "Directory.Build.props").write_bytes(b"<Changed/>\n")
        with self.assertRaisesRegex(RuntimeError, "stale"):
            receipt.verify_receipt(self.root, self.assembly)

    def test_changed_or_missing_dll_fails(self):
        self.assembly.write_bytes(b"different DLL")
        with self.assertRaisesRegex(RuntimeError, "differs"):
            receipt.verify_receipt(self.root, self.assembly)
        self.assembly.unlink()
        with self.assertRaisesRegex(RuntimeError, "differs"):
            receipt.verify_receipt(self.root, self.assembly)

    def test_missing_receipt_fails(self):
        receipt.receipt_path(self.assembly).unlink()
        with self.assertRaisesRegex(RuntimeError, "missing"):
            receipt.verify_receipt(self.root, self.assembly)

    def test_checkout_line_endings_do_not_change_identity(self):
        (self.source / "Fog.cs").write_bytes(b"class Fog {}\r\n")
        receipt.verify_receipt(self.root, self.assembly)

    def test_docs_and_generated_sources_do_not_invalidate_dll(self):
        (self.root / "README.md").write_bytes(b"documentation")
        generated = self.source / "obj/Release"
        generated.mkdir(parents=True)
        (generated / "AssemblyInfo.cs").write_bytes(b"generated")
        receipt.verify_receipt(self.root, self.assembly)


if __name__ == "__main__":
    unittest.main()
