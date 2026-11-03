using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Backpack.Infrastructure
{
    /// <summary>
    /// Where a NEW depot stack may stand (D-095 / D-097 / D-106 / TEST-043, user 2026-10-31 relaxed second pass): rings around the bed, level or levelable ground, free, away from
    /// other stacks, not under a roof, reachable. D-171 (P6): moved out of TrollDepot unchanged so that class gets under 500 lines.
    /// </summary>
    internal static class DepotSpots
    {
        static readonly List<Vector3> Path = new List<Vector3>();

        // Rings around the bed, nearest first. Expected outcome: true + the depot's ground position and facing (long side towards the bed).
        internal static bool FindSpot(Character troll, Vector3 bed, out Vector3 at, out Quaternion rot, out bool level)
        {
            at = bed; rot = Quaternion.identity; level = false;
            bool canLevel = DepotGround.CanLevel;
            float maxSlope = canLevel ? TrollDepot.MaxSlopeLevelled : TrollDepot.MaxSlopeAsIs;
            var ai = troll.GetComponent<BaseAI>();
            var agent = ai != null ? ai.m_pathAgentType : Pathfinding.AgentType.TrollSize;
            int tried = 0, badGround = 0, blocked = 0, nearStack = 0, covered = 0, unreachable = 0;
            // User 2026-10-31 (a full troll stood at the bed and did nothing - test run 141207: "none of 288 spot(s) fits ... unreachable 229"): pass 1 is the strict search; when it finds
            // nothing, pass 2 takes ground up to TrollDepot.MaxSlopeRelaxed uneven (the level op cuts and fills it) and does not ask the game's pathfinding whether the troll can come within
            // 10 m of the spot - the walk to the depot gives up after UnloadWalkSeconds and the unload does not need the troll to stand next to the chest.
            for (int pass = 0; pass < 2; pass++)
            {
                bool relaxed = pass == 1;
                float slopeLimit = relaxed && canLevel ? TrollDepot.MaxSlopeRelaxed : maxSlope;
                for (float r = TrollDepot.MinFromBed; r <= TrollDepot.MaxFromBed; r += TrollDepot.RingStep)
                {
                    for (int k = 0; k < TrollDepot.Angles; k++)
                    {
                        float a = (k + (r / TrollDepot.RingStep) * 0.5f) * Mathf.PI * 2f / TrollDepot.Angles;
                        var p = bed + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                        var toBed = bed - p; toBed.y = 0f;
                        var q = Quaternion.LookRotation(toBed.normalized, Vector3.up);
                        tried++;
                        Vector3 ground; float slope;
                        bool flatten;
                        if (!Level(p, q, slopeLimit, canLevel, out ground, out slope, out flatten)) { badGround++; continue; }
                        if (!Free(ground, q)) { blocked++; continue; }
                        if (!AwayFromStacks(ground)) { nearStack++; continue; }
                        if (Covered(ground, q)) { covered++; continue; }
                        if (!relaxed && !Reachable(troll, agent, ground)) { unreachable++; continue; }
                        at = ground; rot = q; level = flatten;
                        ModLog.Info("TrollDepot: new stack at " + at.ToString("F1") + ", " + r.ToString("0") + " m from the bed, ground uneven by " + slope.ToString("F2") + " m" + (level ? " -> levelled" : "") + (relaxed ? " (relaxed search: reachability not checked)" : "") + " (" + tried + " spot(s) checked)");
                        return true;
                    }
                }
            }
            ModLog.Info("TrollDepot: none of " + tried + " spot(s) near the bed fits (ground too uneven " + badGround + ", blocked " + blocked + ", too near another depot " + nearStack + ", under a roof " + covered + ", unreachable " + unreachable + ")");
            return false;
        }

        // Ground under the footprint (corners, edge middles, centre): within maxSlope of each other. Flat enough as is -> the depot stands
        // on the highest point; uneven (levelled first, D-095) -> on the mean height, so the level op cuts and fills about the same.
        static bool Level(Vector3 p, Quaternion q, float maxSlope, bool canLevel, out Vector3 ground, out float slope, out bool flatten)
        {
            ground = p; slope = 0f; flatten = false;
            var zs = ZoneSystem.instance;
            if (zs == null) return false;
            float lo = float.MaxValue, hi = float.MinValue, sum = 0f;
            int n = 0;
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = -1; iz <= 1; iz++)
                {
                    float h = zs.GetGroundHeight(p + q * new Vector3(ix * DepotData.HalfX, 0f, iz * DepotData.HalfZ));
                    lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h); sum += h; n++;
                }
            slope = hi - lo;
            flatten = canLevel && slope > TrollDepot.MaxSlope;
            ground = new Vector3(p.x, flatten ? sum / n : hi, p.z);
            return slope <= maxSlope;
        }

        static int _blockMask, _roofMask;

        static bool Free(Vector3 ground, Quaternion q)
        {
            if (_blockMask == 0) _blockMask = LayerMask.GetMask("piece", "piece_nonsolid", "static_solid", "Default", "Default_small", "vehicle");
            if (_pieceMask == 0) _pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
            float h = DepotPrefab.Height * 0.5f + 0.15f;
            if (Physics.CheckBox(ground + Vector3.up * (h + 0.1f), new Vector3(DepotData.HalfX, h, DepotData.HalfZ), q, _blockMask)) return false;
            // TEST-043 (user: "why are they that close"): buildings and other depots keep a walking gap of DepotRules.Gap all round
            return !Physics.CheckBox(ground + Vector3.up * (h + 0.1f), new Vector3(DepotData.HalfX + DepotRules.Gap, h, DepotData.HalfZ + DepotRules.Gap), q, _pieceMask);
        }
        static int _pieceMask;

        // TEST-043: a new stack keeps DepotRules.StackSpacing (flat, centre to centre) from every depot stack of any owner
        static bool AwayFromStacks(Vector3 ground)
        {
            foreach (var v in TrollDepot.Near(ground, DepotPrefab.Name))
                if (!DepotRules.FarEnough(TrollDepot.Flat(v.transform.position - ground), DepotRules.StackSpacing)) return false;
            return true;
        }

        // "Not inside the house": nothing built above the middle or the ends of the footprint.
        static bool Covered(Vector3 ground, Quaternion q)
        {
            if (_roofMask == 0) _roofMask = LayerMask.GetMask("piece");
            for (int k = -1; k <= 1; k++)
                if (Physics.Raycast(ground + q * new Vector3(k * (DepotData.HalfX - 0.3f), 0f, 0f) + Vector3.up * (DepotPrefab.Height + 0.3f), Vector3.up, 40f, _roofMask)) return true;
            return false;
        }

        static bool Reachable(Character troll, Pathfinding.AgentType agent, Vector3 ground)
        {
            if (Pathfinding.instance == null) return true;
            Path.Clear();
            if (!Pathfinding.instance.GetPath(troll.transform.position, ground, Path, agent, false)) return false;
            return Path.Count > 0 && TrollDepot.Flat(Path[Path.Count - 1] - ground) <= TrollDepot.Reach;
        }
    }
}
