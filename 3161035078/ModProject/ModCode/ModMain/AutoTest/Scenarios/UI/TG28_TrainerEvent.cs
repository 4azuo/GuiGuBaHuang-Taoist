using ModLib.AutoTest;
using MOD_nE7UL2.Const;
using MOD_nE7UL2.Mod;
using ModLib.Helper;
using ModLib.Object;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.UI
{
    /// <summary>
    /// TG-28 (TrainerEvent): every trainer page opens and a trainer action changes the player.
    /// When the trainer is disabled in the S&amp;M configs its UI is still tested with a temporary instance.
    /// </summary>
    public class TG28_TrainerEvent : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TG-28";

        private static UICustomBase Window => UIHelper.GetUICustomBase(UIType.TextInfoLong).LastOrDefault();

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            TrainerEvent trainer = null;
            var s = new Scenario { Id = "TG-28", Feature = "TrainerEvent", Title = "Trainer: all pages open and an action changes the player" }
                .Step("open page 1", c =>
                {
                    var loaded = IsLoaded(ModConst.TRAINER_EVENT);
                    c.Info(loaded ? "trainer enabled" : "trainer disabled in S&M configs: testing the window with a temporary instance");
                    trainer = loaded ? EventHelper.GetEvent<TrainerEvent>(ModConst.TRAINER_EVENT) : new TrainerEvent { Parent = ModMain.ModObj };   // pages use the ModChild ParameterStore
                    trainer.OpenTrainer(0);
                    c.Assert(g.ui.HasUI(UIType.TextInfoLong) && Window != null, "trainer window opened");
                    c.Assert((Window?.Pages.Count ?? 0) >= 3, $"pages: {Window?.Pages.Count}");
                    c.AssertEqual(0, Window?.CurrentPageIndex ?? -1, "page 1 active");
                })
                .Step("add money", c =>
                {
                    var before = Player.GetUnitMoney();
                    trainer.AddMoney();
                    c.Assert(Player.GetUnitMoney() > before, $"money {before} -> {Player.GetUnitMoney()}");
                    Window?.UpdateUI();
                });
            for (var p = 2; p <= 4; p++)
            {
                var page = p;
                s.Step($"page {page}", c =>
                {
                    if (Window == null || Window.Pages.Count < page) { c.Skip($"no page {page}"); return; }
                    Window.NextPage();
                    c.AssertEqual(page - 1, Window.CurrentPageIndex, $"page {page} active");
                    if (page > 2) c.Assert(trainer.TelePlaces.All(x => !x.Item3.IsActive()), "the teleport buttons are not shown on this page");
                }, 500);
                if (page == 2)
                    s.Step("teleport by area", c =>
                    {
                        // the areas on the left; only the places of the chosen area (the area of the player first)
                        c.Assert(trainer.TeleAreaButtons.Count > 0, $"areas: {string.Join(", ", trainer.TeleAreaButtons.Keys)}");
                        c.AssertEqual(Player.GetUnitPosAreaId(), trainer.TeleArea, "the area of the player is chosen first");
                        var other = trainer.TeleAreaButtons.Keys.FirstOrDefault(a => a != trainer.TeleArea);
                        if (other == 0) { c.Skip("one area only"); return; }
                        trainer.TeleAreaButtons[other].Item.onClick.Invoke();
                        var shown = trainer.TelePlaces.Where(x => x.Item3.IsActive()).ToList();
                        c.Assert(shown.Count > 0 && shown.All(x => x.Item1 == other), $"area {other}: {shown.Count} places shown, all of it");
                        c.Assert(trainer.TelePlaces.Where(x => x.Item1 != other).All(x => !x.Item3.IsActive()), "the places of the other areas hidden");
                        c.Bag["tele_to"] = shown[0].Item2.name;
                        shown[0].Item3.Item.onClick.Invoke();
                        c.AssertEqual(other, Player.GetUnitPosAreaId(), $"teleported to {shown[0].Item2.name} (area {other})");
                    }, 1500);
            }
            return s.Step("close", c =>
            {
                if (g.ui.HasUI(UIType.TextInfoLong)) g.ui.CloseUI(UIType.TextInfoLong);
                c.Assert(!g.ui.HasUI(UIType.TextInfoLong), "trainer window closed");
            });
        }
    }
}
