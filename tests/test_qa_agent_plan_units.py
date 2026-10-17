"""Check reference systemd plan units cannot turn into a deployment agent."""
import configparser
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
UNITS = ROOT / "infrastructure/systemd"

def read(filename):
    parser = configparser.ConfigParser(interpolation=None, strict=True)
    parser.read(UNITS / filename)
    return parser

class QaPlanUnitContractTests(unittest.TestCase):
    def test_service_is_unprivileged_read_only_and_has_no_install_section(self):
        unit = read("pmgm-qa-agent-plan.service")
        self.assertFalse(unit.has_section("Install"))
        self.assertEqual("oneshot", unit["Service"]["Type"])
        self.assertEqual("pmgm-qa-agent", unit["Service"]["User"])
        self.assertEqual("pmgm-qa-agent", unit["Service"]["Group"])
        self.assertEqual("yes", unit["Service"]["NoNewPrivileges"])
        self.assertEqual("strict", unit["Service"]["ProtectSystem"])
        self.assertEqual("yes", unit["Service"]["PrivateDevices"])
        self.assertEqual("", unit["Service"]["CapabilityBoundingSet"])
        self.assertEqual("", unit["Service"]["AmbientCapabilities"])
        command = unit["Service"]["ExecStart"]
        self.assertIn("qa-agent-plan.py", command)
        for banned in ("install-", "docker", "qa-acquire-candidate.py", "sudo ", "curl ", "bash "):
            self.assertNotIn(banned, command)

    def test_service_requires_preapproved_baseline(self):
        unit = read("pmgm-qa-agent-plan.service")
        self.assertEqual("/var/lib/pmgm-qa-agent/last-deployed.json",
                         unit["Unit"]["ConditionPathExists"])
        self.assertIn("--last-deployed /var/lib/pmgm-qa-agent/last-deployed.json",
                      unit["Service"]["ExecStart"])

    def test_timer_only_calls_planner(self):
        timer = read("pmgm-qa-agent-plan.timer")
        self.assertEqual("hourly", timer["Timer"]["OnCalendar"])
        self.assertEqual("false", timer["Timer"]["Persistent"])
        self.assertEqual("pmgm-qa-agent-plan.service", timer["Timer"]["Unit"])
        self.assertFalse(timer.has_section("Service"))

if __name__ == "__main__":
    unittest.main()
