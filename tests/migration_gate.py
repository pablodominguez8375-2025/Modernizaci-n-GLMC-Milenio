#!/usr/bin/env python3
from pathlib import Path
import re
import sys

MIGRATIONS = Path("backend/src/PMGM.Api/Migrations")
POSITIONAL_FOREIGN_KEY = re.compile(r"table\.ForeignKey\(\s*\"")
DB_CONTEXT_ATTR = re.compile(r"\[DbContext\(typeof\([A-Za-z0-9_]+\)\)\]")
MIGRATION_ATTR = re.compile(r"\[Migration\(\"[0-9]{14}_[A-Za-z0-9_]+\"\)\]")


def main() -> int:
    if not MIGRATIONS.exists():
        print(f"MIGRATION GATE FAILED: no existe {MIGRATIONS}")
        return 1

    positional_fk_violations: list[str] = []
    metadata_violations: list[str] = []
    files = sorted(MIGRATIONS.glob("*.cs"))
    for path in files:
        content = path.read_text(encoding="utf-8")
        if POSITIONAL_FOREIGN_KEY.search(content):
            positional_fk_violations.append(str(path))
        if not DB_CONTEXT_ATTR.search(content) or not MIGRATION_ATTR.search(content):
            metadata_violations.append(str(path))

    if positional_fk_violations or metadata_violations:
        print("MIGRATION GATE FAILED.")
        if positional_fk_violations:
            print("Se detectaron ForeignKey con argumentos posicionales ambiguos.")
            print("Use name:, column:, principalSchema:, principalTable: y principalColumn: explícitos.")
            for path in positional_fk_violations:
                print(f" - {path}")
        if metadata_violations:
            print("Se detectaron migraciones sin metadata EF Core [DbContext] + [Migration].")
            print("Sin estos atributos la migración puede compilar pero no ser descubierta/aplicada.")
            for path in metadata_violations:
                print(f" - {path}")
        return 1

    print(f"Migration gate OK: {len(files)} migraciones registradas y sin ForeignKey posicionales ambiguos.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
