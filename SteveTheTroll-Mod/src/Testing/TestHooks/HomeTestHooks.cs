using System;
using System.Linq;
using KydlCore.Common;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Hub.Registration;
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

    /// D-176 (kydl-spec-port: a fresh profile has 2 hub tabs since the first-time gates of 2026-11-04): opens or closes the three gates of the gated tabs - Costume (the set bonus was
    /// active once), Troll Hub (the first hub is built), My trolls / Troll work / Trolls (the first troll tamed). They live on the character; HubTabs.Tick adds the tabs within 2 s.
    /// Tabs that were added stay until their feature is switched off. Expected outcome: how many of the 3 gates are open now.
    [TestHook("stt.tabGates")]
    private static int TabGates(bool open)
    {
        Player p = Player.m_localPlayer;
        if (p == null || p.m_customData == null)
        {
            throw new Exception("no local player");
        }

        string[] keys = { HubPlacement.UnlockKey, TrollTamingPatch.FirstTameKey, HubTabs.HubBuiltKey };
        foreach (string k in keys)
        {
            if (open)
            {
                p.m_customData[k] = "1";
            }
            else
            {
                p.m_customData.Remove(k);
            }
        }

        int n = keys.Count(k => p.m_customData.ContainsKey(k));
        ModLog.Info("HomeTestHooks: hub tab gates " + (open ? "opened" : "closed") + ", " + n + " of 3 open");
        return n;
    }

    /// The troll's health below its maximum in HP (0 = full).
    [TestHook("stt.trollMissingHealth")]
    private static int TrollMissingHealth(GameObject troll)
    {
        Character c = troll.GetComponent<Character>();
        return c != null ? Mathf.RoundToInt(c.GetMaxHealth() - c.GetHealth()) : -1;
    }
}
