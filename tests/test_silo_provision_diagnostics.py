"""Offline safety/flow tests; these do not certify real Silo IAM or recovery."""
import contextlib
import io
import json
from pathlib import Path
from subprocess import CompletedProcess
import unittest
from unittest.mock import patch

import test_silo_real_recovery as lab

SECRET = 'do-not-log-secret-qa-TEST'


def result(code=1, stdout='', stderr=''):
    return CompletedProcess(['secret-argv', SECRET], code, stdout, stderr)


class SiloProvisionDiagnosticsTests(unittest.TestCase):
    def test_lookup_alone_does_not_establish_dns_failure(self):
        self.assertEqual('other', lab._failure_category(result(stderr='user lookup failed')))
        self.assertEqual('dns-resolution', lab._failure_category(result(stderr='lookup x: no such host')))
        self.assertEqual('transport-refused', lab._failure_category(result(stderr='connection refused')))
        self.assertEqual('timeout-or-exec-unavailable', lab._failure_category(None))

    def test_only_known_protocol_code_survives_json_projection(self):
        payload = {'error': {'cause': {'error': {'Code': 'AccessDenied', 'Message': SECRET}}}}
        self.assertEqual('AccessDenied', lab._mcli_error_code(result(stdout=json.dumps(payload))))
        payload['error']['cause']['error']['Code'] = SECRET
        self.assertEqual('unclassified', lab._mcli_error_code(result(stdout=json.dumps(payload))))
        for raw in (SECRET, 'null', '[]', '{', '{"error":null}'):
            self.assertEqual('unclassified', lab._mcli_error_code(result(stdout=raw)))

    def test_state_projection_discards_env_logs_and_arbitrary_values(self):
        state = {'Status': 'running', 'OOMKilled': False, 'ExitCode': 0,
                 'Error': SECRET, 'Health': {'Log': [SECRET]}, 'Config': {'Env': [SECRET]}}
        with patch.object(lab, 'run', side_effect=[result(0, json.dumps(state)), result(0)]) as run:
            snap = lab._provision_snapshot('disposable')
        self.assertEqual({'container_state': 'running', 'oom_killed': False,
                          'container_exit': 0, 'ready': True, 'health_exit': 0}, snap)
        self.assertNotIn(SECRET, json.dumps(snap))
        self.assertIn('{{json .State}}', run.call_args_list[0].args[0])
        state.update(Status=SECRET, OOMKilled=SECRET, ExitCode=SECRET)
        with patch.object(lab, 'run', side_effect=[result(0, json.dumps(state)), None]):
            snap = lab._provision_snapshot('disposable')
        self.assertEqual('unknown', snap['container_state'])
        self.assertIsNone(snap['oom_killed'])
        self.assertIsNone(snap['container_exit'])
        self.assertFalse(snap['ready'])
        self.assertNotIn(SECRET, json.dumps(snap))

    def test_unavailable_or_malformed_inspection_never_masks_original_failure(self):
        for inspection in (None, result(1, stderr=SECRET), result(0, SECRET), result(0, '[]')):
            with self.subTest(inspection=type(inspection).__name__):
                with patch.object(lab, 'run', side_effect=[inspection, None]):
                    snap = lab._provision_snapshot('disposable')
                self.assertEqual('unknown', snap['container_state'])
                self.assertFalse(snap['ready'])
                self.assertNotIn(SECRET, json.dumps(snap))

    def test_six_failures_still_fail_closed_without_secret_output(self):
        output = io.StringIO()
        state = {'Status': 'running', 'OOMKilled': False, 'ExitCode': 0, 'Error': SECRET}
        def fake_run(args, **kwargs):
            if args[1] == 'inspect':
                return result(0, json.dumps(state))
            if 'healthcheck' in args:
                return result(0)
            return result(1, stdout=SECRET, stderr='user lookup failed ' + SECRET)
        with patch.object(lab, 'run', side_effect=fake_run) as run, \
             patch.object(lab.time, 'sleep') as sleep, \
             contextlib.redirect_stdout(output), \
             self.assertRaises(AssertionError) as failure:
            lab._provision('disposable', Path('policy'), SECRET, SECRET, SECRET, SECRET)
        rows = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertEqual(list(range(1, 7)), [row['attempt'] for row in rows])
        self.assertTrue(all(row['step'] == 'alias' and row['ready'] for row in rows))
        self.assertEqual(5, sleep.call_count)
        calls = [c for c in run.call_args_list if 'mcli' in c.args[0]]
        self.assertEqual(6, len(calls))
        self.assertTrue(all('--json' in c.args[0] and c.kwargs['timeout'] == 45 for c in calls))
        self.assertNotIn(SECRET, output.getvalue() + str(failure.exception))
        self.assertNotIn('secret-argv', output.getvalue() + str(failure.exception))

    def test_recovered_attempt_continues_every_provisioning_step(self):
        calls = []
        output = io.StringIO()
        def fake_run(args, **kwargs):
            calls.append(args)
            return result(1 if len(calls) == 1 else 0, stderr=SECRET)
        with patch.object(lab, 'run', side_effect=fake_run), \
             patch.object(lab, '_provision_snapshot', return_value={'ready': False}), \
             patch.object(lab.time, 'sleep') as sleep, contextlib.redirect_stdout(output):
            lab._provision('disposable', Path('policy'), SECRET, SECRET, SECRET, SECRET)
        rows = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertEqual(['silo-provision-failure', 'silo-provision-recovered'],
                         [r['event'] for r in rows])
        self.assertEqual(2, rows[-1]['attempt'])
        self.assertEqual(1, sleep.call_count)
        self.assertEqual(9, len(calls))  # 7 CLI operations + retry + policy copy
        self.assertIn('attach', calls[-1])
        self.assertNotIn(SECRET, output.getvalue())


if __name__ == '__main__':
    unittest.main()
