using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// 2026-11-05 (final gate run 3, unarmed-farm: the troll was ORDERED to break a planted tree, then no farming step ran for 30 s - no travel line, no recovery
/// set-up - and nothing said why): a watchdog outside the AI step. A farmer with a target whose Step (MonsterAI.UpdateAI prefix) has not run for StallSeconds
/// logs everything that can stop that step, at most once per LogSeconds. Diagnostics only - it changes nothing.
internal sealed partial class TrollFarmer
{
    private const float StepStallSeconds = 5f, StepStallLogSeconds = 10f;
    private float _lastStep = -1f, _nextStepStallLog;

    private void Update()
    {
        if (_target == null || _lastStep < 0f || Time.time - _lastStep < StepStallSeconds || Time.time < _nextStepStallLog)
        {
            return;
        }

        _nextStepStallLog = Time.time + StepStallLogSeconds;
        MonsterAI ai = GetComponent<MonsterAI>();
        ZNetView view = GetComponent<ZNetView>();
        Rigidbody body = GetComponent<Rigidbody>();
        Character me = GetComponent<Character>();
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " has target " + _target.name + " but its farming step has not run for " +
                    (Time.time - _lastStep).ToString("F1") + " s - AI " + (ai == null ? "none" : "enabled " + ai.enabled + ", active " + ai.isActiveAndEnabled +
                    ", follow " + (ai.GetFollowTarget() != null ? ai.GetFollowTarget().name : "none") + ", AI target " + (ai.GetTargetCreature() != null ? ai.GetTargetCreature().name : "none")) +
                    ", owner here " + (view != null && view.IsValid() && view.IsOwner()) + ", kinematic " + (body != null && body.isKinematic) +
                    ", dead " + (me != null && me.IsDead()) + ", in attack " + (me != null && me.InAttack()) +
                    ", chase " + (GetComponent<TrollChase>() != null) + ", rage " + (GetComponent<TrollRage>() != null) + ", walk-back " + (GetComponent<TrollReturn>() != null) +
                    ", position " + transform.position.ToString("F0"));
    }
}