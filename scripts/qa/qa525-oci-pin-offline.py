"""Offline OCI manifest/config identity check; no Docker, network or authority."""
import argparse
import hashlib
import json
import re
from pathlib import Path

def digest(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()

def parse(raw):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError("DUPLICATE_JSON_KEY")
            result[key] = value
        return result
    def reject_constant(value):
        raise ValueError("NONFINITE_JSON_NUMBER")
    return json.loads(raw, object_pairs_hook=pairs, parse_constant=reject_constant)

def verify(manifest, config, expected_manifest, expected_revision=None):
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", expected_manifest):
        raise ValueError("EXPECTED_MANIFEST_INVALID")
    if digest(manifest) != expected_manifest:
        raise ValueError("MANIFEST_DIGEST_MISMATCH")
    m, c = parse(manifest), parse(config)
    if type(m) is not dict or type(c) is not dict:
        raise ValueError("OCI_TOP_LEVEL_TYPE")
    if type(m.get("schemaVersion")) is not int or m["schemaVersion"] != 2 or m.get("mediaType") not in (
        "application/vnd.oci.image.manifest.v1+json",
        "application/vnd.docker.distribution.manifest.v2+json"):
        raise ValueError("NOT_SINGLE_IMAGE_MANIFEST")
    desc = m["config"]
    if desc.get("mediaType") not in ("application/vnd.oci.image.config.v1+json",
                                    "application/vnd.docker.container.image.v1+json"):
        raise ValueError("CONFIG_MEDIA_TYPE")
    if type(desc.get("size")) is not int or desc["size"] != len(config) or desc["digest"] != digest(config):
        raise ValueError("CONFIG_DESCRIPTOR_MISMATCH")
    revision = (c.get("config", {}).get("Labels") or {}).get("org.opencontainers.image.revision")
    if expected_revision is not None and revision != expected_revision:
        raise ValueError("REVISION_MISMATCH")
    return {"manifest_digest": digest(manifest), "config_digest": digest(config),
            "revision": revision, "status": "CONTENT_IDENTITY_ONLY", "may_apply": False}

def read(path):
    with Path(path).open("rb") as f:
        raw = f.read(8 * 1024 * 1024 + 1)
    if len(raw) > 8 * 1024 * 1024:
        raise ValueError("INPUT_TOO_LARGE")
    return raw

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("manifest")
    p.add_argument("config")
    p.add_argument("--expected-manifest", required=True)
    p.add_argument("--expected-revision")
    a = p.parse_args()
    try:
        result = verify(read(a.manifest), read(a.config), a.expected_manifest, a.expected_revision)
    except (ValueError, OSError, KeyError, TypeError, AttributeError, RecursionError):
        p.exit(2, "OCI_PIN=STOP\n")
    print(json.dumps(result, sort_keys=True))

if __name__ == "__main__":
    main()
