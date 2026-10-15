#!/usr/bin/env python3
"""Fail-closed intake gate for S3 supplier evaluation (#470), NEVER deployment.

No Docker Compose changes, no credentials, no network calls by default.
A "LAB_PRECHECK_ONLY" result does not attest supply-chain integrity,
license validity, CVE safety or S3 IAM effectiveness.
"""
import argparse
import json
import re
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]
DIGEST_PIN = re.compile(r"^quay\.io/minio/aistor/minio@sha256:[a-f0-9]{64}$")
ACK_TEXT = "ACEPTO_SOLO_LAB_SIN_DESPLIEGUE"
# Identidad OCI index exacta verificada en notas de release del proyecto Silo.
# No equivale a comprobar el digest remoto ni a aprobar despliegue.
SILO_PATCHED_IMAGE = "docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62"
SILO_PATCHED_TAG = "RELEASE.2026-10-15T00-00-00Z"
SILO_PATCHED_SOURCE = "7a3ca0f82100cce06289a465f54139da2639f642"


def evaluate(provider, image="", license_file="", acknowledgment=""):
    base = {
        "provider": provider,
        "status": "BLOCKED",
        "scope": "NO_DEPLOYMENT",
    }

    def blocked(reason):
        return {**base, "reason": reason}

    if provider == "minio-community":
        return blocked(
            "MinIO Community upstream no se mantiene; antigua imagen de piloto bloqueada."
        )
    if provider == "silo":
        if image != SILO_PATCHED_IMAGE:
            return blocked(
                "Silo: solo versión corregida 2026-10-15 fijada a digest OCI "
                "aprobado para evaluación; se rechazan 2026-09-16, latest y digests distintos."
            )
        if acknowledgment != ACK_TEXT:
            return blocked("Se requiere aceptación expresa de evaluación SOLO de laboratorio.")
        return {
            **base,
            "status": "LAB_PRECHECK_ONLY",
            "image": SILO_PATCHED_IMAGE,
            "release": SILO_PATCHED_TAG,
            "source_commit": SILO_PATCHED_SOURCE,
            "requirements": [
                "confirmar firma, procedencia, SBOM y digest remoto de Silo 2026-10-15",
                "revisar licencia AGPLv3 y condiciones institucionales",
                "revisar vulnerabilidades y correcciones vigentes al momento de prueba",
                "probar IAM S3 con servidor real y credenciales sintéticas; exigir denegaciones",
                "probar copia de seguridad, restauración, retención y rollback sintéticos",
                "obtener autorización institucional separada antes de despliegue",
            ],
            "warning": "NO certifica origen remoto, seguridad, IAM ni licencia; "
                       "no autoriza producción/UAT/srv01.",
        }
    if provider != "aistor":
        return blocked("Proveedor no evaluado / no incluido en alcance.")
    if not DIGEST_PIN.fullmatch(image):
        return blocked(
            "AIStor exige imagen exacta quay.io/minio/aistor/minio@sha256:<64 hex>; "
            "se prohíben latest y tags no fijados."
        )
    if acknowledgment != ACK_TEXT:
        return blocked("Debe declarar aceptación específica de evaluación SOLO de laboratorio.")
    if not license_file:
        return blocked("AIStor Free requiere licencia local para un recurso de cómputo.")

    file = Path(license_file).expanduser().resolve()
    if file.is_relative_to(PROJECT_ROOT):
        return blocked("Licencias no deben almacenarse dentro del repositorio.")
    if not file.is_file() or file.stat().st_size == 0:
        return blocked("Licencia local inexistente o vacía. No se valida su autenticidad.")

    return {
        **base,
        "status": "LAB_PRECHECK_ONLY",
        "image": image,
        "requirements": [
            "validar licencia y condiciones con fabricante",
            "inspeccionar digest remoto, firma, SBOM y avisos de seguridad",
            "probar IAM S3 y denegación fuera de pmgm-documents con credenciales sintéticas",
            "probar backup/restauración y rollback con datos sintéticos",
            "aprobación separada antes de cualquier despliegue",
        ],
        "warning": "NO certifica IAM, proveedor, licencia ni autoriza producción/UAT/srv01.",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Criba documental S3 #470: no despliega ni valida credenciales reales."
    )
    parser.add_argument("--provider", required=True)
    parser.add_argument("--image", default="")
    parser.add_argument("--license-file", default="")
    parser.add_argument("--ack-lab", default="")
    args = parser.parse_args()
    result = evaluate(
        args.provider, args.image, args.license_file, args.ack_lab
    )
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0 if result["status"] == "LAB_PRECHECK_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
