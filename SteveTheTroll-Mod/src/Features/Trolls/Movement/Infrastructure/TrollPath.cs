using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Movement.Domain;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Movement.Infrastructure;

/// <summary>
///     D-179 (user 2026-11-05 "do 1-2 and 4 now, 3 later"): a troll's own way through clutter, read from the world. PathRules decides.
///     1 SmallBlocker: the nearest thing in a body-wide cast towards the goal, if it is small and natural (log, stump, bush, small tree) - the troll breaks it.
///     2 Feelers: body-wide casts along 9 headings around the goal line; the heading with the best free run x progress wins (instead of a fixed 6 m side-step).
///     4 Bad spots: where a troll got stuck, remembered for 120 s; feelers through them are penalised, so the next troll (or the next try) goes another way.
///     (3, the game's navmesh with a smaller agent type, is on the mountain roadmap.)
/// </summary>
internal static class TrollPath
{
    private const float ChestHeight = 1.5f, CastRadius = 0.8f, ProbeMetres = 4f;
    private static readonly List<KeyValuePair<Vector3, float>> BadSpots = new();
    private static int _mask;

    private static int Mask
    {
        get
        {
            if (_mask == 0)
            {
                _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "character", "character_net", "vehicle");
            }

            return _mask;
        }
    }

    /// 4: the troll got stuck at `at`. Expected outcome: how many bad spots are remembered now.
    internal static int RecordBadSpot(Vector3 at, string who)
    {
        Prune();
        if (BadSpots.Count >= PathRules.MaxBadSpots)
        {
            BadSpots.RemoveAt(0);
        }

        BadSpots.Add(new KeyValuePair<Vector3, float>(at, Time.time));
        ModLog.Info("TrollPath: bad spot at " + at.ToString("F0") + " (" + who + ") - " + BadSpots.Count + " remembered for " + PathRules.BadSpotSeconds.ToString("0") + " s");
        return BadSpots.Count;
    }

    /// 4: a straight walk from `from` to `to` passes within BadSpotRadius of a remembered bad spot (the spot the troll stands on does not count).
    internal static bool PassesBadSpot(Vector3 from, Vector3 to)
    {
        Prune();
        foreach (KeyValuePair<Vector3, float> b in BadSpots)
        {
            Vector3 p = b.Key;
            if (Flat(p - from) < PathRules.BadSpotRadius)
            {
                continue;
            }

            if (DistanceToSegment(p, from, to) < PathRules.BadSpotRadius)
            {
                return true;
            }
        }

        return false;
    }

    internal static int BadSpotCount
    {
        get
        {
            Prune();
            return BadSpots.Count;
        }
    }

    internal static void ForgetBadSpots()
    {
        BadSpots.Clear();
    }

    /// 1: the nearest thing within 4 m on the way to `goal` when the troll may simply break it (PathRules.PushThrough), else null. `ignore` = the goal object (the
    /// creature chased, the resource) - never a blocker. Expected outcome: the blocker's root object or null; `what` names what was found either way.
    internal static GameObject SmallBlocker(Character troll, Vector3 goal, GameObject ignore, out string what)
    {
        what = "nothing within " + ProbeMetres.ToString("0") + " m";
        Vector3 dir = goal - troll.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
        {
            return null;
        }

        RaycastHit hit;
        if (!Cast(troll, ignore, troll.transform.position + Vector3.up * ChestHeight, dir.normalized, ProbeMetres, out hit))
        {
            return null;
        }

        Collider c = hit.collider;
        Character ch = c.GetComponentInParent<Character>();
        Piece piece = c.GetComponentInParent<Piece>();
        var tree = c.GetComponentInParent<TreeBase>();
        var log = c.GetComponentInParent<TreeLog>();
        var dest = c.GetComponentInParent<Destructible>();
        bool rock = c.GetComponentInParent<MineRock>() != null || c.GetComponentInParent<MineRock5>() != null;
        GameObject root = tree != null ? tree.gameObject : log != null ? log.gameObject : dest != null ? dest.gameObject : piece != null ? piece.gameObject : c.transform.root.gameObject;
        string prefab = Utils.GetPrefabName(root);
        bool push = PathRules.PushThrough(prefab, c.GetComponentInParent<IDestructible>() != null, piece != null, ch != null, rock, log != null);
        what = prefab + " at " + hit.distance.ToString("F1") + " m (" + (push ? "small and natural - breaks it" : ch != null ? "a creature" : piece != null ? "a piece" : rock ? "a rock" : "not small") + ")";
        return push ? root : null;
    }

    /// 2: the best heading around the straight line to `goal` - its walk point (at most `stepMetres` along it) and a line describing every feeler.
    /// Expected outcome: true with a point when a heading with at least PathRules.MinUsefulRun of free run exists.
    internal static bool Feelers(Character troll, Vector3 goal, GameObject ignore, float stepMetres, out Vector3 point, out string log)
    {
        Vector3 pos = troll.transform.position;
        Vector3 line = goal - pos;
        line.y = 0f;
        point = pos;
        if (line.sqrMagnitude < 0.01f)
        {
            log = "already at the goal";
            return false;
        }

        line.Normalize();
        Vector3 chest = pos + Vector3.up * ChestHeight;
        float best = float.MinValue;
        bool found = false;   // Sonar S1244: no float equality on the sentinel
        float bestAngle = 0f, bestFree = 0f;
        var sb = new System.Text.StringBuilder();
        foreach (float a in PathRules.FeelerAngles)
        {
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * line;
            RaycastHit hit;
            float free = Cast(troll, ignore, chest, dir, PathRules.FeelerLength, out hit) ? hit.distance : PathRules.FeelerLength;
            Vector3 end = pos + dir * Mathf.Min(free, stepMetres);
            bool bad = PassesBadSpot(pos, end);
            float score = PathRules.FeelerScore(free, a, bad);
            sb.Append(a.ToString("+0;-0;0")).Append(':').Append(free.ToString("F0")).Append(bad ? "b" : "").Append(' ');
            if (score > best)
            {
                found = true;
                best = score;
                bestAngle = a;
                bestFree = free;
            }
        }

        if (!found)
        {
            log = "feelers (deg:free m, b = bad spot) " + sb + "- no heading free for " + PathRules.MinUsefulRun.ToString("0") + " m";
            return false;
        }

        Vector3 go = Quaternion.Euler(0f, bestAngle, 0f) * line;
        point = pos + go * Mathf.Min(bestFree - 1f, stepMetres);
        point.y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(point) : point.y;
        log = "feelers (deg:free m, b = bad spot) " + sb + "- takes " + bestAngle.ToString("+0;-0;0") + " deg (" + bestFree.ToString("F1") + " m free)";
        return true;
    }

    private static bool Cast(Character troll, GameObject ignore, Vector3 from, Vector3 dir, float length, out RaycastHit best)
    {
        best = default;
        float bestD = float.MaxValue;
        foreach (RaycastHit h in Physics.SphereCastAll(from, CastRadius, dir, length, Mask, QueryTriggerInteraction.Ignore))
        {
            if (h.collider == null || h.collider.transform.IsChildOf(troll.transform) || (ignore != null && h.collider.transform.IsChildOf(ignore.transform)))
            {
                continue;
            }

            if (h.distance <= 0f && h.point == Vector3.zero)
            {
                continue; // started inside it (the game reports such hits at 0 m with no point): not something ahead
            }

            if (h.collider.gameObject.layer == LayerMask.NameToLayer("terrain") && h.normal.y > 0.6f)
            {
                continue; // walkable ground, not a wall
            }

            if (h.distance < bestD)
            {
                bestD = h.distance;
                best = h;
            }
        }

        return bestD < float.MaxValue;
    }

    private static void Prune()
    {
        BadSpots.RemoveAll(b => !PathRules.StillBad(b.Value, Time.time));
    }

    private static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = a.y = b.y = 0f;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector3.Distance(p, a + ab * t);
    }
}
