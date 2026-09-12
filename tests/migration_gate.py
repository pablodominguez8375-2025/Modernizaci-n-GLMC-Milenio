#!/usr/bin/env python3
from pathlib import Path
import re
import sys

MIGRATIONS = Path("backend/src/PMGM.Api/Migrations")
POSITIONAL_FOREIGN_KEY = re.compile(r"table\.ForeignKey\(\s*\"")


def main() -> int:
    if not MIGRATIONS.exists():
        print(f"MIGRATION GATE FAILED: no existe {MIGRATIONS}")
        return 1

    violations: list[str] = []
    files = sorted(MIGRATIONS.glob("*.cs"))
    for path in files:
        content = path.read_text(encoding="utf-8")
        if POSITIONAL_FOREIGN_KEY.search(content):
            violations.append(str(path))

    if violations:
        print("MIGRATION GATE FAILED: se detectaron ForeignKey con argumentos posicionales ambiguos.")
        print("Use name:, column:, principalSchema:, principalTable: y principalColumn: explícitos.")
        for path in violations:
            print(f" - {path}")
        return 1

    print(f"Migration gate OK: {len(files)} migraciones sin ForeignKey posicionales ambiguos.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
