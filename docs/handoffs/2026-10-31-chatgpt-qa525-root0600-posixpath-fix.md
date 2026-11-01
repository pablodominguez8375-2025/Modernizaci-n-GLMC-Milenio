# QA525 #822 — Root0600 approval PosixPath bugfix (31-10-2026)

## PRE — live claim
GitHub dev@50844752ddda37bf2ba291dd6f76e3414d3bc103, main@6dfb9546a4873baff15955cf86abfd7d47e3d111. PR #834 DRAFT, HEAD d21e87fa8a78c9969ed87be2ad655158c9ac6617, 5/7 CI SUCCESS (Silo, PMGM CI ongoing). Existing root0600 reader `scripts/qa/operators/qa525-launcher-root-approval.py` has `trusted_chain` requiring `type(path) is Path`; on Linux `Path('/etc/pmgm/...')` is `PosixPath`, so even a correct trusted root approval is rejected. This makes the physical installer impossible to authorize. Original test only checked expected invalid paths, not a positive valid path. Separate branch avoids edits to PR834 and no main/host changes.

## SCOPE
Change only type guard to `isinstance(path, Path)` (retains absolute/path stat, root ownership, 0600, no symlink/hardlink, nonwritable ancestor, O_NOFOLLOW and inode/size checks), add a hermetic *positive* path test mocking root metadata with real temporary paths, plus adversarial non-Path/relative/symlink tests. Include in QA525 existing workflow; no release, source execution, Docker, installer or glmdch access. PR remains draft until exact-head 7/7 CI, audit GitHub+Drive readback; integrate only dev when green and safe.
