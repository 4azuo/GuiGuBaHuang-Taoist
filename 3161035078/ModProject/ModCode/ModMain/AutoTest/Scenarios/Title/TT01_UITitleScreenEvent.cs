using ModLib.AutoTest;
using MOD_nE7UL2.Const;
using MOD_nE7UL2.Enum;
using MOD_nE7UL2.Mod;
using ModLib.Enum;
using ModLib.Helper;
using ModLib.Mod;
using ModLib.Object;
using System;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Title
{
    /// <summary>
    /// TT-01 (UITitleScreenEvent): Title screen shows Taoist/ModLib buttons and versions are compatible; the panel of the
    /// other mods of the author (only those not loaded, its picture, its rotation).
    /// </summary>
    public class TT01_UITitleScreenEvent : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TT-01";

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            return new Scenario { Id = "TT-01", Feature = "UITitleScreenEvent", Title = "Title screen shows Taoist/ModLib buttons and versions are compatible", Scope = ScenarioScope.Title }
                .Step("login", c =>
                {
                    c.Info($"popups shown on the title screen (closed by the runner before the scenarios): {string.Join(", ", TitlePopups)}");
                    c.Assert(TitlePopups.Contains(UIType.TextInfoLong.uiName), "Taoist Workshop description popup was shown");
                    c.Assert(CoverCount(UIType.Login) >= 2, $"custom covers on Login: {CoverCount(UIType.Login)} (ModLib + Taoist + S&M)");
                    c.Assert(FindButtons(UIType.Login, $"Taoist {ModConst.TAOIST_VERSION}").Count == 1, $"button 'Taoist {ModConst.TAOIST_VERSION}' exists");
                    c.Assert(VersionGreaterOrEqual(ModMaster.ModObj.Version, ModConst.MODLIB_REQUIRED_VERSION), $"ModLib {ModMaster.ModObj.Version} >= required {ModConst.MODLIB_REQUIRED_VERSION} (no warning popup)");
                    c.Assert(!TitlePopups.Contains(UIType.TextInfo.uiName), "no 'ModLib too old' warning popup");
                })
                .Step("other mods: what the player has", c =>
                {
                    // the panel shows only the mods of the config not loaded in this game (this machine may have Dao of Creation)
                    var expected = TitleOtherModsConfig.Conf.Mods.Where(m => string.IsNullOrEmpty(m.ModId) || !UITitleScreenEvent.IsModLoaded(m.ModId)).Select(m => m.Name).ToList();
                    c.Info($"config: {string.Join(", ", TitleOtherModsConfig.Conf.Mods.Select(m => $"{m.Name} ({m.ModId}, loaded {UITitleScreenEvent.IsModLoaded(m.ModId)})"))}");
                    c.AssertEqual(string.Join(", ", expected), string.Join(", ", UITitleScreenEvent.OtherModsToShow().Select(m => m.Name)), "the mods to show: those not loaded");
                    c.AssertEqual(expected.Count > 0, UITitleScreenEvent.OtherModsShown, "the panel is there only when a mod is left to show");
                })
                .Step("other mods panel", c =>
                {
                    // a config with a mod always shown (no id) and a second one: the panel, its picture, the rotation
                    TitleOtherModsConfig.Set(TitleOtherModsConfig.Parse(@"{""Mods"": [
                        {""Name"": ""Dao of Creation"", ""Text"": ""ui_othermods_doc"", ""Image"": ""othermods_doc.jpg"", ""Url"": ""https://discord.gg/XKaP3EYfw"", ""Button"": ""ui_othermods_discord""},
                        {""Name"": ""Test mod"", ""Text"": ""A second mod"", ""Url"": """"}]}"));
                    c.Assert(UITitleScreenEvent.ShowOtherMods() && UITitleScreenEvent.OtherModsShown, "the panel is shown");
                    c.AssertEqual("Dao of Creation", UITitleScreenEvent.OtherModShown, "first mod");
                    var picture = UITitleScreenEvent.OtherModsPanel;
                    c.Assert(picture != null, "panel background");
                    c.Assert(UITitleScreenEvent.OtherModsButton.IsActive() && UITitleScreenEvent.OtherModsButton.Get()?.ToString() == GameTool.LS("ui_othermods_discord"), $"button '{UITitleScreenEvent.OtherModsButton.Get()}'");
                    c.Snap("other-mods");
                }, 800)
                .Step("other mods: next", c =>
                {
                    UITitleScreenEvent.ShowOtherMod(1);
                    c.AssertEqual("Test mod", UITitleScreenEvent.OtherModShown, "second mod");
                    c.Assert(!UITitleScreenEvent.OtherModsButton.IsActive(), "no button without an address");
                    UITitleScreenEvent.ShowOtherMod(2);
                    c.AssertEqual("Dao of Creation", UITitleScreenEvent.OtherModShown, "back to the first one");
                }, 800)
                .Step("other mods: restored", c =>
                {
                    TitleOtherModsConfig.Load(ModMain.ModObj?.ModId ?? "nE7UL2");
                    UITitleScreenEvent.ShowOtherMods();
                    c.AssertEqual(UITitleScreenEvent.OtherModsToShow().Count > 0, UITitleScreenEvent.OtherModsShown, "the panel of the config again");
                }, 300);
        }
    }
}
