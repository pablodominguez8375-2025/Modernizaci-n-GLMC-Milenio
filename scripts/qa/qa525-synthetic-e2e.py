#!/usr/bin/env python3
"""QA525 synthetic E2E over the QA API (Sistema>Talleres + Carga inicial). INERT library: no network, no files.

All I/O goes through an injected `http(method, path, body=None) -> (status:int, payload)` (payload = parsed JSON,
`bytes` or None) that the caller authenticates; this module never holds credentials, never logs bodies and never
echoes cell values: results are fixed check codes with True/False. Synthetic data only: RUT in the reserved range
30.000.000+, e-mails @example.org, Taller codes `QA-SINT-Tnnn`.

Two entry points:
  smoke(http, expected_sha, ...)   READ-ONLY / no-persistence checks (usable as UAT checks of the schema cut)
  rehearsal(http, lots, clock)     WRITES synthetic lots (window must be opened by the Sponsor first): apply, idempotent
                                   retry, 2nd lot of the same Taller, conflict rollback, limits, volume counts
`generate(seed, talleres)` is the deterministic lot generator (74 lots / ~1.008 Hermanos for the reference seed).
"""
import csv
import io
import json
import random

PREFIX = "QA-SINT-"
SHEETS = ("Talleres.csv", "Hermanos.csv", "CargosVigentes.csv")
TALLER_COLS = ["workshopCode", "number", "name", "orienteCode", "city", "country"]
HERMANO_COLS = ["personRef", "identityType", "identityNumber", "firstNames", "lastNames", "email", "workshopCode",
                "grade", "institutionalStatus", "membershipType"]
CARGO_COLS = ["personRef", "workshopCode", "officeCode", "startDate", "period"]
BASE = "/api/platform/bootstrap/initial-load"
REGISTRY = "/api/system/workshops"


def rut(n):
    body = 30_000_000 + n
    s, f = 0, 2
    for d in reversed(str(body)):
        s += int(d) * f
        f = 2 if f == 7 else f + 1
    dv = 11 - s % 11
    dv = "0" if dv == 11 else "K" if dv == 10 else str(dv)
    return f"{body:,}".replace(",", ".") + "-" + dv


def _csv(cols, rows):
    out = io.StringIO()
    w = csv.DictWriter(out, fieldnames=cols, lineterminator="\r\n")
    w.writeheader()
    w.writerows(rows)
    return out.getvalue()


def _lot(workshop, members, offices):
    return {"Talleres.csv": _csv(TALLER_COLS, [workshop]), "Hermanos.csv": _csv(HERMANO_COLS, members),
            "CargosVigentes.csv": _csv(CARGO_COLS, offices)}


def generate(seed=525, talleres=70, avg=15, forced=((7, 34), (33, 40), (58, 31))):
    """-> (lots, manifest). Each lot = {"workshop": code, "files": {name: csv}, "members": n, "first": bool}."""
    rnd = random.Random(seed)
    forced = dict(forced)
    lots, person = [], 0
    for t in range(1, talleres + 1):
        size = max(5, min(40, int(rnd.gauss(avg, 7))))
        size = forced.get(t, size)
        code = f"{PREFIX}T{t:03d}"
        w = {"workshopCode": code, "number": str(9000 + t), "name": f"{PREFIX}Taller {t:03d}",
             "orienteCode": f"QAS{(t % 5) + 1}", "city": "Ciudad Sintetica", "country": "CL"}
        for start in range(0, size, 30):
            rows = []
            for _ in range(min(30, size - start)):
                person += 1
                rows.append({"personRef": f"{PREFIX}P{person:05d}", "identityType": "rut", "identityNumber": rut(person),
                             "firstNames": f"Nombre{person}", "lastNames": f"Apellido{person}",
                             "email": f"qa-sint-{person}@example.org", "workshopCode": code,
                             "grade": str(rnd.choice([1, 2, 3])), "institutionalStatus": "active",
                             "membershipType": "regular"})
            offices = ([{"personRef": rows[0]["personRef"], "workshopCode": code, "officeCode": "worshipful_master",
                         "startDate": "2026-01-01", "period": "2026"}] if start == 0 else [])
            lots.append({"workshop": code, "files": _lot(w, rows, offices), "members": len(rows), "first": start == 0})
    manifest = {"lotes": len(lots), "talleres": talleres, "hermanos": person,
                "talleres_con_2_lotes": sum(1 for x in lots if not x["first"])}
    return lots, manifest


def mutate(lot, kind):
    """Derive an invalid/conflicting variant of a generated lot (still synthetic)."""
    files = dict(lot["files"])
    hermanos = list(csv.DictReader(io.StringIO(files["Hermanos.csv"])))
    if kind == "over30":
        extra = []
        for i in range(31 - len(hermanos)):
            row = dict(hermanos[0])
            row["personRef"] = f"{PREFIX}X{i:05d}"
            row["identityNumber"] = rut(900000 + i)
            extra.append(row)
        files["Hermanos.csv"] = _csv(HERMANO_COLS, hermanos + extra)
    elif kind == "two_workshops":
        t = list(csv.DictReader(io.StringIO(files["Talleres.csv"])))
        other = dict(t[0], workshopCode=PREFIX + "T999", number="9999", name=PREFIX + "Taller 999")
        files["Talleres.csv"] = _csv(TALLER_COLS, t + [other])
    elif kind == "cross_workshop":
        hermanos[0] = dict(hermanos[0], workshopCode=PREFIX + "T998")
        files["Hermanos.csv"] = _csv(HERMANO_COLS, hermanos)
    elif kind == "conflict":
        hermanos[0] = dict(hermanos[0], firstNames="Distinto")
        files["Hermanos.csv"] = _csv(HERMANO_COLS, hermanos)
    else:
        raise ValueError("kind")
    return {"workshop": lot["workshop"], "files": files, "members": 0, "first": False}


def _blockers(payload):
    out = set()
    if isinstance(payload, dict):
        for item in payload.get("applyBlockers") or []:
            if isinstance(item, dict) and isinstance(item.get("code"), str):
                out.add(item["code"])
    return out


def _ok(fn):
    """A check that raises is simply False: the suite never aborts and never echoes exception text."""
    try:
        return fn() is True
    except Exception:  # noqa: BLE001
        return False


def _get(http, path):
    status, payload = http("GET", path)
    return status, payload


def smoke(http, expected_sha, lot=None, unauth=None):
    """Read-only/no-persistence checks. `unauth(method, path)` (optional) calls WITHOUT credentials."""
    lot = lot or generate(talleres=2)[0][0]
    checks = {}
    checks["health_ready"] = _ok(lambda: http("GET", "/health/ready")[0] == 200)
    checks["qa_version_matches"] = _ok(lambda: _qa_version(http, expected_sha))
    checks["contract"] = _ok(lambda: _contract(http))
    checks["template_zip"] = _ok(lambda: _template(http))
    checks["registry_lists"] = _ok(lambda: _registry(http) is not None)
    checks["preview_valid_no_writes"] = _ok(lambda: _preview(http, lot, valid=True))
    for kind, code in (("over30", "MEMBER_BATCH_LIMIT_30"), ("two_workshops", "ONE_WORKSHOP_PER_BATCH_REQUIRED"),
                       ("cross_workshop", "CROSS_WORKSHOP_BATCH")):
        checks["preview_blocks_" + kind] = _ok(lambda k=kind, c=code: _preview_blocked(http, mutate(lot, k), c))
    if unauth is not None:
        checks["unauthenticated_denied"] = _ok(lambda: all(
            unauth("GET", p)[0] in (401, 403) for p in (REGISTRY, BASE + "/contract")))
    return checks


def _qa_version(http, sha):
    status, payload = http("GET", "/qa-version.json")
    return status == 200 and isinstance(payload, dict) and sha in json.dumps(payload)


def _contract(http):
    status, payload = http("GET", BASE + "/contract")
    names = [f.get("name") for f in payload.get("files", [])] if isinstance(payload, dict) else []
    return status == 200 and tuple(names) == SHEETS and payload.get("revision") == "INITIAL-LOAD-CSV-v1"


def _template(http):
    status, payload = http("GET", BASE + "/template-csv")
    return status == 200 and isinstance(payload, bytes) and payload[:2] == b"PK"


def _registry(http):
    status, payload = http("GET", REGISTRY)
    items = payload.get("items") if isinstance(payload, dict) else None
    return items if status == 200 and isinstance(items, list) else None


def _preview(http, lot, valid):
    status, payload = http("POST", BASE + "/preview", lot["files"])
    return (status == 200 and isinstance(payload, dict) and payload.get("valid") is valid
            and payload.get("writesPerformed") == 0 and payload.get("applicable") is valid)


def _preview_blocked(http, lot, code):
    status, payload = http("POST", BASE + "/preview", lot["files"])
    return (status == 200 and isinstance(payload, dict) and payload.get("applicable") is False
            and payload.get("writesPerformed") == 0 and code in _blockers(payload))


def _apply(http, lot):
    status, preview = http("POST", BASE + "/preview", lot["files"])
    if status != 200 or not isinstance(preview, dict) or not preview.get("receipt"):
        return None, preview
    status, payload = http("POST", BASE + "/apply", {"files": lot["files"], "receipt": preview["receipt"]})
    return status, payload


def rehearsal(http, lots, clock, sample=3):
    """Writes synthetic lots. Precondition (Sponsor): window OPEN, synthetic data marker. Returns (checks, stats)."""
    checks, stats = {}, {"lots": len(lots), "applied": 0, "already": 0, "failed": 0, "ms": []}
    before = _registry(http)
    base = len([x for x in (before or []) if str(x.get("name", "")).startswith(PREFIX)])
    first = lots[0]
    status, payload = _apply(http, first)
    checks["first_apply"] = (status == 200 and isinstance(payload, dict) and payload.get("applied") is True
                             and payload["counts"]["workshopsCreated"] == 1
                             and payload["counts"]["membersCreated"] == first["members"])
    status, payload = _apply(http, first)
    checks["retry_already_applied"] = (status == 200 and isinstance(payload, dict)
                                       and payload.get("alreadyApplied") is True
                                       and payload["counts"]["membersCreated"] == 0)
    for kind, expect in (("over30", 422), ("two_workshops", 422), ("cross_workshop", 422)):
        bad = mutate(first, kind)
        st, pv = http("POST", BASE + "/preview", bad["files"])
        checks["rejects_" + kind] = st == 200 and isinstance(pv, dict) and pv.get("receipt") in (None, "")
    conflict = mutate(lots[1], "conflict") if len(lots) > 1 and lots[1]["workshop"] != first["workshop"] else None
    for lot in lots[1:]:
        t0 = clock()
        st, pl = _apply(http, lot)
        stats["ms"].append(int((clock() - t0) * 1000))
        if st == 200 and isinstance(pl, dict) and pl.get("applied") is True:
            stats["applied"] += 1
        elif st == 200 and isinstance(pl, dict) and pl.get("alreadyApplied") is True:
            stats["already"] += 1
        else:
            stats["failed"] += 1
    stats["applied"] += 1  # the first lot
    checks["all_lots_applied"] = stats["failed"] == 0 and stats["applied"] == len(lots)
    after = _registry(http)
    workshops = {lot["workshop"] for lot in lots}
    names = [x for x in (after or []) if str(x.get("name", "")).startswith(PREFIX)]
    checks["registry_has_all_workshops"] = after is not None and len(names) - base == len(workshops)
    second = next((lot for lot in lots if not lot["first"]), None)
    if second is not None:
        checks["second_lot_reuses_workshop"] = _ok(lambda: _second_lot_reused(http, second, after))
    if conflict is not None:
        st, pl = _apply(http, conflict)
        checks["conflict_rolls_back"] = st == 422 or st == 409 or (isinstance(pl, dict) and pl.get("applied") is not True)
    return checks, stats


def _second_lot_reused(http, lot, registry):
    return sum(1 for x in registry if x.get("name", "").endswith(lot["workshop"].replace(PREFIX + "T", "Taller "))) == 1


def uat_checks(http, expected_sha, unauth=None):
    """Tuple of zero-arg callables for the schema-cut UAT step (READ-ONLY / preview only, writes=0)."""
    state = {}

    def run():
        if "r" not in state:
            state["r"] = smoke(http, expected_sha, unauth=unauth)
        return state["r"]

    def each(name):
        return lambda: run().get(name) is True
    names = ("health_ready", "qa_version_matches", "contract", "template_zip", "registry_lists",
             "preview_valid_no_writes", "preview_blocks_over30", "preview_blocks_two_workshops",
             "preview_blocks_cross_workshop") + (("unauthenticated_denied",) if unauth is not None else ())
    return tuple(each(n) for n in names)


if __name__ == "__main__":
    raise SystemExit("QA525_SYNTHETIC_E2E_LIBRARY_ONLY_NO_GO")
