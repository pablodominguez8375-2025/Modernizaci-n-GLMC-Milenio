#!/usr/bin/env python3
import argparse
from datetime import datetime
import json
from pathlib import Path
import re
import sys
from urllib.parse import urlparse

SHA40 = re.compile(r"^[0-9a-f]{40}$", re.IGNORECASE)
EXPECTED_IDS = {f"QA-{i:03d}" for i in range(1, 35)}
ALLOWED = {"pending","pass","fail"}
FORBIDDEN = ("password=", "authorization: bearer", "access_token", "refresh_token", "client_secret", "private_key")

def fail(msg):
    print(f"QA SRV01 REGRESSION GATE FAILED: {msg}")
    raise SystemExit(1)

def valid_ts(value):
    try:
        return datetime.fromisoformat(value.replace("Z","+00:00")).tzinfo is not None
    except Exception:
        return False

def main():
    p=argparse.ArgumentParser()
    p.add_argument("path", type=Path)
    p.add_argument("--allow-pending", action="store_true")
    a=p.parse_args()

    data=json.loads(a.path.read_text(encoding="utf-8"))
    if data.get("schemaVersion") != 1 or data.get("purpose") != "qa-regression-srv01":
        fail("schema/purpose inválido")
    if data.get("formalUatReplacement") is not False:
        fail("formalUatReplacement debe permanecer false")

    sha=data.get("sourceSha")
    if a.allow_pending:
        if sha is not None and not (isinstance(sha,str) and SHA40.fullmatch(sha)):
            fail("sourceSha inválido")
    elif not isinstance(sha,str) or not SHA40.fullmatch(sha):
        fail("sourceSha exacto es obligatorio")

    env=data.get("environment")
    if not isinstance(env,dict) or env.get("name") != "qa-srv01":
        fail("environment.name debe ser qa-srv01")
    base=env.get("baseUrl")
    ts=env.get("executedAtUtc")
    if a.allow_pending:
        if base is not None and not isinstance(base,str):
            fail("baseUrl inválido")
        if ts is not None and not valid_ts(ts):
            fail("executedAtUtc inválido")
    else:
        if not isinstance(base,str):
            fail("baseUrl obligatorio")
        u=urlparse(base)
        if u.scheme not in {"http","https"} or not u.netloc or u.username or u.password:
            fail("baseUrl debe ser http(s) sin credenciales")
        if not isinstance(ts,str) or not valid_ts(ts):
            fail("executedAtUtc obligatorio")

    policy=data.get("dataPolicy")
    expected={"fictionalDataOnly":True,"noSecretsInEvidence":True,"noPersonalDataCommittedToGit":True}
    if policy != expected:
        fail("dataPolicy inválida")

    checks=data.get("checks")
    if not isinstance(checks,list) or len(checks) != 34:
        fail("deben existir exactamente 34 controles")
    seen=set()
    failed=[]
    pending=[]
    for item in checks:
        cid=item.get("id")
        if cid in seen or cid not in EXPECTED_IDS:
            fail(f"id inválido/duplicado: {cid}")
        seen.add(cid)
        if item.get("required") is not True:
            fail(f"{cid}: required debe ser true")
        status=item.get("status")
        if status not in ALLOWED:
            fail(f"{cid}: status inválido")
        ev=item.get("evidence")
        if not isinstance(ev,list):
            fail(f"{cid}: evidence debe ser lista")
        for ref in ev:
            if not isinstance(ref,str) or not ref.strip():
                fail(f"{cid}: evidencia vacía")
            lowered=ref.lower()
            for marker in FORBIDDEN:
                if marker in lowered:
                    fail(f"{cid}: evidencia parece contener secreto")
        if status == "pass" and not ev:
            fail(f"{cid}: pass requiere evidencia")
        if status == "fail":
            failed.append(cid)
            if not isinstance(item.get("notes"),str) or not item["notes"].strip():
                fail(f"{cid}: fail requiere notes")
        if status == "pending":
            pending.append(cid)

    if seen != EXPECTED_IDS:
        fail("faltan controles QA")

    result=data.get("result")
    if not isinstance(result,dict) or result.get("decision") not in {"pending","pass","fail"}:
        fail("result.decision inválido")

    if a.allow_pending:
        print(f"QA SRV01 TEMPLATE OK: 34 controles; pending={len(pending)}, failed={len(failed)}")
        return 0

    if failed:
        fail("existen controles fallidos: "+", ".join(failed))
    if pending:
        fail("existen controles pendientes: "+", ".join(pending))
    if result.get("decision") != "pass":
        fail("result.decision debe ser pass")
    if not valid_ts(result.get("completedAtUtc","")):
        fail("completedAtUtc obligatorio")
    print("QA SRV01 REGRESSION PASS: 34/34 controles")
    return 0

if __name__ == "__main__":
    sys.exit(main())
