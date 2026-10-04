import unittest
from pathlib import Path

class TariffSeedParityTests(unittest.TestCase):
    def test_backend_migration_and_showcase_use_identical_decree_seed(self):
        root = Path(__file__).resolve().parents[1]
        backend = root / 'backend/src/PMGM.Api/Modules/Treasury/decree-1759.json'
        frontend = root / 'frontend/src/api/decree-1759.json'
        self.assertEqual(backend.read_bytes(), frontend.read_bytes(), 'Seed de migración y demo deben representar el mismo decreto')
