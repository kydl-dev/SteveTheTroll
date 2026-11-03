using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Contracts;

/// <summary>
///     Architecture V2 (D-169): moving a troll. Contracts may use Unity value types (Vector3); domain code may not. A
///     MoveTo that returns means "the request was issued",
///     never "arrived" - arrival is measured by the caller.
/// </summary>
internal interface ITrollMovement
{
    /// The troll moves faster than a slow shuffle right now.
    bool IsMoving { get; }

    Vector3 Position { get; }
    Vector3 Forward { get; }

    /// One frame of walking / running to `destination`. Expected outcome: the game's own answer (true = within `arriveMetres`).
    bool MoveTo(float dt, Vector3 destination, float arriveMetres, bool run);

    void LookAt(Vector3 point);
    bool IsLookingAt(Vector3 point, float maxDegrees);
    void Stop();
}
