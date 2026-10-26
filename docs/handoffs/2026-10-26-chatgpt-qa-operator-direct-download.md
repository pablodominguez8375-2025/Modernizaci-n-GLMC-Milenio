# QA operator direct download — #525

Owner: ChatGPT. Scope reserved: this handoff and scripts/qa/operators/refresh-4d60a37-v8.py only. No hot files or runtime workflow edits. Estimated completion: this intervention.

The operator's runbook download returned HTTP403 before any installation command. Export the already reviewed operator from issue525 comment6405614601 byte-for-byte to a raw GitHub file addressed by immutable commit and verified SHA256. This removes the host's dependency on REST comments; it does not bypass signatures, backup, gates or package verification.

Operator SHA256: db5f008329942814cf5ca776da22572b4d8e3b53629888c667ebdc412a429c04. Target4d60a37be6d69ba4335a494ab7682fa06972da71; baseline e3880db57973459b3572896d7d6c8ee541095cf0. The sole delta from reviewed v7 is TARGET; five focused local tests pass. Full application source is still obtained by the installed signed-package verifier. No Keycloak identity creation, no new secrets, no main promotion, no physical deployment claimed.

Existing v7 was observed waiting on a superseded source. Stop it before starting v8; locks remain enforced. Source4d60 Showcase push40247461609 and publisher40247461593 attempt2 are in progress. Do not infer readiness from workflow return status without release exactSHA and digest.
