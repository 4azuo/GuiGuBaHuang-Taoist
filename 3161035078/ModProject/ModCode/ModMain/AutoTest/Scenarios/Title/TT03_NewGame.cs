using ModLib.AutoTest;
using ModLib.Helper;
using MOD_nE7UL2.Mod;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Title
{
    /// <summary>
    /// TT-03 (UIDifficultyEvent/UICreatePlayerEvent): new game screens. The difficulty screen hides the vanilla
    /// descriptions; with an S&amp;M priority destiny level, the destinies of that level get weight 1000 and are
    /// unlocked when the character creation opens. Nothing is created (the screens are closed).
    /// </summary>
    public class TT03_NewGame : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TT-03";

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            return new Scenario { Id = "TT-03", Feature = "UIDifficultyEvent/UICreatePlayerEvent", Title = "New game: difficulty screen and priority destinies", Scope = ScenarioScope.Title, Overlay = false }
                .Step("difficulty screen", c =>
                {
                    var ui = g.ui.OpenUISafe<UIDifficulty>(UIType.Difficulty);
                    ui.InitData(0);
                })
                .Step("descriptions hidden", c =>
                {
                    var ui = g.ui.GetUI<UIDifficulty>(UIType.Difficulty);
                    if (!c.Assert(ui != null, "difficulty screen opened")) return;
                    var items = ui.itemList.ToArray();
                    c.Assert(items.Length > 0, $"difficulty items: {items.Length}");
                    c.Assert(items.All(x => !x.goRoot1.activeSelf && !x.goRoot2.activeSelf), "vanilla difficulty descriptions hidden on every item");
                    foreach (var x in items)
                    {
                        var d = x.goGroupRoot.transform.Find(UIDifficultyEvent.DESC_NAME)?.GetComponentInChildren<UnityEngine.UI.Text>();
                        c.Assert(d != null && d.gameObject.activeInHierarchy && d.text == UIDifficultyEvent.Description((int)x.gameLevel), $"Taoist description of {x.gameLevel} ({(int)x.gameLevel})");
                    }
                    // the note shared by every difficulty: once, a red line at the top right (not in every card)
                    var note = ui.transform.Find(UIDifficultyEvent.NOTE_NAME)?.GetComponent<UnityEngine.UI.Text>();
                    c.Assert(note != null && note.gameObject.activeInHierarchy && note.text == UIDifficultyEvent.Note && !string.IsNullOrEmpty(note.text), $"shared note at the top right: {note?.text}");
                    c.Assert(items.All(x => !UIDifficultyEvent.Description((int)x.gameLevel).Contains(UIDifficultyEvent.Note)), "the note is not repeated in the cards");
                    c.Snap("difficulty");
                })
                .Step("character creation", c =>
                {
                    g.ui.CloseUI(UIType.Difficulty);
                    var sm = SMGlobalConfigsEvent.Instance;
                    c.Bag["priority"] = sm.PriorityDestinyLevel;
                    sm.PriorityDestinyLevel = 2;   // test value, restored at the end
                    c.Info("S&M PriorityDestinyLevel set to 2 for the test");
                    var ui = g.ui.OpenUISafe<UICreatePlayer>(UIType.CreatePlayer);
                    ui.InitData(0, GameLevelType.Common, 0);
                }, 500)
                .Wait("creation screen opened", c => g.ui.HasUI(UIType.CreatePlayer), 15000)
                .Step("priority destinies", c =>
                {
                    var lucks = g.conf.roleCreateFeature._allConfList.ToArray().Where(x => x.type == 1 && x.level == 2).ToList();
                    c.Assert(lucks.Count > 0, $"born destinies of level 2: {lucks.Count}");
                    c.Assert(lucks.All(x => x.weight == 1000 && x.lockLuck == 0), "all have weight 1000 and are unlocked");
                }, 2000)
                .Step("close", c =>
                {
                    if (g.ui.HasUI(UIType.CreatePlayer)) g.ui.CloseUI(UIType.CreatePlayer);
                    SMGlobalConfigsEvent.Instance.PriorityDestinyLevel = (int)c.Bag["priority"];
                    c.Assert(g.ui.HasUI(UIType.Login), "back on the title screen, setting restored");
                }, 2500)
                .Step("title buttons after the creation", c =>
                {
                    // the mod's buttons copied from game buttons went blank after the character creation
                    var v = UITitleScreenEvent.VersionButton?.ButtonLabel;
                    var s = SMGlobalConfigsEvent.TitleButton?.ButtonLabel;
                    c.Assert(v != null && v.text.StartsWith("Taoist "), $"the version button: '{v?.text}'");
                    c.Assert(s != null && !string.IsNullOrEmpty(s.text), $"the S&M button: '{s?.text}'");
                });
        }
    }
}
