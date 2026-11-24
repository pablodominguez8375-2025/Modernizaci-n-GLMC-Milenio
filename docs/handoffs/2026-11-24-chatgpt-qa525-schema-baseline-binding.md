# QA525 — baseline image binding

Claim: ChatGPT, issue #1072. Base dev 26f16cc4b485ec857155321bb1c826087995d503. Scope: schema host, flight and regression tests. Existing candidate build PASS; no physical apply. Distinguish authenticated old API image from target image; preserve fail-closed baseline and container checks. Draft only until implementation and exact-head CI.
