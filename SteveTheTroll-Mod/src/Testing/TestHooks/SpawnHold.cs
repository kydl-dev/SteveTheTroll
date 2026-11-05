using KydlCore.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// 2026-11-05 (gate run 1 + ecosystem run, instruction-latency): a troll spawned 70 m out, in the zone next to the player's, fell through the ground - the test had
/// just moved the player, the game had not built that zone's terrain COLLIDER yet (a raycast down found only tree canopies), so the troll dropped into the water
/// volume below. The troll is held (kinematic) until a terrain collider is under it, then set down on it; at most 15 s.
internal sealed class SpawnHold : MonoBehaviour
{
    private const float MaxSeconds = 15f;
    private Rigidbody _body;
    private float _since;

    internal static bool GroundReady(Vector3 at)
    {
        return Physics.Raycast(at + Vector3.up * 5f, Vector3.down, 80f, LayerMask.GetMask("terrain"));
    }

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _since = Time.time;
        if (_body != null)
        {
            _body.isKinematic = true;
        }
    }

    private void FixedUpdate()
    {
        RaycastHit hit;
        bool ready = Physics.Raycast(transform.position + Vector3.up * 5f, Vector3.down, out hit, 80f, LayerMask.GetMask("terrain"));
        bool late = Time.time - _since > MaxSeconds;
        if (!ready && !late)
        {
            return;
        }

        if (ready)
        {
            transform.position = hit.point + Vector3.up * 0.3f;
        }

        if (_body != null)
        {
            _body.isKinematic = false;
            _body.velocity = Vector3.zero;
        }

        ModLog.Info("SpawnHold: " + name + (ready ? " set down on the terrain collider at " + transform.position.ToString("F0") : " released WITHOUT a terrain collider under it") +
                    " after " + (Time.time - _since).ToString("F1") + " s");
        Destroy(this);
    }
}