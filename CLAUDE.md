# SoulslikePrototype — Project Instructions for Claude

## Project context

A 3D soulslike action game built in **Unity 6.4** using URP.

Combat draws from:

- **Dark Souls** — stamina-gated movement, dodge rolls with i-frames, lock-on targeting.
- **Sekiro** — deflect/parry windows, posture system, deathblows on staggered enemies.

Scope is intentionally focused: interconnected hand-crafted areas (DS1-style), not open world. The current goal is a vertical slice — one arena, one enemy, one full combat loop — before any expansion.

## Implementation plan

The full day-by-day build plan lives at **`docs/day-1-to-7-plan.md`**.

Each day in that document covers:

- The day's goal and acceptance test
- Assets to gather beforehand
- A starting plan-mode prompt
- Specific CLAUDE.md updates to add when complete

Read it before starting any "Day N" work.

---

## Daily workflow

These are trigger phrases. When I use them, follow the exact sequence below.

### When I say "let's start Day N"

1. Read this CLAUDE.md fully.
2. Read the relevant day section in `docs/day-1-to-7-plan.md`.
3. Confirm what we're building today and ask any clarifying questions.
4. Enter plan mode for the day's feature. **Do not write code until I approve the plan.**

### When I say "wrap up the day"

1. Run `git status` and `git diff --stat`. Show me the output.
2. Propose updates to this CLAUDE.md based on what we built. Focus on:
   - Non-obvious architectural decisions and _why_ they were made
   - Gotchas we hit and how we solved them
   - File locations and component contracts other features will depend on
   - Skip generic descriptions of what code does — the code says that
   - Skip information that's only relevant to the day we just finished — this is a project reference, not a journal
3. Wait for me to review and edit the proposed CLAUDE.md changes.
4. After CLAUDE.md is saved, propose a commit message and run the commit.
5. Suggest 2–3 things I should playtest before tomorrow to catch regressions.

---

## Coding conventions

### Language and style

- C# with Unity's Mono backend during development.
- Namespaces: `Soulslike.Combat`, `Soulslike.Player`, `Soulslike.AI`, etc. — one per feature area.
- Prefer `[SerializeField] private` over `public` for inspector-exposed values.
- All MonoBehaviours with tunable values get `[Header(...)]` groups in the Inspector.

### Architecture

- Use **ScriptableObjects** for weapon stats, enemy stats, and other data assets.
- **Avoid Singletons** unless I explicitly ask. Prefer dependency injection or events.
- Animation timing should use **Animation Events**, not coroutine timers, where possible.

## Project structure

All my code and assets live under `Assets/_Project/`:

- `_Project/Scripts/` — code, organized by feature folder
- `_Project/Prefabs/`
- `_Project/Scenes/`
- `_Project/Materials/`
- `_Project/Animations/`
- `_Project/Models/`
- `_Project/VFX/`
- `_Project/Audio/`

Imported asset packs (Mixamo, Asset Store, etc.) live at the root of `Assets/` where their installers place them — don't move them into `_Project/`.

## Workflow rules

- After editing C# scripts, **refresh Unity via MCP** and check the console for compile errors before claiming you're done.
- When you create a new MonoBehaviour, also tell me **how to attach it** — which GameObject, what serialized fields to set, what layer/tag.
- **Don't create new scenes** without asking.
- For animation work, prefer **Animator parameters and state machine transitions** over direct AnimationClip manipulation.
- Before committing, always show me `git status` and `git diff --stat` and wait for confirmation.

---

## Per-day notes

### Day 0 — Input System

- **Active Input Handler:** new only (`activeInputHandler: 1`). Legacy `Input.GetAxis` / `Input.GetKey` will throw at runtime — never use them.
- **Input Actions asset:** `Assets/_Project/Input/PlayerControls.inputactions`. One map: `Player`. Actions: Move (Vector2), Look (Vector2), Sprint, LightAttack, HeavyAttack, Dodge, Parry, LockOn, Interact.
- **Generated wrapper:** `Assets/_Project/Input/PlayerControls.cs`, namespace `Soulslike.Input`, class `PlayerControls`. Re-generated automatically on asset save.
- **Light vs heavy attack disambiguation (PC):** both `LightAttack` and `HeavyAttack` bind to LMB. `LightAttack` uses a Tap interaction, `HeavyAttack` uses `Hold(duration=0.25)`. Gamepad bindings are clean — RB = light only, RT = heavy only. Day 3 will exercise this; expect to tune the 0.25s threshold then.
- **Sprint is toggle, not hold.** Both Left Shift and L3 invoke the same `Sprint` action which flips a single `sprintToggled` bool. No auto-cancel on idle (stamina will gate it on Day 3). If LShift-as-toggle ends up feeling wrong, split Sprint into Hold (Shift) + Toggle (L3) variants.
- **Analog magnitude:** `PlayerController.FixedUpdate` uses `Vector3.ClampMagnitude(forward*v + right*h, 1f)` — preserves stick tilt, prevents diagonal speed-up. Don't replace with `.normalized` — that kills analog input.
- **Camera setup is throwaway.** `CameraFollow.cs` is a 10-line position-copy stub on `CameraRig`. Cinemachine replaces it on Day 2; delete `CameraFollow.cs` and the component then.
- **`CameraRig` is NOT parented to `Player`.** Parenting a rotation-following camera to a player that rotates toward camera-relative input creates a feedback spin loop. Future camera systems (Cinemachine on Day 2) must follow position only or use a yaw pivot decoupled from player rotation.

### Gotchas hit on Day 0

- **InputActionImporter wrapper-codegen settings written into `.meta` YAML do not survive Unity's first import.** Setting `m_GenerateWrapperCode: 1` in the meta gets reset to `False` on import. Fix: write the meta, then programmatically set the importer fields via `SerializedObject` and call `SaveAndReimport()` (or toggle "Generate C# Class" in the inspector). Do this anytime a new `.inputactions` asset is added.
- **MCP scene mutations made during Unity Play mode go into the play-mode-only scene instance and are lost on Play exit.** Always exit Play before scene edits, and call `manage_scene save` after `manage_gameobject create` so the change hits disk, not just the in-memory scene.

### Day 1 — Character & animator

- **Player hierarchy:** root `Player` (Rigidbody, CapsuleCollider, PlayerController) → child `Y Bot` (Animator with `PlayerLocomotion.controller`, **Apply Root Motion: off**). Player root drives position/rotation; the mesh is purely visual. PlayerController auto-resolves the Animator via `GetComponentInChildren` if not assigned.
- **Animator contract — locomotion blend tree:** parameter `Speed` (float). 1D thresholds: 0.0=Idle, 0.5=Walking, 1.0=Run Forward, 2.0=Sprinting. PlayerController writes `Speed` with damping each FixedUpdate. Future combat/dodge/hit states should go on a **separate Animator layer with mask**, not into this state machine.
- **Apply Root Motion is OFF globally** for locomotion. Day 5 (dodge roll) and Day 3 (attacks) may want it ON for specific states — flip per-state via `OnAnimatorMove` or an animator state hook, not by re-enabling globally.
- **Move input shaping:** `stickDeadzone(min=0.15,max=0.95)` on the gamepad Move binding (filters hardware noise). `moveDeadzone=0.3` in PlayerController hides the bottom 30% of stick travel as idle, then post-deadzone magnitude is floored at 0.5 so any movement is at minimum a full Walk anim — no zombie-shuffle range.
- **Backward clips (`Walking Backwards`, `Running Backward`) imported but unwired.** They enter a 2D blend tree on Day 2 once lock-on stops rotation-to-movement.

### Gotchas hit on Day 1

- **`Rigidbody.MoveRotation` accumulates `angularVelocity`.** After several rotated ticks, the rb keeps spinning even after input stops. Zero `rb.angularVelocity` at the top of FixedUpdate when driving rotation authoritatively. (Symptom: character spins on its own during idle after running.)
- **Use `Quaternion.RotateTowards`, not `Slerp`, for chase-rotation.** Slerp oscillates at the 180° antipodal point depending on quaternion sign. RotateTowards commits to one direction at a constant max rate.
- **Stick noise produces visible root yaw wobble (head/hips appear to shake).** Beyond `stickDeadzone`, add an angle deadband in PlayerController — skip rotation if `angleToTarget < 1.5°`. Bone-level gait sway in Mixamo locomotion clips is intentional and not addressed by this — Day 2's Cinemachine composer dampening masks the visual residue.
- **Mixamo FBX importers need their `clipAnimations` set programmatically before first import** — same shape as Day 0's wrapper-codegen meta gotcha. Loop Time on the imported clip won't serialize unless you assign `imp.clipAnimations = imp.defaultClipAnimations` after editing fields, then `SaveAndReimport()`.
- **Y Bot pivot is ~2cm above feet.** Whoever swaps in a different humanoid model (Day 4 enemy) will need to nudge the mesh's local Y to align feet with collider bottom; don't assume model origin == feet.

### Day 2 — Camera, lock-on, and locked locomotion

- **Cinemachine version is 2.10.7 (legacy 2.x API).** Use `CinemachineFreeLook`, `CinemachineVirtualCamera`, `CinemachineComposer`, `CinemachineFramingTransposer`, `CinemachineInputProvider` — NOT the 3.x `CinemachineCamera` API. Future cameras (impact zooms, dodge cams) must follow this.
- **Camera scene structure:** `Main Camera` has `CinemachineBrain` (default blend 0.3s ease-in-out). Two vcams: `FreeLookCam` (priority 10, orbit camera, follows Player + LookAt=`LookTarget` chest pivot child of Player) and `LockedCam` (priority 5 → 20 when locked, single-target Follow=Player + LookAt=current target, FramingTransposer body + Composer aim with screen X=0.4 / Y=0.45 over-shoulder offset).
- **`CinemachineFreeLook.m_BindingMode = WorldSpace.`** Setting binding mode on the per-rig orbital transposers does NOT work — FreeLook overwrites them from its own field every frame. WorldSpace means the camera does *not* auto-follow player rotation (souls/Sekiro convention). `m_XAxis.Value` is interpreted as world yaw (0° = camera at target's −Z, 90° = at −X, etc.).
- **TargetGroup framing was tried and abandoned.** A `CinemachineTargetGroup` with two members + `GroupComposer` aim positions the camera at a *world-space* offset from group center (the group's rotation is identity unless you drive it manually), which puts the camera at random angles and can dive below ground when both targets are at similar height. Lock-on uses single-target framing with screen-position bias on the Composer to fake "frame both."
- **Lock-on contract — `Soulslike.Combat.LockOnSystem`:** public `Transform CurrentTarget`, `bool IsLocked`. Sits on Player. Acquires via `Physics.OverlapSphere(player, 15m, EnemyLayerMask)` filtered by `GeometryUtility.CalculateFrustumPlanes(Camera.main)`, scored by screen-space distance to viewport center + small distance penalty. Auto-releases when target dies (`EnemyHealth.IsDead`), exceeds 25m, or Sprint pressed (Sprint also still flips PlayerController's `sprintToggled` from the same input event).
- **`Enemy` tag and `Enemy` layer (slot 8)** are conventions. Lock-on filters by layer mask, not tag. EnemyHealth lives at `Soulslike.Combat.EnemyHealth` — `int CurrentHealth`, `bool IsDead`, `void TakeDamage(int)`. Day 3 attack hitboxes call this.
- **Animator parameters added:** `MoveX` (float), `MoveY` (float, +forward/−back), `IsLocked` (bool). Locked locomotion uses a separate `LocomotionLocked` 2D Freeform Cartesian blend tree (9 children: Idle at center + walk-tier ring at radius 0.5 + run-tier ring at radius 1.0). Bidirectional transitions to/from `Locomotion` on `IsLocked` with 0.15s blend. PlayerController projects world-space `move` into player-local axes when locked, writes MoveX/MoveY with damping.
- **Sprint is suppressed during lock-on** (sprint press unlocks; while locked, ground speed = `walkSpeed × inputMag`, max 4 m/s). Sprint anims would need separate sprint-strafe clips to look right while locked, which we don't have.
- **Per-clip `timeScale` multipliers in both blend trees** are calibrated against `AnimationClip.apparentSpeed`. Forward/backward clips target ~25% glide (anim foot speed ~75% of body speed) — matches the visual feel of the Sprinting clip which was the user's reference for "perfect" cadence. Strafe clips target body-match with mild glide (~12%). Larger overshoot in either direction reads as either treadmill (anim faster than body) or ice-skating (anim much slower).
- **HeadLookAtIK** at `Soulslike.Player.HeadLookAtIK`, attached to Y Bot child (where the Animator lives). Uses `OnAnimatorIK` + `Animator.SetLookAtPosition`/`SetLookAtWeight`. **IK Pass MUST be enabled on the Animator Controller's layer** (`AnimatorControllerLayer.iKPass = true`) — without it `OnAnimatorIK` never fires. Active when LockOnSystem is locked; head smoothly recenters when unlocked.
- **`LookTarget`** empty GameObject at local (0, 0.4, 0) under Player is the FreeLook's LookAt chest pivot. Don't rename — FreeLookCam references it.

### Gotchas hit on Day 2

- **Replacing a Mixamo FBX (drag-drop overwrite) breaks three things at once**: (1) importer settings reset to defaults (animationType=Generic, avatarSetup=NoAvatar, empty clipAnimations — no Loop Time, no avatar source); (2) `useFileScale` may flip to `false`, causing root motion to read at 100× scale (apparentSpeed ~242 m/s instead of 2.42 m/s); (3) the new AnimationClip sub-asset gets a new fileID, orphaning all `BlendTree.children[i].motion` references that pointed at the old clip. Repair script must do all three: set Humanoid + CopyFromOther(Y BotAvatar) + clipAnimations[].loopTime=true, set useFileScale=true, then walk all blend trees and re-link any null motion to the new clip with the prior timeScale.
- **`AssetDatabase.SaveAssets()` writes the controller to disk, but the Animator caches the controller at the start of Play.** Live edits during Play don't take effect on the running instance — must Stop and re-enter Play. Asset is fine; runtime needs reset.
- **2D Freeform Cartesian blend trees produce blended foot-speed mismatch at intermediate inputs** that single-clip-multiplier tuning can't fully fix. At full input on a cardinal direction the dominant clip is at 100% weight and matches body speed exactly. At 70% input or diagonals, multiple clips with different timeScales contribute weighted averages that don't track linear body-speed scaling. Acceptable for prototype; for shipping quality use a magnitude-driven Speed parameter that drives per-clip time scales from script.
