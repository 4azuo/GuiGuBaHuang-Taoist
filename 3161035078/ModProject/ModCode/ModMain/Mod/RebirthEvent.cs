using MOD_nE7UL2.Helper;
using EGameTypeData;
using MOD_nE7UL2.Const;
using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Helper;
using ModLib.Mod;
using ModLib.Object;
using System.Linq;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Rebirth (player info screen, beside the damage calculation): reset grade/exp/reputation (must leave sect) for a permanent luck; monsters scale with total rebirth grades.
    /// </summary>
    [Cache(ModConst.REBIRTH_EVENT)]
    public class RebirthEvent : ModEvent
    {
        public static RebirthEvent Instance { get; set; }

        public const int REBIRTH_DRAMA = 420109999;
        public const int LUCK_BASE_ID = 420101000;
        public const int MAX_LUCK_LVL = 10;

        public int RebirthLevel { get; set; } = 0;
        public int RebirthCount { get; set; } = 0;
        public int TotalGradeLvl { get; set; } = 0;

        /// <summary>
        /// The rebirth is offered on the player info screen, beside the damage calculation (UIEvent; until 6.0 at the
        /// pub). Null when the player can be reborn now, else the text key of what is missing: off (S&amp;M), a grade above the
        /// last rebirth, in a town whose area is above the last rebirth and at least the player's grade, no sect.
        /// </summary>
        public static string Missing(WorldUnitBase player)
        {
            if (Instance == null || SMLocalConfigsEvent.Instance.Configs.NoRebirth) return "rebirth_off";
            var grade = player.GetGradeLvl();
            if (grade <= Instance.RebirthLevel) return "rebirth_need_grade";
            var town = player.GetMapBuild<MapBuildTown>();
            var area = town?.gridData.areaBaseID ?? 0;
            if (town == null || area <= Instance.RebirthLevel || area < grade) return "rebirth_need_town";
            if (!string.IsNullOrEmpty(player.data.unitData.schoolID)) return "rebirth420103100";
            return null;
        }

        /// <summary>The button label: the next rebirth ("Rebirth 1"...).</summary>
        public static string ButtonText => string.Format(GameTool.LS("rebirth_button"), (Instance?.RebirthCount ?? 0) + 1); // the destinies have names of their own

        /// <summary>The rebirth (button of the player info): what is missing, or the confirmation, then the rebirth.</summary>
        public static void Open(System.Action after = null)
        {
            var player = g.world.playerUnit;
            var title = GameTool.LS("rebirth420103102");
            var missing = Missing(player);
            if (missing != null)
            {
                var grade = player.GetGradeLvl();
                InfoDialog.Msg(title, string.Format(GameTool.LS(missing), Instance?.RebirthLevel ?? 0, System.Math.Max(grade, (Instance?.RebirthLevel ?? 0) + 1)));
                return;
            }
            InfoDialog.Msg(title, GameTool.LS("rebirth420103101") + "\n" + GameTool.LS("rebirth420103103"), MsgBoxButtonEnum.YesNo, () =>
            {
                var playerGradeLvl = player.GetGradeLvl();
                Instance.RebirthCount++;
                Instance.RebirthLevel = playerGradeLvl;
                Instance.TotalGradeLvl += playerGradeLvl;
                TaoistStats.Rebirth(playerGradeLvl);

                //reset grade & exp
                player.ResetGradeLevel();
                player.ClearExp();

                //reset items
                player.SetProperty(UnitPropertyEnum.Reputation, 0);

                after?.Invoke();

                //add luck & open drama
                Instance.AddLuck();
                DramaTool.OpenDrama(REBIRTH_DRAMA);
            });
        }

        /// <summary>The window of the "?" beside the rebirth button: the rebirths done, the conditions (met or not), the gains,
        /// the costs (the monster growth now), the 10 destinies of rebirth (name, effect; the one of the player marked).</summary>
        public static string Help(WorldUnitBase player)
        {
            string Mark(bool ok) => ok ? "[icon:daodian] " : "x ";
            var count = Instance?.RebirthCount ?? 0;
            var last = Instance?.RebirthLevel ?? 0;
            var total = Instance?.TotalGradeLvl ?? 0;
            var grade = player.GetGradeLvl();
            var town = player.GetMapBuild<MapBuildTown>();
            var area = town?.gridData.areaBaseID ?? 0;
            var gameLvl = GameHelper.GetGameLevel().Parse<int>();
            var affect = MonstStrongerEvent.Configs?.RebirthAffect ?? 0f;
            var lines = new System.Collections.Generic.List<string>
            {
                string.Format(GameTool.LS("rebirth_help_now"), count, last, total),
                string.Empty,
                "<b>" + GameTool.LS("rebirth_help_cond") + "</b>",
                Mark(Instance != null && !SMLocalConfigsEvent.Instance.Configs.NoRebirth) + GameTool.LS("rebirth_help_c_on"),
                Mark(grade > last) + string.Format(GameTool.LS("rebirth_help_c_grade"), last + 1),
                Mark(town != null && area > last && area >= grade) + string.Format(GameTool.LS("rebirth_help_c_town"), System.Math.Max(grade, last + 1)),
                Mark(string.IsNullOrEmpty(player.data.unitData.schoolID)) + GameTool.LS("rebirth_help_c_sect"),
                string.Empty,
                "<b>" + GameTool.LS("rebirth_help_gain") + "</b>",
                "+ " + GameTool.LS("rebirth_help_g1"),
                "+ " + GameTool.LS("rebirth_help_g2"),
                "+ " + string.Format(GameTool.LS("rebirth_help_g3"), total),
                string.Empty,
                "<b>" + GameTool.LS("rebirth_help_cost") + "</b>",
                "- " + GameTool.LS("rebirth_help_x1"),
                "- " + string.Format(GameTool.LS("rebirth_help_x2"), (total * affect * gameLvl * 100f).ToString("0.#"), (affect * 100f).ToString("0.#")),
                "- " + GameTool.LS("rebirth_help_x3"),
                string.Empty,
                "<b>" + GameTool.LS("rebirth_help_levels") + "</b>",
            };
            // two destinies a line (the window holds about 25 lines)
            string Level(int i) => (i == count ? "[icon:daodian] " : i < count ? "+ " : "- ") + $"{i}. {GameTool.LS("rebirth" + (LUCK_BASE_ID + i))}: {GameTool.LS("rebirth" + (LUCK_BASE_ID + i) + "desc")}";
            for (var i = 1; i <= MAX_LUCK_LVL; i += 2)
                lines.Add(i + 1 <= MAX_LUCK_LVL ? Level(i) + "        " + Level(i + 1) : Level(i));
            return TextIconHelper.ToSprites(string.Join("\n", lines));
        }

        /// <summary>Opens the window of the rebirth (the "?" beside the button).</summary>
        public static void OpenHelp()
        {
            var player = g.world.playerUnit;
            var count = Instance?.RebirthCount ?? 0;
            var last = Instance?.RebirthLevel ?? 0;
            var total = Instance?.TotalGradeLvl ?? 0;
            var grade = player.GetGradeLvl();
            var town = player.GetMapBuild<MapBuildTown>();
            var area = town?.gridData.areaBaseID ?? 0;
            var gameLvl = GameHelper.GetGameLevel().Parse<int>();
            var affect = MonstStrongerEvent.Configs?.RebirthAffect ?? 0f;
            var ready = Missing(player) == null;
            var conds = new System.Collections.Generic.List<(bool ok, string text)>
            {
                (Instance != null && !SMLocalConfigsEvent.Instance.Configs.NoRebirth, GameTool.LS("rebirth_help_c_on")),
                (grade > last, string.Format(GameTool.LS("rebirth_help_c_grade"), last + 1)),
                (town != null && area > last && area >= grade, string.Format(GameTool.LS("rebirth_help_c_town"), System.Math.Max(grade, last + 1))),
                (string.IsNullOrEmpty(player.data.unitData.schoolID), GameTool.LS("rebirth_help_c_sect")),
            };
            var gains = new[] { GameTool.LS("rebirth_help_g1"), GameTool.LS("rebirth_help_g2"), string.Format(GameTool.LS("rebirth_help_g3"), total) };
            var costs = new[] { GameTool.LS("rebirth_help_x1"), string.Format(GameTool.LS("rebirth_help_x2"), (total * affect * gameLvl * 100f).ToString("0.#"), (affect * 100f).ToString("0.#")), GameTool.LS("rebirth_help_x3") };
            InfoPanel.Open(GameTool.LS("rebirth420103102"), ui =>
            {
                var dy = InfoPanel.Dy(ui);
                var left = InfoPanel.Left(ui) + 20f;
                var width = InfoPanel.Right(ui) - InfoPanel.Left(ui) - 40f;
                var y = InfoPanel.Top(ui);
                // the status card
                var cardH = dy * 2.2f;
                InfoPanel.Box(ui, left, y, width, cardH, ready ? InfoPanel.CARD_ON : InfoPanel.CARD);
                InfoPanel.Text(ui, left + 16f, y - dy * 0.7f, InfoPanel.I("shouming") + ButtonText, InfoPanel.TITLE, 18, UnityEngine.FontStyle.Bold);
                InfoPanel.RightText(ui, left + width - 16f, y - dy * 0.7f, GameTool.LS(ready ? "rebirth_help_ready" : "rebirth_help_notready"), ready ? InfoPanel.GOOD : InfoPanel.BAD, 15, UnityEngine.FontStyle.Bold);
                InfoPanel.Text(ui, left + 16f, y - dy * 1.6f, string.Format(GameTool.LS("rebirth_help_now"), count, last, total), InfoPanel.NOTE, 13);
                y -= cardH + dy * 0.4f;

                // conditions (left), gains and costs (right)
                var half = (width - 20f) / 2f;
                var rx = left + half + 20f;
                var ly = InfoPanel.Section(ui, left, y, half, "mubiaogeshu", GameTool.LS("rebirth_help_cond"));
                foreach (var c in conds)
                    foreach (var line in InfoPanel.Wrap(c.text, 60).Select((t, i) => (t, i)))
                    {
                        InfoPanel.Text(ui, left + 10f, ly - dy * 0.5f, (line.i == 0 ? (c.ok ? InfoPanel.I("daodian") : "x  ") : "     ") + line.t, c.ok ? InfoPanel.GOOD : InfoPanel.BAD, 13, line.i == 0 ? UnityEngine.FontStyle.Bold : UnityEngine.FontStyle.Normal);
                        ly -= dy * 0.9f;
                    }
                var ry = InfoPanel.Section(ui, rx, y, half, "xingyun", GameTool.LS("rebirth_help_gain"));
                foreach (var g1 in gains)
                    foreach (var line in InfoPanel.Wrap(g1, 60).Select((t, i) => (t, i)))
                    {
                        InfoPanel.Text(ui, rx + 10f, ry - dy * 0.5f, (line.i == 0 ? "+ " : "   ") + line.t, InfoPanel.GOOD, 13);
                        ry -= dy * 0.9f;
                    }
                ry = InfoPanel.Section(ui, rx, ry - dy * 0.2f, half, "shanghaiweili", GameTool.LS("rebirth_help_cost"));
                foreach (var c1 in costs)
                    foreach (var line in InfoPanel.Wrap(c1, 60).Select((t, i) => (t, i)))
                    {
                        InfoPanel.Text(ui, rx + 10f, ry - dy * 0.5f, (line.i == 0 ? "- " : "   ") + line.t, InfoPanel.BAD, 13);
                        ry -= dy * 0.9f;
                    }
                y = System.Math.Min(ly, ry) - dy * 0.3f;

                // the destinies of rebirth: two columns, the one of the player highlighted
                y = InfoPanel.Section(ui, left, y, width, "daodian", GameTool.LS("rebirth_help_levels"));
                for (var i = 1; i <= MAX_LUCK_LVL; i++)
                {
                    var col = (i - 1) % 2;
                    var row = (i - 1) / 2;
                    var bx = left + col * (half + 20f);
                    var by = y - row * dy;
                    var on = i == count;
                    InfoPanel.Box(ui, bx, by, half, dy * 0.94f, on ? InfoPanel.CARD_ON : (row % 2 == 0 ? InfoPanel.CARD : new UnityEngine.Color(0f, 0f, 0f, 0f)));
                    var color = on ? InfoPanel.GOLD : i < count ? InfoPanel.TEXT : InfoPanel.GREY;
                    InfoPanel.Text(ui, bx + 10f, by - dy * 0.47f, (i <= count ? InfoPanel.I("daodian") : "   ") + $"{i}. {GameTool.LS("rebirth" + (LUCK_BASE_ID + i))}", color, 13, on ? UnityEngine.FontStyle.Bold : UnityEngine.FontStyle.Normal);
                    InfoPanel.RightText(ui, bx + half - 10f, by - dy * 0.47f, GameTool.LS("rebirth" + (LUCK_BASE_ID + i) + "desc"), i <= count ? InfoPanel.GOOD : InfoPanel.GREY, 13);
                }
                y -= dy * (MAX_LUCK_LVL + 1) / 2;
                InfoPanel.Buttons(ui, System.Math.Max(InfoPanel.Bottom(ui) + dy * 0.6f, y - dy * 1.0f), (GameTool.LS("dlg_ok"), null));
            }, () => Help(player));
        }

        /// <summary>
        /// Removes rebirth lucks.
        /// </summary>
        private void RemoveLuck()
        {
            for (int i = 1; i <= MAX_LUCK_LVL; i++)
            {
                var luckId = LUCK_BASE_ID + i;
                g.world.playerUnit.DelLuck(luckId);
            }
        }

        /// <summary>
        /// Gives the luck of the rebirth count.
        /// </summary>
        internal void AddLuck()
        {
            RemoveLuck();
            g.world.playerUnit.AddLuck(LUCK_BASE_ID + RebirthCount);
        }
    }
}
