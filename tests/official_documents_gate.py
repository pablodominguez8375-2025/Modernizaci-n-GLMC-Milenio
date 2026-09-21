#!/usr/bin/env python3
from pathlib import Path


CANONICAL_DOCUMENTS = (
    Path("docs/PMGM-REQ-001-flujos-institucionales-2026.md"),
    Path("docs/requisitos/PMGM-REQ-022-gran-secretaria-documentos-autorizaciones-espacios.md"),
    Path("docs/requisitos/PMGM-REQ-025-elegibilidad-ceremonias-publicacion-insinuados.md"),
    Path("docs/PMGM-QA-003-uat-flujos-2026.md"),
)

FORBIDDEN_PHRASES = (
    "generar automáticamente la plancha",
    "generación de plancha",
    "gran secretaría podrá generar",
    "gran secretaría genera la plancha",
    "gran secretaría debe poder autorizar y generar",
    "redactar y versionar decretos",
    "crear documentos desde cero",
)

REQUIRED_STATEMENTS = (
    "pdf firmado físicamente",
    "el sistema no genera",
)


def fail(message: str) -> None:
    print(f"OFFICIAL DOCUMENTS GATE FAILED: {message}")
    raise SystemExit(1)


def main() -> int:
    combined = []
    for path in CANONICAL_DOCUMENTS:
        if not path.exists():
            fail(f"No existe el documento canónico {path}.")
        content = path.read_text(encoding="utf-8")
        normalized = content.casefold()
        for phrase in FORBIDDEN_PHRASES:
            if phrase.casefold() in normalized:
                fail(f"{path} volvió a indicar que el sistema crea un documento oficial: {phrase!r}.")
        combined.append(normalized)

    corpus = "\n".join(combined)
    for statement in REQUIRED_STATEMENTS:
        if statement.casefold() not in corpus:
            fail(f"Falta la regla documental obligatoria: {statement!r}.")

    print("Official documents gate passed: Planchas and Decrees require a physically signed PDF.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
