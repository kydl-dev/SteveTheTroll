using Kydl.Common;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Movement.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Core
{
    /// <summary>
    /// Architecture V2 (D-169): the mod's lightweight view of ONE game troll - the game owns the Character, the mod owns its behaviour. Not a MonoBehaviour: a component that
    /// needs it builds one in Awake and keeps it (runtime state only - nothing here is persistent). Building it reads components, never the network; reading Id never writes the
    /// ZDO (EnsureId is the one explicit, owner-only write).
    /// </summary>
    internal sealed class TrollContext : ITrollIdentity, ITrollOwnership
    {
        internal Character Character { get; }
        internal Humanoid Humanoid { get; }
        internal MonsterAI AI { get; }
        internal ZNetView Network { get; }
        internal TrollCapabilities Capabilities { get; }

        internal TrollContext(Character character)
        {
            Character = character;
            Humanoid = character as Humanoid;
            AI = character != null ? character.GetComponent<MonsterAI>() : null;
            Network = character != null ? character.GetComponent<ZNetView>() : null;
            Capabilities = new TrollCapabilities(
                character != null && AI != null ? new ValheimTrollMovement(character, AI) : null,
                Humanoid != null ? new ValheimTrollCombat(Humanoid) : null,
                Network != null ? new ValheimTrollInventory(Network) : null);
            ModLog.Verbose("TrollContext: built for " + Tag + " - ai " + (AI != null) + ", network " + (Network != null) + ", humanoid " + (Humanoid != null) + ", capabilities complete " + Capabilities.Complete);
        }

        public bool IsValid { get { return Character != null && Network != null && Network.IsValid(); } }
        public bool IsOwner { get { return IsValid && Network.IsOwner(); } }
        internal bool IsAlive { get { return Character != null && !Character.IsDead(); } }

        /// Read only: "" when the troll has no id yet (no ZDO write).
        public string Id
        {
            get
            {
                var zdo = IsValid ? Network.GetZDO() : null;
                return zdo != null ? zdo.GetString(TrollRegistry.IdKey, "") : "";
            }
        }

        public string Name { get { return Character != null ? Character.GetHoverName() : ""; } }
        public long OwnerId { get { return TrollTaming.Owner(Character); } }

        /// The id, created now when missing and this client owns the troll (TrollRegistry.IdOf). Expected outcome: a non-empty id unless the troll has none and is not ours.
        internal string EnsureId()
        {
            string id = TrollRegistry.IdOf(Character);
            ModLog.Verbose("TrollContext: " + Tag + " id " + (id.Length > 0 ? id : "(none, not owner)"));
            return id;
        }

        internal Vector3 Position { get { return Character != null ? Character.transform.position : Vector3.zero; } }
        internal string Tag { get { return TrollTaming.Tag(Character); } }
    }
}
