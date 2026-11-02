using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>D-171 (P7): TestLab hooks for the home features (suite tests bed-heal, hold-look).</summary>
    internal static class HomeTestHooks
    {
        /// The troll's health in HP (rounded).
        [TestHook("stt.trollHealth")]
        static int TrollHealth(GameObject troll) { var c = troll.GetComponent<Character>(); return c != null ? Mathf.RoundToInt(c.GetHealth()) : -1; }

        /// The troll loses `damage` HP with no attacker (no counter / rage reaction). Expected outcome: its health afterwards.
        [TestHook("stt.hurtTroll")]
        static int HurtTroll(GameObject troll, float damage)
        {
            var c = troll.GetComponent<Character>();
            c.SetHealth(Mathf.Max(1f, c.GetHealth() - damage));
            return Mathf.RoundToInt(c.GetHealth());
        }

        /// The troll's health below its maximum in HP (0 = full).
        [TestHook("stt.trollMissingHealth")]
        static int TrollMissingHealth(GameObject troll) { var c = troll.GetComponent<Character>(); return c != null ? Mathf.RoundToInt(c.GetMaxHealth() - c.GetHealth()) : -1; }
    }
}
