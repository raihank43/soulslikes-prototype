# Project Plan

## Project Phase: BUILDING

> **This marker controls how Claude behaves at the start of every session — read it first.**
> - `BRAINSTORMING` — the vision/roadmap below isn't settled yet. Before building features, help the user complete the Vision and Features sections. Ask, propose, refine. Don't jump into code.
> - `BUILDING` — the roadmap is agreed. Follow the normal build workflow in CLAUDE.md.
>
> Flip this to `BUILDING` once the user confirms the roadmap is solid enough to start building.

## Current Focus

> The 30-second cold-start brief — so a fresh session (or a post-compaction one) can resume *without re-reading everything*. Kept current by `/checkpoint`. **Read this first**, then read only the docs the "Start here" line points to.

_Last checkpoint: 2cab1eb (doc system adopted, full scan + lean consolidation)_

- **Just shipped:** **Day 5 — dodge roll with i-frames, shipped with its P1 test.** Directional dodge (4 Mixamo step-dodge clips → `Dodge` 2D blend tree, baked root motion, no rotation), i-frames on a guaranteed-to-end coroutine writing `PlayerHealth.IsInvulnerable`, `Dodging` tag locks input. P1 PlayMode suite (`Soulslike.Tests.Play`) proves i-frames + the stamina gate — 4 green. Verified working end-to-end in Play (trigger → state → root motion → recover). Before that: P0 asmdef split + P2 import verifier (both green); Day 4.5; growing-docs; the `/rethink` plan — all on `origin/main`.
- **In flight:** nothing — clean stopping point. Full combat loop now includes evasion.
- **Next:** **Day 6 — the Sekiro parry** (`docs/day-1-to-7-plan.md`, Day 6; the centerpiece). Consider backfilling more **P1** invariants (damage table, cooldowns, range bands) and **P3/P4** (screenshot + telemetry) when useful.
- **Start here:** `docs/day-1-to-7-plan.md` (Day 6), `docs/feature-player.md` (dodge + the P1 harness pattern), `docs/feature-combat.md`, `docs/feature-ai.md`. Background: `docs/proposals/2026-06-21-rethink.md`.
- **Day 5 dodge fixed (post-playtest):** the Mixamo step-dodge clips travel ~56° diagonally, so dodges curved. Switched dodge movement from root motion to **scripted velocity** (`PlayerDodge` sets `rb.linearVelocity`); now dead-straight, ~3.1m at `dodgeSpeed=3.5`, facing preserved. Remaining tuning is feel only (`dodgeSpeed`/`dodgeDuration`/i-frame window); cosmetic foot-slide from the diagonal clips would clear up with cleaner straight-dodge clips.

## Vision

A focused 3D soulslike **vertical slice** built in Unity 6.4 / URP: one arena, one enemy, one full combat loop — proven before any expansion. Combat draws from two sources:

- **Dark Souls** — stamina-gated movement, dodge rolls with i-frames, lock-on targeting.
- **Sekiro** — deflect/parry windows, a posture system, deathblows on staggered enemies.

Scope is intentionally interconnected hand-crafted areas (DS1-style), **not** open world. The full day-by-day build roadmap lives in `docs/day-1-to-7-plan.md`.

## Features

| Feature | Priority | Status | Doc |
|---------|----------|--------|-----|
| Input System (gamepad + KB/M) | High | done | (see CLAUDE.md Day 0 notes) |
| Player locomotion + animator | High | done | [feature-player.md](feature-player.md) |
| Third-person camera + lock-on | High | done | [feature-combat.md](feature-combat.md) |
| Melee combat (light/heavy, combos, stamina) | High | done | [feature-player.md](feature-player.md) |
| Health + HUD bars | High | done | [feature-ui.md](feature-ui.md) |
| Enemy AI (the Mutant) | High | done | [feature-ai.md](feature-ai.md) |
| Dodge roll with i-frames | High | done | [feature-player.md](feature-player.md) |
| Sekiro parry / posture | High | planned | _Day 6_ |
| Death, respawn, combat HUD polish | Medium | planned | _Day 7_ |
| Turn-in-place strafe + mirrored turn clips | Low | planned | _deferred polish_ |

Status values: `planned` | `in-progress` | `done` | `cut`

## Tooling & Test Infrastructure

Adopted from `/rethink` 2026-06-21 to close the build feedback loop. Order is the agreed sequence.

| Item | Priority | Status | Doc |
|------|----------|--------|-----|
| P0 — first-party asmdef split (test prereq) | High | done | [feature-import-verifier.md](feature-import-verifier.md) |
| P2 — asset-import verifier (FBX reset guard) | High | done | [feature-import-verifier.md](feature-import-verifier.md) |
| P1 — PlayMode combat-invariants suite | High | started | [feature-player.md](feature-player.md) · i-frame + stamina-gate tests (`Soulslike.Tests.Play`); more invariants to backfill |
| P3 — screenshot-based visual verification | Medium | planned | [proposal](proposals/2026-06-21-rethink.md) |
| P4 — runtime combat telemetry | Medium | planned | [proposal](proposals/2026-06-21-rethink.md) |
| P5 — `/playtest` project skill | Low | planned | [proposal](proposals/2026-06-21-rethink.md) · build last |
| P6 — deterministic combat-sim mode | Low | conditional | [proposal](proposals/2026-06-21-rethink.md) · only if P1 flaky |

## Decisions Log

Record every significant decision so future-you (or post-compaction-you) knows WHY things are the way they are.

| Decision | Rationale | Date |
|----------|-----------|------|
| Animator is the combo state machine (no script-side counter) | Avoids dual-source-of-truth bugs; transitions decide the graph | Day 3 |
| Enemy uses NavMeshAgent, no Rigidbody | Agent owns translation; a Rigidbody would conflict | Day 4 |
| Enemy attack completion via absolute-time wait, not animation event | Unity inflates FBX clip length on reimport, drifting events past exit time | Day 4.5 |
| Per-attack cooldown to throttle signature moves (jump) | Lower probability alone still spams because AI re-rolls each cooldown | Day 4.5 |
| Phased moveset: foundation first, sophistication later | Ship a working loop before adding moves; deferred turn-in-place/mirrored clips | Day 4 |
| Adopted growing-docs; consolidated CLAUDE.md to a lean system prompt | Per-day notes relocated verbatim to `docs/build-journal.md`; `docs/feature-*.md` + code are the maintained truth | 2026-06-21 |
| Adopt a test + observability layer before resuming features (`/rethink`) | Journal analysis: iteration count was driven by *invisible feedback gaps* (silent FBX-import resets, dropped anim events, feel bugs only the user could see), not logic errors. Closing those loops lets Claude self-correct before a human round-trip. Scope: P0–P4 committed, P5 deferred, P6 conditional | 2026-06-21 |
| i-frames (Day 5) ship *with* their PlayMode test (P1) | I-frame invulnerability is a binary, numeric property — perfectly testable and genuinely hard to verify by eye. Co-shipping the test makes the headline Day-5 feature provable, not vibes | 2026-06-21 |
| Reuse installed test/observability stack; no new deps | Test Framework 1.6.0, Performance Testing, ScreenCapture module, and MCP `run_tests`/`execute_code` are already present — adding a 3rd-party test asset would be the generic-best-practice trap RULES warns against | 2026-06-21 |
| Import verifier keys its baseline by FBX path, checks `apparentSpeed` as an upper bound only | Every player Mixamo clip shares the internal name `"mixamo.com"` (name keys collide); idles/in-place attacks read `apparentSpeed≈0` so a lower bound false-fails. Both learned from dumping real import state before coding | 2026-06-21 |
| Mutant Swiping/JumpAttack logged as a known length-inflation defect (not auto-fixed) | They're already inflated (5.33/10.29 vs ~2.0/2.8) from Day-4.5 importer events; gameplay unaffected (absolute-time waits). Re-import clean later, then bake true length as the guard. P2 is detect-only by decision | 2026-06-21 |
| Dodge i-frames driven by a coroutine on absolute time, not animation events | A dropped `EndIFrames` event would leave the player permanently invulnerable; coroutine + try/finally guarantees the window closes, and makes it deterministically P1-testable without the animator | Day 5 |
| Directional dodge: 4 step-dodge clips in a `Dodge` 2D blend tree, character does NOT rotate | Each clip carries its own local-space baked root motion, so selecting the clip by local input direction dodges correctly while keeping facing (locked-on stays on target); rotating would curve the dodge; scripted velocity reads as ice-skating | Day 5 |

## Rejected Ideas

Record ideas we considered and explicitly decided NOT to do. This prevents re-suggesting them after compaction.

| Idea | Why Rejected | Date |
|------|-------------|------|
| CinemachineTargetGroup framing for lock-on | World-space offset from group center → random angles, below-ground dives | Day 2 |
| Drive enemy attack completion from `AttackComplete` animation event | FBX importer rescales event times unpredictably; event lands past exit time and is dropped | Day 4.5 |
| Scripted lunge velocity on attacks | Desyncs feet from body (ice-skating); clips bake their own translation | Day 3 |
