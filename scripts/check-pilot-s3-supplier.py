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
        return blocked(
            "SN-2026-015: servidor Silo release 2026-09-16 afectado; "
            "no hay release corregida verificada. Se exige reevaluación y nueva revisión."
        )
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
