using System;
using HarmonyLib;
using SteveTheTroll.Features.Trolls.Contracts;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Movement.Infrastructure;

/// <summary>
///     Architecture V2 (D-169): ITrollMovement on the game's MonsterAI - the same private BaseAI.MoveTo / LookAt the
///     farming patch uses (cached delegates, no allocation per call).
/// </summary>
internal sealed class ValheimTrollMovement : ITrollMovement
{
    private const float MovingSpeed = 0.3f;

    /// D-171 (P9): the ONE definition of the game's private BaseAI.MoveTo / LookAt delegates (TrollFarming and TrollWaterKeep use these).
    internal static readonly Func<BaseAI, float, Vector3, float, bool, bool> MoveToFn =
        AccessTools.MethodDelegate<Func<BaseAI, float, Vector3, float, bool, bool>>(
            AccessTools.Method(typeof(BaseAI), "MoveTo"));

    internal static readonly Action<BaseAI, Vector3> LookAtFn =
        AccessTools.MethodDelegate<Action<BaseAI, Vector3>>(AccessTools.Method(typeof(BaseAI), "LookAt"));

    private readonly MonsterAI _ai;

    private readonly Character _body;

    internal ValheimTrollMovement(Character body, MonsterAI ai)
    {
        _body = body;
        _ai = ai;
    }

    public bool IsMoving => _body != null && _body.GetVelocity().magnitude > MovingSpeed;
    public Vector3 Position => _body.transform.position;
    public Vector3 Forward => _body.transform.forward;

    public bool MoveTo(float dt, Vector3 destination, float arriveMetres, bool run)
    {
        return MoveToFn(_ai, dt, destination, arriveMetres, run);
    }

    public void LookAt(Vector3 point)
    {
        LookAtFn(_ai, point);
    }

    public bool IsLookingAt(Vector3 point, float maxDegrees)
    {
        return _ai.IsLookingAt(point, maxDegrees);
    }

    public void Stop()
    {
        _ai.StopMoving();
    }

    /// Both private game methods were found (in-game test Startup_TamingReflection checks the same pair through TrollFarming).
    internal static bool SelfCheck()
    {
        return MoveToFn != null && LookAtFn != null;
    }
}
