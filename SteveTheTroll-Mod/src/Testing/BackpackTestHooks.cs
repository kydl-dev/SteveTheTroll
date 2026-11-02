using Kydl.Common;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>D-171 (P6): TestLab hooks for the backpack pickup and the death crates (suite tests backpack-pickup, backpack-death-crates).</summary>
    internal static class BackpackTestHooks
    {
        /// `count` items of `prefab` dropped on the ground `dx`,`dz` metres from the troll. Expected outcome: how many drops were made.
        [TestHook("stt.dropNear")]
        static int DropNear(GameObject troll, string prefab, int count, float dx, float dz)
        {
            var p = ZNetScene.instance.GetPrefab(prefab);
            if (p == null) throw new System.Exception(prefab + " prefab missing");
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                var at = troll.transform.position + new Vector3(dx + i * 0.3f, 0f, dz);
                at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.3f;
                Object.Instantiate(p, at, Quaternion.identity);
                n++;
            }
            ModLog.Info("BackpackTestHooks: " + n + " x " + prefab + " dropped near " + TrollTaming.Tag(troll.transform));
            return n;
        }

        /// The troll dies (the game's own damage, so BackpackDeath runs). Expected outcome: whether it is dead.
        [TestHook("stt.killTroll")]
        static bool KillTroll(GameObject troll)
        {
            var c = troll.GetComponent<Character>();
            var h = new HitData();
            h.m_damage.m_damage = 1e7f;
            h.m_point = c.transform.position;
            c.Damage(h);
            return c.IsDead() || c.GetHealth() <= 0f;
        }

        /// Cargo crates within `radius` m of the player.
        [TestHook("stt.cratesNear")]
        static int CratesNear(float radius)
        {
            var p = Player.m_localPlayer.transform.position;
            int n = 0;
            foreach (var v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
                if (v != null && v.IsValid() && Utils.GetPrefabName(v.gameObject) == "CargoCrate" && Vector3.Distance(v.transform.position, p) <= radius) n++;
            return n;
        }
    }
}
