# Enemy AI (the Mutant)

_Last updated: 7ed2c71 — recency signal, not a correctness guarantee. If the code has moved past this, trust the code._

## Description
The single enemy: a Mixamo "Mutant" with a hand-rolled state machine, NavMesh locomotion, three attacks (Punch/Swipe/JumpAttack), distance-banded attack selection, combo chains, hit reactions, and death. The combat target the player verifies against.

## Files
- `Scripts/AI/EnemyAI.cs` — the state machine (Idle/Aggro/Chase/Attack/Hurt/Dead/Returning) + `AttackRoutine` coroutine.
- `Scripts/AI/EnemyAttack.cs` — `EnemyAttackType` enum, `BeginAttack(type)` trigger routing, hitbox event handlers, `DurationFor(type)`.
- `Scripts/AI/EnemyHitbox.cs` — `Physics.OverlapBoxNonAlloc` poll in FixedUpdate, dedupe per swing.
- `Scripts/AI/MutantRootMotionForwarder.cs` — forwards `deltaPosition` to `NavMeshAgent.Move`, with a per-swing XZ `motionMultiplier`.
- `Scripts/AI/HurtRootMotionBehaviour.cs` — SMB: `applyRootMotion=true` on Hurt entry.
- `Scripts/AI/EnemyAttackMotionBehaviour.cs` — SMB: `applyRootMotion=true` on Swipe/JumpAttack entry.
- `Scripts/AI/EnemyAnimationEventRelay.cs` — relays animation events to `EnemyAttack` (kept for parity; `EnemyAttack` also receives directly).
- `Animations/MutantAI.controller` — the animator graph.
- Scene: `Enemy_Mutant` root (NavMeshAgent, EnemyHealth, EnemyAI) → child `mutant_model` (Animator, EnemyAttack, MutantRootMotionForwarder) at localPos (0, -0.226, 0).

## Dependencies
- `Soulslike.Combat.EnemyHealth` (HealthChanged → Hurt, Died → Dead).
- `Soulslike.AI.EnemyHitbox` → `Soulslike.Combat.PlayerHealth`.
- NavMesh baked on the arena ground; AI Navigation package.

## API / Interface
- **`EnemyAI`** state machine. Key serialized knobs: ranges (`aggroRange=12`, `loseAggroRange=25`, `runDistanceThreshold=6`); attack bands (`punchRange=1.6`, `swipeRange=2.8`, `jumpAttackMinRange=3.5`, `jumpAttackMaxRange=6.0`); speeds (`walkSpeed`, `runSpeed`); combo/tells (`comboChance=0.5`, `comboMaxLength=2`, `windupMin/Max`, `attackSpeedMin/Max`); cooldowns (`attackCooldown=1.2`, `jumpAttackCooldown=5`); lunge amplifiers (`swipeMotionMultiplier=1.5`, `jumpAttackClipTravel`, `jumpAttackLandClearance`, `jumpAttackMaxMultiplier`).
- **`EnemyAttack.BeginAttack(EnemyAttackType)`** routes to `PunchTrigger`/`SwipeTrigger`/`JumpAttackTrigger`. **`DurationFor(type)`** returns wall-clock swing length at AttackSpeed=1 (Punch 0.88, Swipe 2.0, JumpAttack 2.8). `EnableHitbox(int)`/`DisableHitbox`/`AttackComplete`/`ForceCancel`.
- **`MutantRootMotionForwarder.motionMultiplier`** (NonSerialized, set per-swing by EnemyAI) scales baked XZ root motion.

## Architecture notes
- **`AttackRoutine` (coroutine) owns the full attack sequence**, replacing flag-polling. Per swing: pick attack for range → wind-up delay → set `AttackSpeed` + `motionMultiplier` → `BeginAttack` → wait `DurationFor()/AttackSpeed` → `AttackComplete()` → decide chain. Cancelled by `EnterHurt`/`OnDied`.
- **Distance-banded weighted selection (`PickAttackForRange`):** close/mid bands use `WeightedPick` with the **last-used attack's weight halved** (no-repeat bias). Returning `null` = "no attack for this range" → stay in Chase to close the gap.
- **Attack commit:** `EnterAttack`/`AttackRoutine` calls `SnapFacePlayer()` (instant LookRotation) at swing start; the mutant does NOT track the player mid-swing — the player must dodge the committed arc (soulslike convention).
- **Adaptive jump distance:** JumpAttack's `motionMultiplier` is computed at strike-commit to land `jumpAttackLandClearance` (~1m) short of the player: `clamp((dist - clearance)/jumpAttackClipTravel, 1, jumpAttackMaxMultiplier)`. Locked at commit, so moving after the leap starts still dodges it.
- **Speed param** driven from `agent.desiredVelocity` (instant) not `agent.velocity` (lags). Aggro→Chase waits for the Roar clip to fully exit, then a 0.2s grace before the body translates (prevents glide-out-of-Roar).

## Gotchas
- **FBX importer inflates `clip.length` on every reimport.** Authoring `ModelImporterClipAnimation.events` + `SaveAndReimport()` repeatedly ballooned `clip.length` (~2.6× per pass: Swipe 2.67s→5.33s→10.67s…) while the true take length stayed correct. Event times computed from `clip.length` then drifted past the visible swing AND past state exit time (silently dropped). **Fix: do NOT drive enemy attack completion from an `AttackComplete` animation event** — the coroutine waits an absolute `DurationFor(type)` instead. `EnableHitbox`/`DisableHitbox` still work (low clip fractions, before exit time).
- **Inflated length makes `normalizedTime` thresholds unreliable across attacks** (Punch stays 1.1s, Swipe/Jump inflate). Use absolute-time waits, not normalizedTime.
- **A new attack state with baked translation needs the root-motion SMB or it swings in place** — `applyRootMotion` defaults off and the forwarder drops the delta. Punch deliberately has NO SMB (in-place strike); Swipe/JumpAttack have `EnemyAttackMotionBehaviour`.
- **Per-roll attack selection ≠ rate limiting.** A low leap probability still spams because the AI re-rolls every `attackCooldown`. Throttle signature moves with a dedicated cooldown timer (`jumpAttackCooldown`), not just lower probability.
- **Animation events after a state's exit time are silently dropped** — author gameplay-critical events before exit time; SMBs are the cleanup backstop.
- **No Rigidbody on the enemy** — NavMeshAgent owns translation; a Rigidbody would conflict.
- **NavMeshAgent foot origin ≠ model origin.** Mutant origin is 22.6cm above foot soles; the model child is offset `localPosition.y = -0.226`. `baseOffset` is the wrong fix (it moves the avoidance cylinder, not the visual).
- **`EnemyHitbox` uses `OverlapBoxNonAlloc` in FixedUpdate, not OnTrigger\*** — the player Rigidbody sleeps at rest, which suppresses trigger callbacks entirely. `targetMask` = Player layer. EnemyHitbox layer (slot 10) collides with Player (slot 11) only.
- **Mutant avatar uses `CreateFromThisModel` not `CopyFromOther`** — the model has LeftEye/RightEye bones animation-only FBXs lack, so `CopyFromOther` fails the bone-count check. All `Mutant@*.fbx` use `CreateFromThisModel`.

## Changelog
- Day 4: enemy built — state machine, NavMesh locomotion, Punch, hurt reaction, death, idle variants, aggro Roar.
- Day 4.5: added Swipe + JumpAttack, `EnemyAttackType` routing, `AttackRoutine` coroutine, distance-banded weighted selection, combo chains, wind-up/speed randomization, adaptive jump distance, jump cooldown.
