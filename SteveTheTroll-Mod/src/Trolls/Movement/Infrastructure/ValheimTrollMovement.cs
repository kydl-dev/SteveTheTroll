using HarmonyLib;
using SteveTheTroll.Trolls.Contracts;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Movement.Infrastructure
{
    /// <summary>
    /// Architecture V2 (D-169): ITrollMovement on the game's MonsterAI - the same private BaseAI.MoveTo / LookAt the farming patch uses (cached delegates, no allocation per call).
    /// </summary>
    internal sealed class ValheimTrollMovement : ITrollMovement
    {
        /// D-171 (P9): the ONE definition of the game's private BaseAI.MoveTo / LookAt delegates (TrollFarming and TrollWaterKeep use these).
        internal static readonly System.Func<BaseAI, float, Vector3, float, bool, bool> MoveToFn =
            AccessTools.MethodDelegate<System.Func<BaseAI, float, Vector3, float, bool, bool>>(AccessTools.Method(typeof(BaseAI), "MoveTo"));
        internal static readonly System.Action<BaseAI, Vector3> LookAtFn = AccessTools.MethodDelegate<System.Action<BaseAI, Vector3>>(AccessTools.Method(typeof(BaseAI), "LookAt"));
        const float MovingSpeed = 0.3f;

        readonly Character _body;
        readonly MonsterAI _ai;

        internal ValheimTrollMovement(Character body, MonsterAI ai)
        {
            _body = body;
            _ai = ai;
        }

        /// Both private game methods were found (in-game test Startup_TamingReflection checks the same pair through TrollFarming).
        internal static bool SelfCheck() { return MoveToFn != null && LookAtFn != null; }

        public bool IsMoving { get { return _body != null && _body.GetVelocity().magnitude > MovingSpeed; } }
        public Vector3 Position { get { return _body.transform.position; } }
        public Vector3 Forward { get { return _body.transform.forward; } }
        public bool MoveTo(float dt, Vector3 destination, float arriveMetres, bool run) { return MoveToFn(_ai, dt, destination, arriveMetres, run); }
        public void LookAt(Vector3 point) { LookAtFn(_ai, point); }
        public bool IsLookingAt(Vector3 point, float maxDegrees) { return _ai.IsLookingAt(point, maxDegrees, false); }
        public void Stop() { _ai.StopMoving(); }
    }
}
