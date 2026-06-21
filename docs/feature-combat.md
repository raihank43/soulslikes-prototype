# Combat — hitboxes, health, lock-on

_Last updated: 7ed2c71 — recency signal, not a correctness guarantee. If the code has moved past this, trust the code._

## Description
The shared combat layer: weapon/enemy hitboxes, health for both sides, the lock-on targeting system, and the animation-event plumbing that ties swings to damage. The `Soulslike.Combat` namespace.

## Files
- `Scripts/Combat/LockOnSystem.cs` — target acquisition + Cinemachine camera swap (on `Player`).
- `Scripts/Combat/WeaponHitbox.cs` — player's sword trigger collider (on `Sword_R/Hitbox`).
- `Scripts/Combat/PlayerHealth.cs` — `max=100`, `TakeDamage`, `HealthChanged`/`Died` events, `IsDead`.
- `Scripts/Combat/EnemyHealth.cs` — `int CurrentHealth`/`MaxHealth`, `TakeDamage(int)`, `HealthChanged`/`Died`, `IsDead`.
- `Scripts/Combat/AttackingTagBehaviour.cs` — SMB on player attack states (flags + root motion + hitbox-off backstop).
- `Scripts/Combat/AnimationEventRelay.cs` — forwards player animation events to PlayerCombat/WeaponHitbox (lives on Y Bot, where the Animator is).

## Dependencies
- `WeaponHitbox` → `EnemyHealth.TakeDamage`. `EnemyHitbox` (AI) → `PlayerHealth.TakeDamage`.
- `LockOnSystem` → Cinemachine `LockedCam`/`FreeLookCam`, `EnemyHealth.IsDead`, `PlayerControls` (LockOn + Sprint + Look actions).
- HUD bars subscribe to the health events.

## API / Interface
- **`LockOnSystem`**: `Transform CurrentTarget`, `bool IsLocked`. Acquire = `Physics.OverlapSphere(acquireRadius=15, enemyMask)` filtered by camera frustum, scored by screen-space distance to viewport center + small distance penalty (`screenDist + dist*0.02`). Auto-releases on target death, beyond `releaseRadius=25`, or Sprint press. Right-stick flick switch is a logged **stub** (single enemy — nothing to switch to yet).
- **`PlayerHealth` / `EnemyHealth`**: `TakeDamage(int)`, `event Action<int,int> HealthChanged(current, max)` (fired in Awake + every hit), `event Action Died` (once at 0). On death both disable child colliders; the body stays in scene for the death animation (no `SetActive(false)`).
- **`WeaponHitbox`**: `Enable(int damage)` / `Disable()`. Collider disabled by default; `HashSet<EnemyHealth>` dedupes per swing. On `PlayerHitbox` layer (slot 9), collides with Enemy (slot 8) only.
- **`AttackingTagBehaviour`** (SMB): on enter sets `IsAttacking=true`, `ComboReady=false`, `applyRootMotion=true`; on non-chain exit clears flags and calls `cachedHitbox.Disable()`.

## Gotchas
- **`StateMachineBehaviour.OnStateExit` fires AFTER the destination's `OnStateEnter`** during a transition. Chain transitions (Light1→Light2) break unless the source's OnStateExit bails when the current/next state is still `Attacking`-tagged — otherwise it clobbers the flags the next state just set.
- **`DisableHitbox` animation events fire after state exit time and get dropped**, leaving the weapon hitbox enabled (you'd damage the enemy by walking near it post-swing). `AttackingTagBehaviour.OnStateExit` calls `cachedHitbox.Disable()` as the source-of-truth backstop.
- **Cinemachine is 2.10.7 (legacy 2.x API)** — use `CinemachineFreeLook`/`CinemachineVirtualCamera`/`CinemachineComposer`/`CinemachineFramingTransposer`, NOT the 3.x `CinemachineCamera` API.
- **`CinemachineFreeLook.m_BindingMode = WorldSpace`** so the camera does NOT auto-follow player rotation (souls convention). Setting binding mode on the per-rig transposers doesn't work — FreeLook overwrites them each frame.
- **TargetGroup framing for lock-on was tried and abandoned** — it placed the camera at world-space offsets from group center, giving random angles and below-ground dives. Lock-on uses single-target framing with screen-position bias instead.
- **`Enemy` layer (slot 8) is a convention; lock-on filters by `enemyMask`, not tag.**
- Animation Events with an int parameter need exact method-name spelling or they silently no-op (console warns "X is missing" on first trigger).

## Changelog
- Day 2: LockOnSystem + Cinemachine camera swap.
- Day 3: WeaponHitbox, EnemyHealth, AttackingTagBehaviour, AnimationEventRelay.
- Day 4: PlayerHealth, EnemyHealth.Died event (body stays for death anim), collider-disable on death.
- Day 4.5: tightened player attack event windows; AttackingTagBehaviour hitbox-off backstop.
