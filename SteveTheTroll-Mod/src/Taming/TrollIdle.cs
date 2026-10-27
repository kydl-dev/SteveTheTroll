using System.Collections.Generic;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-062 (user: the 2-minute check "for any moment when no job was given and the backpack storages are not full"): on the client that
    /// owns it, a tamed troll that is not farming, not walking to its tamer, not following and not fighting, with room in its backpack, starts
    /// farming on its own after AutoFarmSeconds (its focus item first, then the default priority, D-058) - with the dark-yellow message.
    /// </summary>
    internal static class TrollIdle
    {
        static readonly Dictionary<int, float> Since = new Dictionary<int, float>();
        static float _next;

        /// Plugin.Update. Expected outcome: how many trolls were sent farming this time.
        internal static int Tick()
        {
            if (Time.time < _next || !ModSettings.On) return 0;
            _next = Time.time + 1f;
            int sent = 0;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c)) continue;
                var view = c.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                int id = c.GetInstanceID();
                if (Busy(c, view.GetZDO())) { Since.Remove(id); continue; }
                float since;
                if (!Since.TryGetValue(id, out since)) { Since[id] = Time.time; continue; }
                if (Time.time - since < ModSettings.AutoFarmSeconds.Value) continue;
                Since.Remove(id);
                string focus = Ui.TrollFocus.Normalize(view.GetZDO().GetString(TrollRegistry.FocusKey, ""));
                ModLog.Info("TrollIdle: " + c.name + " had no job for " + ModSettings.AutoFarmSeconds.Value.ToString("0") + " s - farms on its own (focus '" + focus + "')");
                if (TrollFarming.Begin(c, focus, true)) sent++;
            }
            if (Since.Count > 200) Since.Clear();
            return sent;
        }

        // A job: farming, walking to its tamer, following, fighting - or a full backpack (nothing to do until it is emptied).
        static bool Busy(Character c, ZDO zdo)
        {
            var ai = c.GetComponent<MonsterAI>();
            if (ai == null || c.GetComponent<TrollFarmer>() != null || c.GetComponent<TrollArrival>() != null) return true;
            if (ai.GetFollowTarget() != null || ai.GetTargetCreature() != null) return true;
            return Backpack.BackpackStore.IsFull(zdo);
        }
    }
}
