# Player — locomotion, attacks, stamina

_Last updated: 7ed2c71 — recency signal, not a correctness guarantee. If the code has moved past this, trust the code._

## Description
Everything the player drives: camera-relative locomotion, lock-on strafing, the melee attack/combo system, stamina gating, and head-look IK. The `Soulslike.Player` namespace.

## Files
- `Scripts/Player/PlayerController.cs` — movement, rotation, animator locomotion params (on `Player` root).
- `Scripts/Player/PlayerCombat.cs` — attack input, combo triggers, stamina spend (on `Player` root).
- `Scripts/Player/PlayerStamina.cs` — `max=100`, regen `20/s` after `1s` delay.
- `Scripts/Player/HeadLookAtIK.cs` — head tracks lock-on target via `OnAnimatorIK` (on Y Bot).
- `Scripts/Player/RootMotionForwarder.cs` — forwards **attack** root motion to the Rigidbody (Y stripped); gated by `activeTags` (`Attacking`). Dodge travel is scripted in `PlayerDodge`, not forwarded.
- `Scripts/Player/PlayerDodge.cs` — dodge roll with i-frames + scripted directional movement (on `Player` root).
- `Scripts/Player/DodgingTagBehaviour.cs` — SMB that keeps `applyRootMotion` **off** on the `Dodge` state so the directional clip plays in-place (PlayerDodge scripts the travel).
- Hierarchy: `Player` (Rigidbody, CapsuleCollider, controller/combat/stamina/health/lock-on/dodge) → child `Y Bot` (Animator with `PlayerLocomotion.controller`, **Apply Root Motion off**).

## Dependencies
- Input System (`PlayerControls`): Move, Look, Sprint, LightAttack, HeavyAttack, LockOn, etc.
- `PlayerController` ← `LockOnSystem.IsLocked/CurrentTarget`, `PlayerHealth.Died`.
- `PlayerCombat` ← `PlayerStamina.TrySpend`, `PlayerHealth.Died`.
- `WeaponHitbox` (Combat) driven by animation events on the attack clips.

## API / Interface
- **Locomotion (`PlayerController.FixedUpdate`):** writes `Speed` (1D blend) when free, `MoveX`/`MoveY` (2D strafe blend) + `IsLocked` when locked. Camera-relative move vector via `Vector3.ClampMagnitude(forward*v + right*h, 1f)` (preserves analog tilt — never `.normalized`). `moveDeadzone=0.3`, post-deadzone magnitude floored to 0.5 (min Walk anim). Rotation via `Quaternion.RotateTowards` (not Slerp), skipped under `rotationAngleDeadband=1.5°`.
- **Attacks (`PlayerCombat`):** The **Animator IS the combo state machine** — no script-side counter. On press: `PlayerStamina.TrySpend(cost)` then `animator.SetTrigger("LightAttack"/"HeavyAttack")`. `IsInCommittedAttack()` blocks heavy interrupts during Heavy/Light3 only. Input buffering (`bufferLifetime=0.4s`) + acceptance lockout (`inputAcceptedLockout=0.15s`). Costs: Light 22/18/28, Heavy 45. Damage: Light 18/22/32, Heavy 45.
- **Sprint is a toggle** (`sprintToggled` flips on each Sprint press) and is **suppressed during lock-on** (a sprint press unlocks instead).
- **Dodge (`PlayerDodge`) — turn-and-roll hybrid:** `TryDodge()` (Dodge input + P1 tests) gates on not-already-dodging / not-dead / off-cooldown / `PlayerStamina.TrySpend(30)`. Uses the dodge clips' **real root motion** (forwarded by `RootMotionForwarder` on the `Dodging` tag), NOT scripted velocity. `AimDodge` picks the clip via `DodgeX`/`DodgeY` in the `Dodge` blend tree by the input's local cardinal: **forward/back → a dive clip + rotate-to-aim** (`facingYaw = aimYaw - clipTravelAngle`, `clipTravelAngle≈-36°`; the character turns INTO the roll, so facing is not preserved mid-roll); **left/right → directional sidestep clips, keep facing** (their own root motion does the lateral hop). Dodge state `speed=1.5`, exit `0.92` + `0.20s` blend. A `Dodging` tag locks movement (`PlayerController`) + blocks attacks (`PlayerCombat`), same pattern as `Attacking`.
- **i-frames:** driven by `PlayerDodge`'s coroutine on **absolute time** (≈`iFrameStart`0.2→`iFrameEnd`0.55s), NOT animation events — a dropped `EndIFrames` event would leave the player permanently invulnerable. The coroutine sets `PlayerHealth.IsInvulnerable` and a `try/finally` (+ `OnDisable`) guarantees it clears on death/interrupt. Window + `dodgeDuration` are serialized tunables.
- **Death:** `PlayerController` and `PlayerCombat` both subscribe to `PlayerHealth.Died` → `enabled = false` (no respawn until Day 7).

## Gotchas
- **`Rigidbody.MoveRotation` accumulates `angularVelocity`** — zero `rb.angularVelocity` at the top of FixedUpdate or the character spins on its own at idle after rotating.
- **Fighting `MovePosition` with `linearVelocity=0` cancels the move.** On a non-kinematic rigidbody, `MovePosition` computes the velocity needed to reach the target each step; re-zeroing XZ velocity every FixedUpdate during attacks undoes root-motion translation. Zero velocity ONCE on attack entry via the `wasAttacking` edge detector, then leave the rigidbody alone.
- **`RootMotionForwarder` strips the Y component** — Mixamo attack clips bake 14–20cm of vertical pelvis bob that would lift the body off the ground. Y is owned by physics.
- **"In-place" Mixamo attack clips actually bake 0.5–2.4m in `RootT.z`** — don't override with scripted velocity; the foot animation is authored to match the baked translation (scripted lunges read as ice-skating).
- **`HeadLookAtIK` needs IK Pass enabled on the animator layer** (`iKPass=true`) or `OnAnimatorIK` never fires.
- **Camera must NOT be parented to Player** — a rotation-following camera on a player that rotates toward camera-relative input is a feedback spin loop.
- **Light vs heavy on PC both bind to LMB** — Light uses Tap, Heavy uses Hold(0.25s). Gamepad is clean (RB light, RT heavy).
- **Dodge i-frames are coroutine-driven, never animation events.** A dropped event = permanent invulnerability. `PlayerDodge` is built so the i-frame window runs on absolute time independent of the animator — which is also why the P1 PlayMode test (`Soulslike.Tests.Play`) can prove i-frames with no scene/clip.
- **The dodge clips have a body-vs-root-motion angle offset — a clip-data quirk, not Unity.** Each clip's baked travel points ~36–57° off its body facing (`clip.averageSpeed`: dive ~-36°, dodges ~-57°). So any *straight-scripted* slide makes the legs and the travel disagree → "sideways skating" (we tried scripted velocity first and abandoned it). The working fix is **turn-and-roll**: forward the clip's OWN root motion (body + travel stay the authored coherent pair) and rotate the character to aim that travel (forward/back); left/right keep facing and accept the natural lateral-ish direction. `PlayerController` skips `FaceTarget` during the `Dodging` tag. Unity import can't realign the offset (Root Transform offset rotates body+root together) — only editing the clip (Blender) or a clean clip fixes it at the source. **Most Mixamo clips are fine** (locomotion/attacks were); this is specific to these dodges. If you swap the dive clip, re-measure its `averageSpeed` angle into `clipTravelAngle`.
- **The dive clip lands in a crouch**, so the `Dodge→Locomotion` transition pops to standing. Softened with `speed 1.5` + exit `0.92` + `0.20s` fixed blend; a tuck-roll clip (ends standing) would remove it fully.
- **`MovePosition` on a non-kinematic rigidbody compounds velocity and ~doubles the distance** (learned the hard way during the scripted-dodge detour). To script-move this body, set `linearVelocity`; attacks/dodges use baked root motion via `RootMotionForwarder` (`activeTags = [Attacking, Dodging]`, set on the **scene component** too since the serialized value overrides the code default).
- **Play-mode observation samples a moving game.** Polling position/state with discrete MCP calls can catch a bad instant (a dodge mid-flight read as "not moved"). Sample after enough elapsed time, or trust the deterministic PlayMode test.

## Changelog
- Day 1: PlayerController + animator locomotion (Y Bot, blend tree).
- Day 2: lock-on strafe locomotion (MoveX/MoveY/IsLocked), HeadLookAtIK.
- Day 3: PlayerCombat, PlayerStamina, RootMotionForwarder, combo state machine.
- Day 4: PlayerHealth death handling (disable controller/combat).
- Day 5: PlayerDodge (dodge + i-frames), DodgingTagBehaviour, `Dodge` blend-tree state; P1 PlayMode i-frame tests. Final = **turn-and-roll hybrid** (fwd/back dive rotate-to-aim, left/right sidesteps keep facing, using clips' real root motion). Iterated through scripted-velocity first and abandoned it — the dodge clips' root motion is angle-offset from the body, so scripted-straight skated.
