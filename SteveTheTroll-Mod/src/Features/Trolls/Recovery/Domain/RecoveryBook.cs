using System;
using System.Collections.Generic;

namespace SteveTheTroll.Features.Trolls.Recovery.Domain;

/// <summary>
///     D-167 (spec "failure counter attached to each troll-resource pair"): per troll and per resource (its network
///     identity, never the prefab) how many complete recovery cycles failed,
///     and until when the resource is unavailable to that troll. Lives in a static registry, not in a movement component,
///     so a re-created farmer cannot erase it.
/// </summary>
internal sealed class RecoveryBook
{
    private const int MaxEntries = 2000;

    private readonly Dictionary<Key, Entry> _entries = new();

    /// How many entries are kept (diagnostics).
    internal int Count => _entries.Count;

    private static Key K(string troll, long a, long b)
    {
        return new Key { Troll = troll ?? "", A = a, B = b };
    }

    private Entry Get(string troll, long a, long b, bool create)
    {
        Entry e;
        if (_entries.TryGetValue(K(troll, a, b), out e))
        {
            return e;
        }

        if (!create)
        {
            return null;
        }

        if (_entries.Count > MaxEntries)
        {
            _entries.Clear();
        }

        e = new Entry();
        _entries[K(troll, a, b)] = e;
        return e;
    }

    internal int Failed(string troll, long a, long b)
    {
        Entry e = Get(troll, a, b, false);
        return e != null ? e.Failed : 0;
    }

    internal bool IsBlacklisted(string troll, long a, long b, float now)
    {
        Entry e = Get(troll, a, b, false);
        return e != null && now < e.BlacklistedUntil;
    }

    /// A complete cycle failed. Expected outcome: true when this was the failure that blacklists the resource (only when `canBlacklist`: a hub is never abandoned; its counter
    /// keeps counting so the detour direction keeps alternating). The counter starts again after a blacklisting.
    internal bool CycleFailed(string troll, long a, long b, float now, bool canBlacklist)
    {
        Entry e = Get(troll, a, b, true);
        e.Failed++;
        bool blacklist = canBlacklist && e.Failed >= StuckRules.MaxFailedCycles;
        if (blacklist)
        {
            e.BlacklistedUntil = now + StuckRules.BlacklistSeconds;
            e.Failed = 0;
        }

        return blacklist;
    }

    /// The troll recovered (reached the resource, or made continuous progress for StuckRules.SustainedReset): the counter starts from zero. A running blacklist stays.
    internal void Recovered(string troll, long a, long b)
    {
        Entry e = Get(troll, a, b, false);
        if (e != null)
        {
            e.Failed = 0;
        }
    }

    /// Drops entries that are neither counting nor blacklisted any more.
    internal int Prune(float now)
    {
        var dead = new List<Key>();
        foreach (KeyValuePair<Key, Entry> kv in _entries)
        {
            if (kv.Value.Failed == 0 && now >= kv.Value.BlacklistedUntil)
            {
                dead.Add(kv.Key);
            }
        }

        foreach (Key k in dead)
        {
            _entries.Remove(k);
        }

        return dead.Count;
    }

    private sealed class Entry
    {
        internal float BlacklistedUntil;
        internal int Failed;
    }

    private struct Key : IEquatable<Key>
    {
        internal string Troll;
        internal long A, B;

        public bool Equals(Key o)
        {
            return Troll == o.Troll && A == o.A && B == o.B;
        }

        public override bool Equals(object o)
        {
            return o is Key && Equals((Key)o);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((((Troll != null ? Troll.GetHashCode() : 0) * 397) ^ A.GetHashCode()) * 397) ^ B.GetHashCode();
            }
        }
    }
}
