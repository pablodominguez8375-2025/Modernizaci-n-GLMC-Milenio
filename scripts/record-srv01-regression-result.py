#!/usr/bin/env python3
import argparse
import datetime as dt
import json
from pathlib import Path

FORBIDDEN=("password=","authorization: bearer","access_token","refresh_token","client_secret","private_key")

def main():
    p=argparse.ArgumentParser()
    p.add_argument("path", type=Path)
    p.add_argument("check_id")
    p.add_argument("status", choices=["pending","pass","fail"])
    p.add_argument("--evidence", action="append", default=[])
    p.add_argument("--notes")
    a=p.parse_args()

    data=json.loads(a.path.read_text(encoding="utf-8"))
    item=next((x for x in data.get("checks",[]) if x.get("id")==a.check_id),None)
    if item is None:
        raise SystemExit(f"No existe {a.check_id}")
    for ref in a.evidence:
        lowered=ref.lower()
        if any(marker in lowered for marker in FORBIDDEN):
            raise SystemExit("La referencia de evidencia parece contener un secreto")
    if a.status=="pass" and not a.evidence:
        raise SystemExit("pass requiere al menos --evidence")
    if a.status=="fail" and not (a.notes and a.notes.strip()):
        raise SystemExit("fail requiere --notes")

    item["status"]=a.status
    item["evidence"]=a.evidence
    item["notes"]=a.notes
    statuses=[x["status"] for x in data["checks"]]
    if "fail" in statuses:
        data["result"]["decision"]="fail"
        data["result"]["completedAtUtc"]=None
    elif all(x=="pass" for x in statuses):
        data["result"]["decision"]="pass"
        data["result"]["completedAtUtc"]=dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00","Z")
    else:
        data["result"]["decision"]="pending"
        data["result"]["completedAtUtc"]=None

    a.path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    print(f"{a.check_id} -> {a.status}; resultado global={data['result']['decision']}")

if __name__=="__main__":
    main()
