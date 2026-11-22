using MOD_nE7UL2.Helper;
using ModLib.Enum;
using ModLib.Helper;
using ModLib.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MOD_nE7UL2.Mod
{
    /// <summary>Windows of the clan elders: the elders of the player's clan (and the candidates), the war council.</summary>
    public partial class ClanElderEvent
    {
        private static Color GOLD => new Color(0.55f, 0.35f, 0f);
        private static Color RED => new Color(0.6f, 0.1f, 0.1f);

        /// <summary>
        /// The elders of the player's clan in the town: each with its grade, salary and loyalty (dismiss), then the
        /// candidates of the area with the chance they accept (invite).
        /// </summary>
        public static void OpenElders(MapBuildTown town)
        {
            g.ui.CloseUI(UIType.TextInfoLong);
            g.ui.CloseUI(UIType.TextInfo);
            var ui = new UICustom1(string.Format(GameTool.LS("elder_title"), ClanEvent.ClanName(Player), town.name));
            var row = ui.FirstRow;
            var list = Of(town, Player);
            var max = MaxElders(town, Player);
            foreach (var line in InfoDialog.Wrap(string.Format(GameTool.LS("elder_rules"), MinGrade(town), Conf.HeadVotes), InfoDialog.WRAP_CHARS_LONG))
                ui.AddText(ui.MidCol, row++, line).Format(null, 14);
            row++;
            ui.AddText(ui.MidCol, row++, string.Format(GameTool.LS("elder_current"), list.Count, max)).Format(GOLD, 15);
            var textX = ui.Columns[ui.MidCol] - 90f;
            var btnX = ui.Columns[ui.LastCol] - 130f;
            if (list.Count == 0) ui.AddText(ui.MidCol, row++, GameTool.LS("elder_none")).Format(Color.gray, 14);
            foreach (var e in list)
            {
                var u = Unit(e.UnitId);
                if (u == null) continue;
                ui.AddText(textX, ui.Rows[row], string.Format(GameTool.LS("elder_line"), u.GetName(), u.GetGradeLvl(), Salary(u), e.Loyalty))
                    .Format(e.Loyalty < Conf.FleeLoyalty ? RED : Color.black, 14);
                ui.AddButton(btnX, ui.Rows[row], () =>
                {
                    Leave(e, "log_clan_elder_dismissed");
                    OpenElders(town);
                }, GameTool.LS("elder_dismiss")).Size(130, 28);
                row++;
            }
            row++;
            ui.AddText(ui.MidCol, row++, string.Format(GameTool.LS("elder_candidates"), MinGrade(town))).Format(GOLD, 15);
            var cands = Candidates(town, Player, 6);
            if (cands.Count == 0) ui.AddText(ui.MidCol, row++, GameTool.LS("elder_no_candidate")).Format(Color.gray, 14);
            foreach (var u in cands)
            {
                if (row >= ui.LastRow - 1) break;
                ui.AddText(textX, ui.Rows[row], string.Format(GameTool.LS("elder_candidate_line"), u.GetName(), u.GetGradeLvl(), (int)Intim(u, Player), Salary(u), (int)Math.Round(ChanceOf(town, u) * 100)))
                    .Format(Color.black, 14);
                var cand = u;
                ui.AddButton(btnX, ui.Rows[row], () =>
                {
                    var rs = Invite(town, cand, Roll);
                    OpenElders(town);
                    InfoDialog.Msg(town.name, string.Format(GameTool.LS(rs), cand.GetName(), ClanEvent.ClanName(Player)));
                }, GameTool.LS("elder_invite")).Size(130, 28);
                row++;
            }
            ui.AddText(ui.MidCol, ui.LastRow, GameTool.LS("elder_hint")).Format(Color.gray, 13);
        }

        /// <summary>The war council of the player's clan: the clans of the town hostile to it, war declared on one by a vote.</summary>
        public static void OpenWarCouncil(MapBuildTown town)
        {
            g.ui.CloseUI(UIType.TextInfoLong);
            var ui = new UICustom1(string.Format(GameTool.LS("elder_council_title"), ClanEvent.ClanName(Player), town.name));
            var row = ui.FirstRow;
            var elders = Of(town, Player).Count;
            foreach (var line in InfoDialog.Wrap(string.Format(GameTool.LS("elder_council_rules"), Conf.HeadVotes, elders, Conf.HeadVotes + elders), InfoDialog.WRAP_CHARS_LONG))
                ui.AddText(ui.MidCol, row++, line).Format(null, 14);
            row++;
            var foes = ClanEvent.Heads(town).Where(x => !ClanEvent.IsPlayerClan(x)).Select(Unit)
                .Select(h => (h, rel: ClanEvent.Relation(town, Player, h))).Where(x => x.rel <= ClanEvent.Conf.HostileAt).OrderBy(x => x.rel).ToList();
            if (foes.Count == 0) ui.AddText(ui.MidCol, row++, GameTool.LS("elder_council_none")).Format(Color.gray, 14);
            var textX = ui.Columns[ui.MidCol] - 90f;
            var btnX = ui.Columns[ui.LastCol] - 130f;
            var ours = ClanEvent.SidePower(town, Player, null, true);
            foreach (var (h, rel) in foes.Take(8))
            {
                var theirs = ClanEvent.SidePower(town, h, null, true);
                ui.AddText(textX, ui.Rows[row], string.Format(GameTool.LS("elder_council_line"), ClanEvent.ClanName(h), rel, ClanEvent.Members(h).Count, Of(town, h).Count,
                    (int)Math.Round(ClanEvent.WinChance(ours, theirs) * 100))).Format(RED, 14);
                var foe = h;
                ui.AddButton(btnX, ui.Rows[row], () => DeclareWar(town, foe), GameTool.LS("elder_declare")).Size(130, 28);
                row++;
            }
        }

        /// <summary>The ballots of a war council (onPassed: the button that starts the war).</summary>
        public static UICustomBase ShowBallots(MapBuildTown town, WorldUnitBase head, WorldUnitBase enemy, List<Ballot> ballots, bool passed, Action onPassed)
        {
            var yes = ballots.Where(x => x.Yes).Sum(x => x.Votes);
            var all = ballots.Sum(x => x.Votes);
            var lines = new List<string> { string.Format(GameTool.LS("elder_ballot_head"), ClanEvent.ClanName(head), ClanEvent.ClanName(enemy)) };
            lines.AddRange(ballots.Select(b => string.Format(GameTool.LS(b.IsHead ? "elder_ballot_line_head" : "elder_ballot_line"), b.Name, b.Votes,
                GameTool.LS(b.Yes ? "elder_ballot_for" : "elder_ballot_against"))));
            lines.Add(string.Format(GameTool.LS(passed ? "elder_ballot_passed" : "elder_ballot_failed"), yes, all));
            // a short title: the small window cuts a long one
            var title = GameTool.LS("elder_council_button");
            return passed && onPassed != null
                ? InfoDialog.Show(title, string.Join("\n", lines), (GameTool.LS("elder_to_war"), onPassed))
                : InfoDialog.Show(title, string.Join("\n", lines), (GameTool.LS("dlg_close"), null));
        }

        /// <summary>The player's post in the town (Clans window): the clan it guards, the salary, resign.</summary>
        public static void AddClansButtons(UICustom1 ui, MapBuildTown town)
        {
            if (!Enabled || Player == null) return;
            var y = ui.Rows[ui.LastRow - 2];
            var mid = ui.Columns[ui.MidCol];
            if (ClanEvent.Heads(town).Contains(Player.GetUnitId()))
            {
                ui.AddButton(mid - 120f, y, () => OpenElders(town), string.Format(GameTool.LS("elder_button"), Of(town, Player).Count, MaxElders(town, Player))).Size(200, 30);
                ui.AddButton(mid + 120f, y, () => OpenWarCouncil(town), GameTool.LS("elder_council_button")).Size(200, 30);
            }
            var post = PlayerPost;
            if (post != null && post.TownId == town.buildData.id)
            {
                var head = Unit(post.Head);
                ui.AddText(ui.MidCol, ui.LastRow - 3, string.Format(GameTool.LS("elder_post"), ClanEvent.ClanName(head), Salary(Player))).Format(GOLD, 14);
                ui.AddButton(mid + 360f, y, () => InfoDialog.Msg(town.name, string.Format(GameTool.LS("elder_resign_confirm"), ClanEvent.ClanName(head)), MsgBoxButtonEnum.YesNo, () =>
                {
                    Leave(post, "log_clan_elder_resigned");
                    ClanEvent.OpenClans(town);
                }), GameTool.LS("elder_resign")).Size(160, 30);
            }
        }

        /// <summary>The elders of a clan for its line in the Clans window ("" when none).</summary>
        public static string ClanLineSuffix(MapBuildTown town, WorldUnitBase head)
        {
            if (!Enabled) return string.Empty;
            var names = Of(town, head).Select(x => Unit(x.UnitId)?.GetName()).Where(x => x != null).ToList();
            return names.Count == 0 ? string.Empty : string.Format(GameTool.LS("elder_clan_suffix"), string.Join(", ", names));
        }
    }
}
