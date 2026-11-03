using System;
using System.Collections.Generic;
using Kydl.Common;
using KydlCore;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Presentation.Hub.Commands;
using SteveTheTroll.Presentation.Hub.ViewModels;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Presentation.Hub.Pages
{
    /// <summary>
    /// T-098: the "My trolls" and "Troll work" tabs as described pages (replaces MyTrollsTab / MainMenuView.WorkTab in the hub; those stay for the Unity preview).
    /// This mod only says WHAT each row holds; KydlCore draws it and sets the text size. Distances, "doing" and "farming" are texts that change without the State changing
    /// (KydlCore updates them in place every refresh); the State holds what changes the layout (which trolls, dead or not, the focus and its choices).
    /// </summary>
    internal static class TrollListKyPages
    {
        static bool AnyLiving(IList<TrollInfo> trolls)
        {
            if (trolls != null) foreach (var t in trolls) if (t.DiedTicks <= 0) return true;
            return false;
        }

        static KyPage None(Func<string, string> text)
        {
            var p = new KyPage { State = "none" };
            p.Section("").Note(text("stevethetroll_troll_none"));
            return p;
        }

        /// <summary>Expected outcome: name | distance | Follow | Kill (asks Yes / No) per troll; a dead troll shows a text instead of the buttons.</summary>
        internal static KyPage MyTrolls()
        {
            Func<string, string> text = k => Loc.T(k);
            var trolls = TrollRegistry.Trolls();
            if (!AnyLiving(trolls)) return None(text);
            var actions = TrollCommands.Actions();
            var page = new KyPage();
            var s = page.Section(text("stevethetroll_mytrolls_heading"));
            s.Note(text("stevethetroll_mytrolls_note"));
            var rows = new List<IList<KyCell>>();
            var state = new System.Text.StringBuilder();
            foreach (var t in trolls)
            {
                var troll = t;
                bool dead = troll.DiedTicks > 0;
                state.Append(troll.Id).Append(dead ? "x" : "o").Append('|');
                float metres = actions != null && actions.Distance != null ? actions.Distance(troll.Id) : -1f;
                var row = new List<KyCell> { KyCell.Label(troll.Shown(text)), KyCell.Label(MyTrollsTab.DistanceText(metres)) };
                if (dead) row.Add(KyCell.Label(text("stevethetroll_mytrolls_dead")));
                else
                {
                    row.Add(KyCell.Button(text("stevethetroll_follow"), () => { if (actions != null && actions.Follow != null) actions.Follow(troll.Id); }));
                    row.Add(KyCell.Button(text("stevethetroll_kill"), () => { if (actions != null && actions.Kill != null) actions.Kill(troll.Id); })
                        .Confirm(string.Format(text("stevethetroll_kill_confirm"), troll.Shown(text)), text("stevethetroll_yes"), text("stevethetroll_no")));
                }
                rows.Add(row);
            }
            s.Table(new[] { text("stevethetroll_troll_name"), text("stevethetroll_troll_distance") }, rows);
            page.State = state.ToString();
            return page;
        }

        /// <summary>Expected outcome: name | doing | focus drop-down | Go farm | farming per troll.</summary>
        internal static KyPage Work()
        {
            Func<string, string> text = k => Loc.T(k);
            var trolls = TrollRegistry.Trolls();
            if (!AnyLiving(trolls)) return None(text);
            var page = new KyPage();
            var s = page.Section(text("stevethetroll_focus_heading"));
            s.Note(text("stevethetroll_focus_note"));
            var rows = new List<IList<KyCell>>();
            var state = new System.Text.StringBuilder();
            foreach (var t in trolls)
            {
                var troll = t;
                var ids = TrollFocus.ChoicesFor(troll.Biome, troll.Focus);
                string focus = TrollFocus.Normalize(troll.Focus);
                var choices = new List<KyChoice>();
                foreach (var id in ids) choices.Add(new KyChoice(id, text(TrollFocus.KeyOf(id))));
                state.Append(troll.Id).Append(':').Append(focus).Append(':').Append(ids.Count).Append('|');
                rows.Add(new List<KyCell>
                {
                    KyCell.Label(troll.Shown(text)),
                    KyCell.Label(text(troll.Status) + (troll.Loaded ? "" : " *")),
                    KyCell.Drop(choices, focus, v => SlipperMenuData.FocusChosen(troll.Id, TrollFocus.Normalize(v))),
                    KyCell.Button(text("stevethetroll_gofarm"), () => SlipperMenuData.GoFarm(troll.Id)),
                    KyCell.Label(TrollFocus.Shown(troll.Farming, text)),
                });
            }
            s.Table(new[] { text("stevethetroll_troll_name"), text("stevethetroll_troll_doing"), text("stevethetroll_focus_label"), "", text("stevethetroll_troll_farming") }, rows);
            s.Note(text("stevethetroll_troll_lastseen_note"));
            page.State = state.ToString();
            return page;
        }
    }
}

