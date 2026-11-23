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
        /// The elders of the player's clan in the town: a card each (portrait, name, realm, salary, loyalty; dismiss),
        /// then the candidates of the area (portrait, name, realm, intimacy, salary, the chance they accept; invite).
        /// A click on a portrait opens the cultivator's information.
        /// </summary>
        public static void OpenElders(MapBuildTown town)
        {
            g.ui.CloseUI(UIType.TextInfoLong);
            g.ui.CloseUI(UIType.TextInfo);
            var ui = new UICustom1(string.Format(GameTool.LS("elder_title"), ClanEvent.ClanName(Player), town.name));
            var dy = InfoSheet.RowHeight(ui);
            var left = ui.Columns[ui.FirstCol];
            var width = ui.Columns[ui.LastCol] - left;
            var row = ui.FirstRow;
            var list = Of(town, Player).Where(x => Unit(x.UnitId) != null).ToList();
            var max = MaxElders(town, Player);
            foreach (var line in InfoDialog.Wrap(string.Format(GameTool.LS("elder_rules"), MinGrade(town), Conf.HeadVotes), InfoDialog.WRAP_CHARS_LONG))
                ui.AddText(ui.MidCol, row++, line).Format(new Color(0.3f, 0.3f, 0.3f), 13);
            var cands = Candidates(town, Player, 6);
            // two sections of cards (a header, the portrait, 3 lines, a button): the portrait takes what is left
            var top = ui.Rows[row] - dy * 0.2f;
            var bottom = ui.Rows[ui.LastRow] + dy * 0.8f;
            // a section: its header (0.8), the portrait, the name (0.6), two lines, the button (3.2) and a margin
            var picH = Math.Max(dy * 2f, Math.Min(dy * 5.5f, ((top - bottom) - 2 * dy * 6f) / 2f));
            var picW = picH * 0.85f;
            var cardH = picH + dy * 6f;
            var cardW = Math.Min(width / 6f, picW + 70f);

            // the elders
            var y = top - dy * 0.5f;
            ui.AddText(ui.Columns[ui.MidCol], y, string.Format(GameTool.LS("elder_current"), list.Count, max)).Format(GOLD, 15, FontStyle.Bold);
            if (list.Count == 0)
                ui.AddText(ui.Columns[ui.MidCol], y - dy * 1.5f, GameTool.LS("elder_none")).Format(Color.gray, 14);
            for (var i = 0; i < list.Count; i++)
            {
                var e = list[i];
                var u = Unit(e.UnitId);
                var cx = ui.Columns[ui.MidCol] + cardW * (i - (list.Count - 1) / 2f);
                var low = e.Loyalty < Conf.FleeLoyalty;
                Card(ui, u, cx, y - dy * 0.8f, picW, picH, dy,
                    string.Format(GameTool.LS("elder_card_line"), u.GetGradeLvl(), Salary(u)), Color.black,
                    string.Format(GameTool.LS("elder_card_loyalty"), e.Loyalty), low ? RED : new Color(0.1f, 0.45f, 0.1f),
                    GameTool.LS("elder_dismiss"), () =>
                    {
                        Leave(e, "log_clan_elder_dismissed");
                        OpenElders(town);
                    }, cardW - 16f);
            }

            // the candidates
            y = top - cardH - dy * 0.6f;
            ui.AddText(ui.Columns[ui.MidCol], y, string.Format(GameTool.LS("elder_candidates"), MinGrade(town))).Format(GOLD, 15, FontStyle.Bold);
            if (cands.Count == 0)
                ui.AddText(ui.Columns[ui.MidCol], y - dy * 1.5f, GameTool.LS("elder_no_candidate")).Format(Color.gray, 14);
            for (var i = 0; i < cands.Count; i++)
            {
                var cand = cands[i];
                var cx = ui.Columns[ui.MidCol] + cardW * (i - (cands.Count - 1) / 2f);
                var chance = (int)Math.Round(ChanceOf(town, cand) * 100);
                Card(ui, cand, cx, y - dy * 0.8f, picW, picH, dy,
                    string.Format(GameTool.LS("elder_card_cand"), cand.GetGradeLvl(), (int)Intim(cand, Player)), Color.black,
                    string.Format(GameTool.LS("elder_card_accept"), Salary(cand), chance), chance <= 0 ? RED : chance >= 50 ? new Color(0.1f, 0.45f, 0.1f) : Color.black,
                    GameTool.LS("elder_invite"), () =>
                    {
                        var rs = Invite(town, cand, Roll);
                        OpenElders(town);
                        InfoDialog.Msg(town.name, string.Format(GameTool.LS(rs), cand.GetName(), ClanEvent.ClanName(Player)));
                    }, cardW - 16f);
            }
            ui.AddText(ui.MidCol, ui.LastRow, GameTool.LS("elder_hint")).Format(Color.gray, 13);
        }

        /// <summary>A card of a cultivator: its portrait under top, its name, two lines, a button.</summary>
        private static void Card(UICustom1 ui, WorldUnitBase u, float cx, float top, float picW, float picH, float dy,
            string line1, Color color1, string line2, Color color2, string button, Action act, float btnW)
        {
            InfoSheet.Portrait(ui, u, cx, top - picH / 2f, picW, picH);
            var y = top - picH - dy * 0.6f;
            ui.AddText(cx, y, u.GetName()).Format(Color.black, 14, FontStyle.Bold);
            ui.AddText(cx, y - dy, line1).Format(color1, 12);
            ui.AddText(cx, y - dy * 2f, line2).Format(color2, 12);
            ui.AddButton(cx, y - dy * 3.2f, act, button).Size(Math.Max(90f, btnW), 28);
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

        /// <summary>The names of the elders of a clan (its card in the Clans window).</summary>
        public static List<string> ElderNames(MapBuildTown town, WorldUnitBase head) =>
            !Enabled ? new List<string>() : Of(town, head).Select(x => Unit(x.UnitId)?.GetName()).Where(x => x != null).ToList();
    }
}
