# Architecture

_Last updated: 2026-06-21 (1984e55) — recency signal, not a correctness guarantee. The folder tree and data flow below rot fastest; if they disagree with the repo, trust the repo._

## Tech Stack

| Layer | Technology | Why |
|-------|-----------|-----|
| Engine | Unity 6.4, URP | 3D action game with modern render pipeline |
| Language | C# (Mono backend during dev) | Unity standard |
| Input | Unity Input System (new), gamepad + KB/M | Legacy `Input.*` is disabled (`activeInputHandler: 1`) — it throws at runtime |
| Camera | Cinemachine **2.10.7** (legacy 2.x API) | `CinemachineFreeLook` + `CinemachineVirtualCamera`; NOT the 3.x `CinemachineCamera` API |
| Navigation | AI Navigation package (NavMesh) 2.0.12 | Enemy locomotion via `NavMeshAgent` |
| Animation | Mecanim (Humanoid), Mixamo clips | Animator-driven locomotion + combat, root motion forwarded selectively |

## Folder Structure

```
soulslikes-prototype/
├── Assets/
│   ├── _Project/                # all first-party work
│   │   ├── Scripts/
│   │   │   ├── AI/              # enemy state machine, attacks, hitboxes, root-motion forwarders, SMBs
│   │   │   ├── Combat/          # lock-on, weapon hitbox, health (player+enemy), anim-event relay, attack-tag SMB
│   │   │   ├── Player/          # controller, combat input, stamina, head IK, root-motion forwarder
│   │   │   └── UI/              # HUD bars (stamina, player HP, enemy HP), lock-on indicator
│   │   ├── Animations/          # PlayerLocomotion.controller, MutantAI.controller
│   │   ├── Input/               # PlayerControls.inputactions + generated PlayerControls.cs wrapper
│   │   ├── Models/              # Mixamo FBXs — Y Bot (player), mutants/, weapon packs
│   │   ├── Materials/
│   │   ├── UI/
│   │   ├── Tests/Editor/        # Soulslike.Tests.Editor — import verifier + Measure Clip Travel (EditMode)
│   │   ├── Tests/Play/          # Soulslike.Tests.Play — P1 dodge i-frame tests (PlayMode)
│   │   └── Soulslike.asmdef     # first-party runtime assembly (covers Scripts/ + Input/)
│   └── Scenes/
│       └── SampleScene.unity    # the one scene — player, mutant, arena, HUD, cameras
├── docs/                        # this doc system + day-1-to-7-plan.md (build roadmap)
├── Packages/                    # manifest.json (package deps)
├── ProjectSettings/             # layers, physics collision matrix, input handler
└── CLAUDE.md                    # workflow + per-day implementation notes
```

> Note: `_Project/` holds only the folders currently in use (Animations, Input, Materials, Models, Scripts, Tests, UI). Prefabs/VFX/Audio are conventions from CLAUDE.md that don't exist on disk yet. The active scene is `Assets/Scenes/SampleScene.unity`, not under `_Project/`.

> **Assembly layout (since 2026-06-21):** all first-party runtime code compiles into the **`Soulslike`** assembly (asmdef at `_Project/` root, refs `Unity.InputSystem` + `Cinemachine` + `UnityEngine.UI`). EditMode tests live in **`Soulslike.Tests.Editor`** (refs `Soulslike` + the Test Framework); PlayMode tests in **`Soulslike.Tests.Play`** (P1 dodge i-frame/combat invariants). This split is what lets tests target first-party types; adding the asmdef did not break scene/prefab references (Unity binds MonoBehaviours by script GUID, unaffected by assembly membership). See `docs/feature-import-verifier.md`.

## System Overview

A single-scene vertical slice of a soulslike combat loop: one player, one enemy (the Mutant), one arena.

**Player side** — `Player` root (Rigidbody, CapsuleCollider) drives position/rotation; a child `Y Bot` mesh holds the Animator and is purely visual. `PlayerController` reads Input System actions and writes velocity + animator params each `FixedUpdate`. `PlayerCombat` owns attack input and drives the Animator's combo state machine. `PlayerStamina` gates attacks. `LockOnSystem` acquires a target and swaps Cinemachine cameras. `PlayerHealth` ends the run on death.

**Enemy side** — `Enemy_Mutant` root (NavMeshAgent, EnemyHealth, EnemyAI) drives translation; a child `mutant_model` holds the Animator + `EnemyAttack` + root-motion forwarder. `EnemyAI` is a hand-rolled state machine (Idle/Aggro/Chase/Attack/Hurt/Dead/Returning) whose Attack state runs a coroutine that picks distance-banded attacks, chains combos, and randomizes tells.

**Shared combat** — hitboxes are trigger colliders enabled/disabled by Animation Events during a swing; they call `TakeDamage` on the opposing health component. `HealthChanged`/`Died` events drive HUD bars and AI reactions.

## Data Flow

**Player attack lands on enemy:**
```
Input System (LMB/RB) → PlayerCombat.OnLightAttackPressed
  → PlayerStamina.TrySpend → animator.SetTrigger("LightAttack")
  → Animator enters Light1 (tag=Attacking) → AttackingTagBehaviour sets flags + applyRootMotion
  → Animation Event EnableHitbox(dmg) → WeaponHitbox.Enable
  → OnTriggerEnter on Enemy → EnemyHealth.TakeDamage
  → HealthChanged event → EnemyHealthBar (HUD) + EnemyAI.OnHealthChanged → EnterHurt
```

**Enemy attack lands on player:**
```
EnemyAI.AttackRoutine picks attack by range → EnemyAttack.BeginAttack(type)
  → animator.SetTrigger(...) → attack state (EnemyAttackMotionBehaviour enables root motion)
  → Animation Event EnableHitbox(dmg) → EnemyHitbox active flag
  → FixedUpdate Physics.OverlapBox finds Player → PlayerHealth.TakeDamage
  → Died event → PlayerController + PlayerCombat disable (no respawn yet)
```

**Root motion:** attack/hurt/death clips bake translation into the root track. `RootMotionForwarder` (player) / `MutantRootMotionForwarder` (enemy) read `animator.deltaPosition` in `OnAnimatorMove` and forward it — player via `Rigidbody.MovePosition` (Y stripped), enemy via `NavMeshAgent.Move`. Forwarding only happens while `applyRootMotion` is true, which per-state StateMachineBehaviours toggle on entry/exit.

## Key Patterns

- **The Animator IS the combo state machine.** No script-side combo counter on the player. `PlayerCombat` just sets triggers; transition conditions (`ComboReady`, `IsAttacking`) decide where the graph goes. Avoids dual-source-of-truth bugs.
- **StateMachineBehaviours own per-state side effects.** `AttackingTagBehaviour`, `HurtRootMotionBehaviour`, `EnemyAttackMotionBehaviour` flip `applyRootMotion`/flags on state enter/exit — the reliable hook since Animation Events near state exit get dropped.
- **Animation Events for timing, with SMB/coroutine safety nets.** Hitbox enable/disable is event-driven; completion/cleanup is backstopped because Unity silently drops events authored past a state's exit time (and inflates FBX clip lengths — see `feature-ai.md`).
- **Events over singletons.** Health/stamina expose C# `event`s; HUD and AI subscribe. No global managers.
- **Two-body rig.** A physics/agent root owns transform; a child mesh owns the Animator. Camera follows position only — never parent a rotation-following camera to a player that rotates toward camera-relative input (feedback spin loop).
- **ScriptableObjects** are the intended home for weapon/enemy stats (per CLAUDE.md conventions) — not yet realized; current stats are serialized fields.
