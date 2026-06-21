# Asset Import Verifier

_Last updated: 2026-06-21 (1984e55; built + green, + Measure Clip Travel sibling tool) — recency signal, not a correctness guarantee. If the code has moved past this, trust the code._

## Description

A detect-only guard that catches the **silent Mixamo/FBX import resets** that recurred across Days 0–4.5 — the single biggest source of build iterations. When an FBX reimports (on a meta change, a re-drag, or a Unity restart), Unity can silently flip `useFileScale` (→ 100× scale, `apparentSpeed` ~242 m/s), wipe `clipAnimations[].loopTime`, reset `animationType`/avatar, orphan `BlendTree.children[].motion` references, or inflate `clip.length`. None of these throw — they surface only in a playtest, costing a full human round-trip.

The verifier asserts that every **tracked** asset still matches its known-good import config, so a reset is caught the moment I run `run_tests` (or the user clicks the menu) instead of mid-playtest. It is **P2** in the build-feedback-loop plan (`docs/proposals/2026-06-21-rethink.md`).

**Scope guard:** only assets reachable from the two controllers (+ the two avatar-source models) are checked — the ~50 unused Pro Sword & Shield Pack clips are deliberately ignored.

## Decided design

_Forged 2026-06-21. Each choice carries its rejected alternative inline. Choices marked **(forge-default)** were my recommendation, presented but not separately confirmed by the user._

- **Truth source — auto-discover + lean C# rules.** The tracked clip set is *derived* by walking both controllers (`PlayerLocomotion`, `MutantAI`) for every `Motion`; expected values are **universal invariants** applied to all, plus a small C# override table (`ImportSpec.Overrides`) declaring only the per-clip exceptions. _(Rejected: a ScriptableObject manifest — a hand-maintained asset that can itself drift or orphan and must be opened in Unity to review. Rejected: universal-rules-only — would miss a wiped per-clip loop flag.)_
- **Detect-only.** The check reports each violation as `asset · field · actual · expected` and never mutates. Repair stays the existing `execute_code` recipe (memory: FBX replacement repair recipe; journal Day 2). _(Rejected: detect+one-click-repair menu — more surface area, and a test that fixes things can mask a regression. May revisit as a follow-up once P2 proves itself.)_
- **Trigger — EditMode test + editor menu.** One `[Test]` I run via MCP `run_tests` (and future CI), plus `Tools/Soulslike/Verify Imports` that prints the same report to the Console. _(Rejected: an `AssetPostprocessor` import-time warning — fires on every import with overhead/noise and can't perform the controller-level orphan check. Parked as a future enhancement.)_
- **Clip-length check — opt-in per clip.** An optional `expectedLength` on an override asserts `clip.length` within **±10%**; clips without it skip the length check. Catches the Day-4.5 2.6× inflation directly for the clips that had importer-authored events (Swipe, JumpAttack). _(Rejected: skip length entirely — would miss that whole class; we keep it opt-in so undeclared clips carry no maintenance burden.)_
- **(forge-default) `apparentSpeed` band [0.5, 8.0] m/s** for every tracked animation clip — wide enough for slow idles and fast attacks, tight enough that the 100× scale bug (~242 m/s) always trips it.
- **(forge-default) `loopTime` defaults `false`**; only loopers (Idle, Walk, Run, Sprint, strafes) are declared `true` in the override table. Keeps the table short since most clips are one-shots.
- **(forge-default) Two avatar-source models are an explicit 2-entry list** (`Y Bot.fbx`, `mutant_model.fbx`) — they aren't referenced by the controllers as motions, so they can't be auto-derived; they still need avatar/scale assertions.
- **(forge-default) Avatar-mode classification:** avatar-source models + every `Mutant@*` clip assert `CreateFromThisModel` (the Day-4 eye-bone gotcha — `CopyFromOther` fails the bone-count check); all other tracked (player) clips assert `CopyFromOther` with `sourceAvatar == Y BotAvatar`.
- **(forge-default) Lives in an Editor test assembly** `Soulslike.Tests.Editor` that references the P0 runtime asmdef + `UnityEditor` + NUnit. The baseline data lives in `ImportSpec.cs`; the checks + menu item in `ImportVerifier.cs`; the `[Test]` in `ImportVerifierTests.cs`.

### Build refinements (2026-06-21) — data overruled three forge-defaults

Dumping the real import state before writing the baseline overturned three assumptions. Recorded here because the forge couldn't have known them:

- **Keyed by FBX asset path, not clip name.** Every player Mixamo clip shares the internal name `"mixamo.com"` (all 11 collide); only the mutant clips have distinct names. Clip-name keys were unusable — the table is keyed by asset path.
- **`apparentSpeed` is an UPPER bound only (`<= 10`), not the planned `[0.5, 8.0]` band.** Idles and in-place attacks legitimately read `apparentSpeed ≈ 0`; a lower bound would false-fail them. The 100×-scale bug inflates speed (2.42 → 242), so only the ceiling matters. `useFileScale == true` remains the primary scale guard.
- **A full per-asset baseline, not "universal invariants + sparse overrides."** With ground truth in hand, every tracked clip gets an explicit `ImportBaseline` (loop + all three lock flags + avatar mode). A discovered clip with no baseline entry is itself a failure — that keeps the table complete and flags newly added clips.

## Files

- `Assets/_Project/Soulslike.asmdef` — **P0**: the first-party runtime assembly (covers `Scripts/` + the generated `Input/PlayerControls.cs`). References `Unity.InputSystem`, `Cinemachine`, `UnityEngine.UI`. Adding it does **not** break scene/prefab refs — Unity binds MonoBehaviours by script GUID, which an asmdef doesn't change.
- `Assets/_Project/Tests/Editor/Soulslike.Tests.Editor.asmdef` — Editor-only test assembly; references `Soulslike`, `UnityEditor.TestRunner`, `UnityEngine.TestRunner`, `nunit.framework.dll`; `defineConstraints: [UNITY_INCLUDE_TESTS]`.
- `Assets/_Project/Tests/Editor/ImportSpec.cs` — `AvatarMode` enum, `ImportBaseline`/`ModelBaseline` structs, the universal-invariant constants (`MaxApparentSpeed`, `LengthTolerance`), and the per-asset baseline dictionaries (`SourceModels`, `Clips`) keyed by FBX path.
- `Assets/_Project/Tests/Editor/ImportVerifier.cs` — discovery (walk controllers), the assertion logic, `BuildReport`, and the `Tools/Soulslike/Verify Imports` `[MenuItem]`.
- `Assets/_Project/Tests/Editor/ImportVerifierTests.cs` — the `[Test]` `TrackedAssets_ImportConfig_MatchesBaseline`.
- `Assets/_Project/Tests/Editor/ClipTravelMeasure.cs` — a sibling editor helper (not part of the verifier): **Tools ▸ Soulslike ▸ Measure Clip Travel** prints a clip's root-motion travel angle (`AnimationClip.averageSpeed`), used to screen dodge/roll clips for the body-vs-root-motion offset and read off `PlayerDodge.clipTravelAngle`. See `feature-player.md`.

## Dependencies

- **P0 — first-party asmdef split** (prereq): the runtime scripts must live in a named asmdef so the test assembly can reference them. P2 cannot land before P0.
- **Unity Test Framework 1.6.0** — already installed (`Packages/manifest.json`); no new dependency.
- **The two controllers** (`PlayerLocomotion.controller`, `MutantAI.controller`) and **`Y BotAvatar`** (sub-asset of `Y Bot.fbx`) — read at verify time.
- Reads importer state via `ModelImporter` + `SerializedObject` (the `lockRoot*` flags live on `ModelImporterClipAnimation`, not the public API).

## API / Interface

```csharp
// ImportSpec.cs
enum AvatarMode { CreateFromThis, CopyFromYBot }
readonly struct ClipExpectation {
    public bool  Loop;          // default false
    public bool? LockXZ, LockY, LockRot;   // null = don't assert
    public float? Length;       // null = don't assert; else ±10%
}
static class ImportSpec {
    public const float MinApparentSpeed = 0.5f, MaxApparentSpeed = 8.0f;
    public const float LengthTolerance  = 0.10f;
    public static readonly string[] AvatarSourceModels;       // 2 entries
    public static readonly Dictionary<string, ClipExpectation> Overrides;
}

// ImportVerifier.cs
readonly struct Violation { string AssetPath; string Field; string Actual; string Expected; }
static class ImportVerifier {
    static IReadOnlyList<Violation> Verify();   // pure; used by both entry points
}
// [Test] TrackedAssets_ImportConfig_IsValid  -> Assert.IsEmpty(Verify())
// [MenuItem("Tools/Soulslike/Verify Imports")] -> logs Verify() report
```

**Report line format:** `FAIL  <asset>\n  <field>: <actual>  (expected <expected>)`.

## Gotchas

- **Discovery + orphan check are the same walk.** Recursing controllers to collect motions naturally surfaces any `null` `BlendTree.children[].motion` / `state.motion` — that *is* the orphan violation. No separate pass.
- **Mutant clips use `CreateFromThisModel`, not `CopyFromOther`** (eye bones fail the bone-count check). Classifying every `Mutant@*` clip as `CopyFromOther` would produce false failures.
- **The two avatar-source models can't be auto-derived** from the controllers (they're scene/prefab references, not motions). They're an explicit list — if a third character is added, append it.
- **Don't assert `clip.length` unless declared.** Lengths legitimately change when a clip is retrimmed; a blanket length assertion would false-fail. Opt-in only.
- **EditMode only — never enter Play.** All checks are static asset/importer inspection; entering Play would be slower and pointless here.
- **`lockRoot*` and `useFileScale` aren't all on the public `ModelImporter` API** — read via `SerializedObject` / `ModelImporterClipAnimation`. (Same access pattern the repair recipe already uses.)
- **This guards config, not feel.** It catches scale/loop/avatar/orphan/length resets; it does NOT judge whether an animation *looks* right (that's P3 screenshots + the user's eyes).
- **KNOWN DEFECT — `Mutant@Mutant Swiping` (len 5.33) and `Mutant@Mutant Jump Attack` (len 10.29) are already length-inflated** vs their true swings (~2.0 / ~2.8 per `EnemyAttack.DurationFor`), a leftover from the Day-4.5 importer-event authoring. Gameplay is unaffected (the AI uses absolute-time waits, not `clip.length`), so it's latent. Their `ExpectedLength` is left `0` (length unchecked) until the two FBXs are re-imported clean — at which point bake the true length to lock the guard. Follow-up task, not a P2 blocker.
- **How to prove the guard fires (regression-proof the verifier itself):** reflection-invoke `ImportVerifier.Verify()`, flip one importer flag (e.g. `Idle.fbx` loopTime), `SaveAndReimport`, re-invoke (expect a violation), then restore in a `finally` and re-invoke (expect 0). Done in a single `execute_code` transaction so the asset never persists dirty. A green-only test is false confidence — it must be seen red once.

## Changelog

- 2026-06-21: **Built + green.** P0 runtime asmdef split + P2 verifier (EditMode test + `Verify Imports` menu). Test passes on the known-good baseline; proven to catch a planted loopTime reset and self-restore. Data dump overruled three forge-defaults (key by path, apparentSpeed upper-bound, full baseline — see Build refinements). Surfaced the Swiping/JumpAttack length-inflation as a known defect.
- 2026-06-21: Forged (design decided).
