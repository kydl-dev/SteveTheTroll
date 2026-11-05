using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     D-178 (user 2026-11-05 01:29: "the problem with trolls walking into my character during testing is back"; fix11 depot-full-builds-new: after unloading the troll
///     went back to farming next to the player and TrollHold froze it 3 m away): TEST ONLY. Added to the local player by stt.reset; four times a second it logs (warning)
///     every tamed troll that comes within ContactMetres of the player, once per troll per 5 s, and counts them for stt.trollContacts. A troll walking into the player
///     while the player does not look at it logged nothing before.
/// </summary>
internal sealed class TestProximityWatch : MonoBehaviour
{
    internal const float ContactMetres = 4f;
    private static readonly Dictionary<int, float> LastLog = new();
    private float _next;

    /// Contacts since the last stt.reset.
    internal static int Contacts { get; private set; }

    /// Closest troll distance since the last stt.reset (float.MaxValue = none came near).
    internal static float Closest { get; private set; } = float.MaxValue;

    internal static void ResetCounts()
    {
        Contacts = 0;
        Closest = float.MaxValue;
        LastLog.Clear();
    }

    private void Update()
    {
        if (Time.time < _next)
        {
            return;
        }

        _next = Time.time + 0.25f;
        Vector3 me = transform.position;
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c))
            {
                continue;
            }

            Vector3 d = c.transform.position - me;
            d.y = 0f;
            float m = d.magnitude;
            if (m >= ContactMetres)
            {
                continue;
            }

            Closest = Mathf.Min(Closest, m);
            int id = c.GetInstanceID();
            float last;
            if (LastLog.TryGetValue(id, out last) && Time.time - last < 5f)
            {
                continue;
            }

            LastLog[id] = Time.time;
            Contacts++;
            ModLog.Warn("TestProximityWatch: " + TrollTaming.Tag(c) + " is " + m.ToString("F1") + " m from the player (contact " + Contacts + " since stt.reset) at " +
                        c.transform.position.ToString("F0"));
        }
    }
}
