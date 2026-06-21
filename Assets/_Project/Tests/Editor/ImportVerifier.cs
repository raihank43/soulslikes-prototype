using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Soulslike.Tests
{
    /// <summary>One failed expectation: which asset, which field, what it is vs what it should be.</summary>
    internal readonly struct Violation
    {
        public readonly string Asset;
        public readonly string Field;
        public readonly string Actual;
        public readonly string Expected;

        public Violation(string asset, string field, string actual, string expected)
        {
            Asset = asset; Field = field; Actual = actual; Expected = expected;
        }

        public override string ToString() => $"FAIL  {Asset}\n        {Field}: {Actual}  (expected {Expected})";
    }

    /// <summary>
    /// Detect-only guard for the silent Mixamo/FBX import resets (see docs/feature-import-verifier.md).
    /// Auto-discovers the tracked clip set by walking the controllers in ImportSpec, then checks each
    /// against the known-good baseline. Never mutates. Run via the EditMode test or the menu item.
    /// </summary>
    internal static class ImportVerifier
    {
        internal static List<Violation> Verify()
        {
            var violations = new List<Violation>();
            var seen = new HashSet<AnimationClip>();

            // 1. Avatar-source models (explicit -- not referenced as controller motions).
            foreach (var kv in ImportSpec.SourceModels)
            {
                var imp = AssetImporter.GetAtPath(kv.Key) as ModelImporter;
                if (imp == null)
                {
                    violations.Add(new Violation(kv.Key, "asset", "missing / not a model", "a ModelImporter"));
                    continue;
                }
                CheckModelInvariants(kv.Key, imp, kv.Value.Avatar, violations);
            }

            // 2. Controller-referenced clips (auto-discovered).
            foreach (var path in ImportSpec.Controllers)
            {
                var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ac == null)
                {
                    violations.Add(new Violation(path, "asset", "missing", "an AnimatorController"));
                    continue;
                }
                CollectAndCheck(ac, path, seen, violations);
            }

            return violations;
        }

        private static void CollectAndCheck(AnimatorController ac, string controllerPath, HashSet<AnimationClip> seen, List<Violation> violations)
        {
            var smQueue = new Queue<AnimatorStateMachine>();
            foreach (var layer in ac.layers)
                if (layer.stateMachine != null) smQueue.Enqueue(layer.stateMachine);

            while (smQueue.Count > 0)
            {
                var sm = smQueue.Dequeue();
                foreach (var sub in sm.stateMachines)
                    if (sub.stateMachine != null) smQueue.Enqueue(sub.stateMachine);

                foreach (var cas in sm.states)
                {
                    var st = cas.state;
                    if (st.motion == null)
                    {
                        violations.Add(new Violation(controllerPath, "state '" + st.name + "'.motion", "null", "a clip or blend tree (orphaned)"));
                        continue;
                    }

                    // Walk the motion tree iteratively (no recursion -- blend trees nest).
                    var mQueue = new Queue<Motion>();
                    mQueue.Enqueue(st.motion);
                    while (mQueue.Count > 0)
                    {
                        var m = mQueue.Dequeue();
                        if (m is BlendTree bt)
                        {
                            var kids = bt.children;
                            for (int i = 0; i < kids.Length; i++)
                            {
                                if (kids[i].motion == null)
                                    violations.Add(new Violation(controllerPath, "BlendTree '" + bt.name + "' child[" + i + "].motion", "null", "a clip (orphaned)"));
                                else
                                    mQueue.Enqueue(kids[i].motion);
                            }
                            continue;
                        }
                        if (m is AnimationClip clip && seen.Add(clip))
                            CheckClip(clip, violations);
                    }
                }
            }
        }

        private static void CheckModelInvariants(string path, ModelImporter imp, AvatarMode expectedAvatar, List<Violation> violations)
        {
            if (imp.animationType != ModelImporterAnimationType.Human)
                violations.Add(new Violation(path, "animationType", imp.animationType.ToString(), "Human"));
            if (!imp.useFileScale)
                violations.Add(new Violation(path, "useFileScale", "False", "True"));
            CheckAvatar(path, imp, expectedAvatar, violations);
        }

        private static void CheckAvatar(string path, ModelImporter imp, AvatarMode expected, List<Violation> violations)
        {
            var expectedSetup = expected == AvatarMode.CreateFromThis
                ? ModelImporterAvatarSetup.CreateFromThisModel
                : ModelImporterAvatarSetup.CopyFromOther;
            if (imp.avatarSetup != expectedSetup)
                violations.Add(new Violation(path, "avatarSetup", imp.avatarSetup.ToString(), expectedSetup.ToString()));
        }

        private static void CheckClip(AnimationClip clip, List<Violation> violations)
        {
            var path = AssetDatabase.GetAssetPath(clip);

            if (!ImportSpec.Clips.TryGetValue(path, out var b))
            {
                violations.Add(new Violation(path, "baseline", "clip '" + clip.name + "' has no baseline entry", "an entry in ImportSpec.Clips"));
                return;
            }

            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null)
            {
                violations.Add(new Violation(path, "importer", "not a ModelImporter", "a ModelImporter"));
                return;
            }

            // Universal invariants.
            if (imp.animationType != ModelImporterAnimationType.Human)
                violations.Add(new Violation(path, "animationType", imp.animationType.ToString(), "Human"));
            if (!imp.useFileScale)
                violations.Add(new Violation(path, "useFileScale", "False", "True"));
            if (clip.apparentSpeed > ImportSpec.MaxApparentSpeed)
                violations.Add(new Violation(path, "apparentSpeed", clip.apparentSpeed.ToString("F1"), "<= " + ImportSpec.MaxApparentSpeed + " (scale bug?)"));
            CheckAvatar(path, imp, b.Avatar, violations);

            // Loop flag (read from the baked clip settings -- reliable across import paths).
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime != b.Loop)
                violations.Add(new Violation(path, "loopTime", settings.loopTime.ToString(), b.Loop.ToString()));

            // Root-lock flags (live on the importer clip entry, not the baked clip).
            var arr = imp.clipAnimations;
            if (arr == null || arr.Length == 0) arr = imp.defaultClipAnimations;
            ModelImporterClipAnimation entry = null;
            if (arr != null)
                foreach (var c in arr)
                    if (c.name == clip.name) { entry = c; break; }
            if (entry != null)
            {
                if (entry.lockRootPositionXZ != b.LockXZ)
                    violations.Add(new Violation(path, "lockRootPositionXZ", entry.lockRootPositionXZ.ToString(), b.LockXZ.ToString()));
                if (entry.lockRootHeightY != b.LockY)
                    violations.Add(new Violation(path, "lockRootHeightY", entry.lockRootHeightY.ToString(), b.LockY.ToString()));
                if (entry.lockRootRotation != b.LockRot)
                    violations.Add(new Violation(path, "lockRootRotation", entry.lockRootRotation.ToString(), b.LockRot.ToString()));
            }

            // Optional length check (opt-in; catches the FBX clip-length inflation bug).
            if (b.ExpectedLength > 0f)
            {
                float tol = b.ExpectedLength * ImportSpec.LengthTolerance;
                if (Mathf.Abs(clip.length - b.ExpectedLength) > tol)
                    violations.Add(new Violation(path, "length", clip.length.ToString("F2"),
                        b.ExpectedLength.ToString("F2") + " +/-" + (ImportSpec.LengthTolerance * 100f) + "%"));
            }
        }

        internal static string BuildReport(List<Violation> violations)
        {
            if (violations.Count == 0) return "Import verifier: all tracked assets OK.";
            var sb = new StringBuilder();
            sb.AppendLine("Import verifier found " + violations.Count + " violation(s):");
            foreach (var v in violations) sb.AppendLine(v.ToString());
            return sb.ToString();
        }

        [MenuItem("Tools/Soulslike/Verify Imports")]
        private static void VerifyMenu()
        {
            var violations = Verify();
            var report = BuildReport(violations);
            if (violations.Count == 0) Debug.Log(report);
            else Debug.LogError(report);
        }
    }
}
