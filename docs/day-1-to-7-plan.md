# Soulslike Implementation Plan — Day 0 through Day 7

A focused plan to build the core combat loop of your soulslike in one room. By the end of Day 7, you'll have one arena, one enemy, and a fully working combat loop with attacks, dodges, parries, death, and respawn. **No levels, no second weapon, no menus, no story.** That comes later.

> **Note for Claude:** the daily workflow (start-of-day, end-of-day, commits, CLAUDE.md updates) is defined in the project's `CLAUDE.md`. This document only covers the per-day *content* — what we're building, what to gather, and how to verify it works. Don't re-explain the workflow; just follow it.

---

## How to Use This Guide

Each day's section has:

- **Goal** — what should be working at the end of the day.
- **Assets I need to gather** — things you (the human) need to download or import before the AI session.
- **Plan-mode prompt** — the starting prompt for Claude. When you say "let's start Day N," Claude will use this as its baseline.
- **Implementation watchpoints** — things to keep an eye on while Claude works.
- **Acceptance test** — playtest steps to confirm it's done.
- **CLAUDE.md additions to consider** — day-specific facts worth adding to project context.

Working setup: Unity Editor open with your scene visible, VS Code with terminal Claude Code running in your project folder, both side by side.

---

## Input philosophy: gamepad-first

This project targets **Xbox controller** as the primary input, with keyboard/mouse as a free fallback (Unity's Input System gives us both for one set of bindings). This matches the genre — soulslikes are designed around analog movement, four-button face layouts, and shoulder-triggered attacks.

Default control scheme:

| Action | Gamepad | Keyboard/Mouse |
|---|---|---|
| Move | Left stick | WASD |
| Look / Camera | Right stick | Mouse |
| Sprint | Click left stick (L3) | Left Shift |
| Light attack | Right bumper (RB) | Left mouse |
| Heavy attack | Right trigger (RT) | Hold left mouse |
| Dodge / roll | B button | Space |
| Parry / block | Left bumper (LB) | Right mouse |
| Lock-on | Click right stick (R3) | Tab |
| Interact | A button | E |

These bindings live in an Input Actions asset created on Day 0. Every later day uses them by name, not by hardcoded keys.

---

## Day 0 — Input System Setup (Gamepad + Keyboard)

### Goal
Replace the legacy `Input.GetAxis` movement we set up earlier with Unity's Input System. WASD and the Xbox controller's left stick should both move the character, with no code changes needed to switch between them. Plug-and-play hot-swap between gamepad and KB/M while playing.

### Assets I need to gather
- Plug in your Xbox controller (USB or Bluetooth) and confirm Windows recognizes it (Settings → Bluetooth & devices, or just check Game Controllers in the Control Panel).

### Plan-mode prompt

```
Day 0: migrate to Unity Input System with gamepad + keyboard/mouse support.

Currently my PlayerController uses legacy Input.GetAxis for movement. I want to:

1. Install the Input System package (com.unity.inputsystem) via Package Manager.
   When Unity prompts to switch to the new input system and restart, accept it.

2. Create an Input Actions asset at Assets/_Project/Input/PlayerControls.inputactions
   with these actions in a "Player" action map:
   - Move (Vector2): WASD composite + left stick
   - Look (Vector2): mouse delta + right stick
   - Sprint (Button): Left Shift + L3 (left stick click)
   - LightAttack (Button): left mouse + right bumper
   - HeavyAttack (Button): hold-style — same bindings as LightAttack but
     read via separate "Hold" interaction
   - Dodge (Button): Space + B button
   - Parry (Button): right mouse + left bumper
   - LockOn (Button): Tab + R3 (right stick click)
   - Interact (Button): E + A button

3. Generate a C# class from the Input Actions asset (right-click → Generate C# Class)
   so we can subscribe to events strongly-typed, not via string lookups.

4. Refactor PlayerController to use the generated class:
   - On Enable, enable the Player action map and subscribe to events.
   - On Disable, unsubscribe and disable.
   - Replace Input.GetAxis("Horizontal/Vertical") with reading the Move action's value.
   - The character should still walk with WASD AND the left stick — both work without
     changing code. Verify hot-swap works (switch from KB to gamepad mid-play).

5. Confirm camera mouse-look still works (we'll keep using Mouse delta for now;
   the Look action will become important on Day 2 when we add Cinemachine).

Plan only. Don't write code yet.
```

### Implementation watchpoints
- When Unity prompts to switch to the new input system, **accept and let it restart**. This disables the legacy Input class — any old `Input.GetKey` calls will silently stop working, so check the console after restart.
- The "Hold" interaction for HeavyAttack lets us distinguish tap-vs-hold on the same button. This is how soulslikes implement light-vs-heavy on one button.
- Use `Application.focusChanged` or similar if you find input getting stuck when alt-tabbing — but that's a Day 0 nice-to-have, not a blocker.

### Acceptance test
- Hit Play. WASD walks the character.
- Without stopping play, push the left stick on your controller. Character keeps walking, now driven by stick. **No re-binding, no key press needed.**
- Both inputs feel responsive. Stick movement is analog (slight tilt = slow walk).
- Camera still follows with mouse look.

### CLAUDE.md additions to consider
- Input System package is in use; do not use legacy Input class anywhere.
- Input Actions asset path: `Assets/_Project/Input/PlayerControls.inputactions`.
- Generated C# class path and namespace.
- Heavy attack is a "Hold" interaction on the same binding as light attack.

---

## Day 1 — Replace the Capsule with a Real Character

### Goal
Replace the placeholder capsule with a humanoid Mixamo model that moves with stick/WASD, sprints, and turns smoothly to face movement direction. Animations match input — analog stick gives proper walk/run blending.

### Assets I need to gather

1. Go to **https://www.mixamo.com**, sign in with a free Adobe account.
2. Search "Y Bot" or "X Bot" — simple humanoid rigs perfect for prototyping.
3. Click **Download** with these settings:
   - Format: **FBX for Unity (.fbx)**
   - Pose: **T-pose**
   - Skin: **With Skin**
4. Drag the FBX into `Assets/_Project/Models/` (create the folder if needed).
5. While on Mixamo, search and download these animations applied to your character. Use **Without Skin** for animations:
   - "Idle" (any standing idle, not pose-specific)
   - "Walking"
   - "Running" or "Run Forward"

### Plan-mode prompt

```
Day 1: replace the placeholder capsule with a Mixamo Y Bot.

I have:
- Y Bot model at Assets/_Project/Models/Ybot.fbx
- Idle, walk, run animations in Assets/_Project/Animations/

Plan how to:
1. Replace the placeholder capsule "Player" with the Y Bot model while keeping
   the existing PlayerController, Rigidbody, and CapsuleCollider.
2. Configure the Mixamo FBX (rig type Humanoid, avatar, animation import settings).
3. Create an Animator Controller with Idle/Walk/Run states using a "Speed" float
   parameter. Speed should be analog — stick tilt produces proportional values
   between idle, walk, and run, not just discrete steps.
4. Update PlayerController to write current movement magnitude to the Animator.
5. Make the character smoothly rotate to face the movement direction.

Plan only.
```

### Implementation watchpoints
- The FBX rig **must** be set to Humanoid, not Generic. Animation retargeting depends on this.
- Walk/run animations should have Loop Time enabled.
- Speed thresholds in the Animator should produce smooth blending — if you tilt the stick halfway, the character should be in mid-walk-to-run blend, not snapping between states.

### Acceptance test
- Stand still: idle plays.
- Light stick tilt or single WASD tap: walk animation, slow movement.
- Full stick or sprint button: run animation, fast movement.
- Rotate the stick around a circle: character smoothly faces the new direction without foot-sliding.

### CLAUDE.md additions to consider
- Player rig: Humanoid, Y Bot Mixamo at known path.
- Animator parameter "Speed" is analog — written from input magnitude each frame.

---

## Day 2 — Camera and Lock-On Foundation

### Goal
A proper third-person Cinemachine camera that follows smoothly with stick/mouse look, plus the bones of a lock-on system. No combat yet — just the camera and targeting infrastructure combat will need.

### Assets I need to gather

1. In Unity, **Window → Package Manager**.
2. Switch the dropdown to **Unity Registry**.
3. Search **Cinemachine** and install.

### Plan-mode prompt

```
Day 2: third-person camera with lock-on, gamepad-friendly.

I just installed Cinemachine. The Look action (right stick + mouse) is already
wired in our Input Actions.

Build:
1. Replace my manual CameraRig with a Cinemachine camera that follows the player
   smoothly. Right stick / mouse drives orbit. Tune the right-stick sensitivity
   separately from mouse — controllers and mice need different values.

2. Lock-on system:
   - LockOn input (R3 / Tab) toggles lock.
   - On activation, find the nearest GameObject tagged "Enemy" within 15 meters
     and roughly within camera frustum. Use Physics.OverlapSphere on an Enemy
     layer mask, not FindObjectsOfType.
   - While locked, camera frames both player and target; player rotates to face
     target.
   - Right-stick flick left/right while locked switches to next target in that
     direction (we'll wire the input but it's optional — just a stub for now).
   - Press input again, target dies, or target leaves 25m → unlock.

3. Add a placeholder enemy: a red capsule tagged "Enemy" on the Enemy layer at
   (5, 1, 5), with EnemyHealth.cs (public int currentHealth = 100, public method
   TakeDamage(int)). No death logic yet — just log damage.

LockOnSystem.cs should be its own component, separate from PlayerController.
Plan only.
```

### Implementation watchpoints
- Cinemachine deadzone settings on the right stick matter — too small and the camera drifts, too big and it feels unresponsive.
- The "find nearest target" should respect the camera's view cone, not just distance, or you'll lock onto enemies behind you.

### Acceptance test
- Right stick orbits camera smoothly. Mouse also works without conflict.
- Press R3 (or Tab) near the red enemy: camera frames it, character faces it.
- Press again: lock releases.
- Walk 30m away: lock auto-releases.

### CLAUDE.md additions to consider
- Camera: Cinemachine. Right-stick and mouse sensitivity tuned separately.
- Lock-on uses Physics.OverlapSphere on an "Enemy" layer.
- LockOnSystem.cs exposes a public CurrentTarget property other systems read.

---

## Day 3 — Attack, Damage, and Stamina

### Goal
Right bumper for light attack, right trigger for heavy attack. Hitboxes activate via Animation Events. Hits damage the enemy. Stamina drains on attack and regenerates when idle. Enemy can die.

### Assets I need to gather

1. Mixamo (Without Skin, FBX for Unity):
   - "Sword And Shield Slash" or "Standing Melee Attack Horizontal" (light)
   - "Sword And Shield Power Attack" or any heavy slash (heavy)
   - "Sword And Shield Death" or "Dying" (player death — needed Day 4 too)
2. Optional: free sword from Sketchfab (filter Downloadable + free, search "low poly sword"). Drop in `Assets/_Project/Models/Weapons/`.

### Plan-mode prompt

```
Day 3: attack system with damage and stamina.

Animations are in Assets/_Project/Animations/. The LightAttack and HeavyAttack
input actions are already bound (RB and RT, with LMB as fallback). HeavyAttack
uses a Hold interaction.

Build:
1. PlayerStamina.cs: max 100, regen 20/sec, 1-second regen delay after spending.

2. Attack system in PlayerCombat.cs:
   - LightAttack input: 25 stamina, 20 damage.
   - HeavyAttack input: 45 stamina, 40 damage.
   - Insufficient stamina = no attack.
   - Attacks lock movement until animation ends (Animator state tag "Attacking",
     PlayerController checks state.tag).
   - Hitbox is a child trigger collider on the sword (or right hand if no sword).
     Disabled by default. Animation Events on each attack clip fire EnableHitbox()
     and DisableHitbox() at the right frames.

3. Damage application:
   - Hitbox uses OnTriggerEnter, checks for EnemyHealth, calls TakeDamage().
   - HashSet<EnemyHealth> tracks who's been hit this swing — clear on EnableHitbox,
     prevents double-hits.

4. Enemy death:
   - When EnemyHealth.currentHealth <= 0, log "Enemy died" and disable the GameObject.
   - Proper death animation comes Day 4.

5. Simple stamina UI: a UI Slider that reflects stamina. No art yet.

Plan only. Highlight gotchas around animation events and movement-locking.
```

### Implementation watchpoints
- Animation Events fire on the GameObject the Animator is on, so `EnableHitbox` and `DisableHitbox` methods must be on a script on that same GameObject (or a parent reachable via SendMessage). This trips people up.
- Hitbox collider must have `Is Trigger` checked.
- Set up the Layer Collision Matrix so PlayerHitbox only collides with Enemy layer — saves performance and prevents weird self-hits.

### Acceptance test
- HUD shows full stamina.
- Press RB: light attack swing, stamina drops by 25, regenerates after pause.
- Hold RT: heavy attack, more stamina, more damage.
- Connecting the swing reduces enemy HP (verify in Inspector).
- Spam attacks while low-stamina: nothing happens.
- Five light attacks → enemy disappears, "Enemy died" logged.

### CLAUDE.md additions to consider
- Stamina costs and regen rates as configured.
- Animation events `EnableHitbox`/`DisableHitbox` must be on the Animator GameObject.
- Layer collision matrix is configured for PlayerHitbox ↔ Enemy only.

---

## Day 4 — A Real Enemy

### Goal
The placeholder capsule becomes a humanoid enemy with idle, walk, attack, hit-reaction, and death animations. Simple state machine AI: idle → chase → attack → return. Enemy can hurt the player.

### Assets I need to gather

1. Mixamo: a different humanoid character (e.g., "Mutant" or "Brute") for visual variety.
2. Animations for it (or retargeted from any Humanoid):
   - Combat idle (try "sword and shield idle")
   - Walking (slow approach)
   - Attack (any aggressive melee)
   - Hit reaction (search "standing react")
   - Death (search "dying")

### Plan-mode prompt

```
Day 4: real enemy with AI.

Replace the placeholder red capsule with a humanoid enemy. Model and animations
are in Assets/_Project/Models/Enemy.fbx and Assets/_Project/Animations/Enemy/.

Build:
1. EnemyAI.cs state machine — Idle, Chase, Attack, Hurt, Dead.
   - Simple enum + switch, no behavior trees.
   - Vision: 12m to acquire, 20m to lose.
   - Chase uses NavMeshAgent.
   - Attack: stop within 2m, play attack animation.
   - Attack hitbox uses the same animation-event pattern as the player.
     Damage: 25.
   - Hurt: brief stun (~0.4s) on taking damage. Plays hit reaction.
   - Dead: disable AI, play death animation, disable colliders, don't despawn.

2. PlayerHealth.cs: max 100. Takes damage from enemy hitbox. On death, disables
   PlayerController and plays death animation. Respawn comes Day 7.

3. Bake a NavMesh on the floor plane for pathfinding.

NavMeshAgent vs Rigidbody: pick one approach for enemy locomotion (probably
NavMeshAgent without a Rigidbody) and stick with it. Don't mix.

Plan only.
```

### Implementation watchpoints
- For the NavMesh: in Unity 6, use the **NavMeshSurface** component (from the AI Navigation package) — it's the modern approach. Bake at runtime or in the editor.
- Hit reactions need an Animator "Any State" transition with a trigger to interrupt cleanly.
- If the enemy has both Rigidbody and NavMeshAgent, they will fight each other. Pick one.

### Acceptance test
- Enter Play. Enemy idles.
- Walk within 12m. Enemy notices, walks toward you.
- Within 2m: enemy stops, attacks, you take 25 damage if standing still.
- Hit the enemy: brief flinch, then resumes chase.
- Kill it: death animation, stays on the ground.
- Run 25m+ away: enemy gives up, returns to spawn idle.

### CLAUDE.md additions to consider
- Enemies use NavMeshAgent only, no Rigidbody locomotion.
- AI is enum-based state machine in EnemyAI.cs.
- Vision/attack/lose-sight ranges as configured.

---

## Day 5 — Dodge Roll with i-Frames

### Goal
Dodge input (B button / Space) triggers a roll. During the middle frames, player is invulnerable — the iconic Dark Souls i-frames. Roll has stamina cost, brief cooldown, and direction follows movement input or backward if no input.

### Assets I need to gather

1. Mixamo: "Sword And Shield Roll" or "Forward Roll" or "Dodge". One direction is fine for now.

### Plan-mode prompt

```
Day 5: dodge roll with i-frames.

Roll animation is at Assets/_Project/Animations/Roll.fbx. The Dodge input is
already bound (B / Space).

Build:
1. PlayerDodge.cs:
   - Dodge input triggers a roll.
   - Cost: 30 stamina. Insufficient = no roll.
   - Direction: current Move input direction; if no input, backward relative to
     character facing.
   - During roll, character moves at fixed speed (~6 m/s) in roll direction.
   - Movement input is locked.

2. i-frames:
   - During ~0.2s to ~0.55s of the roll animation (middle 35% of a ~1s roll),
     player ignores damage.
   - Animation Events StartIFrames() and EndIFrames() set
     PlayerHealth.isInvulnerable.
   - PlayerHealth.TakeDamage early-returns when invulnerable.

3. Animator state tagged "Dodging". PlayerController locks input on this tag,
   same as "Attacking" tag.

Plan only.
```

### Implementation watchpoints
- Test whether the roll animation has root motion baked in. You may want to disable Apply Root Motion and drive the roll movement manually — easier to tune speed.
- The i-frame window timing (0.2–0.55s) is a starting point. Expect to tune it.

### Acceptance test
- Press B: character rolls in input direction (or backward if neutral).
- Stamina drops by 30 per roll. Can't roll while low.
- Time a roll to overlap an enemy attack: **no damage**.
- Roll too early or too late: damage taken.
- Spam B: rolls only fire when stamina allows.

### CLAUDE.md additions to consider
- Roll animation event timings for StartIFrames/EndIFrames.
- PlayerHealth.isInvulnerable is the single source of truth for i-frame state.
- Animator tag "Dodging" locks player input.

---

## Day 6 — The Sekiro Parry (The Hard One)

### Goal
Parry input (LB / RMB) triggers a brief deflect window. If an enemy attack hits during the window: spark VFX, metal clang, enemy posture takes damage, brief stagger, no damage to player. Max enemy posture → staggered → vulnerable to deathblow. Player has posture too — failed parries chip it.

This is the centerpiece of your game. Expect to spend the full day, then re-tune over the next week.

### Assets I need to gather

1. Mixamo: "block" or "shield block" — any short defensive pose.
2. Free spark VFX: Unity Asset Store "Particle Pack" (free) has hit sparks. Or Sketchfab/OpenGameArt.
3. Free clang SFX: Freesound.org has many CC-licensed metal-clash sounds. Save as `.wav` in `Assets/_Project/Audio/`.

### Plan-mode prompt

```
Day 6: Sekiro-style deflect/parry. This is the centerpiece feature.

Build:
1. Player parry on Parry input (LB / RMB):
   - 0.2s "perfect deflect" window right after input.
   - Then 0.4s "block" window (reduces damage 70%, but player posture damage).
   - Then 0.3s recovery.
   - On perfect deflect: nullify damage, spark VFX at parry point, clang SFX,
     enemy takes 35 posture damage.
   - On block: 30% incoming damage, 20 player posture damage.
   - On recovery: full damage to player.
   - Cost: 10 stamina per attempt.

2. PostureSystem.cs on player and enemies:
   - Player max 100, enemy max 80.
   - Regen 8/sec when not recently hit (1s delay).
   - At max posture: enter "Staggered" state, briefly cannot act, vulnerable
     to deathblow.

3. Deathblow: when enemy is Staggered and player presses LightAttack within range,
   instant kill (use any heavy animation as placeholder).

4. UI: posture bars for player (near stamina) and enemy (floating above when
   locked-on). Only visible when posture > 0.

5. Feedback:
   - Spark VFX prefab spawned at parry contact point.
   - Clang SFX 3D-positioned.
   - Hit-stop: 0.05s at 0.1x time scale on perfect deflect.
   - Camera shake: very subtle (< 0.05 amplitude). Less is more.

Architectural decision to flag: when enemy attack hitbox hits player, does it
ask the player "are you parrying?" (player-pull) or does the player have a
parry-collider that intercepts attacks (collision-based)? Pick player-pull for
the first version — simpler.

Plan only.
```

### Implementation watchpoints
- **First implementation will feel bad.** That's normal. Tuning IS the work.
- Key tunables: deflect window width (0.15–0.3s range), posture damage values, hit-stop duration, camera shake amplitude.
- Don't go below 0.15s on the deflect window — feels miserable.
- Don't go above 0.1s on hit-stop — feels disruptive.

### Acceptance test
- Time the parry input within 0.2s of an enemy hit: spark, clang, no damage, enemy stumbles, enemy posture takes 35.
- Slightly late (0.2–0.6s): blocked. Reduced damage. Player posture takes ~20.
- Way late: full damage.
- Parry 3–4 times successfully: enemy posture fills, enters Staggered. Light-attack input within range = deathblow animation.
- Fail multiple parries: player's posture fills, brief stumble.

### CLAUDE.md additions to consider
- Parry windows: 0.2s deflect / 0.4s block / 0.3s recovery.
- Architecture chosen: player-pull (enemy hitbox queries player state on hit).
- Posture max values, regen, and stagger thresholds.
- Feedback constants (hit-stop duration, camera shake amplitude) — these are tuning targets.

---

## Day 7 — Death, Respawn, and Combat HUD

### Goal
Close the loop. On death: fade to black, respawn at checkpoint with full HP/stamina, dead enemies revived (Dark Souls bonfire behavior). Clean HUD shows HP, stamina, posture.

### Assets I need to gather

Nothing new — use what you have. Unity may prompt to import TextMeshPro essentials when you first add TMP text; accept.

### Plan-mode prompt

```
Day 7: death/respawn loop and final HUD.

Build:
1. Checkpoint system:
   - Checkpoint GameObject (just a marker, no model).
   - Place near player start.
   - PlayerHealth on death: respawn coroutine — fade to black 1s, teleport to
     last checkpoint, restore full HP/stamina/posture, fade in 0.5s.

2. Enemy respawn (Dark Souls bonfire behavior):
   - On player respawn, all enemies restored: re-enabled, full health/posture,
     AI back to Idle, position back to spawn.
   - EnemySpawnPoint.cs records initial state on Start, restores on respawn.

3. Final HUD:
   - HP bar (top-left, red).
   - Stamina (below, green).
   - Player posture (below, yellow, only when > 0).
   - Locked-on enemy: HP and posture floating above their head.
   - Unity UI Canvas + Image (Filled type).

4. Death screen: "YOU DIED" red TextMeshPro text fades in for 2s before respawn
   fade-out.

Plan only.
```

### Implementation watchpoints
- Coroutines are the right tool for the fade/teleport sequence.
- Enemy respawn needs to capture spawn state cleanly — position, rotation, full health, full posture, AI state.

### Acceptance test
The full loop:
1. Start scene. HUD shows full bars.
2. Walk to enemy. Lock on. Enemy HP bar appears.
3. Combat: attack, dodge, parry. Eventually you die.
4. "YOU DIED" appears, fade to black.
5. Fade in: at checkpoint, full HP/stamina/posture. Enemy alive again, full HP, idle.
6. Repeat. The whole loop should feel smooth.

### CLAUDE.md additions to consider
- Checkpoint mechanic: PlayerHealth respawns to last checkpoint position.
- EnemySpawnPoint.cs records-and-restores enemy initial state.
- HUD lives on a single Canvas using Filled Image bars and TextMeshPro.

---

## After Day 7: What Now

You have the entire core loop working in one room. **Resist the urge to build levels or a second weapon.** Instead:

### Week 2: Tune Until It Feels Good

Play your prototype for 30 minutes a day for a week. Notes on what feels bad. Tune those numbers. The list will be long:

- "Light attacks feel sluggish to start" → reduce animation startup frames.
- "Roll i-frames feel too generous" → narrow the window by 0.05s.
- "Parry timing is unforgiving" → widen by 0.05s or add audio lead time.
- "Enemy attacks are too easy to parry" → vary timing, add unparriable attacks.

This is the actual game. Tuning numbers IS the work. Have a friend play silently — you'll see ten things obvious to fix.

### Week 3+: Then Expand

Only after the single-room combat is satisfying:
1. Second enemy type (different attack pattern, e.g., a fast unparriable lunge).
2. Bonfire/checkpoint object with a "rest" interaction.
3. Second area connected by a doorway/loading trigger.
4. Second weapon with different timing/damage feel.
5. Better art.

If single-room combat doesn't feel good, none of the above will save it. That's the From Software lesson.
