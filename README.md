# Balance Puzzle

A 3D physics-based mobile balancing game built in Unity, developed in small,
controlled stages.

## Process

Every stage follows: **PLANNING → DEVELOPMENT → TESTING → VERIFICATION → LOCK**.

- A stage is only LOCKED after all critical tests pass and a Verification
  Report is written.
- A locked stage's public interfaces and approved behavior are a contract:
  future work must never silently break them.
- Any change to an already-shared file must be reported as
  FILE CHANGED / WHY / WHAT CHANGED / RISK / TEST REQUIRED and approved first.

Current stage: **Stage 1 — Core Physics Sandbox** (in development).

## Setup

See [Docs/Stage1/unity-setup.md](Docs/Stage1/unity-setup.md) for Unity version,
packages, project settings, and config-asset creation.

## Branches

- `main` — always reflects the latest LOCKED stage.
- `stage-N-development` → `stage-N-testing` → `stage-N-locked` per stage.
- Locked stages are tagged, e.g. `v0.1.0-stage1-locked`.

## Security

Never commit secrets (tokens, passwords, API keys) anywhere in this repo —
not in source, assets, docs, or logs. Git authentication uses transient
in-memory credentials only. No push/force-push/branch-deletion without
explicit approval.
