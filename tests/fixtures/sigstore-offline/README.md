# Offline Sigstore fixtures (TEST ONLY — never an authority)

Public material used to prove that the pinned `gh attestation verify` really verifies a Sigstore
attestation **offline** (`--bundle` + `--custom-trusted-root`) and fails closed on anything else.
They are NOT QA525 release material; the production trusted root and identity are fixed by the Sponsor
outside GitHub/Drive before any root step.

| file | origin |
|---|---|
| `sigstore-js-2.1.0.tgz` | `sigstore@2.1.0` npm tarball (artifact; its attestation subject is a sha512 digest) |
| `sigstore-js-2.1.0-bundle.json` | its Sigstore bundle, from `cli/cli` `pkg/cmd/attestation/test/data/` (identity `sigstore/sigstore-js`, `.github/workflows/release.yml`, issuer `https://token.actions.githubusercontent.com`, source `26d16513386ffaa790b1c32f927544f1322e4194`) |
| `trusted-root-public-good.jsonl` | output of `gh attestation trusted-root` (Sigstore public-good + GitHub roots) fetched 2026-11-10 |

sha256: bundle `cfadddd5…f761`, tgz `f0c641fc…0eed`, root `65ca537f…2b9c`.
