using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using UnityEngine;

namespace SteveTheTroll.Features.Hubs.Infrastructure;

/// <summary>
///     D-135: on the hub prefab. The first frame after the player placed it (its creator is set by then) the owner's
///     client numbers it
///     (HUB 1, 2, 3 ...: the smallest free number of that player), stores owner and name in the ZDO and levels the ground
///     around it.
///     The rules (10 per player, 1000 m apart) are checked before it is placed (HubPlacement).
/// </summary>
internal class TrollHubPiece : MonoBehaviour
{
    private ZNetView _nview;
    private Piece _piece;
    private float _tries;

    private void Awake()
    {
        _nview = GetComponent<ZNetView>();
        _piece = GetComponent<Piece>();
    }

    private void Update()
    {
        if (_nview == null || !_nview.IsValid() || !_nview.IsOwner())
        {
            return;
        }

        ZDO zdo = _nview.GetZDO();
        if (zdo.GetInt(HubRegistry.NumberKey) > 0)
        {
            enabled = false;
            return;
        }

        long creator = _piece != null ? _piece.GetCreator() : 0L;
        if (creator == 0L)
        {
            _tries += Time.deltaTime;
            if (_tries > 5f)
            {
                enabled = false;
            }

            return;
        } // a hub the game spawned without a builder: not ours to number

        var used = new List<int>();
        foreach (HubInfo h in HubRegistry.Own(creator))
        {
            used.Add(h.Number);
        }

        int no = HubRules.NextNumber(used);
        Player p = Player.m_localPlayer;
        zdo.Set(HubRegistry.OwnerKey, creator);
        zdo.Set(HubRegistry.OwnerNameKey, p != null && p.GetPlayerID() == creator ? p.GetPlayerName() : "");
        zdo.Set(HubRegistry.NumberKey, no);
        zdo.Set(HubRegistry.NameKey, HubRules.DefaultName(no));
        bool levelled = DepotGround.FlattenHub(transform.position);
        ModLog.Info("TrollHubPiece: " + HubRules.DefaultName(no) + " set up at " + transform.position.ToString("F1") +
                    " for player " + creator +
                    (levelled ? ", ground levelled" : ", ground NOT levelled (no level op)"));
        HubRegistry.All(true);
        if (p != null && p.GetPlayerID() == creator)
        {
            Notify.Show(Loc.T("stevethetroll_hub_msg_built", HubRules.DefaultName(no)), Notify.Kind.Good);
        }

        enabled = false;
    }
}
