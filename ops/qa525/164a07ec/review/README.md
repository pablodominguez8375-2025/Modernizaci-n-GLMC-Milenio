# G6 review chain — preparation only

Cut: dev@164a07ecd36eb45428b6aeac4854a17e339ca525. Issue #525 / PR #1007.

No merge, host execution or GO is granted by these files. Backup and isolated
restore already succeeded. This chain only prepares/finalizes the local review
document after independently verifying the returned encrypted copy, receipt and
restore report. It does not authorize API/WEB deployment or the global G6 gate.

## Separate authority

- Kit A runs rootless and creates only kit-g6-review. Keep its new MANIFEST_PIN
  in the Sponsor's independent store; never use the backup/restore PIN.
- Kit B copies only matching review configuration/evidence into root custody;
  it runs system binaries, not project Python, and installs no GO.
- GO-T must run on the Sponsor's Termux with the review kit line. It rejects
  backup/restore commands and signs one new review-only GO for 14 minutes.
- GO-H checks receipt/restore canonical JSON and root 0600 custody, positive
  table/object counts, production_touched=false, exact archive/repo/epoch,
  downloads-v2 hash and custody, review absence, no plaintext/verifier, healthy
  active services and at least 30 minutes before the 48-hour cut deadline.
  These checks precede consent and GO installation. The original GO signature,
  command/pins/nonce/TTL and manifest PIN gates remain enforced.
- Bootstrap is invoked only through the authenticated root custody entrypoint.
  Direct execution of qa525-vnext-g6-v2-run.py is prohibited.
- Final success requires exit 0, FD delivery, GO archival, exact public markers,
  a canonical root 0600 review matching the fixed Drive ID, unchanged
  receipt/restore bytes, unchanged downloaded ciphertext, healthy services and
  no verifier/plaintext. Final hashes are emitted for the next evidence gate.

The fixed Drive ID is 1THaJaMm0sKRi1_VGp1eqOBuyzgQP54aT. It and the copy
1j7SvSnrJ3oR0Xzw_EnBVPT7FEkx3Co1H were both downloaded through the Drive
connector and hashed: 262909 bytes, SHA256
b0c77c1c26ba48e1c6942abab3ea5d5b1a9aaa3228cbd65753bc274a775276fe.

## Validation and limits

Run `python -m unittest discover -s tests -p test_qa525_review_operator_164a07ec.py -v`.
Ten sandbox tests cover canonical receipt/report/review validation, wrong
hash/repo/epoch, touched production, invalid counts, wrong modes, symlinks and
hardlinks, existing review, real ephemeral SSH signatures, wrong command/cut,
expired/future/short GO, shell syntax and embedded Python compilation. The actual
producer is also exercised against temporary fixture files: one exclusive review
write, unchanged inputs and a rejected second write without truncation.

The root UID/GID check is substituted only in sandbox test copies; physical
ownership is not proved by these tests. The complete root bootstrap, Docker,
Termux/sudo/PTY/signals/FD delivery orchestration and Kit A/B on a real release
must still receive independent review/validation. No physical commands were run
by the author. A success here is not disaster recovery or global deployment GO.

Stop on any discrepancy; preserve evidence and do not retry execution without
the Sponsor's applicable explicit instruction. Never paste GO, PIN, private key
or passphrase into GitHub, Drive or chat.
