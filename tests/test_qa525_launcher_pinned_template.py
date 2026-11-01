"""QA525 SHA-pinned launcher generation contract, fake/offline only."""
import ast
import hashlib
import importlib.util
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
FILE = ROOT / "scripts/qa/operators/qa525-launcher-pinned-template.py"
spec = importlib.util.spec_from_file_location("qa525_pinned_launcher_template", FILE)
t = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = t
spec.loader.exec_module(t)

SHA = "a" * 40
OTHER = "b" * 40


class PinnedLauncherTests(unittest.TestCase):
    def test_expected_template_is_deterministic_and_legacy_compatible(self):
        payload = t.render(SHA)
        path = "/opt/centenario/qa525-signed-stage/" + SHA + (
            "/scripts/qa/operators/qa525-update.py")
        expected = (
            "#!/usr/bin/python3\nimport os,sys\n" +
            'os.execv("/usr/bin/python3", ["/usr/bin/python3", "-B", ' +
            repr(path) + ', *sys.argv[1:]])\n').encode()
        self.assertEqual(expected, payload)
        self.assertEqual(payload, t.render(SHA))
        self.assertEqual(path, str(t.pinned_source_path(SHA)))

    def test_validation_is_explicitly_not_a_signature_or_install(self):
        raw = t.render(SHA)
        report = t.inspect_template(SHA, raw)
        self.assertEqual("SHA_PINNED_LAUNCHER_TEMPLATE_NO_INSTALL",
                         report["status"])
        self.assertEqual(SHA, report["source_sha"])
        self.assertEqual(hashlib.sha256(raw).hexdigest(), report["launcher_sha256"])
        self.assertFalse(report["signature_verified_by_template"])
        self.assertFalse(report["package_installed_by_template"])
        self.assertFalse(report["may_install"])
        self.assertFalse(report["may_apply"])

    def test_reject_unsafe_source_identifiers_and_mutable_aliases(self):
        variants = (
            None, True, 42, b"a" * 40, "", "dev", "main", "../" + SHA,
            SHA + "/../../tmp", SHA + "\n", SHA + "\x00",
            SHA.upper(), "0x" + SHA, SHA[:39], SHA + "0",
            "/opt/centenario/qa525-signed-stage/" + SHA,
            "refs/heads/dev", "qa-signed-" + SHA,
        )
        for candidate in variants:
            with self.subTest(source=candidate), self.assertRaisesRegex(
                    t.Blocked, "LAUNCHER_SOURCE_SHA_NOT_EXACT"):
                t.render(candidate)

    def test_reject_tampered_interpreter_arguments_and_target(self):
        original = t.render(SHA)
        variants = (
            original.replace(b'"/usr/bin/python3"', b'"/tmp/python3"'),
            original.replace(b'"-B", ', b'"-B", "-c", '),
            original.replace(b'qa525-update.py', b'untrusted.py'),
            original.replace(SHA.encode(), OTHER.encode()),
            original.replace(b'sys.argv[1:]', b'sys.argv[:]'),
            original + b'import subprocess\n',
            original.replace(b'qa525-signed-stage/', b'../'),
            original.replace(b'qa525-update.py', b'qa525-update.py; rm -rf /'),
        )
        for candidate in variants:
            with self.subTest(payload=candidate[-100:]), self.assertRaisesRegex(
                    t.Blocked, "LAUNCHER_TEMPLATE_OR_SOURCE_MISMATCH"):
                t.inspect_template(SHA, candidate)

    def test_cross_release_replay_not_equivalent(self):
        self.assertNotEqual(t.render(SHA), t.render(OTHER))
        with self.assertRaisesRegex(
                t.Blocked, "LAUNCHER_TEMPLATE_OR_SOURCE_MISMATCH"):
            t.inspect_template(OTHER, t.render(SHA))

    def test_reject_nonbytes_and_empty_candidates(self):
        for candidate in (None, True, "", b"", bytearray(t.render(SHA)), []):
            with self.subTest(candidate=type(candidate)), self.assertRaises(
                    t.Blocked):
                t.inspect_template(SHA, candidate)

    def test_generated_ast_execs_only_pinned_operator(self):
        raw = t.render(SHA)
        tree = ast.parse(raw.decode())
        self.assertEqual(3, len(tree.body))
        self.assertIsInstance(tree.body[-1], ast.Expr)
        call = tree.body[-1].value
        self.assertIsInstance(call, ast.Call)
        self.assertIsInstance(call.func, ast.Attribute)
        self.assertEqual("execv", call.func.attr)
        self.assertEqual(2, len(call.args))
        arglist = call.args[1]
        self.assertIsInstance(arglist, ast.List)
        self.assertEqual("/usr/bin/python3", arglist.elts[0].value)
        self.assertEqual("-B", arglist.elts[1].value)
        self.assertEqual(str(t.pinned_source_path(SHA)), arglist.elts[2].value)
        self.assertIsInstance(arglist.elts[3], ast.Starred)

    def test_helper_itself_has_no_host_mutation_or_unsafe_imports(self):
        code = FILE.read_text()
        tree = ast.parse(code)
        modules = [alias.name for node in ast.walk(tree)
                   if isinstance(node, ast.Import) for alias in node.names]
        self.assertFalse(set(modules).intersection(
            {"os", "subprocess", "socket", "fcntl", "shutil", "urllib", "requests"}))
        forbidden = {"open", "system", "popen", "execv", "execve",
                     "replace", "rename", "unlink", "chmod", "chown",
                     "write_bytes", "write_text", "mkdir"}
        for node in ast.walk(tree):
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute):
                self.assertNotIn(node.func.attr, forbidden)
        self.assertNotIn("O_WRONLY", code)
        self.assertNotIn("O_CREAT", code)
        self.assertIn("LIBRARY_ONLY_NO_INSTALL_ENTRYPOINT", code)


if __name__ == "__main__":
    unittest.main()
