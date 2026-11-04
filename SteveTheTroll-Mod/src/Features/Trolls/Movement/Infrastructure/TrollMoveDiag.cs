using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Movement.Infrastructure;

/// <summary>
///     D-176 diagnostics (fix8: guard-mob-kills, a troll whose real speed was 0.0 m/s for 15 s with MoveTo called every step, "nothing in the straight line"). Says WHY the game
///     may not be moving a troll that is told to walk: who owns it, whether the game's navmesh has a path from here to the goal (BaseAI.MoveTo uses it), how steep the ground
///     under it is, whether it stands on the ground and which way the game has it moving. Only reads; never changes behaviour.
/// </summary>
internal static class TrollMoveDiag
{
    private static readonly string[] GroundLayers = { "terrain", "Default", "static_solid", "Default_small" };

    /// One line of facts about the troll's movement towards `goal`. Expected outcome: the text (never throws).
    internal static string Describe(Character body, Vector3 goal)
    {
        if (body == null)
        {
            return "movement unknown (no character)";
        }

        string owner = "no network view";
        ZNetView view = body.GetComponent<ZNetView>();
        if (view != null && view.IsValid())
        {
            owner = view.IsOwner() ? "owner here" : "OWNED BY ANOTHER PEER";
        }

        string path = "navmesh path unknown";
        BaseAI ai = body.GetComponent<BaseAI>();
        if (ai != null && Pathfinding.instance != null)
        {
            bool have = Pathfinding.instance.HavePath(body.transform.position, goal, ai.m_pathAgentType);
            path = "navmesh path to the goal " + (have ? "exists" : "NONE") + " (agent " + ai.m_pathAgentType + ")";
        }

        string ground = "ground not found";
        RaycastHit hit;
        if (Physics.Raycast(body.transform.position + Vector3.up * 2f, Vector3.down, out hit, 5f, LayerMask.GetMask(GroundLayers)))
        {
            ground = "ground slope " + Vector3.Angle(hit.normal, Vector3.up).ToString("F0") + " deg (" + hit.collider.name + ")";
        }

        return owner + ", " + path + ", " + ground + ", on ground " + body.IsOnGround() + ", move dir " + body.GetMoveDir().ToString("F1") + ", running " + body.IsRunning();
    }
}