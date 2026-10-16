"""CVE-2026-39414 binary regression in an isolated real Silo instance.

Small synthetic records only; no load/DoS test. COUNT(*) keeps output tiny so
OverMaxRecordSize proves rejection of input rather than a large output record.
"""
import datetime
import hashlib
import json
import os
from pathlib import Path
import secrets
import shutil
import struct
import tempfile
import unittest
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zlib

from test_silo_real_iam import BUCKET, IMAGE, POLICY, run, s3_request, signature
from test_silo_real_recovery import _provision, _start


def event_headers(data):
    result = {}
    offset = 0
    while offset < len(data):
        size = data[offset]
        offset += 1
        name = data[offset:offset + size].decode('ascii')
        offset += size
        kind = data[offset]
        offset += 1
        if kind != 7:
            raise ValueError('unexpected non-string event header')
        length = struct.unpack_from('!H', data, offset)[0]
        offset += 2
        value = data[offset:offset + length].decode('utf-8')
        offset += length
        result[name] = value
    return result


def select_events(data):
    frames = []
    offset = 0
    while offset < len(data):
        if len(data) - offset < 16:
            raise ValueError('truncated event prelude')
        total, headers, prelude_crc = struct.unpack_from('!III', data, offset)
        if total < 16 or headers > total - 16 or offset + total > len(data):
            raise ValueError('invalid event length')
        frame = data[offset:offset + total]
        if zlib.crc32(frame[:8]) != prelude_crc:
            raise ValueError('invalid prelude CRC')
        if zlib.crc32(frame[:-4]) != struct.unpack_from('!I', frame, total - 4)[0]:
            raise ValueError('invalid message CRC')
        frames.append(event_headers(frame[12:12 + headers]))
        offset += total
    if not frames:
        raise ValueError('no event frames')
    return frames


def select_count(ip, key, user, secret, kind):
    host = ip + ':9000'
    now = datetime.datetime.now(datetime.timezone.utc)
    stamp = now.strftime('%Y%m%dT%H%M%SZ')
    day = now.strftime('%Y%m%d')
    scope = day + '/us-east-1/s3/aws4_request'
    query = 'select=&select-type=2'
    uri = '/' + urllib.parse.quote(BUCKET + '/' + key, safe='/-_.~')
    serialization = ('<CSV><FileHeaderInfo>NONE</FileHeaderInfo></CSV>' if kind == 'csv'
                     else '<JSON><Type>LINES</Type></JSON>')
    body = ('<SelectObjectContentRequest xmlns="http://s3.amazonaws.com/doc/2006-03-01/">'
            '<Expression>SELECT COUNT(*) FROM S3Object s</Expression><ExpressionType>SQL</ExpressionType>'
            '<InputSerialization><CompressionType>NONE</CompressionType>' + serialization +
            '</InputSerialization><OutputSerialization><JSON/></OutputSerialization>'
            '</SelectObjectContentRequest>').encode()
    payload_hash = hashlib.sha256(body).hexdigest()
    canonical_headers = ('host:' + host + '\nx-amz-content-sha256:' + payload_hash +
                         '\nx-amz-date:' + stamp + '\n')
    signed = 'host;x-amz-content-sha256;x-amz-date'
    canonical = 'POST\n' + uri + '\n' + query + '\n' + canonical_headers + '\n' + signed + '\n' + payload_hash
    to_sign = 'AWS4-HMAC-SHA256\n' + stamp + '\n' + scope + '\n' + hashlib.sha256(canonical.encode()).hexdigest()
    auth = ('AWS4-HMAC-SHA256 Credential=' + user + '/' + scope + ', SignedHeaders=' + signed +
            ', Signature=' + signature(secret, day, 'us-east-1', 's3', to_sign))
    request = urllib.request.Request('http://' + host + uri + '?' + query, data=body, method='POST',
                                    headers={'Host': host, 'X-Amz-Date': stamp,
                                             'X-Amz-Content-Sha256': payload_hash,
                                             'Authorization': auth, 'Content-Type': 'application/xml'})
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    try:
        with opener.open(request, timeout=15) as response:
            return response.status, response.read(4 * 1024 * 1024)
    except urllib.error.HTTPError as error:
        return error.code, error.read(4096)


class SiloSelectRecordLimitRealTests(unittest.TestCase):
    def test_real_binary_rejects_oversized_csv_and_json_lines_before_aggregation(self):
        if not shutil.which('docker'):
            if os.getenv('GITHUB_ACTIONS'):
                self.fail('Docker is mandatory for the binary CVE regression')
            self.skipTest('Requires real Docker; offline parsing tests are separate')
        suffix = uuid.uuid4().hex[:10]
        container, network = 'select-limit-' + suffix, 'select-limit-net-' + suffix
        admin, app = 'admin' + secrets.token_hex(8), 'docs' + secrets.token_hex(8)
        admin_secret, app_secret = '--' + secrets.token_urlsafe(28), '-' + secrets.token_urlsafe(28)
        try:
            with tempfile.TemporaryDirectory(prefix='silo-select-limit-') as temporary:
                policy = Path(temporary) / 'policy.json'
                policy.write_text(json.dumps(POLICY))
                run(['docker', 'network', 'create', network])
                run(['docker', 'pull', IMAGE], timeout=240)
                ip = _start(container, network, admin, admin_secret)
                _provision(container, policy, admin, admin_secret, app, app_secret)
                for kind in ('csv', 'jsonl'):
                    normal = b'value\n' if kind == 'csv' else b'{"value":"small"}\n'
                    oversized = (b'a' * ((1 << 20) + 1) + b'\n' if kind == 'csv' else
                                 b'{"value":"' + b'a' * ((1 << 20) + 1) + b'"}\n')
                    for label, payload in (('normal', normal), ('oversized', oversized)):
                        with self.subTest(kind=kind, label=label):
                            key = 'synthetic/' + kind + '-' + label
                            self.assertEqual(200, s3_request(ip, 'PUT', BUCKET, key, app, app_secret, payload)[0])
                            status, response = select_count(ip, key, app, app_secret, kind)
                            self.assertEqual(200, status, 'Select request must enter actual event-stream parser')
                            frames = select_events(response)
                            errors = [f.get(':error-code') for f in frames if f.get(':message-type') == 'error']
                            if label == 'oversized':
                                self.assertEqual(['OverMaxRecordSize'], errors)
                            else:
                                self.assertEqual([], errors)
                                self.assertTrue(any(f.get(':event-type') == 'Records' for f in frames))
                                self.assertTrue(any(f.get(':event-type') == 'End' for f in frames))
                            self.assertEqual(0, run(['docker', 'exec', container, 'silo', 'healthcheck', 'ready']).returncode)
        finally:
            run(['docker', 'rm', '-f', container], check=False, timeout=30)
            run(['docker', 'network', 'rm', network], check=False, timeout=30)


if __name__ == '__main__':
    unittest.main()
