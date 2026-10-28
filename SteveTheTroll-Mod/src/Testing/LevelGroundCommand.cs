using Jotunn.Entities;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>
    /// D-105 (TEST-044: the depot levelling made a spike and a pit): console command stt_levelground. Levels one 14 x 14 m square flat
    /// (DepotGround.RepairRadius) - around the nearest troll depot within 15 m at the height it stands on, or else around the player at the
    /// height of the player's feet. Saved and synced like hoe work. Needs devcommands.
    /// </summary>
    internal sealed class LevelGroundCommand : ConsoleCommand
    {
        const float DepotSearch = 15f;
        public override string Name { get { return "stt_levelground"; } }
        public override string Help { get { return "SteveTheTroll test: level a 14 x 14 m square flat (at the nearest troll depot within 15 m, else where you stand)"; } }

        public override void Run(string[] args)
        {
            var p = Player.m_localPlayer;
            if (p == null || ZNetScene.instance == null) { Console.instance.Print("stt_levelground: no player / world"); return; }
            if (!Terminal.m_cheat) { Console.instance.Print("stt_levelground: needs devcommands"); return; }
            Vector3 centre = p.transform.position;
            float height = centre.y;
            string where = "where you stand";
            float best = DepotSearch;
            foreach (var v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
            {
                if (v == null || !v.IsValid() || Utils.GetPrefabName(v.gameObject) != Backpack.DepotPrefab.Name) continue;
                float d = Vector3.Distance(v.transform.position, p.transform.position);
                if (d > best) continue;
                // the lowest frame of that stack is the one standing on the ground
                best = d; centre = v.transform.position; height = v.transform.position.y; where = "at the troll depot " + d.ToString("0") + " m away";
            }
            foreach (var v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
                if (v != null && v.IsValid() && Utils.GetPrefabName(v.gameObject) == Backpack.DepotPrefab.Name && new Vector2(v.transform.position.x - centre.x, v.transform.position.z - centre.z).magnitude < 0.5f)
                    height = Mathf.Min(height, v.transform.position.y);
            bool ok = Backpack.DepotGround.Repair(centre, height);
            string msg = ok ? "stt_levelground: levelled " + (2f * Backpack.DepotGround.RepairRadius).ToString("0") + " x " + (2f * Backpack.DepotGround.RepairRadius).ToString("0") + " m " + where + " at height " + height.ToString("F1")
                            : "stt_levelground: the level op is not available";
            Console.instance.Print(msg);
            ModLog.Info("LevelGroundCommand: " + msg);
        }
    }
}
