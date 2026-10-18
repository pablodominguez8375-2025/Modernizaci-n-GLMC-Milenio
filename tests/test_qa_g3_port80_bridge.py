"""Offline contract: port-80 bridge must fail closed without touching QA LAB2."""
from pathlib import Path
import unittest

SCRIPT = (Path(__file__).resolve().parents[1] / "scripts/qa/install-g3-port80-bridge.sh").read_text()


class G3Port80BridgeContract(unittest.TestCase):
    def test_exact_private_bind(self):
        self.assertIn('PRIVATE_IP="172.17.5.107"', SCRIPT)
        self.assertIn('bind 172.17.5.107', SCRIPT)
        self.assertNotIn('bind 0.0.0.0', SCRIPT)

    def test_peer_restriction(self):
        self.assertIn('@outside not remote_ip 172.17.5.0/24', SCRIPT)
        self.assertIn('route {\n      respond @outside "Forbidden" 403', SCRIPT)

    def test_admin_master_denied(self):
        for path in ('/identity/admin/*', '/identity/realms/master/*'):
            self.assertIn(path, SCRIPT)
        self.assertIn('respond @deny_identity "Not Found" 404', SCRIPT)

    def test_tls_verified_upstream_and_sni(self):
        self.assertIn('https://127.0.0.1:18443', SCRIPT)
        self.assertIn('tls_server_name sistema.granlogiamixta.cl', SCRIPT)
        self.assertIn('tls_trust_pool file /etc/caddy/trust.pem', SCRIPT)
        self.assertIn('--cacert "$tmp/trust.pem"', SCRIPT)
        self.assertNotIn('tls_insecure_skip_verify\n', SCRIPT)
        self.assertNotIn('curl -k', SCRIPT)

    def test_gateway_and_security_gates(self):
        self.assertIn('127.0.0.1:18443->443/tcp', SCRIPT)
        self.assertIn('docker image inspect "$GATEWAY_IMAGE"', SCRIPT)
        self.assertIn('caddy validate --config', SCRIPT)
        self.assertIn('--pull never', SCRIPT)
        self.assertIn('--cap-drop ALL', SCRIPT)
        self.assertIn('--security-opt no-new-privileges', SCRIPT)
        self.assertIn('--read-only', SCRIPT)

    def test_rollback_without_db_or_firewall_changes(self):
        self.assertIn('docker rm -f "$NAME"', SCRIPT)
        for forbidden in ('ufw allow', 'iptables -A', 'docker compose down', 'docker volume rm',
                          'docker system prune', 'systemctl restart docker', 'git checkout main'):
            self.assertNotIn(forbidden, SCRIPT)


if __name__ == "__main__":
    unittest.main()
