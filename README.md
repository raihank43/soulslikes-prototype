# SoulslikePrototype

A focused 3D soulslike combat vertical slice — one arena, one enemy, one full combat loop — built in Unity 6.4 / URP.

## About

A learning/prototype project building a soulslike action game from the ground up, day by day. Combat blends **Dark Souls** (stamina-gated movement, dodge rolls with i-frames, lock-on) and **Sekiro** (deflect/parry windows, posture, deathblows). Scope is intentionally narrow: prove the core combat loop against a single enemy before any expansion. The full roadmap lives in [`docs/day-1-to-7-plan.md`](docs/day-1-to-7-plan.md).

## Features

- **Locomotion** — camera-relative movement with analog-preserving input, animator blend-tree locomotion, sprint toggle.
- **Lock-on & camera** — Cinemachine third-person FreeLook + a dedicated locked camera, single-target framing, strafe locomotion.
- **Melee combat** — light/heavy attacks, animator-driven combo chains, stamina gating, weapon hitboxes.
- **Enemy AI (the Mutant)** — NavMesh state machine with aggro/chase/return, three attacks (Punch/Swipe/Jump), distance-banded selection, combo chains, randomized tells, hit reactions, and death.
- **Health & HUD** — player health/stamina bars, world-space enemy health bar, lock-on reticle.

## Tech Stack

- **Engine:** Unity 6.4 (URP)
- **Language:** C# (Mono backend in dev)
- **Input:** Unity Input System (new) — gamepad + keyboard/mouse
- **Camera:** Cinemachine 2.10.7 (legacy 2.x API)
- **Navigation:** AI Navigation (NavMesh) 2.0.12
- **Animation:** Mecanim Humanoid + Mixamo clips

## Getting Started

Open the project in **Unity 6.4** and load `Assets/Scenes/SampleScene.unity`. Press Play. Default controls: move (WASD / left stick), look (mouse / right stick), lock-on (middle mouse / R3), light attack (LMB tap / RB), heavy attack (LMB hold / RT), sprint (Shift / L3, toggle).

## Documentation

- [`CLAUDE.md`](CLAUDE.md) — AI workflow + per-day implementation notes and gotchas.
- [`docs/PLAN.md`](docs/PLAN.md) — roadmap, feature status, decisions.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — stack, structure, data flow.
- [`docs/RULES.md`](docs/RULES.md) — conventions and anti-patterns.
- [`docs/feature-*.md`](docs/) — per-area deep docs (AI, combat, player, UI).
