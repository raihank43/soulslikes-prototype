# Project Plan

## Project Phase: BUILDING

> **This marker controls how Claude behaves at the start of every session — read it first.**
> - `BRAINSTORMING` — the vision/roadmap below isn't settled yet. Before building features, help the user complete the Vision and Features sections. Ask, propose, refine. Don't jump into code.
> - `BUILDING` — the roadmap is agreed. Follow the normal build workflow in CLAUDE.md.
>
> Flip this to `BUILDING` once the user confirms the roadmap is solid enough to start building.

## Current Focus

> The 30-second cold-start brief — so a fresh session (or a post-compaction one) can resume *without re-reading everything*. Kept current by `/checkpoint`. **Read this first**, then read only the docs the "Start here" line points to.

_Last checkpoint: doc system adopted (full scan), commit 7ed2c71_

- **Just shipped:** Day 4.5 — mutant moveset depth (Swipe + JumpAttack, combo chains, distance-banded selection, wind-up tells, jump cooldown). Pushed to `origin/main`.
- **In flight:** nothing — clean stopping point. The combat loop (player attacks, enemy AI, mutual damage/death) is fully playable.
- **Next:** Day 5 — dodge roll with i-frames (`docs/day-1-to-7-plan.md`, Day 5 section). The committed enemy attacks built in Day 4.5 are exactly what the roll exists to evade.
- **Start here:** `docs/day-1-to-7-plan.md` (Day 5), `docs/feature-player.md`, `docs/feature-ai.md`.

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
| Dodge roll with i-frames | High | planned | _Day 5_ |
| Sekiro parry / posture | High | planned | _Day 6_ |
| Death, respawn, combat HUD polish | Medium | planned | _Day 7_ |
| Turn-in-place strafe + mirrored turn clips | Low | planned | _deferred polish_ |

Status values: `planned` | `in-progress` | `done` | `cut`

## Decisions Log

Record every significant decision so future-you (or post-compaction-you) knows WHY things are the way they are.

| Decision | Rationale | Date |
|----------|-----------|------|
| Animator is the combo state machine (no script-side counter) | Avoids dual-source-of-truth bugs; transitions decide the graph | Day 3 |
| Enemy uses NavMeshAgent, no Rigidbody | Agent owns translation; a Rigidbody would conflict | Day 4 |
| Enemy attack completion via absolute-time wait, not animation event | Unity inflates FBX clip length on reimport, drifting events past exit time | Day 4.5 |
| Per-attack cooldown to throttle signature moves (jump) | Lower probability alone still spams because AI re-rolls each cooldown | Day 4.5 |
| Phased moveset: foundation first, sophistication later | Ship a working loop before adding moves; deferred turn-in-place/mirrored clips | Day 4 |

## Rejected Ideas

Record ideas we considered and explicitly decided NOT to do. This prevents re-suggesting them after compaction.

| Idea | Why Rejected | Date |
|------|-------------|------|
| CinemachineTargetGroup framing for lock-on | World-space offset from group center → random angles, below-ground dives | Day 2 |
| Drive enemy attack completion from `AttackComplete` animation event | FBX importer rescales event times unpredictably; event lands past exit time and is dropped | Day 4.5 |
| Scripted lunge velocity on attacks | Desyncs feet from body (ice-skating); clips bake their own translation | Day 3 |
