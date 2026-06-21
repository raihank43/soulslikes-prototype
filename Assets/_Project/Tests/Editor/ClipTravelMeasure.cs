using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Soulslike.Tests
{
    /// <summary>
    /// Editor helper: measures an animation clip's baked root-motion travel direction
    /// (<see cref="AnimationClip.averageSpeed"/>). Use it to screen a new Mixamo clip for the
    /// "body-vs-root-motion offset" quirk BEFORE committing to it, and to read off the angle to
    /// plug into <c>PlayerDodge.clipTravelAngle</c> for turn-and-roll.
    ///
    /// Measures the selected FBX(s) in the Project window; if nothing's selected, scans every FBX
    /// in <c>Assets/_Project/Models/dodge</c>. Angle legend: 0 = forward, +90 = right, -90 = left,
    /// 180 = back. A "forward" clip reading ~0 is clean; a large angle means the clip travels off
    /// its facing (turn-and-roll aims it; a scripted-straight slide would skate).
    /// </summary>
    internal static class ClipTravelMeasure
    {
        [MenuItem("Tools/Soulslike/Measure Clip Travel")]
        private static void Measure()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Clip travel — root-motion direction (0=fwd, +90=right, -90=left, 180=back):");
            int n = 0;
            foreach (var path in CollectFbxPaths())
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var clip = o as AnimationClip;
                    if (clip == null || clip.name.StartsWith("__preview__")) continue;
                    var a = clip.averageSpeed;
                    float speed = new Vector2(a.x, a.z).magnitude;
                    float angle = Mathf.Atan2(a.x, a.z) * Mathf.Rad2Deg;
                    string note = speed < 0.05f
                        ? "in-place (no travel)"
                        : "travels " + angle.ToString("F0") + "deg @ " + speed.ToString("F2") + " m/s  (clipTravelAngle = " + angle.ToString("F0") + ")";
                    sb.AppendLine("  " + System.IO.Path.GetFileName(path) + " :: '" + clip.name + "'  ->  " + note);
                    n++;
                }
            }
            if (n == 0) sb.AppendLine("  (no clips — select an FBX in the Project window, or put clips in Assets/_Project/Models/dodge/)");
            Debug.Log(sb.ToString());
        }

        private static List<string> CollectFbxPaths()
        {
            var result = new List<string>();
            foreach (var obj in Selection.objects)
            {
                var p = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(p) && p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) && !result.Contains(p))
                    result.Add(p);
            }
            if (result.Count == 0)
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Project/Models/dodge" }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) && !result.Contains(p)) result.Add(p);
                }
            return result;
        }
    }
}
