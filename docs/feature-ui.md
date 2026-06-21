# UI / HUD

_Last updated: 7ed2c71 — recency signal, not a correctness guarantee. If the code has moved past this, trust the code._

## Description
The heads-up display and lock-on visuals: stamina bar, player health bar, world-space enemy health bar, and the lock-on reticle. All event-driven off the gameplay systems — no polling. The `Soulslike.UI` namespace.

## Files
- `Scripts/UI/StaminaBar.cs` — screen-space Slider bound to `PlayerStamina.StaminaChanged`.
- `Scripts/UI/PlayerHealthBar.cs` — screen-space Slider bound to `PlayerHealth.HealthChanged` (red fill, anchored bottom-left above the stamina bar).
- `Scripts/UI/EnemyHealthBar.cs` — world-space billboard Slider over the enemy, clamped to viewport.
- `Scripts/UI/LockOnIndicator.cs` — SpriteRenderer reticle positioned over the locked target.

## Dependencies
- `StaminaBar` ← `Soulslike.Player.PlayerStamina`.
- `PlayerHealthBar` ← `Soulslike.Combat.PlayerHealth`.
- `EnemyHealthBar` ← `Soulslike.Combat.EnemyHealth`, `Camera.main`.
- `LockOnIndicator` ← `Soulslike.Combat.LockOnSystem`, `Camera.main`.

## API / Interface
- **Bars are passive subscribers.** Each grabs its `Slider` in Awake, subscribes to the relevant `…Changed` event in OnEnable, unsubscribes in OnDisable, and writes `slider.value`/`maxValue` on the callback. `interactable=false`. The serialized source reference (stamina/health) is wired in the inspector.
- **`EnemyHealthBar`** follows `enemy.position + worldOffset` in LateUpdate, billboards toward `Camera.main`, and **clamps its viewport position within a 5% margin** (`viewportMargin=0.05`) so it stays on-screen during lock-on at any camera angle.
- **`LockOnIndicator`** resolves the target's visual center (collider bounds → renderer bounds → transform), nudges the reticle `towardCameraOffset` toward the camera, and faces it. Hidden (`sr.enabled=false`) when not locked.

## Gotchas
- **World-space UI clamping is what keeps the enemy bar visible.** Without the viewport clamp the bar floats off-screen when the camera is close or at a steep lock-on angle. Project to viewport, clamp, project back to world.
- **HUD layout:** stamina bottom-left; player HP bar red, anchored bottom-left at ~(40, 70), just above stamina. Mirror this pattern for any new bar.
- These components assume `Camera.main` exists; they re-resolve it lazily in LateUpdate if it was null at Awake.

## Changelog
- Day 3: StaminaBar (+ world-space EnemyHealthBar billboard).
- Day 4: PlayerHealthBar; EnemyHealthBar gained viewport clamping.
- Day 4.5: lowered enemy HP bar offset; confirmed viewport clamp during the new lunge attacks.
