using ModLib.AutoTest;
using ModLib.Helper;
using MOD_nE7UL2.Mod;
using System;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Title
{
    /// <summary>
    /// TT-10 (CustomDestinyEvent): the destiny window of the character creation: the list sorted by quality then name, its
    /// filters, the picked ones in their column, the P they cost. Nothing is created (the screens are closed).
    /// </summary>
    public class TT10_CustomDestiny : IScenarioDefinition
    {
        public string Id => "TT-10";

        public Scenario Create()
        {
            return new Scenario { Id = "TT-10", Feature = "CustomDestinyEvent", Title = "New game: pick any destinies", Scope = ScenarioScope.Title, Overlay = false }
                .Step("character creation", c =>
                {
                    var ui = g.ui.OpenUISafe<UICreatePlayer>(UIType.CreatePlayer);
                    ui.InitData(0, GameLevelType.Common, 0);
                }, 500)
                .Wait("creation screen opened", c => g.ui.HasUI(UIType.CreatePlayer), 15000)
                .Step("stats tab", c =>
                {
                    var ui = g.ui.GetUI<UICreatePlayer>(UIType.CreatePlayer);
                    var tab = new[] { ui?.tglProperty_En, ui?.tglProperty }.FirstOrDefault(t => t != null && t.gameObject.activeInHierarchy);
                    if (tab != null) tab.isOn = true;
                }, 2000)
                .Step("notice", c =>
                {
                    var buttons = FindButtons(UIType.CreatePlayer, GameTool.LS("newgame010"));
                    if (c.Assert(buttons.Count == 1, "the button of the creation screen")) buttons[0].Item.onClick.Invoke();
                }, 1500)
                .Step("accept the notice", c =>
                {
                    if (c.Assert(g.ui.HasUI(UIType.CheckPopup), "notice about the score"))
                        ClickIn(g.ui.GetUI<UICheckPopup>(UIType.CheckPopup), "Yes", "OK", "是", "确定", "確定");
                }, 1500)
                .Step("window", c =>
                {
                    var w = CustomDestinyEvent.Window;
                    if (!c.Assert(w != null && g.ui.HasUI(UIType.TextInfoLong), "window opened")) return;
                    c.Assert(w.Items.Count > 50, $"destinies: {w.Items.Count}");
                    c.Assert(w.Items.Zip(w.Items.Skip(1), (a, b) => a.Level > b.Level || (a.Level == b.Level && string.Compare(a.Name, b.Name, StringComparison.CurrentCulture) <= 0)).All(x => x), "sorted by quality, then name");
                }, 1500)
                .Step("pick 5", c =>
                {
                    var w = CustomDestinyEvent.Window;
                    foreach (var d in w.Items.Take(5).ToList()) w.Toggle(d);
                    c.AssertEqual(5, w.Chosen.Count, "5 picked (no limit of 3)");
                }, 1500)
                .Step("filters", c =>
                {
                    var w = CustomDestinyEvent.Window;
                    w.Level = 2;
                    w.Refresh();
                    c.Assert(w.Filtered().Count > 0 && w.Filtered().All(x => x.Level == 2), $"quality 2: {w.Filtered().Count}");
                    w.Level = 0;
                    w.DescFilter = w.Items[0].Desc.Split(' ')[0];
                    c.Assert(w.Filtered().Any(x => x.Id == w.Items[0].Id), $"description '{w.DescFilter}': {w.Filtered().Count}");
                    w.DescFilter = string.Empty;
                    w.NameFilter = w.Items[3].Name;
                    w.Refresh();
                    c.Assert(w.Filtered().Any(x => x.Id == w.Items[3].Id), $"name '{w.NameFilter}': {w.Filtered().Count}");
                }, 1500)
                .Step("OK", c =>
                {
                    var w = CustomDestinyEvent.Window;
                    w.UI.UI.btnOK.onClick.Invoke();
                    c.AssertEqual(5, CustomDestinyEvent.Picked.Count, "5 destinies kept");
                    c.Info($"cost {CustomDestinyEvent.TotalPenalty(w.Items.Take(5).Select(x => x.Level))} P");
                }, 1500)
                .Step("close", c =>
                {
                    if (g.ui.HasUI(UIType.TextInfoLong)) g.ui.CloseUI(UIType.TextInfoLong);
                    if (g.ui.HasUI(UIType.CreatePlayer)) g.ui.CloseUI(UIType.CreatePlayer);
                    CustomDestinyEvent.Picked.Clear();
                }, 1500);
        }
    }
}
