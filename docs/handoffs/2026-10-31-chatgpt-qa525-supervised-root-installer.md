# QA525 #822 — Supervised root launcher installer (31-10-2026)

## PRE / ownership and safety
ChatGPT IA. Claimed branch from live dev@97165b52282dca51a313233fbb8f5d59b695455b; main@6dfb9546a4873baff15955cf86abfd7d47e3d111. Issue #822 OPEN. #827 signed publication OPEN. PR #833 in separate review changes root-source inspector and QA525 workflow; this PR must avoid those same paths until #833 merged and CI succeeds.

**Goal:** develop the real one-shot replacement entrypoint as **source code for independent review**, never execute it on glmdch in this session. Fail closed without root/glmdch, exact fresh public GitHub signature/manifest/source gate, separate root0600 launcher-only approval (TTL/nonce), five shared QA operator locks, existing trusted old launcher, root-owned retained package tree, durable intent and backup, atomic swap and receipt plus fsync, no automatic crash reconciliation. Signed external bootstrap and physical host authorization remain essential.

**Hot paths:** only a new installer entrypoint and its tests/handoff in this branch. Do not edit the PR #833 workflow until it merges. No app/API/WEB, database/EF100, Docker/Compose, Keycloak/SMTP, G6, prod/main/srv01, second VM or PR771/737/766.

## Trust boundary
A raw Python script from GitHub must **never** run with sudo. Physical execution requires the signed package to have been independently verified and root-owned before loading this module, then runtime checks independently verify current HEAD/release/GitHub Sigstore and package manifest. The installer must never claim that CI alone proves release or host readiness.

## DURANTE / POST
Record exact files, commits, adverse tests, CI and postmerge SHA, G6 and host verification limitations; GitHub + Drive mirror. Remain DRAFT if any check fails. #822 remains open until a successful authorized physical rollout and UAT.
