using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Soulslike.Player;
using Soulslike.Combat;

namespace Soulslike.Tests
{
    /// <summary>
    /// P1 — combat invariants for the dodge roll. The headline is i-frames: a binary, numeric
    /// property that's genuinely hard to verify by eye. PlayerDodge drives the i-frame window on
    /// absolute time independent of the animator, so these run on a bare 3-component GameObject —
    /// no scene, no clip, fully deterministic.
    /// </summary>
    public class PlayerDodgeTests
    {
        private GameObject player;

        private PlayerDodge MakePlayer(out PlayerHealth health, out PlayerStamina stamina)
        {
            player = new GameObject("TestPlayer");
            health = player.AddComponent<PlayerHealth>();
            stamina = player.AddComponent<PlayerStamina>();
            return player.AddComponent<PlayerDodge>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (player != null) Object.Destroy(player);
            player = null;
        }

        [UnityTest]
        public IEnumerator IFrames_BlockDamage_MidWindow()
        {
            var dodge = MakePlayer(out var health, out _);
            Assert.IsTrue(dodge.TryDodge(), "dodge should start with full stamina");

            // Land squarely in the middle of the invulnerability window.
            float mid = dodge.IFrameStart + (dodge.IFrameEnd - dodge.IFrameStart) * 0.5f;
            yield return new WaitForSeconds(mid);

            Assert.IsTrue(health.IsInvulnerable, "should be invulnerable mid-window");
            int before = health.CurrentHealth;
            health.TakeDamage(25);
            Assert.AreEqual(before, health.CurrentHealth, "damage during i-frames must be ignored");
        }

        [UnityTest]
        public IEnumerator Damage_Applies_AfterWindow()
        {
            var dodge = MakePlayer(out var health, out _);
            Assert.IsTrue(dodge.TryDodge());

            yield return new WaitForSeconds(dodge.IFrameEnd + 0.1f);

            Assert.IsFalse(health.IsInvulnerable, "window should be closed after iFrameEnd");
            int before = health.CurrentHealth;
            health.TakeDamage(25);
            Assert.AreEqual(before - 25, health.CurrentHealth, "damage after the window must apply");
        }

        [UnityTest]
        public IEnumerator Damage_Applies_BeforeWindow()
        {
            var dodge = MakePlayer(out var health, out _);
            Assert.IsTrue(dodge.TryDodge());

            // Same frame, before iFrameStart elapses: not yet invulnerable.
            Assert.IsFalse(health.IsInvulnerable, "should not be invulnerable before the window opens");
            int before = health.CurrentHealth;
            health.TakeDamage(25);
            Assert.AreEqual(before - 25, health.CurrentHealth, "damage before the window must apply");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dodge_Gated_ByStamina()
        {
            var dodge = MakePlayer(out var health, out var stamina);
            // Drain stamina below the 30 cost.
            Assert.IsTrue(stamina.TrySpend(stamina.Current - 10f));

            Assert.IsFalse(dodge.TryDodge(), "must not dodge without enough stamina");
            Assert.IsFalse(health.IsInvulnerable, "no dodge -> no i-frames");
            yield return null;
        }
    }
}
