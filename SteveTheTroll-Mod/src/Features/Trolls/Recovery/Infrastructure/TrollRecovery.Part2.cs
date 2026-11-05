using System.Collections.Generic;
using System.Text;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Core;
using SteveTheTroll.Features.Trolls.Farming.Domain;
using SteveTheTroll.Features.Trolls.Recovery.Application;
using SteveTheTroll.Features.Trolls.Recovery.Domain;
using SteveTheTroll.Features.Trolls.Recovery.Execution;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Recovery.Infrastructure;

/// <summary>Part 2 of TrollRecovery (split out so the class stays under the size limit).</summary>
internal sealed partial class TrollRecovery
{
    // ---- detour legs ----

    private void StartLeg(RecoveryDecision decision)
    {
        string tag = TrollTaming.Tag(this);
        Vector3 from = transform.position;
        Vector3 flat = _goal - from;
        flat.y = 0f;
        if (decision.Leg == 0)
        {
            _intended = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
        }

        Vector3 dir;
        float metres;
        Side side = decision.LegSide;
        if (decision.Leg == 0)
        {
            // D-174: a search over CandidateAngles on both sides (preferred side first), each candidate checked metre by metre and by the path search; the reason
            // every rejected candidate failed is logged (the log of the far-troll run only said "right 0 m / left 0 m")
            CandMetres.Clear();
            CandDirs.Clear();
            CandNames.Clear();
            var why = new StringBuilder();
            for (int pass = 0; pass < 2; pass++)
            {
                Side sd = pass == 0 ? side : StuckRules.Opposite(side);
                foreach (float a in StuckRules.CandidateAngles)
                {
                    float cx, cz;
                    StuckRules.Rotate(_intended.x, _intended.z, sd, a, out cx, out cz);
                    var cdir = new Vector3(cx, 0f, cz);
                    string reason;
                    float m = PlanMetres(from, cdir, StuckRules.CandidateMetres(a), out reason);
                    CandMetres.Add(m);
                    CandDirs.Add(cdir);
                    CandNames.Add(sd + " " + a.ToString("0"));
                    why.Append(" ").Append(sd == Side.Right ? "R" : "L").Append(a.ToString("0")).Append(" ")
                        .Append(m.ToString("0")).Append(" m");
                    if (reason.Length > 0)
                    {
                        why.Append(" (").Append(reason).Append(")");
                    }

                    why.Append(";");
                }
            }

            int pick = StuckRules.PickCandidate(CandMetres);
            if (pick >= 0)
            {
                metres = CandMetres[pick];
                dir = CandDirs[pick];
                side = pick < StuckRules.CandidateAngles.Length ? side : StuckRules.Opposite(side);
            }
            else
            {
                metres = 0f;
                dir = LegDir(side);
            }

            ModLog.Info("TrollRecovery: " + tag + " detour " + decision.DetourNumber +
                        (decision.HeadOn
                            ? " (head on with " + (_other != null ? TrollTaming.Tag(_other) : "a troll") + ")"
                            : "") + ", leg 1: " +
                        (pick >= 0
                            ? "turns " + CandNames[pick] + " degrees, walks " + metres.ToString("F0") + " m"
                            : "no walkable candidate") + " - candidates:" + why);
        }
        else
        {
            dir = _intended; // the second leg of the L: turn back (90 degrees the other way) to the intended direction
            string reason2;
            metres = PlanMetres(from, dir, StuckRules.LegMetres, out reason2);
            ModLog.Info("TrollRecovery: " + tag +
                        " detour 2, leg 2: turns back to its intended direction, valid ground " +
                        metres.ToString("F0") + " m" + (reason2.Length > 0 ? " (" + reason2 + ")" : "") + ", walks it");
        }

        _legStart = from;
        _legLast = from;
        _legPlanned = metres;
        _legBegan = Time.time;
        _legStalled = 0f;
        _legTarget = from + dir * metres;
    }

    private Vector3 LegDir(Side s)
    {
        float dx, dz;
        StuckRules.Direction(_intended.x, _intended.z, s, out dx, out dz);
        return new Vector3(dx, 0f, dz);
    }

    private float GroundY(Vector3 p, float refY)
    {
        if (_groundMask == 0)
        {
            _groundMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
        }

        int n = Physics.RaycastNonAlloc(new Vector3(p.x, refY + 30f, p.z), Vector3.down, DownHits, 80f, _groundMask,
            QueryTriggerInteraction.Ignore);
        float best = float.MinValue, lowest = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Collider c = DownHits[i].collider;
            if (c == null || c.transform.IsChildOf(transform))
            {
                continue;
            }

            float y = DownHits[i].point.y;
            if (y < lowest)
            {
                lowest = y;
            }

            if (y <= refY + WalkUp && y > best)
            {
                best = y;
            }
        }

        if (best > float.MinValue)
        {
            return best;
        }

        if (lowest < float.MaxValue)
        {
            return lowest;
        }

        float h;
        return ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(p, out h) ? h : p.y;
    }

    // The valid metres of a leg from `from` along `dir` (at most `max`): every metre needs ground that does not jump, shallow liquid and a free line (StuckRules.StepOk), and the
    // game's path search must find a way to the end (a point it cannot reach is shortened). D-174: `reason` says why the leg stops short ("" = full length) - and when the path
    // search fails even for the first metre that the ground probes accept, the area has no path data (far from the player): the ground result stands, said in `reason`.
    private float PlanMetres(Vector3 from, Vector3 dir, float max, out string reason)
    {
        reason = "";
        if (_blockMask == 0)
        {
            _blockMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "vehicle");
        }

        var steps = new List<bool>();
        Vector3 prev = from;
        float prevH = GroundY(from, from.y);
        for (int i = 1; i <= (int)max; i++)
        {
            Vector3 p = from + dir * i;
            float h = GroundY(p, prevH);
            float depth = Mathf.Max(0f, Floating.GetLiquidLevel(new Vector3(p.x, h, p.z)) - h);
            bool blocked = Physics.Linecast(prev + Vector3.up * ChestHeight, p + Vector3.up * ChestHeight, _blockMask,
                QueryTriggerInteraction.Ignore);
            bool ok = StuckRules.StepOk(h - prevH, depth, blocked);
            if (!ok && reason.Length == 0)
            {
                reason = (blocked ? "blocked" :
                    depth > StuckRules.MaxWaterDepth ? "water " + depth.ToString("F1") + " m" :
                    "height change " + (h - prevH).ToString("F1") + " m") + " at " + i + " m";
            }

            steps.Add(ok);
            prev = new Vector3(p.x, h, p.z);
            prevH = h;
        }

        float metres = StuckRules.PlanMetres(steps, max);
        BaseAI ai = GetComponent<BaseAI>();
        if (metres < StuckRules.MinLegMetres || Pathfinding.instance == null || ai == null)
        {
            return Mathf.Max(0f, metres);
        }

        var path = new List<Vector3>();
        float ground = metres;
        while (metres >= StuckRules.MinLegMetres &&
               !Pathfinding.instance.GetPath(from, from + dir * metres, path, ai.m_pathAgentType))
        {
            metres -= 2f;
        }

        if (metres < StuckRules.MinLegMetres)
        {
            if (!Pathfinding.instance.GetPath(from, from + dir * StuckRules.MinLegMetres, path, ai.m_pathAgentType))
            {
                reason = "no path data here - ground probes used";
                return ground;
            }

            reason = "path search: no way";
        }
        else if (metres < ground - 0.01f)
        {
            reason = "path search shortened it from " + ground.ToString("0") + " m";
        }

        return Mathf.Max(0f, metres);
    }

    private bool LegDone(float dt)
    {
        Vector3 pos = transform.position;
        float moved = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(_legLast.x, 0f, _legLast.z));
        _legStalled = moved < StuckRules.LegStallMetres * dt / StuckRules.LegStallSeconds ? _legStalled + dt : 0f;
        _legLast = pos;
        float walked = Flat2(_legStart);
        bool clear = M.HeadOn && _other != null &&
                     StuckRules.HeadOnClear(Vector3.Distance(pos, _other.transform.position));
        return clear || StuckRules.LegDone(walked, _legPlanned, Time.time - _legBegan, _legStalled);
    }

    // ---- driving ----

    private bool Drive(float dt, ITrollCombat combat)
    {
        ITrollMovement mv = _ctx.Capabilities.Movement;
        if (mv == null)
        {
            return mv != null;
        }

        if (M.Stage == RecoveryStage.Leg)
        {
            mv.MoveTo(dt, _legTarget, 0.4f, true);
            return M.Stage == RecoveryStage.Leg;
        }

        if (M.Stage != RecoveryStage.Rage)
        {
            return M.Stage == RecoveryStage.Rage;
        }

        if (_rageTarget == null)
        {
            mv.MoveTo(dt, _goal, 1f, false);
            return _rageTarget == null;
        } // gone already: walks on to its goal while the 5 s run out

        Collider c = _rageTarget.GetComponentInChildren<Collider>();
        Vector3 point = c != null
            ? c.ClosestPoint(transform.position + Vector3.up * ChestHeight)
            : _rageTarget.transform.position;
        Vector3 flat = point - transform.position;
        flat.y = 0f;
        if (flat.magnitude > RageReach)
        {
            mv.MoveTo(dt, point, 1.5f, false);
        }
        else
        {
            mv.Stop();
        }

        mv.LookAt(point);
        if (combat.InAttack || Time.time < _nextSwing || !mv.IsLookingAt(point, 40f))
        {
            return M.Stage == RecoveryStage.Rage;
        }

        bool low = c != null && c.bounds.max.y < transform.position.y + 1f;
        if (combat.SwingAt(_rageTarget, low))
        {
            _nextSwing = Time.time + SwingGap;
        }

        return M.Stage == RecoveryStage.Rage;
    }

    // A sphere cast along the line to the goal, chest height: the nearest thing that is not the troll itself and not the resource it walks to.
    private void Probe(out bool rageable, out bool headOn)
    {
        rageable = false;
        headOn = false;
        _kind = Obstruction.None;
        _obstacleName = "nothing found";
        _rageTarget = null;
        _other = null;
        if (_mask == 0)
        {
            _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain",
                "character", "character_net", "vehicle");
        }

        Vector3 dir = _goal - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
        {
            return;
        }

        dir.Normalize();
        Vector3 origin = transform.position + Vector3.up * ChestHeight;
        var best = default(RaycastHit);
        float bestD = float.MaxValue;
        foreach (RaycastHit h in Physics.SphereCastAll(origin, ProbeRadius, dir, ProbeDistance, _mask,
                     QueryTriggerInteraction.Ignore))
        {
            if (h.collider == null || h.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (_goalObject != null && h.collider.transform.IsChildOf(_goalObject.transform))
            {
                continue;
            }

            if (h.distance < bestD)
            {
                bestD = h.distance;
                best = h;
            }
        }

        if (best.collider == null)
        {
            return;
        }

        Collider c = best.collider;
        Character ch = c.GetComponentInParent<Character>();
        Piece piece = c.GetComponentInParent<Piece>();
        bool playerBuilt = piece != null && piece.IsPlacedByPlayer();
        bool breakable = c.GetComponentInParent<IDestructible>() != null;
        _kind = StuckRules.Classify(ch != null, ch != null && ch.IsPlayer(), ch != null && ch.IsTamed(), playerBuilt,
            breakable);
        GameObject root = ch != null ? ch.gameObject : piece != null ? piece.gameObject : c.gameObject;
        _obstacleName = root.name + " (" + c.name + ", " + bestD.ToString("F1") + " m)";
        rageable = StuckRules.Rageable(_kind);
        if (piece != null && _kind == Obstruction.Destructible && LoggedPieces.Add(piece.GetInstanceID()))
        {
            // D-172 (user): a fortress wall / world piece as Destructible is normal; a wall a player built as Destructible (placedByPlayer false) is the anomaly
            ZNetView pv = piece.GetComponent<ZNetView>();
            ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " treats piece " + piece.name +
                        " as destructible: placedByPlayer " + piece.IsPlacedByPlayer() + ", GetCreator " +
                        piece.GetCreator()
                        + ", ZDO creator " +
                        (pv != null && pv.IsValid()
                            ? pv.GetZDO().GetLong(ZDOVars.s_creator).ToString()
                            : "no valid ZDO") + ", layer " + LayerMask.LayerToName(c.gameObject.layer));
        }

        if (rageable)
        {
            _rageTarget = ch != null ? ch.gameObject : c.gameObject;
        }

        if (ch != null && ch.IsTamed() && TrollTaming.IsTroll(ch))
        {
            Vector3 mf = transform.forward;
            Vector3 of = ch.transform.forward;
            headOn = StuckRules.HeadOn(new GroundPoint(transform.position.x, transform.position.z), new GroundPoint(mf.x, mf.z),
                new GroundPoint(ch.transform.position.x, ch.transform.position.z), new GroundPoint(of.x, of.z));
            if (headOn)
            {
                _other = ch;
            }
        }
    }
}
