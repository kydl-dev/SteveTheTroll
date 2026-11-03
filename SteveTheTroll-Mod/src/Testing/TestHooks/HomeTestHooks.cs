using KydlCore.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>D-171 (P7): TestLab hooks for the home features (suite tests bed-heal, hold-look).</summary>
internal static class HomeTestHooks
{
    /// The troll's health in HP (rounded).
    [TestHook("stt.trollHealth")]
    private static int TrollHealth(GameObject troll)
    {
        Character c = troll.GetComponent<Character>();
        return c != null ? Mathf.RoundToInt(c.GetHealth()) : -1;
    }

    /// The troll loses `damage` HP with no attacker (no counter / rage reaction). Expected outcome: its health afterwards.
    [TestHook("stt.hurtTroll")]
    private static int HurtTroll(GameObject troll, float damage)
    {
        Character c = troll.GetComponent<Character>();
        c.SetHealth(Mathf.Max(1f, c.GetHealth() - damage));
        return Mathf.RoundToInt(c.GetHealth());
    }

    /// The troll's health below its maximum in HP (0 = full).
    [TestHook("stt.trollMissingHealth")]
    private static int TrollMissingHealth(GameObject troll)
    {
        Character c = troll.GetComponent<Character>();
        return c != null ? Mathf.RoundToInt(c.GetMaxHealth() - c.GetHealth()) : -1;
    }
}
