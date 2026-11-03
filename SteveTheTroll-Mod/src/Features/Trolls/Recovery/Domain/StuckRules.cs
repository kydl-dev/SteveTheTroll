using System.Collections.Generic;
using System;
using SteveTheTroll.Trolls.Farming.Domain;

namespace SteveTheTroll.Trolls.Recovery.Domain
{
    /// <summary>
    /// D-167 (user spec "Troll Movement - Stuck Detection and Recovery System"): the game-free numbers and decisions of the stuck detection and recovery. All distances are flat metres
    /// (x / z), all times seconds. TrollRecovery reads the game and acts; nothing here knows Unity or Valheim.
    /// </summary>
    internal static class StuckRules
    {
        /// Blocked this long without progress = stuck (spec 1).
        internal const float StuckTrigger = 3f;
        /// Rage mode against a destructible obstruction (spec 1).
        internal const float RageSeconds = 5f;
        /// One detour leg walks forward up to this far (spec 1 / 2).
        internal const float LegMetres = 10f;
        /// A leg shorter than this is not worth walking: the other side is tried (spec 2: valid navigable positions only).
        internal const float MinLegMetres = 1f;
        /// A preferred side with less than this room is only kept when the other side has no more (lab run 2026-11-01: 1 m of room on the right, 10 m on the left, and the troll walked the 1 m).
        internal const float GoodLegMetres = 3f;
        /// Continuous progress for this long means the troll has recovered: the failure counter of its resource is reset (spec "When should the counter reset?").
        internal const float SustainedReset = 10f;
        /// A resource the troll could not reach in two complete recovery cycles is unavailable to THAT troll this long (spec 3).
        internal const float BlacklistSeconds = 120f;
        internal const int MaxFailedCycles = 2;
        /// The distance to the goal must beat the best one so far by this much to count as progress (a troll walking sideways or into a tree makes none).
        internal const float MinGain = 0.25f;
        /// Gains closer together than this count as continuous progress.
        internal const float GainWindow = 1.5f;
        /// A detour leg ends after this long, or when the troll moved less than LegStallMetres for LegStallSeconds (walked into something).
        internal const float LegMaxSeconds = 8f, LegStallSeconds = 1.5f, LegStallMetres = 0.3f;
        /// Two trolls that turned away from each other are clear of each other from this distance (spec 4: shorter than 10 m is fine).
        internal const float HeadOnClearMetres = 4f;
        /// Another troll counts as "facing me, in my way" within this distance ahead / this far off my line, and when its facing points against mine (dot product at most HeadOnOpposed).
        internal const float HeadOnAhead = 3.5f, HeadOnHalfWidth = 1.8f, HeadOnOpposed = -0.5f;
        /// A step of a detour is valid when the ground rises or falls at most this much per metre and the liquid is at most this deep.
        internal const float StepMaxRise = 0.9f, MaxWaterDepth = 0.4f;

        /// Spec 1: player-built (any player) and tamed creatures are never attacked; what is not breakable at all is walked around; the rest is destroyed.
        /// Expected outcome: Destructible only for something a troll may attack.
        internal static Obstruction Classify(bool isCharacter, bool isPlayer, bool tamed, bool playerBuilt, bool breakable)
        {
            if (playerBuilt) return Obstruction.PlayerBuilt;
            if (isCharacter) return isPlayer || tamed ? Obstruction.Protected : Obstruction.Destructible;
            return breakable ? Obstruction.Destructible : Obstruction.Indestructible;
        }

        /// Rage mode is for destructible obstructions only (spec 1).
        internal static bool Rageable(Obstruction o) { return o == Obstruction.Destructible; }

        /// The unit direction 90 degrees to the `side` of the intended direction (ix, iz). A zero intended direction gives (0, 0).
        internal static void Direction(float ix, float iz, Side side, out float dx, out float dz)
        {
            float len = (float)Math.Sqrt(ix * ix + iz * iz);
            if (len < 1e-5f) { dx = 0f; dz = 0f; return; }
            ix /= len; iz /= len;
            dx = side == Side.Right ? iz : -iz;
            dz = side == Side.Right ? -ix : ix;
        }

        internal static Side Opposite(Side s) { return s == Side.Right ? Side.Left : Side.Right; }

        /// One step of a detour (a point one metre further along the leg) is valid: not blocked, the ground does not jump (cliff / wall), the liquid is shallow.
        /// D-174 (review of far-troll-goes-home: 116 m from home every detour found "right 0 m / left 0 m" - only 90 degrees, 10 m, was ever tried): the first leg of a detour
        /// searches these angles from the intended direction (degrees, preferred side first, then the other side), each up to CandidateMetres.
        internal static readonly float[] CandidateAngles = { 70f, 90f, 45f, 110f, 135f };
        /// Walking speed a leg is given time for (m/s): a long candidate leg must not be cut by LegMaxSeconds.
        internal const float MinLegSpeed = 2.5f;

        /// How far a candidate leg at `angleDeg` may go: 20 m up to 70 degrees, 30 m wider (around a long obstacle). Expected outcome: 20 or 30.
        internal static float CandidateMetres(float angleDeg) { return angleDeg <= 70f ? 20f : 30f; }

        /// `side` of the intended direction turned by `angleDeg` (90 = Direction). Expected outcome: a unit vector (0,0 for a zero input).
        internal static void Rotate(float ix, float iz, Side side, float angleDeg, out float dx, out float dz)
        {
            float len = (float)Math.Sqrt(ix * ix + iz * iz);
            if (len < 1e-5f) { dx = 0f; dz = 0f; return; }
            ix /= len; iz /= len;
            double a = (side == Side.Right ? angleDeg : -angleDeg) * Math.PI / 180.0;
            float c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
            dx = ix * c + iz * sn;
            dz = -ix * sn + iz * c;
        }

        /// D-174: which candidate leg to walk. `metres[i]` = valid metres of candidate i (in search order). The first one with GoodLegMetres wins (search order = preference:
        /// the preferred side's angles nearest to the goal first); else the longest one with at least MinLegMetres. Expected outcome: the index, -1 = none walkable.
        internal static int PickCandidate(IList<float> metres)
        {
            if (metres == null) return -1;
            int best = -1; float bestM = MinLegMetres - 0.001f;
            for (int i = 0; i < metres.Count; i++)
            {
                if (metres[i] >= GoodLegMetres) return i;
                if (metres[i] > bestM) { bestM = metres[i]; best = i; }
            }
            return best;
        }

        /// D-174: time a leg of `planned` metres is given before it counts as done. Expected outcome: LegMaxSeconds, or more for a long leg.
        internal static float LegSeconds(float planned) { return Math.Max(LegMaxSeconds, planned / MinLegSpeed); }

        internal static bool StepOk(float rise, float waterDepth, bool blocked)
        {
            return !blocked && Math.Abs(rise) <= StepMaxRise && waterDepth <= MaxWaterDepth;
        }

        /// How many metres of a leg are valid: the number of leading valid steps (stepsOk[i] = the point i + 1 m along the leg), at most `maxMetres`.
        internal static float PlanMetres(IList<bool> stepsOk, float maxMetres)
        {
            int n = 0;
            while (stepsOk != null && n < stepsOk.Count && n < (int)maxMetres && stepsOk[n]) n++;
            return n;
        }

        /// The side a leg goes: the preferred one when it has at least GoodLegMetres (or at least MinLegMetres and no less than the other side), else the other side when it has MinLegMetres,
        /// else the preferred side with 0 m (nothing to walk).
        internal static Side ChooseSide(Side preferred, float preferredMetres, float otherMetres, out float metres)
        {
            if (preferredMetres >= GoodLegMetres || (preferredMetres >= MinLegMetres && preferredMetres >= otherMetres)) { metres = preferredMetres; return preferred; }
            if (otherMetres >= MinLegMetres) { metres = otherMetres; return Opposite(preferred); }
            metres = 0f;
            return preferred;
        }

        /// A detour leg is over: nothing to walk, walked far enough, took too long, or stalled (moved less than LegStallMetres for LegStallSeconds).
        internal static bool LegDone(float walked, float planned, float legSeconds, float stalledSeconds)
        {
            return planned < MinLegMetres || walked >= planned - 0.5f || legSeconds >= LegSeconds(planned) || stalledSeconds >= LegStallSeconds;
        }

        /// Spec 4: two trolls blocked head on. `other` stands in front of me, close, and its facing (odx, odz) points against mine (mdx, mdz; unit vectors).
        internal static bool HeadOn(float mx, float mz, float mdx, float mdz, float ox, float oz, float odx, float odz)
        {
            float gx = mx + mdx * 10f, gz = mz + mdz * 10f;
            bool inFront = TargetRules.Blocked(mx, mz, gx, gz, ox, oz, HeadOnAhead, HeadOnHalfWidth);
            return inFront && mdx * odx + mdz * odz <= HeadOnOpposed;
        }

        /// Spec 4: the head-on turn is over once the two are this far apart.
        internal static bool HeadOnClear(float separation) { return separation >= HeadOnClearMetres; }
    }

}
