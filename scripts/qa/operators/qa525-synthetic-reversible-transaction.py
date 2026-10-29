#!/usr/bin/env python3
"""Synthetic-only QA525 API+WEB reversible transaction coordinator.

NO HOST ADAPTER, NO DOCKER, NO SSH, NO FILE I/O, NO LIVE --apply.
The sole purpose is proving the order of critical phases and recovery paths.
Future physical adapter needs independent review + explicit authorization.
"""
from contextlib import ExitStack

SCOPE = "SYNTHETIC_REHEARSAL_NO_HOST_NO_DEPLOYMENT"
PHASES = (
    "preflight", "signed_source", "snapshot", "backup_pmgm",
    "backup_keycloak", "offhost_custody", "restore_drill",
    "recheck", "apply_api", "apply_web", "health",
    "unchanged_dependencies", "unchanged_migrations", "record",
)


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def simulate(adapter):
    """Test one signed API+WEB transaction with an in-memory FAKE adapter.

    No receipts, signed JSON booleans or simulated result can authorize host
    deployment. Every mutation is performed ONLY on synthetic adapter state.
    """
    require(type(adapter).__name__ == "SyntheticQa525Adapter"
            and getattr(adapter, "synthetic_only", None) is True,
            "PHYSICAL_ADAPTER_NOT_SUPPORTED")
    trace = []
    changed = False
    with adapter.exclusive():
        try:
            require(adapter.preflight() is True, "PREFLIGHT_FAIL")
            trace.append("preflight")
            require(adapter.verify_signed_source() is True, "SOURCE_NOT_SIGNED")
            trace.append("signed_source")
            baseline = adapter.snapshot()
            require(type(baseline) is dict and
                    set(baseline) == {"api", "web", "keycloak",
                                     "postgres", "minio", "gateway", "clamav"},
                    "SEVEN_SERVICE_BASELINE_INVALID")
            trace.append("snapshot")
            require(adapter.backup("pmgm") is True, "PMGM_BACKUP_FAIL")
            trace.append("backup_pmgm")
            require(adapter.backup("pmgm_keycloak") is True, "KEYCLOAK_BACKUP_FAIL")
            trace.append("backup_keycloak")
            require(adapter.offhost_custody() is True, "OFFHOST_CUSTODY_FAIL")
            trace.append("offhost_custody")
            require(adapter.restore_drill() is True, "RESTORE_DRILL_FAIL")
            trace.append("restore_drill")
            require(adapter.recheck(baseline) is True, "PRE_APPLY_DRIFT")
            trace.append("recheck")
            # Set changed BEFORE first mutating call, including timeouts
            # whose side effects cannot be inferred from the error.
            changed = True
            adapter.apply("api")
            trace.append("apply_api")
            adapter.apply("web")
            trace.append("apply_web")
            require(adapter.health() is True, "HEALTH_FAIL")
            trace.append("health")
            require(adapter.dependencies_untouched(baseline) is True,
                    "DEPENDENCIES_CHANGED")
            trace.append("unchanged_dependencies")
            require(adapter.migrations_untouched() is True,
                    "MIGRATIONS_CHANGED")
            trace.append("unchanged_migrations")
            adapter.record_success()
            trace.append("record")
            return {"status": "SYNTHETIC_TRANSACTION_PASS_NOT_DEPLOYED",
                    "scope": SCOPE, "trace": trace, "may_apply": False,
                    "host_updated": False}
        except Exception:
            if changed:
                # Attempt BOTH rollback operations even if one throws.
                errors = []
                for service in ("web", "api"):
                    try:
                        adapter.rollback(service)
                    except Exception:
                        errors.append(service)
                try:
                    verified = adapter.verify_rollback(baseline) is True
                except Exception:
                    verified = False
                try:
                    adapter.record_failure()
                except Exception:
                    errors.append("record_failure")
                return {"status": ("SYNTHETIC_ROLLBACK_VERIFIED_NOT_DEPLOYED"
                                   if verified and not errors
                                   else "SYNTHETIC_ROLLBACK_FAILED_MANUAL_REVIEW"),
                        "scope": SCOPE, "trace": trace,
                        "rollback_attempted": ["web", "api"],
                        "rollback_verified": verified and not errors,
                        "may_apply": False, "host_updated": False}
            return {"status": "SYNTHETIC_BLOCKED_BEFORE_MUTATION",
                    "scope": SCOPE, "trace": trace,
                    "rollback_attempted": [],
                    "may_apply": False, "host_updated": False}


class SyntheticQa525Adapter:
    """In-memory test double, NEVER inspect or operate any physical services."""

    synthetic_only = True
    SERVICES = ("api", "web", "keycloak", "postgres",
                "minio", "gateway", "clamav")

    def __init__(self, fail_at=None, rollback_fail=()):
        self.fail_at = fail_at
        self.rollback_fail = frozenset(rollback_fail)
        self.events = []
        self.services = {name: "old-" + name for name in self.SERVICES}
        self.old = self.services.copy()
        self.candidate = {"api": "new-api", "web": "new-web"}
        self.backups = set()
        self.success = False
        self.failed = False

    def _do(self, name):
        self.events.append(name)
        if self.fail_at == name:
            raise Blocked("SYNTHETIC_FAIL_" + name)

    def exclusive(self):
        from contextlib import nullcontext
        return nullcontext()

    def preflight(self):
        self._do("preflight")
        return True

    def verify_signed_source(self):
        self._do("signed_source")
        return True

    def snapshot(self):
        self._do("snapshot")
        return self.services.copy()

    def backup(self, database):
        self._do("backup_" + database)
        self.backups.add(database)
        return True

    def offhost_custody(self):
        self._do("offhost_custody")
        return self.backups == {"pmgm", "pmgm_keycloak"}

    def restore_drill(self):
        self._do("restore_drill")
        return self.backups == {"pmgm", "pmgm_keycloak"}

    def recheck(self, original):
        self._do("recheck")
        return self.services == original

    def apply(self, service):
        self._do("apply_" + service)
        self.services[service] = self.candidate[service]

    def health(self):
        self._do("health")
        return self.services["api"] == "new-api" and self.services["web"] == "new-web"

    def dependencies_untouched(self, baseline):
        self._do("unchanged_dependencies")
        return all(self.services[name] == baseline[name]
                   for name in self.SERVICES if name not in ("api", "web"))

    def migrations_untouched(self):
        self._do("unchanged_migrations")
        return True

    def record_success(self):
        self._do("record")
        self.success = True

    def rollback(self, service):
        self.events.append("rollback_" + service)
        if service in self.rollback_fail:
            raise Blocked("SYNTHETIC_ROLLBACK_FAILED")
        self.services[service] = self.old[service]

    def verify_rollback(self, original):
        self.events.append("verify_rollback")
        return self.services == original

    def record_failure(self):
        self.events.append("record_failure")
        self.failed = True
