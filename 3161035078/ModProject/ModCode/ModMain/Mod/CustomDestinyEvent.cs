using EGameTypeData;
using MOD_nE7UL2.Const;
using MOD_nE7UL2.Helper;
using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Helper;
using ModLib.Mod;
using ModLib.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnhollowerBaseLib;
using UnityEngine;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Character creation: a button opens a window with every born destiny of the game (sorted by quality, then name;
    /// filters by quality, name, description); any number can be picked, on top of the 3 rolled by the game. Each one
    /// lowers the S&amp;M score (P) of the new save (Penalty). Given on the first map of the new game.
    /// </summary>
    [Cache(ModConst.CUSTOM_DESTINY_EVENT, CacheType = CacheAttribute.CType.Global, WorkOn = CacheAttribute.WType.All)]
    public class CustomDestinyEvent : ModEvent
    {
        public static CustomDestinyEvent Instance { get; set; }

        /// <summary>P of one destiny: PENALTY_BASE + PENALTY_PER_LEVEL x its quality (1 white .. 6 red).</summary>
        public const int PENALTY_BASE = 2000;
        public const int PENALTY_PER_LEVEL = 1000;

        /// <summary>The destinies picked for the character being created (not saved: a new creation starts empty).</summary>
        public static List<int> Picked { get; } = new List<int>();

        /// <summary>P lost by one destiny of a quality (1 .. 6, others brought into it).</summary>
        public static int Penalty(int level) => PENALTY_BASE + PENALTY_PER_LEVEL * Math.Max(1, Math.Min(6, level));

        public static int TotalPenalty(IEnumerable<int> levels) => levels.Sum(x => Penalty(x));

        /// <summary>What a filter keeps: the quality (0: all), the text in the name, the text in the description.</summary>
        public static bool Matches(int level, string name, string desc, int wantLevel, string wantName, string wantDesc) =>
            (wantLevel == 0 || level == wantLevel) && Contains(name, wantName) && Contains(desc, wantDesc);

        private static bool Contains(string text, string part) =>
            string.IsNullOrWhiteSpace(part) || (text ?? string.Empty).IndexOf(part.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;

        public class Destiny
        {
            public int Id;
            public int Level;
            public string Name;
            public string Desc;
        }

        /// <summary>Every born destiny: best quality first, then by name.</summary>
        public static List<Destiny> All()
        {
            var rs = new List<Destiny>();
            foreach (var x in g.conf.roleCreateFeature._allConfList)
            {
                if (x.type != 1 || string.IsNullOrEmpty(x.name) || x.name == "0") continue;
                var name = GameTool.LS(x.name);
                if (string.IsNullOrEmpty(name)) continue;
                var desc = string.IsNullOrEmpty(x.tips) || x.tips == "0" ? string.Empty : GameTool.LS(x.tips) ?? string.Empty;
                rs.Add(new Destiny { Id = x.id, Level = x.level, Name = OneLine(name), Desc = OneLine(desc) });
            }
            return rs.OrderByDescending(x => x.Level).ThenBy(x => x.Name, StringComparer.CurrentCulture).ToList();
        }

        private static string OneLine(string s) =>
            System.Text.RegularExpressions.Regex.Replace(s.Replace("\\n", " ").Replace('\n', ' ').Replace('\r', ' '), "<[^>]*>", string.Empty).Trim();

        private static readonly Color[] LEVEL_COLORS =
        {
            Color.black, new Color(0.3f, 0.3f, 0.3f), new Color(0.1f, 0.45f, 0.1f), new Color(0.1f, 0.3f, 0.65f),
            new Color(0.45f, 0.15f, 0.6f), new Color(0.65f, 0.42f, 0f), new Color(0.75f, 0.1f, 0.08f),
        };

        public static Color LevelColor(int level) => level >= 1 && level <= 6 ? LEVEL_COLORS[level] : LEVEL_COLORS[0];

        private static UIItemButton button;

        private static string ButtonText => Picked.Count == 0 ? GameTool.LS("newgame010") : string.Format(GameTool.LS("newgame011"), Picked.Count,
            TotalPenalty(Picked.Select(id => g.conf.roleCreateFeature.GetItem(id)?.level ?? 1)).ToString(ModConst.FORMAT_NUMBER));

        public override void OnOpenUIEnd(OpenUIEnd e)
        {
            base.OnOpenUIEnd(e);
            if (e.uiType.uiName != UIType.CreatePlayer.uiName) return;
            Picked.Clear();
            var cover = new UICover<UICreatePlayer>(e.ui);
            // under the rolled destinies, on the Stats tab only
            button = cover.AddButton(UIHelper.GetUIScreenWidth() * 0.2f, UIHelper.GetUIScreenHeight() * -0.06f, OpenPicker, ButtonText);
            button.Size(320, 40).Format(Color.black, 15).Align(TextAnchor.MiddleCenter);
            button.SetWork(new UIItemWork { UpdateAct = ActionHelper.WTracedAction<UIItemBase>(x => x.Active(StatsTab(cover.UI))) });
            cover.IsAutoUpdate = true;
            cover.UpdateUI();
        }

        /// <summary>The Stats tab (the destinies) is shown.</summary>
        private static bool StatsTab(UICreatePlayer ui) =>
            new[] { ui.tglProperty_En, ui.tglProperty }.Any(t => t != null && t.gameObject.activeInHierarchy && t.isOn);

        /// <summary>The first destinies: the player is told about the score, and agrees.</summary>
        private static void OpenPicker()
        {
            if (Picked.Count > 0) { OpenWindow(); return; }
            g.ui.MsgBox(GameTool.LS("newgame012"), string.Format(GameTool.LS("newgame013"), Penalty(1).ToString(ModConst.FORMAT_NUMBER), Penalty(6).ToString(ModConst.FORMAT_NUMBER)),
                MsgBoxButtonEnum.YesNo, () => OpenWindow());
        }

        /// <summary>The last window opened (tests).</summary>
        public static Picker Window { get; private set; }

        public static void OpenWindow() => TaoistWindows.Open1(() => Window = new Picker());

        private static void Confirm(List<int> ids)
        {
            Picked.Clear();
            Picked.AddRange(ids);
            button?.Set(ButtonText);
        }

        /// <summary>The window: filters, the list by pages, the picked ones in their own column.</summary>
        public class Picker
        {
            public readonly UICustom1 UI;
            public readonly List<Destiny> Items = All();
            public readonly List<int> Chosen = new List<int>(Picked);
            public int Level;
            public string NameFilter = string.Empty, DescFilter = string.Empty;

            /// <summary>Longest description shown on its line, in Latin characters.</summary>
            private const int DESC_CHARS = 100;

            private readonly List<UIItemButton> rows = new List<UIItemButton>(), chosenRows = new List<UIItemButton>();
            private readonly List<UIItemText> descs = new List<UIItemText>();
            private readonly UIItemText pageText, chosenText, chosenPageText, penaltyText;
            private readonly int perPage;
            private int page, chosenPage;

            public Picker()
            {
                UI = new UICustom1(GameTool.LS("newgame012"), string.Empty, () => Confirm(Chosen), true);
                var left = UI.Columns[0];
                var w = UI.Columns[UI.LastCol] - left;
                var dy = UIHelper.GetUIDeltaY();
                float X(float share) => left + w * share;

                penaltyText = InfoSheet.LeftText(UI, X(0f), UI.Rows[2], string.Empty, InfoSheet.BAD, 15, FontStyle.Bold);
                // filters
                var fy = UI.Rows[1];
                InfoSheet.LeftText(UI, X(0f), fy, GameTool.LS("newgame014"), InfoSheet.LABEL, 14);
                var nameInput = UI.AddInput(X(0.14f), fy, string.Empty);
                InfoSheet.LeftText(UI, X(0.25f), fy, GameTool.LS("newgame015"), InfoSheet.LABEL, 14);
                var descInput = UI.AddInput(X(0.42f), fy, string.Empty);
                InfoSheet.LeftText(UI, X(0.53f), fy, GameTool.LS("newgame016"), InfoSheet.LABEL, 14);

                // the list (left), the picked ones (right)
                var first = 3;
                perPage = Math.Max(5, UI.LastRow - 2 - first);
                for (var i = 0; i < perPage; i++)
                {
                    var n = i;
                    var y = UI.Rows[first + i];
                    var b = UI.AddButton(X(0.11f), y, () => Toggle(At(Filtered(), page * perPage + n)), string.Empty);
                    b.Size(w * 0.21f, dy * 0.95f).Align(TextAnchor.MiddleCenter);
                    rows.Add(b);
                    descs.Add(InfoSheet.LeftText(UI, X(0.225f), y, string.Empty, Color.black, 13));
                    var c = UI.AddButton(X(0.87f), y, () => RemoveChosen(chosenPage * perPage + n), string.Empty);
                    c.Size(w * 0.25f, dy * 0.95f).Align(TextAnchor.MiddleCenter);
                    chosenRows.Add(c);
                }
                chosenText = UI.AddText(X(0.88f), UI.Rows[2], string.Empty).Format(InfoSheet.LABEL, 15, FontStyle.Bold);

                var py = UI.Rows[UI.LastRow - 1];
                UI.AddButton(X(0.22f), py, () => { page--; Refresh(); }, "<").Size(60, 34);
                pageText = UI.AddText(X(0.33f), py, string.Empty);
                UI.AddButton(X(0.44f), py, () => { page++; Refresh(); }, ">").Size(60, 34);
                UI.AddButton(X(0.79f), py, () => { chosenPage--; Refresh(); }, "<").Size(60, 34);
                chosenPageText = UI.AddText(X(0.88f), py, string.Empty);
                UI.AddButton(X(0.97f), py, () => { chosenPage++; Refresh(); }, ">").Size(60, 34);

                // last: its open list stands over the rows
                var levels = new[] { GameTool.LS("newgame017") }.Concat(Enumerable.Range(27, 6).Select(i => GameTool.LS("smcfgs0" + i))).ToArray();
                var levelSelect = UI.AddSelect(X(0.68f), fy, levels, 0);
                levelSelect.SetWork(new UIItemWork { ChangeAct = ActionHelper.WTracedAction<UIItemBase, object>((s, v) => { Level = v.Parse<int>(); page = 0; Refresh(); }) });
                nameInput.Item.onValueChanged.AddListener(ActionHelper.TracedUnityAction<string>(v => { NameFilter = v; page = 0; Refresh(); }));
                descInput.Item.onValueChanged.AddListener(ActionHelper.TracedUnityAction<string>(v => { DescFilter = v; page = 0; Refresh(); }));
                Refresh();
            }

            public List<Destiny> Filtered() => Items.Where(x => Matches(x.Level, x.Name, x.Desc, Level, NameFilter, DescFilter)).ToList();

            private static Destiny At(List<Destiny> list, int i) => i >= 0 && i < list.Count ? list[i] : null;

            public void Toggle(Destiny d)
            {
                if (d == null) return;
                if (!Chosen.Remove(d.Id)) Chosen.Add(d.Id);
                Refresh();
            }

            private void RemoveChosen(int i)
            {
                if (i < Chosen.Count) Chosen.RemoveAt(i);
                Refresh();
            }

            public void Refresh()
            {
                var list = Filtered();
                var pages = Math.Max(1, (list.Count + perPage - 1) / perPage);
                page = Math.Max(0, Math.Min(pages - 1, page));
                var chosenPages = Math.Max(1, (Chosen.Count + perPage - 1) / perPage);
                chosenPage = Math.Max(0, Math.Min(chosenPages - 1, chosenPage));
                for (var i = 0; i < perPage; i++)
                {
                    var d = At(list, page * perPage + i);
                    rows[i].Active(d != null);
                    descs[i].Active(d != null);
                    if (d != null)
                    {
                        var on = Chosen.Contains(d.Id);
                        rows[i].Set((on ? "√ " : string.Empty) + d.Name);
                        rows[i].Format(LevelColor(d.Level), 13, on ? FontStyle.Bold : FontStyle.Normal);
                        var lines = SMGlobalConfigsEvent.WrapText(d.Desc, DESC_CHARS);
                        descs[i].Set(lines.Count == 0 ? string.Empty : lines[0] + (lines.Count > 1 ? "…" : string.Empty));
                    }
                    var k = chosenPage * perPage + i;
                    var c = k < Chosen.Count ? Items.FirstOrDefault(x => x.Id == Chosen[k]) : null;
                    chosenRows[i].Active(c != null);
                    if (c != null)
                    {
                        chosenRows[i].Set($"× {c.Name} (-{Penalty(c.Level).ToString(ModConst.FORMAT_NUMBER)})");
                        chosenRows[i].Format(LevelColor(c.Level), 12);
                    }
                }
                pageText.Set($"{page + 1} / {pages} ({list.Count})");
                chosenPageText.Set($"{chosenPage + 1} / {chosenPages}");
                chosenText.Set(string.Format(GameTool.LS("newgame018"), Chosen.Count));
                var total = TotalPenalty(Chosen.Select(id => Items.FirstOrDefault(x => x.Id == id)?.Level ?? 1));
                var p = SMGlobalConfigsEvent.Instance?.Score ?? 0;
                penaltyText.Set(string.Format(GameTool.LS("newgame019"), p.ToString(ModConst.FORMAT_NUMBER), total.ToString(ModConst.FORMAT_NUMBER), (p - total).ToString(ModConst.FORMAT_NUMBER)));
            }
        }

        // ------------------------------------------------------------------ the new game

        /// <summary>First map of a new game: the destinies and the score (before the start bonus, SMScoreEvent.OnLoadGame).</summary>
        public override void OnLoadNewGame()
        {
            base.OnLoadNewGame();
            // an older save loaded for the first time with the mod is not the character just created
            if (Picked.Count > 0 && GameHelper.GetGameTotalMonth() <= 1) Apply(g.world.playerUnit, Picked.ToList());
            Picked.Clear();
        }

        /// <summary>Gives the destinies to a unit as born ones, and takes their P from the score of the save.</summary>
        public static void Apply(WorldUnitBase unit, List<int> ids)
        {
            var data = unit.data.unitData.propertyData;
            int penalty = 0, given = 0;
            foreach (var id in ids)
            {
                var conf = g.conf.roleCreateFeature.GetItem(id);
                if (conf == null) continue;
                if (data.bornLuck != null && data.bornLuck.Any(x => x != null && x.id == id)) continue; // rolled too: free
                try
                {
                    var luck = new DataUnit.LuckData { id = id, duration = -1, createTime = 0, objData = new DataObjectData() };
                    var old = data.bornLuck;
                    var count = old == null ? 0 : old.Length;
                    var list = new Il2CppReferenceArray<DataUnit.LuckData>(count + 1);
                    for (var i = 0; i < count; i++) list[i] = old[i];
                    list[count] = luck;
                    data.bornLuck = list;
                    unit.CreateLuck(luck)?.NewCreateLuck();
                    given++;
                    penalty += Penalty(conf.level);
                }
                catch (Exception ex) { DebugHelper.WriteLine($"CustomDestinyEvent: destiny {id}: {ex}"); }
            }
            var cfg = SMLocalConfigsEvent.Instance?.Configs;
            if (cfg != null) cfg.Score -= penalty;
            TaoistLogEvent.Log("log_custom_destiny", given, penalty);
            DebugHelper.WriteLine($"CustomDestinyEvent: {given} destinies given ({string.Join(", ", ids)}), score -{penalty} = {cfg?.Score}");
        }
    }
}
