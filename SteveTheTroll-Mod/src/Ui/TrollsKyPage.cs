using System;
using System.Collections.Generic;
using Kydl.Common;
using KydlCore;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// T-098 first page on the described-page API: the "Trolls" tab says WHAT it shows (a troll picker and the detail blocks of the picked troll) and KydlCore draws it with its
    /// theme and text size - this mod sets no font, colour or size here. The page state is the picked troll's encoded values, so it is rebuilt only when something shown changed.
    /// </summary>
    internal static class TrollsKyPage
    {
        static string _picked = "";

        /// <summary>Expected outcome: a page with the picker and the blocks, or one note when the player has no living tamed troll.</summary>
        internal static KyPage Build()
        {
            var page = new KyPage();
            var trolls = Taming.TrollRegistry.Trolls();
            bool any = false;
            if (trolls != null) foreach (var t in trolls) if (t.DiedTicks <= 0) { any = true; break; }
            if (!any)
            {
                page.Section("").Note(Loc.T("stevethetroll_troll_none"));
                page.State = "none";
                return page;
            }
            Func<string, string> text = k => Loc.T(k);
            TrollInfo pick = null;
            var choices = new List<KyChoice>();
            foreach (var t in trolls)
            {
                choices.Add(new KyChoice(t.Id, t.Shown(text)));
                if (t.Id == _picked) pick = t;
            }
            if (pick == null) { pick = trolls[0]; _picked = pick.Id; }
            page.Section("").Choice(text("stevethetroll_troll_pick"), choices, pick.Id, id => { _picked = id; });
            foreach (var g in TrollDetailsBuilder.Build(pick, text, DateTime.UtcNow.Ticks))
            {
                var s = page.Section(g.Title);
                foreach (var r in g.Rows) s.Value(r.Label, r.Value, r.MaxValue);
            }
            page.State = pick.Id + "|" + pick.Encode() + pick.Loaded + "|" + choices.Count;
            return page;
        }
    }
}
