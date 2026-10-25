#!/usr/bin/env python3
"""External-only QA readiness probe; no credentials, no SSH or host writes.

This is not a deployment checker. Never infer G7/G9/G10 from these results.
Run --probe only on trusted push-to-dev Actions; a failed public dependency
produces BLOCKED evidence but does not invalidate a correctly built package.
"""
import argparse
import datetime
import http.client
import json
import os
from pathlib import Path
import socket
import ssl

HOST = "sistema.granlogiamixta.cl"
ORIGIN = "https://" + HOST
ISSUER = ORIGIN + "/identity/realms/pmgm"
LIMIT = 65536


def request(path):
    # Fixed canonical hostname, TLS peer verification & no redirect following.
    conn = http.client.HTTPSConnection(
        HOST, port=443, timeout=7, context=ssl.create_default_context()
    )
    try:
        conn.request("GET", path, headers={"Accept": "application/json"})
        response = conn.getresponse()
        body = response.read(LIMIT)
        return response.status, dict(response.getheaders()), body
    finally:
        conn.close()


def inspect(probe):
    evidence = {
        "target": ORIGIN,
        "scope": "PUBLIC_NETWORK_FROM_GITHUB_RUNNER_ONLY",
        "physical_deployment": "NOT_VERIFIED",
        "ssh_host": "NOT_VERIFIED",
        "database_roles_on_glmdch": "NOT_VERIFIED",
        "smoke_regression_uat": "NOT_VERIFIED",
        "dns": "NOT_CHECKED",
        "https_validated": "NOT_CHECKED",
        "oidc_issuer": "NOT_CHECKED",
        "admin_and_master_denied": "NOT_CHECKED",
        "decision": "NOT_CHECKED",
        "brand_assets": "NOT_CHECKED",
    }
    if not probe:
        return evidence
    try:
        addresses = socket.getaddrinfo(HOST, 443, type=socket.SOCK_STREAM)
        if not addresses:
            raise OSError("no address")
    except (OSError, ValueError):
        evidence["dns"] = "BLOCKED"
        evidence["decision"] = "BLOCKED_DNS"
        return evidence
    evidence["dns"] = "RESOLVED"
    try:
        status, headers, _ = request("/")
    except (OSError, ssl.SSLError, TimeoutError, http.client.HTTPException):
        evidence["https_validated"] = "BLOCKED"
        evidence["decision"] = "BLOCKED_TLS_OR_HTTPS"
        return evidence
    # Reaching any HTTP status with strict CA validation proves a TLS
    # handshake, NOT application readiness or a successful HTTP route.
    evidence["https_validated"] = "CERTIFICATE_TRUSTED"
    evidence["public_root_http"] = status
    if status != 200:
        evidence["decision"] = "BLOCKED_PUBLIC_ROOT"
        return evidence
    try:
        code, _, data = request("/identity/realms/pmgm/.well-known/openid-configuration")
        payload = json.loads(data) if code == 200 else {}
        exact_issuer = payload.get("issuer") == ISSUER
        endpoints = all(
            isinstance(payload.get(k), str)
            and payload[k].startswith(ISSUER + "/")
            for k in ("authorization_endpoint", "token_endpoint", "jwks_uri")
        )
    except (OSError, ssl.SSLError, TimeoutError, http.client.HTTPException,
            ValueError, UnicodeError):
        exact_issuer = endpoints = False
    evidence["oidc_issuer"] = "PASS" if exact_issuer and endpoints else "BLOCKED"
    if evidence["oidc_issuer"] != "PASS":
        evidence["decision"] = "BLOCKED_OIDC"
        return evidence
    try:
        statuses = [
            request(path)[0]
            for path in ("/identity/admin", "/identity/realms/master",
                         "/identity/realms/master/.well-known/openid-configuration")
        ]
    except (OSError, ssl.SSLError, TimeoutError, http.client.HTTPException):
        statuses = []
    evidence["admin_and_master_denied"] = "PASS" if statuses == [404, 404, 404] else "BLOCKED"
    if evidence["admin_and_master_denied"] != "PASS":
        evidence["decision"] = "BLOCKED_ADMIN_MASTER"
        return evidence

    # #553 P0. Public, static, fixed GET routes: never submit credentials or
    # follow redirects. A healthy SPA/issuer does not prove that brand files
    # are readable by nginx. Only emit HTTP statuses, never asset contents.
    brand_paths = (
        "/brand/logo-glmch-reducido-v2-azul.svg",
        "/brand/favicon-glmch.svg",
        "/brand/favicon-32.png",
        "/brand/apple-touch-icon.png",
    )
    statuses = []
    for path in brand_paths:
        try:
            code, _, _ = request(path)
            statuses.append(code)
        except (OSError, ssl.SSLError, TimeoutError, http.client.HTTPException):
            statuses.append(0)
    evidence["brand_http"] = statuses
    evidence["brand_assets"] = "PASS" if all(code == 200 for code in statuses) else "BLOCKED"
    evidence["decision"] = (
        "PUBLIC_ENTRY_CHECKS_PASS_ONLY"
        if evidence["brand_assets"] == "PASS" else "BLOCKED_BRAND_ASSETS"
    )
    return evidence


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--probe", action="store_true")
    parser.add_argument("--output", required=True)
    parser.add_argument("--source-sha", required=True)
    args = parser.parse_args()
    if len(args.source_sha) != 40 or any(c not in "0123456789abcdef" for c in args.source_sha):
        parser.error("source sha must be lowercase git SHA-1")
    result = inspect(args.probe)
    result["source_sha"] = args.source_sha
    result["checked_at_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
    print("QA PUBLIC PREFLIGHT "
          + result["decision"] + " DNS=" + result["dns"]
          + " TLS=" + result["https_validated"] + " OIDC=" + result["oidc_issuer"])
    print("QA PHYSICAL DEPLOYMENT: NOT_VERIFIED (G7/G9/G10 remain open)")


if __name__ == "__main__":
    main()
