#!/usr/bin/env python3
"""Reject unexpected fields before storing remote output or adding summary text."""
import datetime
import json
import re
import sys
from pathlib import Path

SERVICES = {'ssh', 'ufw', 'docker', 'containerd'}
PACKAGES = {'docker-ce', 'docker.io', 'docker-compose-plugin', 'containerd.io', 'podman'}
FIELDS = {'schema', 'helper_version', 'helper_sha256', 'timestamp', 'hostname', 'cpu_count',
          'mem_total_kib', 'opt_total_bytes', 'opt_available_bytes', 'services', 'packages',
          'listeners', 'firewall', 'qa'}


def validate(raw, expected_hash):
    if len(raw) > 65536:
        raise ValueError('size')
    data = json.loads(raw)
    if set(data) != FIELDS or data['schema'] != 1 or type(data['schema']) is not int:
        raise ValueError('schema')
    if data['hostname'] != 'glmdch' or data['helper_version'] != 'centenario-qa-readonly-v1':
        raise ValueError('identity')
    if not re.fullmatch('[0-9a-f]{64}', expected_hash) or data['helper_sha256'] != expected_hash:
        raise ValueError('helper hash')
    datetime.datetime.strptime(data['timestamp'], '%Y-%m-%dT%H:%M:%SZ')
    for key in ('cpu_count', 'mem_total_kib', 'opt_total_bytes', 'opt_available_bytes'):
        if type(data[key]) is not int or not 0 <= data[key] <= 2**63 - 1:
            raise ValueError('metrics')
    if not data['cpu_count'] or not data['mem_total_kib'] or data['opt_available_bytes'] > data['opt_total_bytes']:
        raise ValueError('capacity')
    if set(data['services']) != SERVICES or any(v not in ('active', 'inactive', 'failed', 'unknown') for v in data['services'].values()):
        raise ValueError('services')
    if set(data['packages']) != PACKAGES or any(type(v) is not bool for v in data['packages'].values()):
        raise ValueError('packages')
    if type(data['listeners']) is not list or len(data['listeners']) > 512:
        raise ValueError('listeners')
    for item in data['listeners']:
        if (set(item) != {'protocol', 'port', 'scope'} or item['protocol'] not in ('tcp', 'udp')
                or type(item['port']) is not int or not 1 <= item['port'] <= 65535
                or item['scope'] not in ('loopback', 'wildcard', 'specific')):
            raise ValueError('listener')
    if data['firewall'] != 'NO_VERIFICADO_REQUIERE_OPERADOR' or data['qa'] != 'NO_VERIFICADO':
        raise ValueError('readiness')
    return data


if __name__ == '__main__':
    try:
        data = validate(Path(sys.argv[1]).read_bytes(), sys.argv[3])
        Path(sys.argv[2]).write_text(json.dumps(data, indent=2) + '\n')
    except Exception:
        sys.exit('Invalid QA report (remote content suppressed)')
