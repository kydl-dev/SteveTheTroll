using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Backpack.Infrastructure;

/// <summary>
///     T-008c (D-062): one chest of the backpack on the troll - hover "Steve 1's backpack - chest 2 / [E] Open", E opens
///     that chest
///     (BackpackStore.Open). Sits on the chest's visual copy with its own box collider (layer piece_nonsolid: nothing
///     walks into it) and a
///     kinematic rigidbody (so it is not part of the troll's body collider).
/// </summary>
internal sealed class BackpackChest : MonoBehaviour, Hoverable, Interactable
{
    internal int Index;
    internal Character Troll;

    public string GetHoverName()
    {
        Tameable t = Troll != null ? Troll.GetComponent<Tameable>() : null;
        string n = t != null ? t.GetHoverName() : "Troll";
        return Loc.T("stevethetroll_backpack_hover", n, (Index + 1).ToString());
    }

    public string GetHoverText()
    {
        return GetHoverName() +
               Localization.instance.Localize("\n[<color=yellow><b>$KEY_Use</b></color>] $piece_container_open");
    }

    /// The backpack sits high on the troll: the player may hover it from a bit further away than usual.
    public float GetHoverOffset()
    {
        return 3f;
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold)
        {
            return false;
        }

        return BackpackStore.Open(Troll, Index, user);
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        return false;
    }

    /// Adds the collider + chest component to a chest's visual copy. Expected outcome: the BackpackChest.
    internal static BackpackChest Make(GameObject copy, Character troll, int index)
    {
        var b = new Bounds();
        bool any = false;
        foreach (MeshFilter mf in copy.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null)
            {
                continue;
            }

            Bounds mb = mf.sharedMesh.bounds;
            for (int k = 0; k < 8; k++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                Vector3 p = copy.transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!any)
                {
                    b = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    b.Encapsulate(p);
                }
            }
        }

        if (!any)
        {
            b = new Bounds(new Vector3(0f, 0.4f, 0f), new Vector3(1.6f, 0.8f, 0.9f));
        }

        BoxCollider box = copy.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = b.size;
        int layer = LayerMask.NameToLayer("piece_nonsolid");
        if (layer >= 0)
        {
            copy.layer = layer;
        }

        Rigidbody rb = copy.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        int ignored = 0; // never push its own troll
        foreach (Collider col in troll.GetComponentsInChildren<Collider>(true))
        {
            if (col != box && col.GetComponentInParent<BackpackChest>() == null)
            {
                Physics.IgnoreCollision(box, col);
                ignored++;
            }
        }

        BackpackChest c = copy.AddComponent<BackpackChest>();
        c.Index = index;
        c.Troll = troll;
        ModLog.Verbose("BackpackChest: " + TrollTaming.Tag(troll) + " chest " + (index + 1) + " collider " +
                       b.size.ToString("F2") + " m (local), layer " + layer + ", ignores " + ignored +
                       " troll collider(s)");
        return c;
    }
}
