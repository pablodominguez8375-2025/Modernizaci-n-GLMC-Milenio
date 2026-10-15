#!/usr/bin/env python3
"""Read-only Docker Compose evidence gate for pilot Caddy → nginx/web → API.

Does NOT start/recreate containers, write env files, leak secrets, certify an
external client IP, or authorize QA/UAT/srv01. Issue #470.
"""
import argparse
import ipaddress
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PILOT = ROOT / "infrastructure/docker-compose.pilot.yml"
DB2 = ROOT / "infrastructure/docker-compose.pilot-db2.yml"
SERVICES = ("api", "web", "gateway")
HEADER_ENV = "ForwardedHeaders__KnownProxies"


def blocked(reason, required=None):
    result = {
        "status": "BLOCKED",
        "scope": "NO_QA_CERTIFICATION",
        "reason": reason,
    }
    if required is not None:
        result["_local_required"] = required
    return result


def _private_ipv4(raw):
    try:
        address = ipaddress.ip_address(raw)
    except ValueError:
        return None
    return str(address) if isinstance(address, ipaddress.IPv4Address) and (
        address.is_private and not address.is_loopback and not address.is_unspecified
        and not address.is_multicast and not address.is_reserved
    ) else None


def evaluate(compose_config, inspected):
    """Pure function. No network, file or Docker access; inspected keyed by service."""
    try:
        compose_env = compose_config["services"]["api"]["environment"]
        services = {name: inspected[name] for name in SERVICES}
        for name in SERVICES:
            if services[name]["State"]["Running"] is not True:
                return blocked(f"El contenedor {name} no está en ejecución.")
            if not services[name]["NetworkSettings"]["Networks"]:
                return blocked(f"El contenedor {name} no tiene red Docker.")

        shared = set(services["api"]["NetworkSettings"]["Networks"])
        for name in ("web", "gateway"):
            shared &= set(services[name]["NetworkSettings"]["Networks"])
        if len(shared) != 1:
            return blocked("Se espera exactamente una red privada común a api, web y gateway.")
        network = next(iter(shared))
        ips = {}
        for name in SERVICES:
            ip = _private_ipv4(
                services[name]["NetworkSettings"]["Networks"][network]["IPAddress"]
            )
            if ip is None:
                return blocked(f"IP de {name} inválida o no privada dentro del piloto.")
            ips[name] = ip
        if len(set(ips.values())) != 3:
            return blocked("api, web y gateway deben tener direcciones diferentes.")

        required = {ips["web"], ips["gateway"]}
        configured = compose_env.get(HEADER_ENV, "")
        if not isinstance(configured, str):
            return blocked("La configuración de los proxies no es texto.")
        tokens = [part.strip() for part in configured.split(",")] if configured else []
        parsed = [_private_ipv4(part) for part in tokens]
        if (len(parsed) != 2 or None in parsed or len(set(parsed)) != 2
                or set(parsed) != required):
            return blocked(
                "Las dos IPs exactas configuradas no coinciden con web/nginx y gateway/Caddy.",
                [ips["web"], ips["gateway"]],
            )

        running_env = services["api"].get("Config", {}).get("Env", [])
        if not isinstance(running_env, list):
            return blocked("No es posible leer la configuración en memoria de la API.")
        actual = [v.split("=", 1)[1] for v in running_env
                  if isinstance(v, str) and v.startswith(HEADER_ENV + "=")]
        actual_parsed = [part.strip() for part in actual[0].split(",")] if len(actual) == 1 else []
        if len(actual_parsed) != 2 or set(actual_parsed) != required:
            return blocked(
                "La API en ejecución no utiliza las IPs exactas declaradas en Compose; "
                "posible contenedor antiguo. No reiniciar automáticamente.",
                [ips["web"], ips["gateway"]],
            )
        return {
            "status": "READY_FOR_AUDIT_ACCEPTANCE",
            "scope": "NO_QA_CERTIFICATION",
            "trusted_proxy_count": 2,
            "audit_evidence_pending": True,
            "warning": "Solo verifica topología y configuración local, NO la IP real de un evento.",
        }
    except (KeyError, TypeError, AttributeError, ValueError, IndexError):
        return blocked("Estructura incompleta o inesperada de Compose/inspect.")


def _run(command, timeout=30):
    try:
        p = subprocess.run(command, capture_output=True, text=True, timeout=timeout,
                           check=False)
    except (OSError, subprocess.TimeoutExpired):
        raise RuntimeError("Docker no disponible o tiempo máximo excedido.") from None
    if p.returncode:
        # Never expose subprocess stderr; docker compose config may contain secrets.
        raise RuntimeError("No se pudo consultar Docker/Compose; revisar solo en terminal local.")
    return p.stdout


def inspect_running_pilot(env_file, bd2=False):
    env = Path(env_file).expanduser().resolve()
    if not env.is_file():
        return blocked("No se encontró el archivo local del entorno piloto.")
    compose = ["docker", "compose", "--env-file", str(env), "-f", str(PILOT)]
    if bd2:
        compose += ["-f", str(DB2)]
    try:
        config = json.loads(_run(compose + ["config", "--format", "json"]))
        ids = {}
        for service in SERVICES:
            cid = _run(compose + ["ps", "--status", "running", "-q", service]).strip()
            if not cid or len(cid.splitlines()) != 1:
                return blocked(f"Debe existir exactamente un contenedor {service} en ejecución.")
            ids[service] = cid
        entries = json.loads(_run(["docker", "inspect", *ids.values()]))
        by_id = {item["Id"]: item for item in entries}
        inspected = {}
        for service, cid in ids.items():
            matches = [item for key, item in by_id.items() if key.startswith(cid)]
            if len(matches) != 1:
                return blocked("No fue posible resolver de forma única un contenedor.")
            inspected[service] = matches[0]
        return evaluate(config, inspected)
    except (RuntimeError, ValueError, KeyError, TypeError) as error:
        return blocked(str(error) if isinstance(error, RuntimeError)
                       else "Respuesta Docker no es JSON/estructura válida.")


def main():
    parser = argparse.ArgumentParser(
        description="Valida en solo lectura las IPs Caddy/nginx/API; no certifica auditoría."
    )
    parser.add_argument("--env-file", default=str(ROOT / "infrastructure/.env.pilot.local"))
    parser.add_argument("--bd2", action="store_true",
                        help="Añade solo el overlay nuevo PostgreSQL BD2.")
    parser.add_argument("--show-required-local", action="store_true",
                        help="Mostrar en terminal local IPs internas para configurar en otro paso.")
    args = parser.parse_args()
    result = inspect_running_pilot(args.env_file, args.bd2)
    # Never show secrets, paths to env files or full Docker inspect JSON.
    suggestion = result.pop("_local_required", None)
    if args.show_required_local and suggestion:
        result["local_only_suggestion"] = (
            "PMGM_PILOT_TRUSTED_PROXY_IPS=" + ",".join(suggestion)
        )
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0 if result["status"] == "READY_FOR_AUDIT_ACCEPTANCE" else 1


if __name__ == "__main__":
    raise SystemExit(main())
