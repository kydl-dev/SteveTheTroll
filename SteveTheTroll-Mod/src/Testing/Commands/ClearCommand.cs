using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Jotunn.Entities;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.Commands
{
    /// <summary>
    /// D-093 (user: "a command that kills all (players not included) in a 500m circular area ... a clean test"): console command
    /// stt_clear [radius m, default 500] [drops]. Every creature (anything with a Character that is not a Player - wild, tamed, our own
    /// trolls too) within the radius of the local player, measured flat (x / z), is removed: loaded ones through the game, unloaded ones by
    /// deleting their saved world objects (ZDOs), so the far parts of the circle are clean too. Default: removed without drops;
    /// "drops" = loaded creatures are killed (they drop their loot), unloaded ones are still removed. Needs devcommands like the game's own.
    /// </summary>
    internal sealed class ClearCommand : ConsoleCommand
    {
        internal const float DefaultRadius = 500f, MaxRadius = 5000f;
        public override string Name { get { return "stt_clear"; } }
        public override string Help { get { return "SteveTheTroll test: remove every creature (no players) within [radius=500] m; add 'drops' to kill the loaded ones instead"; } }

        static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> Objects = AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");

        public override void Run(string[] args)
        {
            float radius = DefaultRadius;
            bool drops = false;
            if (args != null)
                foreach (var a in args)
                {
                    float r;
                    if (string.Equals(a, "drops", System.StringComparison.OrdinalIgnoreCase)) drops = true;
                    else if (float.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out r)) radius = r;
                }
            radius = Mathf.Clamp(radius, 1f, MaxRadius);
            var p = Player.m_localPlayer;
            if (p == null || ZNetScene.instance == null || ZDOMan.instance == null) { Console.instance.Print("stt_clear: no player / world"); return; }
            if (!Terminal.m_cheat) { Console.instance.Print("stt_clear: needs devcommands"); return; }
            Vector3 at = p.transform.position;

            int killed = 0, removed = 0, far = 0;
            var loaded = new HashSet<ZDO>();
            foreach (var c in new List<Character>(Character.GetAllCharacters()))
            {
                if (c == null || c is Player) continue;
                var view = c.GetComponent<ZNetView>();
                if (view != null && view.GetZDO() != null) loaded.Add(view.GetZDO());
                if (!Within(at, c.transform.position, radius)) continue;
                if (drops && !c.IsDead())
                {
                    var hit = new HitData { m_point = c.transform.position };
                    hit.m_damage.m_damage = 1e10f;
                    c.Damage(hit);
                    killed++;
                }
                else if (view != null && view.IsValid())
                {
                    view.ClaimOwnership();
                    ZNetScene.instance.Destroy(c.gameObject);
                    removed++;
                }
            }

            var creatures = CreaturePrefabs();
            var gone = new List<ZDO>();
            foreach (var z in Objects(ZDOMan.instance).Values)
                if (z != null && !loaded.Contains(z) && creatures.Contains(z.GetPrefab()) && Within(at, z.GetPosition(), radius)) gone.Add(z);
            long me = ZDOMan.GetSessionID();
            foreach (var z in gone)
            {
                if (ZNetScene.instance.FindInstance(z) != null) continue;      // loaded meanwhile: handled above / next run
                z.SetOwner(me);
                ZDOMan.instance.DestroyZDO(z);
                far++;
            }
            string msg = "stt_clear: " + (drops ? killed + " killed, " : "") + removed + " removed nearby, " + far + " removed in unloaded areas (radius " + radius.ToString("0", CultureInfo.InvariantCulture) + " m, players kept)";
            Console.instance.Print(msg);
            ModLog.Info("ClearCommand: " + msg);
        }

        /// Pure: flat (x / z) distance within the radius, inclusive.
        internal static bool Within(Vector3 centre, Vector3 point, float radius)
        {
            float dx = point.x - centre.x, dz = point.z - centre.z;
            return dx * dx + dz * dz <= radius * radius;
        }

        static HashSet<int> CreaturePrefabs()
        {
            var set = new HashSet<int>();
            foreach (var go in ZNetScene.instance.m_prefabs)
                if (go != null && go.GetComponent<Character>() != null && go.GetComponent<Player>() == null) set.Add(go.name.GetStableHashCode());
            return set;
        }
    }
}
