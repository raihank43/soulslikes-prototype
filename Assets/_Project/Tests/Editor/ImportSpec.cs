using System.Collections.Generic;

namespace Soulslike.Tests
{
    /// <summary>Avatar setup expected for a tracked model/clip FBX.</summary>
    internal enum AvatarMode { CreateFromThis, CopyFromOther }

    /// <summary>Known-good import baseline for one tracked animation FBX (keyed by asset path).</summary>
    internal readonly struct ImportBaseline
    {
        public readonly AvatarMode Avatar;
        public readonly bool Loop;
        public readonly bool LockXZ;
        public readonly bool LockY;
        public readonly bool LockRot;
        public readonly float ExpectedLength; // <= 0 means "don't check length"

        public ImportBaseline(AvatarMode avatar, bool loop, bool lockXZ, bool lockY, bool lockRot, float length = 0f)
        {
            Avatar = avatar; Loop = loop; LockXZ = lockXZ; LockY = lockY; LockRot = lockRot; ExpectedLength = length;
        }
    }

    /// <summary>Baseline for an avatar-source model FBX (no animation clip of its own).</summary>
    internal readonly struct ModelBaseline
    {
        public readonly AvatarMode Avatar;
        public ModelBaseline(AvatarMode avatar) { Avatar = avatar; }
    }

    /// <summary>
    /// The single source of truth for what "correctly imported" means.
    /// Baseline captured 2026-06-21 from the known-good working project (see docs/feature-import-verifier.md).
    /// Keyed by FBX asset path because Mixamo clips all share the internal name "mixamo.com".
    /// </summary>
    internal static class ImportSpec
    {
        // Universal invariants applied to every tracked animation FBX:
        //   animationType == Human, useFileScale == true, apparentSpeed <= MaxApparentSpeed.
        // apparentSpeed is an UPPER bound only: the 100x-scale bug inflates it (2.42 -> 242),
        // while idles/in-place attacks legitimately read ~0, so a lower bound would false-fail.
        public const float MaxApparentSpeed = 10f;
        public const float LengthTolerance = 0.10f; // +/-10% when a length is declared

        public static readonly string[] Controllers =
        {
            "Assets/_Project/Animations/PlayerLocomotion.controller",
            "Assets/_Project/Animations/MutantAI.controller",
        };

        // Avatar-source models: referenced by the scene/prefab, not as controller motions,
        // so they cannot be auto-discovered and are listed explicitly.
        public static readonly Dictionary<string, ModelBaseline> SourceModels = new Dictionary<string, ModelBaseline>
        {
            ["Assets/_Project/Models/Y Bot.fbx"] = new ModelBaseline(AvatarMode.CreateFromThis),
            ["Assets/_Project/Models/mutants/mutant_model.fbx"] = new ModelBaseline(AvatarMode.CreateFromThis),
        };

        // Per-asset baseline keyed by FBX path. Every clip discovered from the controllers
        // must have an entry here, or the verifier fails -- which keeps this table complete
        // and flags any newly added clip that hasn't been blessed yet.
        public static readonly Dictionary<string, ImportBaseline> Clips = new Dictionary<string, ImportBaseline>
        {
            // --- Player: CopyFromOther -> Y BotAvatar ---
            ["Assets/_Project/Models/Pro Sword and Shield Pack/sword and shield idle.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: true, lockY: false, lockRot: true),
            ["Assets/_Project/Models/Walking.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/Run Forward.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/Sprinting.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/Idle.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/Walking Backwards.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/Left Strafe Walking.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/Right Strafe Walking.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/Running Backward.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/Left Strafe.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/Right Strafe.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/Pro Sword and Shield Pack/sword and shield slash.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: false, lockXZ: false, lockY: true, lockRot: false),
            ["Assets/_Project/Models/Pro Sword and Shield Pack/sword and shield slash (3).fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: false, lockXZ: false, lockY: true, lockRot: false),
            ["Assets/_Project/Models/Pro Sword and Shield Pack/sword and shield attack (2).fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: false, lockXZ: false, lockY: true, lockRot: false),
            ["Assets/_Project/Models/Pro Sword and Shield Pack/sword and shield attack (3).fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: false, lockXZ: false, lockY: true, lockRot: false),

            // --- Mutant: CreateFromThisModel (eye bones make CopyFromOther fail the bone-count check) ---
            ["Assets/_Project/Models/mutants/Mutant@Mutant Breathing Idle.fbx"] = new ImportBaseline(AvatarMode.CreateFromThis, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/mutants/Mutant@Mutant Walking.fbx"] = new ImportBaseline(AvatarMode.CreateFromThis, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/mutants/Mutant@Mutant Run.fbx"] = new ImportBaseline(AvatarMode.CreateFromThis, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/mutants/Mutant@Mutant Idle.fbx"] = new ImportBaseline(AvatarMode.CreateFromThis, loop: true, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/mutants/Mutant@Mutant Roaring.fbx"] = new ImportBaseline(AvatarMode.CreateFromThis, loop: false, lockXZ: false, lockY: false, lockRot: false),
            ["Assets/_Project/Models/mutants/Mutant@Mutant Punch.fbx"] = new ImportBaseline(AvatarMode.CreateFromThis, loop: false, lockXZ: true, lockY: true, lockRot: false),
            ["Assets/_Project/Models/mutants/Mutant@Standing React Large From Front.fbx"] = new ImportBaseline(AvatarMode.CreateFromThis, loop: false, lockXZ: false, lockY: true, lockRot: false),
            ["Assets/_Project/Models/mutants/Mutant@Mutant Dying.fbx"] = new ImportBaseline(AvatarMode.CreateFromThis, loop: false, lockXZ: false, lockY: false, lockRot: false),
            // NOTE: Swiping (len 5.33) and Jump Attack (len 10.29) are CURRENTLY length-inflated vs their
            // true swings (~2.0 / ~2.8 per EnemyAttack.DurationFor). Length is intentionally left unchecked
            // (ExpectedLength = 0) until the FBXs are re-imported clean -- see docs/feature-import-verifier.md.
            ["Assets/_Project/Models/mutants/Mutant@Mutant Swiping.fbx"] = new ImportBaseline(AvatarMode.CreateFromThis, loop: false, lockXZ: false, lockY: true, lockRot: true),
            ["Assets/_Project/Models/mutants/Mutant@Mutant Jump Attack.fbx"] = new ImportBaseline(AvatarMode.CreateFromThis, loop: false, lockXZ: false, lockY: true, lockRot: true),

            // --- Dodge (Day 5; HYBRID, all use REAL root motion; CopyFromOther -> Y BotAvatar; lockY
            //     strips vertical bob, XZ kept so the clips travel). Forward/back = dive (turn-and-roll);
            //     left/right = directional sidesteps (keep facing). "Forward/Backward" dodge clips are
            //     unused (left on disk). ---
            ["Assets/_Project/Models/dodge/Y Bot@Standing Dive Forward.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: false, lockXZ: false, lockY: true, lockRot: false),
            ["Assets/_Project/Models/dodge/Y Bot@Standing Dodge Left.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: false, lockXZ: false, lockY: true, lockRot: false),
            ["Assets/_Project/Models/dodge/Y Bot@Standing Dodge Right.fbx"] = new ImportBaseline(AvatarMode.CopyFromOther, loop: false, lockXZ: false, lockY: true, lockRot: false),
        };
    }
}
