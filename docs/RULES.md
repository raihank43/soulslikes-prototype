# Code Rules

## Main Principles

- **C# with Unity's Mono backend** during development.
- **Avoid Singletons** unless explicitly asked. Prefer dependency injection or C# `event`s (health/stamina expose events; HUD + AI subscribe).
- **Use ScriptableObjects** for weapon stats, enemy stats, and other data assets (intended; current stats are serialized fields awaiting this).
- **Animation timing uses Animation Events**, not coroutine timers, where possible — with StateMachineBehaviour/coroutine backstops because Unity drops events authored past a state's exit time.
- **Code is the source of truth.** If a doc contradicts the code, fix the doc.

## Folder Structure Conventions

- All first-party code and assets live under `Assets/_Project/`. Scripts are organized by feature folder: `Scripts/AI/`, `Scripts/Combat/`, `Scripts/Player/`, `Scripts/UI/`.
- **Imported asset packs (Mixamo, Asset Store) stay at the root of `Assets/`** where their installers place them — don't move them into `_Project/`.
- One namespace per feature area: `Soulslike.AI`, `Soulslike.Combat`, `Soulslike.Player`, `Soulslike.UI`, `Soulslike.Input`.

## Naming Conventions

- Namespaces match the feature folder (`Soulslike.<Area>`).
- Animator parameter hashes cached as `static readonly int XxxHash = Animator.StringToHash("Xxx")`.
- Layers/tags by convention: `Enemy` layer slot 8, `PlayerHitbox` slot 9, `EnemyHitbox` slot 10, `Player` slot 11.

## Code Style

- Prefer `[SerializeField] private` over `public` for inspector-exposed values.
- Group tunables with `[Header(...)]` in the Inspector.
- When creating a MonoBehaviour, also state **how to attach it** — which GameObject, serialized fields, layer/tag.
- Auto-resolve references in `Awake` (`GetComponentInChildren`, etc.) as a fallback when the inspector field is unset.

## Workflow Rules (Unity-specific)

- After editing C# scripts, **refresh Unity via MCP** and check the console for compile errors before claiming done.
- **Don't create new scenes** without asking. **Don't reintroduce legacy `Input.GetAxis`/`GetKey`** — the new Input System is the only active handler; legacy calls throw at runtime.
- For animation work, prefer **Animator parameters + state-machine transitions** over direct AnimationClip manipulation.
- The Animator **caches the controller at Play start** — live edits to states/params during Play don't take effect on the running instance; Stop and re-enter Play.
- **Don't commit** `Assets/_Recovery/` (MCP scene-backup snapshots) or `combat_log.txt` (debug log).
- The Unity MCP CodeDom executor doesn't accept `using` directives or top-level local functions — fully-qualify types and inline lambdas via `System.Action`/`System.Func`.
- **Assembly homes:** first-party runtime code → the `Soulslike` asmdef (`Assets/_Project/Soulslike.asmdef`); EditMode tests/editor tooling → `Soulslike.Tests.Editor`. A new MonoBehaviour goes in `Scripts/<area>/` and compiles into `Soulslike` automatically.
- **After any FBX reimport, swap, or new clip wired into a controller, run the import verifier** — `run_tests` (EditMode, `Soulslike.Tests.Editor`) or `Tools > Soulslike > Verify Imports`. A newly added clip needs a baseline row in `ImportSpec.Clips` or the verifier fails by design (keeps the baseline complete). See `docs/feature-import-verifier.md`.
- **Mixamo player clips all share the internal name `"mixamo.com"`** — never key a dictionary/lookup by `clip.name` for player animations; key by FBX asset path. (Mutant clips have distinct names; player clips don't.)
- **Player Settings → "Run In Background" is ON** (committed in `ProjectSettings.asset`) so MCP play-tests run while the Unity window is unfocused. Keep it enabled; if play-tests hang waiting for focus, re-check it.
- **Attacks AND dodges move via baked root motion** (`RootMotionForwarder`, `activeTags = [Attacking, Dodging]`), not scripted velocity. If you ever DO script-move this non-kinematic Rigidbody, set `linearVelocity`, not `MovePosition` — `MovePosition` on a non-kinematic body compounds velocity and ~doubles the travel (learned during the scripted-dodge detour; see `feature-player.md`).
- **Mixamo clips can have a body-vs-root-motion angle offset** (the dodge clips travel ~36–57° off their body facing). Screen a new travel clip by measuring `AnimationClip.averageSpeed`'s angle vs its expected direction; if offset, scripted-straight movement will skate — use the clip's real root motion (+ rotate to aim) or fix the clip in Blender. Most clips (locomotion, attacks) are fine.

## Anti-Patterns

Things we tried that didn't work. **Do not repeat these.**

| Anti-Pattern | What Went Wrong | Better Approach |
|-------------|----------------|-----------------|
| Re-zeroing `linearVelocity.xz` every FixedUpdate during attacks | Cancels `MovePosition` root-motion translation on a non-kinematic rigidbody | Zero velocity ONCE on attack entry via edge detector |
| Parenting a rotation-following camera to the Player | Feedback spin loop (player rotates to camera-relative input) | Camera follows position only / decoupled yaw pivot |
| `Quaternion.Slerp` for chase rotation | Oscillates at the 180° antipodal point | `Quaternion.RotateTowards` (constant max rate) |
| `.normalized` on the move vector | Kills analog stick magnitude | `Vector3.ClampMagnitude(..., 1f)` |
| Driving enemy attack completion from `AttackComplete` event | FBX importer inflates clip length, event drifts past exit time and is dropped | Absolute-time coroutine wait (`DurationFor(type)`) |
| Lowering attack probability to stop spam | AI re-rolls every cooldown, still spams | Dedicated per-attack cooldown timer |
| Toggling `collider.enabled` + OnTriggerEnter for enemy hitbox | Sleeping player Rigidbody suppresses trigger callbacks | `Physics.OverlapBoxNonAlloc` poll in FixedUpdate |
| `NavMeshAgent.baseOffset` to fix floating feet | Offsets the avoidance cylinder, not the visual | Offset the model child's `localPosition.y` |
