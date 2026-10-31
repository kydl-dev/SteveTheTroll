using Kydl.Common;
using SteveTheTroll.Backpack;
using UnityEngine;

namespace SteveTheTroll.Hub
{
    /// <summary>
    /// D-135: on the hub prefab. The first frame after the player placed it (its creator is set by then) the owner's client numbers it
    /// (HUB 1, 2, 3 ...: the smallest free number of that player), stores owner and name in the ZDO and levels the ground around it.
    /// The rules (10 per player, 1000 m apart) are checked before it is placed (HubPlacement).
    /// </summary>
    internal class TrollHubPiece : MonoBehaviour
    {
        ZNetView _nview;
        Piece _piece;
        float _tries;

        void Awake() { _nview = GetComponent<ZNetView>(); _piece = GetComponent<Piece>(); }

        void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            var zdo = _nview.GetZDO();
            if (zdo.GetInt(HubRegistry.NumberKey, 0) > 0) { enabled = false; return; }
            long creator = _piece != null ? _piece.GetCreator() : 0L;
            if (creator == 0L) { _tries += Time.deltaTime; if (_tries > 5f) enabled = false; return; }     // a hub the game spawned without a builder: not ours to number
            var used = new System.Collections.Generic.List<int>();
            foreach (var h in HubRegistry.Own(creator)) used.Add(h.Number);
            int no = HubRules.NextNumber(used);
            var p = Player.m_localPlayer;
            zdo.Set(HubRegistry.OwnerKey, creator);
            zdo.Set(HubRegistry.OwnerNameKey, p != null && p.GetPlayerID() == creator ? p.GetPlayerName() : "");
            zdo.Set(HubRegistry.NumberKey, no);
            zdo.Set(HubRegistry.NameKey, HubRules.DefaultName(no));
            bool levelled = DepotGround.FlattenHub(transform.position);
            ModLog.Info("TrollHubPiece: " + HubRules.DefaultName(no) + " set up at " + transform.position.ToString("F1") + " for player " + creator + (levelled ? ", ground levelled" : ", ground NOT levelled (no level op)"));
            HubRegistry.All(true);
            if (p != null && p.GetPlayerID() == creator) Ui.Notify.Show(Loc.T("stevethetroll_hub_msg_built", HubRules.DefaultName(no)), Ui.Notify.Kind.Good);
            enabled = false;
        }
    }
}
