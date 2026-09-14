#!/usr/bin/env python3
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

MANIFEST = Path("release/PMGM-RELEASE-1.0.0-rc1.json")
PROGRAM = Path("backend/src/PMGM.Api/Program.cs")
CI_WORKFLOW = Path(".github/workflows/ci.yml")
SEMVER_RC = re.compile(r"^\d+\.\d+\.\d+-rc\d+$")
FORBIDDEN_BOOTSTRAP_KEYS = {
    "rut",
    "run",
    "email",
    "phone",
    "telefono",
    "address",
    "direccion",
    "birthdate",
    "fecha_nacimiento",
    "firstname",
    "firstnames",
    "lastname",
    "lastnames",
    "person",
    "member",
    "username",
    "password",
    "secret",
    "credential",
    "credentials",
    "token",
}


def fail(message: str) -> None:
    print(f"RELEASE GATE FAILED: {message}")
    raise SystemExit(1)


def load_json(path: Path) -> dict:
    if not path.exists():
        fail(f"No existe {path}.")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"No fue posible leer {path}: {exc}")
    if not isinstance(value, dict):
        fail(f"{path} debe contener un objeto JSON.")
    return value


def require_file(path: Path) -> str:
    if not path.exists():
        fail(f"No existe {path}.")
    return path.read_text(encoding="utf-8")


def normalized_key(value: str) -> str:
    return re.sub(r"[^a-z0-9_]", "", value.lower())


def scan_bootstrap(value, trail: str = "$") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            key_normalized = normalized_key(str(key))
            if key_normalized in FORBIDDEN_BOOTSTRAP_KEYS:
                fail(f"El bootstrap contiene un campo personal/secreto no permitido: {trail}.{key}")
            scan_bootstrap(child, f"{trail}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            scan_bootstrap(child, f"{trail}[{index}]")


def main() -> int:
    manifest = load_json(MANIFEST)

    required_manifest_fields = {
        "schemaVersion",
        "releaseVersion",
        "releaseChannel",
        "target",
        "sourceBranch",
        "targetBranch",
        "apiProject",
        "pilotCompose",
        "pilotRealm",
        "bootstrapPackage",
        "requiredCiJobs",
        "machineRequirements",
        "stablePromotionRequires",
    }
    missing = sorted(required_manifest_fields - manifest.keys())
    if missing:
        fail(f"El manifest omite campos obligatorios: {', '.join(missing)}")

    version = str(manifest["releaseVersion"]).strip()
    if not SEMVER_RC.fullmatch(version):
        fail(f"releaseVersion no es una release candidate SemVer válida: {version}")
    if manifest["releaseChannel"] != "release_candidate":
        fail("releaseChannel debe ser release_candidate para esta rama.")
    if manifest["target"] != "pilot_operational":
        fail("target debe ser pilot_operational.")
    if manifest["sourceBranch"] != "release/v1.0-rc1" or manifest["targetBranch"] != "dev":
        fail("Las ramas declaradas para v1.0-rc1 no coinciden con el flujo aprobado.")

    csproj = Path(str(manifest["apiProject"]))
    if not csproj.exists():
        fail(f"No existe el proyecto API declarado: {csproj}")
    try:
        project = ET.parse(csproj).getroot()
    except (ET.ParseError, OSError) as exc:
        fail(f"No fue posible analizar {csproj}: {exc}")
    version_node = project.find(".//Version")
    project_version = version_node.text.strip() if version_node is not None and version_node.text else ""
    if project_version != version:
        fail(f"Versión csproj {project_version!r} != manifest {version!r}.")

    program = require_file(PROGRAM)
    if "AssemblyInformationalVersionAttribute" not in program or "version = apiVersion" not in program:
        fail("/api/system/info debe obtener la versión desde AssemblyInformationalVersion.")
    if re.search(r"\bversion\s*=\s*\"\d+\.\d+", program):
        fail("Program.cs volvió a introducir una versión API hardcodeada.")

    compose_path = Path(str(manifest["pilotCompose"]))
    compose = require_file(compose_path)
    if not re.search(r"DemoData__Enabled:\s*[\"']?false[\"']?", compose, flags=re.IGNORECASE):
        fail("El piloto debe mantener DemoData__Enabled=false.")
    if not re.search(r"VITE_USE_MOCKS:\s*[\"']?false[\"']?", compose, flags=re.IGNORECASE):
        fail("El frontend del piloto debe mantener VITE_USE_MOCKS=false.")

    realm_path = Path(str(manifest["pilotRealm"]))
    realm = load_json(realm_path)
    if realm.get("sslRequired") != "external":
        fail("El realm piloto debe exigir sslRequired=external.")
    clients = [x for x in realm.get("clients", []) if x.get("clientId") == "pmgm-web"]
    if len(clients) != 1:
        fail("El realm piloto debe contener exactamente un cliente pmgm-web.")
    client = clients[0]
    if client.get("standardFlowEnabled") is not True:
        fail("pmgm-web debe mantener Authorization Code Flow habilitado.")
    if client.get("directAccessGrantsEnabled") is not False:
        fail("pmgm-web no puede habilitar password grant en piloto.")
    if client.get("attributes", {}).get("pkce.code.challenge.method") != "S256":
        fail("pmgm-web debe mantener PKCE S256.")
    users = realm.get("users", [])
    if len(users) != 1:
        fail("El realm piloto versionado sólo debe declarar el bootstrap admin parametrizado.")
    admin = users[0]
    if "platform_superadmin" not in admin.get("realmRoles", []):
        fail("El administrador inicial del piloto debe poseer platform_superadmin.")
    if admin.get("attributes", {}).get("pmgm_scope") != ["order"]:
        fail("El Superadmin inicial debe operar con pmgm_scope=order.")
    credential_values = [str(c.get("value", "")) for c in admin.get("credentials", [])]
    if credential_values != ["${PMGM_PILOT_ADMIN_PASSWORD}"]:
        fail("El realm piloto no puede versionar una contraseña fija.")

    bootstrap_path = Path(str(manifest["bootstrapPackage"]))
    bootstrap = load_json(bootstrap_path)
    scan_bootstrap(bootstrap)
    if bootstrap.get("institution", {}).get("name") != "Gran Logia Mixta de Chile":
        fail("El paquete de bootstrap no identifica la institución matriz aprobada.")
    workshops = bootstrap.get("workshops", [])
    if not any(str(item.get("number")) == "23" and "Libertad" in str(item.get("name", "")) for item in workshops):
        fail("El paquete de bootstrap debe incluir Respetable Logia Libertad Nº 23.")

    workflow = require_file(CI_WORKFLOW)
    required_jobs = manifest.get("requiredCiJobs")
    if not isinstance(required_jobs, list) or len(required_jobs) != 5:
        fail("requiredCiJobs debe declarar exactamente los cinco jobs críticos del piloto.")
    absent_jobs = [name for name in required_jobs if str(name) not in workflow]
    if absent_jobs:
        fail(f"Hay jobs críticos no presentes en CI: {', '.join(absent_jobs)}")
    for gate_name in (
        "Privacy gate — Ley 21.719",
        "Data classification gate",
        "Migration safety gate",
    ):
        if gate_name not in workflow:
            fail(f"CI ya no contiene el gate obligatorio: {gate_name}")

    machine_requirements = manifest.get("machineRequirements")
    if not isinstance(machine_requirements, dict) or not machine_requirements:
        fail("machineRequirements debe ser un objeto no vacío.")
    false_requirements = [key for key, value in machine_requirements.items() if value is not True]
    if false_requirements:
        fail(f"Todos los requisitos automatizables de la RC deben estar activos: {', '.join(false_requirements)}")

    stable_requirements = manifest.get("stablePromotionRequires")
    if not isinstance(stable_requirements, list) or len(stable_requirements) < 5:
        fail("stablePromotionRequires debe documentar las condiciones manuales para promover a v1.0 estable.")

    print(
        "Release gate OK: "
        f"{version} coherente; piloto sin mocks/demo; OIDC PKCE; Superadmin parametrizado; "
        "bootstrap sin datos personales y cinco jobs CI declarados."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
