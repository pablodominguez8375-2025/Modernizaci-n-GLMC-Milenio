import importlib.util
from pathlib import Path
import unittest

path = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-launcher-custody-chain.py"
spec = importlib.util.spec_from_file_location("custody", path)
custody = importlib.util.module_from_spec(spec)
spec.loader.exec_module(custody)

A, B, C, D, E, N, M, P, Q = (c * 64 for c in "abcdef012")

class CustodyTests(unittest.TestCase):
    def test_unordered_successors(self):
        self.assertEqual(custody.verify_chain(original=A, live=D,
            edges=[(P,C,D),(N,A,B),(M,B,C)]), tuple(sorted((N,M,P))))

    def test_no_change(self):
        self.assertEqual(custody.verify_chain(original=A, live=A, edges=[]), ())

    def test_reject(self):
        for edges in (
            [(N,A,B),(M,A,D)],
            [(N,A,B),(N,B,D)],
            [(N,A,B),(M,B,A)],
            [(N,A,D),(M,B,C)],
            [(N,A,B)],
            [(N,A,B),(M,C,B)],
            [(N,A,A)],
            [(N,"sha256:"+A,D)],
        ):
            with self.subTest(edges=edges), self.assertRaises(ValueError):
                custody.verify_chain(original=A, live=D, edges=edges)

if __name__ == "__main__":
    unittest.main()
