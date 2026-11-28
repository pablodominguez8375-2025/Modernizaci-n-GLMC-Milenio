# QA525 manual cut e5c40d3a — what to do on each STOP (draft for ChatGPT review; not an authorization)

Rules for every STOP: do NOT re-run the driver, do NOT retry the migrator, do NOT run `docker compose down`, do NOT
roll back blindly, do NOT delete the journal or the nonce marker. Keep writes quiesced. Preserve evidence (journal,
receipts, `docker ps`, `docker inspect` of api/web/pg, EF history). Report the closed STOP code to the coordinator and
wait. Any restoration is a separate runbook step with its own dictamen and literal Sponsor authorization.

| Terminal (journal) | Meaning | State to expect | Next step (needs explicit authorization) |
|---|---|---|---|
| `PRE_DDL_STOP` | Stopped before any DDL (preflight, backup, quiesce) | EF 100, schema untouched; API may be stopped if quiesce ran | Read-only inspection; if API stopped, restart the OLD API image only under a reviewed, separately authorised step |
| `PARTIAL_SCHEMA_STOP` | Migrator/grants/post-gate failed or ambiguous | EF between 100 and 104 or grants unverified (BASE_ADELANTADA) | Keep quiesce. Compare EF history and grants against the pinned plan. Restore from the final backup only via the reviewed restore runbook after dictamen |
| `APP_PARTIAL_STOP` | Schema 104 verified, API/WEB swap not fully verified | EF 104, grants verified; some instance old/new | No schema action. Inspect per-instance probes; re-run of the flight only via a new reviewed step |
| `UAT_STOP` | App swapped and healthy, UAT failed | EF 104, new API/WEB | `ApplyEnabled` stays false; investigate UAT evidence; no data load |

BASE_ADELANTADA = the database is ahead of the application baseline: never reverse migrations automatically.
