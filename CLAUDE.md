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
- **Camera scene structure:** `Main Camera` has `CinemachineBrain` (default blend 0.3s ease-in-out). Two vcams: `FreeLookCam` (priority 10, orbit camera, follows Player + LookAt=`LookTarget` chest pivot child of Player) and `LockedCam` (priority 5 → 20 when locked, single-target Follow=Player + LookAt=current target, FramingTransposer body `trackedObjectOffset.y=0, cameraDistance=4.5, screenY=0.65` + Composer aim `screenX=0.5 (centered), screenY=0.55`).
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

### Day 3 — Combat: attacks, stamina, combos, weapons

- **Combat code lives at `Soulslike.Combat` (`PlayerCombat`, `WeaponHitbox`, `AttackingTagBehaviour`, `AnimationEventRelay`, `EnemyHealth`) and `Soulslike.Player` (`PlayerStamina`, `RootMotionForwarder`).** `PlayerCombat` owns input + trigger setting; the Animator IS the combo state machine — there is no script-side combo counter. `PlayerCombat.IsInCommittedAttack()` blocks heavy interrupts only during Heavy and Light3 (Light1/Light2 are interruptible into Heavy via dedicated transitions).
- **Animator state graph (base layer):** added states `Light1`, `Light2`, `Light3`, `Heavy` (all tagged `Attacking`). New params: `LightAttack`/`HeavyAttack` triggers, `ComboReady` bool, `IsAttacking` bool, `AttackLungeSpeed` float (currently unused — leave param for future re-introduction). Transitions: Any-State → Light1/Heavy on trigger + `!IsAttacking`. Light1→Light2, Light2→Light3 on `LightAttack && ComboReady`. Light1→Heavy and Light2→Heavy on `HeavyAttack` (allows light→heavy chain). Each attack state has TWO exit transitions: → `Locomotion` (`!IsLocked`) and → `LocomotionLocked` (`IsLocked`) with the same `exitTime`/`duration` so post-attack snap-back is identical regardless of lock-on. **`InterruptionSource = Source` on the exits** so a late press during the blend can still interrupt.
- **Exit timing convention:** `Light1/Light2 exitTime=0.40, duration=0.15` (interruptible openers); `Light3/Heavy exitTime=0.55, duration=0.15` (committed). Recovery tail of the clip gets cut — the strike commits, then a quick blend back to locomotion. Don't extend exitTime to "finish the animation" — the tail IS the wait the player feels.
- **`AttackingTagBehaviour` (StateMachineBehaviour) sets `IsAttacking=true`, `applyRootMotion=true` on enter; on exit it BAILS if the next state is also Attacking** (chain transitions fire OnStateExit AFTER OnStateEnter of destination — without this guard, Light1's exit clobbers the IsAttacking/applyRootMotion that Light2's entry just set, freezing root motion mid-chain). **On non-chain exit, also calls `cachedHitbox.Disable()`** as a safety net — `DisableHitbox` animation events that fire after state exit time are silently dropped, so the SMB is the source of truth for "no longer attacking."
- **Root motion contract:** attack states use baked clip root motion. `RootMotionForwarder` (on Y Bot) reads `anim.deltaPosition` in `OnAnimatorMove` and forwards to the parent Rigidbody via `MovePosition`, **stripping the Y component** — Mixamo attack clips bake 14–20cm of vertical pelvis bob into the root track which would lift the body off the ground. Y is owned by physics. `PlayerController` zeros XZ velocity ONCE on attack entry (kills sprint carryover via a `wasAttacking` edge detector) then leaves the rigidbody alone — re-zeroing every FixedUpdate fights `MovePosition` on a non-kinematic rigidbody and produces zero forward translation.
- **Mixamo "in-place" is a lie.** Every Pro-Pack attack clip used has 0.5–2.4m baked in `RootT.z`. Light1/Light2 = ~0.5m (a step); Light3/Heavy = ~2m+ (large committed lunges). Don't override these with scripted velocity — the foot animation is authored to match the baked translation, scripted lunges desync feet from body and read as ice-skating.
- **Idle root drift fix:** the `sword and shield idle.fbx` clip had ±5cm of `RootT.x/z` drift baked into the pose with `lockRootPositionXZ=False, lockRootRotation=False`. Set BOTH to `True` to extract drift to the root motion channel where `applyRootMotion=off` discards it. **`lockRootHeightY=False` retained** to preserve breathing bob in the pose. Same lock pattern (`lockRootHeightY=True`) applied to all four attack clips so vertical pelvis motion goes into the root track that `RootMotionForwarder` zeros.
- **Foot IK**: `iKOnFeet=True` on `Locomotion`, `LocomotionLocked`, and all four attack states. Requires `iKPass=true` on layer 0 (already on from Day 2 HeadLookAtIK). Needed especially for attack states because Mixamo clips were authored at varying ground heights and without IK the feet float visibly.
- **Animation Events on attack clips** (authored via `ModelImporterClipAnimation.events`, persisted on FBX import): `EnableHitbox(int damage)` at ~18-20% of clip (visual sword contact), `DisableHitbox` at ~33-45% (before exit time so it fires within state lifetime). Light1/Light2 also have `OpenComboWindow` at ~12-15% and `CloseComboWindow` AFTER exit time (intentionally never fires — `AttackingTagBehaviour.OnStateExit` resets `ComboReady` instead, keeping the combo window open from mid-swing until state exit). Damage values: Light1=18, Light2=22, Light3=32, Heavy=45. Stamina costs: 22/18/28/45.
- **`WeaponHitbox`** sits on `Sword_R/Hitbox` (BoxCollider, `isTrigger=true`, `PlayerHitbox` layer slot 9). Per-swing damage configured via the `EnableHitbox(int)` event parameter; `HashSet<EnemyHealth>` dedupes hits within a single swing. Layer Collision Matrix: `PlayerHitbox` collides with `Enemy` only (slot 8), nothing else.
- **`PlayerStamina`** at `Soulslike.Player.PlayerStamina`: max=100, regen=20/s, regen-delay=1s after spend. Public `bool TrySpend(float)`, `Current`, `Max`, `event StaminaChanged`. `PlayerCombat` spends at the moment of input acceptance (before SetTrigger). Insufficient stamina = press silently consumed, no chain advance, no half-attempt.
- **`EnemyHealth`** has `event Action<int,int> HealthChanged(current, max)` fired in `Awake` and on every `TakeDamage`, plus `event Action Died` (invoked once at 0 HP — body stays in scene for death animation, no `SetActive(false)`). World-space billboard health bar lives at `Enemy_Mutant/HealthBar` — see `Soulslike.UI.EnemyHealthBar` (Slider in a world-space Canvas, `LateUpdate` billboards toward Camera.main + clamps to viewport margins so it stays visible during lock-on at any angle).
- **Sword/Shield mesh attachment** is per-weapon manual tuning on the scene transforms (no automated bone-axis math — that approach took multiple iterations and still required manual fixup). When a real weapon-swap system arrives, encapsulate per-weapon `localPosition`/`localRotation`/`localScale` on a `WeaponAttachment` MonoBehaviour stored on each weapon prefab so equip code reads the values rather than computing them.
- **Asset prep recipe extension** (in addition to Day 1's Humanoid+CopyFromOther+useFileScale): for any clip with visible feet glide or "hovering during animation," check `RootT.x/y/z` curve ranges via `AnimationUtility.GetCurveBindings`. If a range > ~3cm and pose drift is visible, set the matching `lockRoot*` field on the importer's `clipAnimations[i]`. For attacks where you want the body to translate, leave `lockRootPositionXZ=False`; for in-place idles set it `True`.

### Gotchas hit on Day 3

- **`StateMachineBehaviour.OnStateExit` fires AFTER the destination state's `OnStateEnter` during a transition.** Chain transitions (Light1→Light2) silently break unless the source's OnStateExit checks `current.tagHash == AttackingTagHash || (IsInTransition && next.tagHash == Attacking)` and bails. Without the guard, the source clobbers IsAttacking/applyRootMotion mid-chain.
- **Reordering transitions via `RemoveTransition + AddTransition` of the same object destroys it.** Unity's API treats `RemoveTransition` as destruction; the reference becomes invalid. To reorder, capture all field values, remove all, then create FRESH transitions in the desired order with `AddTransition(state)` and re-author conditions/timing manually.
- **Fighting `Rigidbody.MovePosition` with `linearVelocity = 0` cancels the move.** On a non-kinematic rigidbody, `MovePosition` works by computing the velocity needed to reach the target each physics step. Scripts that re-zero `linearVelocity.xz` every FixedUpdate during attacks (to prevent sprint carryover) silently undo the root-motion-driven translation. Solution: zero velocity ONCE on state entry via an edge detector, then leave the rigidbody alone.
- **CodeDom executor in Unity MCP doesn't accept `using` directives or top-level local function declarations.** Fully-qualify Unity types (`UnityEditor.Animations.AnimatorState`) and inline lambdas via `System.Action`/`System.Func`. Don't waste a round trip writing the Roslyn-style version first.
- **Animation Events with int parameter need exact spelling for the receiving method.** A typo in `EnableHitbox` event vs method name = silent no-op, hitbox never activates. Console warns "X is missing" on first trigger — check on first play test of any new clip.
- **Animator caches the controller asset at Play start** (Day 2 lesson re-confirmed). Live edits to states/transitions/parameters during Play don't propagate to the running Animator. Stop and re-enter Play after any controller mutation.
- **Foot IK fights the pose if XZ root motion is locked but feet still stride.** When `lockRootPositionXZ=True` on a clip whose feet authored a forward step, the pelvis is frozen but feet reach forward in pelvis-local space — IK pulls the foot to ground at the extended position, stretching the leg. Either unlock XZ (let body translate with feet) or disable IK on that state. Mixamo light-attack clips ship with foot strides; we unlocked XZ.

### Day 4 — Enemy AI, player health, damage loop

- **Enemy hierarchy:** root `Enemy_Mutant` (NavMeshAgent, EnemyHealth, EnemyAI) at (5, 0.08, 5) → child `mutant_model` at localPosition (0, -0.226, 0) to align feet with NavMeshAgent base (Mixamo mutant origin is 22.6cm above foot bones). Model child has Animator with `MutantAI.controller`, `MutantRootMotionForwarder`, `EnemyAttack`. **No Rigidbody on enemy** — NavMeshAgent owns translation; a Rigidbody would conflict.
- **AI state machine at `Soulslike.AI.EnemyAI`:** states Idle/Aggro/Chase/Attack/Hurt/Dead/Returning. NavMeshAgent drives locomotion with `walkSpeed=1.22, runSpeed=2.45` (matched to Mixamo clip `apparentSpeed`). Speed param driven from `agent.desiredVelocity` (not actual velocity — desiredVelocity responds instantly, actual velocity lags behind acceleration). Agent tuning: `radius=0.6, height=2.44, angularSpeed=360, acceleration=200, stoppingDistance=1.2`.
- **Aggro → Chase pipeline:** Aggro triggers a Roar animation; `AggroRoutine` coroutine polls `animator.GetCurrentAnimatorStateInfo(0).IsName("Roar")` until the clip fully exits before entering Chase. `chaseMoveAllowedTime` adds a 0.2s grace period after entering Chase so the animator settles into Locomotion before the body starts translating (prevents glide-out-of-Roar).
- **Attack commit convention:** `EnterAttack` calls `SnapFacePlayer()` (instant `Quaternion.LookRotation`) to commit strike direction at swing start. `TickAttack` does NOT face the player — the mutant is locked to its strike vector mid-punch (soulslike convention: player must dodge the committed arc, not a tracking swing). Future attacks should follow this pattern.
- **`EnemyAttack` at `Soulslike.AI.EnemyAttack`:** orchestrates hitbox enable/disable via Animation Events (`EnableHitbox(int damage)`, `DisableHitbox`, `AttackComplete`). `IsAttackComplete` flag checked by `EnemyAI.TickAttack` to transition back to Chase. `ForceCancel()` called on hurt/death to abort in-progress swings.
- **`EnemyHitbox` at `Soulslike.AI.EnemyHitbox`:** uses `FixedUpdate` with `Physics.OverlapBoxNonAlloc` instead of `OnTriggerEnter/Stay`. Reason: player Rigidbody can sleep, which suppresses trigger events entirely. Manual overlap poll with `[SerializeField] LayerMask targetMask` (set to Player layer 2048) guarantees detection. `HashSet<PlayerHealth>` dedupes hits per swing. BoxCollider 0.8³ on `mixamorig:RightHand` bone. **Layer `EnemyHitbox` at slot 10** collides with **Player at slot 11** only.
- **`MutantRootMotionForwarder`** on mutant_model child: `OnAnimatorMove` reads `animator.deltaPosition` and forwards to `NavMeshAgent.Move()` when agent is enabled and on navmesh. Used by Hurt state (backstep root motion) and Death state (fall-to-ground root motion). `HurtRootMotionBehaviour` (StateMachineBehaviour) flips `applyRootMotion=true` on Hurt entry, restores on exit.
- **MutantAI.controller:** Parameters: `Speed`(float), `IdleVariant`(int), `RoarTrigger`, `PunchTrigger`, `HurtTrigger`, `DeadTrigger`. States: Locomotion (1D blend tree), IdleArm, Roar, Punch (tag=`Attacking`, speed=1.0, iKOnFeet=false), Hurt (tag=`Hurt`, speed=1.5, iKOnFeet=true, `HurtRootMotionBehaviour` SMB), Dead (iKOnFeet=false). Hurt transitions: AnyState→Hurt duration=0.05s; Hurt→Locomotion exitTime=0.30, duration=0.25s, interruptionSource=Source.
- **Hurt animation:** `Standing React Large From Front` clip at 1.5× speed, exit at 30% — plays fast with recovery tail cut. Root motion XZ unlocked for backstep (body actually moves backward via NavMeshAgent.Move). `lockRootHeightY=true` to prevent vertical drift.
- **Mutant FBX avatar:** uses `CreateFromThisModel` (not `CopyFromOther`). The mutant model has LeftEye/RightEye bones that animation-only FBXs lack — `CopyFromOther` fails the bone-count check. All `Mutant@*.fbx` animation files also use `CreateFromThisModel` for consistency.
- **Mutant textures:** Mixamo embeds textures inside the FBX. Unity doesn't auto-extract them — must call `ModelImporter.ExtractTextures("targetFolder")` to get diffuse/normal/specular as standalone assets, then wire them to a URP/Lit material.
- **`PlayerHealth` at `Soulslike.Combat.PlayerHealth`:** max=100, `TakeDamage(int)`, events `HealthChanged(int current, int max)` and `Died`. Player death: `PlayerController` and `PlayerCombat` both subscribe to `Died` → `enabled = false` (no respawn until Day 7). `PlayerHealthBar` at `Soulslike.UI.PlayerHealthBar` mirrors StaminaBar pattern — screen-space Slider anchored bottom-left at (40, 70), red fill.
- **Idle variant cycling:** `EnemyAI.TickIdle` toggles `IdleVariant` (int param, 0 or 1) every 4-7s random interval. Animator has `IdleArm` state driven by this param. Adds ambient life to idle mutants.
- **Punch clip events:** `EnableHitbox(25)` at 50%, `DisableHitbox` at 70%, `AttackComplete` at 80%. `lockRootPositionXZ=true, lockRootHeightY=true` (in-place strike). Death clip: all locks false (root motion drives fall-to-ground).
- **`_Recovery/` folder:** Unity MCP creates scene backup snapshots here. Add to `.gitignore` — don't commit.

### Gotchas hit on Day 4

- **Mixamo `CreateFromThisModel` needed per-file** — can't share one avatar across model + animation FBXs when the model has extra bones (eye bones). Each FBX builds its own avatar from the humanoid it contains.
- **`defaultClipAnimations` is empty after first Humanoid reimport.** Must clear `clipAnimations = new ModelImporterClipAnimation[0]`, call `SaveAndReimport()`, THEN read `defaultClipAnimations` to get the auto-generated entries. Only then can you modify and re-assign them.
- **`__preview__mixamo.com` clips:** `AssetDatabase.LoadAllAssetsAtPath` returns preview clips alongside real ones. Filter with `!name.StartsWith("__preview__")`.
- **NavMeshAgent foot origin vs model origin:** Mixamo mutant's origin is 22.6cm above foot soles. The model child must be offset `localPosition.y = -0.226` so feet sit on the NavMesh surface. `NavMeshAgent.baseOffset` is NOT the right fix (it offsets the avoidance cylinder, not the visual).
- **Sleeping Rigidbody kills OnTriggerEnter/Stay.** Player Rigidbody goes to sleep at rest. Enemy hitbox toggling `collider.enabled` while the player is inside produces zero trigger callbacks. `OverlapBoxNonAlloc` in `FixedUpdate` is the reliable path.
- **`agent.desiredVelocity` vs `agent.velocity` for Speed param.** `velocity` lags behind actual movement due to NavMeshAgent acceleration smoothing. `desiredVelocity` reflects intent instantly — use it for animator Speed parameter to avoid delayed animation transitions.
- **Animation events after state exit time are silently dropped.** Events authored at normalized times beyond the state's `exitTime` fraction never fire because the out-transition has already started. Author all gameplay-critical events (hitbox enable/disable, combo windows) before exit time, and use StateMachineBehaviour.OnStateExit as a defensive cleanup.
- **`CloseComboWindow` intentionally placed after exit time.** This is by design — the combo window stays open from `OpenComboWindow` until `AttackingTagBehaviour.OnStateExit` resets `ComboReady`. Placing `CloseComboWindow` before exit time shrinks the combo window and breaks chaining.
