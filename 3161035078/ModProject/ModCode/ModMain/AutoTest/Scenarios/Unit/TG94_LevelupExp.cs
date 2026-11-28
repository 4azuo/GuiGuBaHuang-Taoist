using ModLib.AutoTest;
using ModLib.Helper;
using MOD_nE7UL2.Const;
using MOD_nE7UL2.Mod;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Unit
{
    /// <summary>
    /// TG-94 (ConfBalance): the exp to level up follows the S&amp;M "Levelup Exp" rate of the save (value of the game x
    /// ratio of mod_configs.json x (1 + rate)); a new save copies the rate of the global S&amp;M configs (+100 %: twice the
    /// exp of the game); the global configs and the values of the save are put back.
    /// </summary>
    public class TG94_LevelupExp : IScenarioDefinition
    {
        public string Id => "TG-94";

        private static SMGlobalConfigsEvent Cfg => SMLocalConfigsEvent.Instance.Configs;

        private static ConfRoleGradeItem NextLevel => g.conf.roleGrade.GetNextGradeItem(Player.GetPhaseId());

        public Scenario Create()
        {
            return new Scenario { Id = "TG-94", Feature = "ConfBalance", Title = "Level-up exp by the S&M rate of the save (loaded save, new save +100 %)" }
                .Step("save rate", c =>
                {
                    var item = NextLevel;
                    if (!c.Assert(item != null, "the next level of the player")) return;
                    var orig = ConfBalance.OriginalLevelExp(item);
                    if (!c.Assert(orig != null, "the exp of the game is kept")) return;
                    var rate = Cfg.AddLevelupExpRate;
                    var global = SMGlobalConfigsEvent.Instance?.AddLevelupExpRate ?? 0f;
                    c.Info($"save: Levelup Exp {rate:+0.##%;-0.##%;0%} (global S&M configs: {global:+0.##%;-0.##%;0%}), ratio {ConfBalance.LevelExpRatio}");
                    c.Info($"next level {item.id}: game {orig:#,0}, now {item.exp:#,0}; player exp {Player.GetExp():#,0} / {Player.GetMaxExpCurrentPhase():#,0}");
                    c.AssertEqual(ConfBalance.Scale(orig.Value, ConfBalance.LevelExpRatio, rate), item.exp, $"exp of the next level = {orig} x {ConfBalance.LevelExpRatio} x (1 + {rate})");
                    c.AssertEqual(item.exp, Player.GetMaxExpCurrentPhase(), "the max exp of the player is the scaled value");
                    var wrong = g.conf.roleGrade._allConfList.ToArray().Count(x => x.exp != ConfBalance.Scale(ConfBalance.OriginalLevelExp(x) ?? x.exp, ConfBalance.LevelExpRatio, rate));
                    c.AssertEqual(0, wrong, "every level scaled by the rate of the save");
                })
                .Step("new save with +100 %", c =>
                {
                    var item = NextLevel;
                    var orig = ConfBalance.OriginalLevelExp(item) ?? 0;
                    var sm = SMGlobalConfigsEvent.Instance;
                    var global0 = sm.AddLevelupExpRate;
                    try
                    {
                        // the tester moves the slider to +100 % on the title screen (global configs), then creates a save:
                        // the new save reads the global file (SMLocalConfigsEvent.OnLoadClass) and loads (ConfBalance.Apply)
                        sm.AddLevelupExpRate = 1f;
                        CacheHelper.SaveGlobalCache(sm);
                        CacheHelper.FlushWrites();
                        var copy = CacheHelper.ReadGlobalCacheFile<SMGlobalConfigsEvent>(sm.ModId, ModConst.SM_GLOBAL_CONFIGS_EVENT);
                        if (!c.Assert(copy != null, "global configs read like a new save")) return;
                        c.AssertEqual(1f, copy.AddLevelupExpRate, "the new save copies Levelup Exp +100 %");
                        ConfBalance.Apply(copy);
                        ConfBalance.Apply(copy);
                        var at0 = ConfBalance.Scale(orig, ConfBalance.LevelExpRatio, 0);
                        c.Info($"next level {item.id}: rate 0 -> {at0:#,0}, +100 % -> {item.exp:#,0}");
                        c.AssertEqual(ConfBalance.Scale(orig, ConfBalance.LevelExpRatio, 1), item.exp, "+100 %: twice the exp of the game (applied twice: once)");
                        c.Assert(item.exp >= at0 * 2 - 1 && item.exp <= at0 * 2 + 1, $"{item.exp:#,0} = 2 x {at0:#,0}");
                        c.AssertEqual(item.exp, Player.GetMaxExpCurrentPhase(), "the max exp of the player follows");
                    }
                    finally
                    {
                        sm.AddLevelupExpRate = global0;
                        CacheHelper.SaveGlobalCache(sm);
                        CacheHelper.FlushWrites();
                        ConfBalance.Apply(Cfg);
                    }
                })
                .Step("player info", c => g.ui.OpenUISafe<UIPlayerInfo>(UIType.PlayerInfo), 1500)
                .Step("exp shown", c =>
                {
                    c.Assert(g.ui.HasUI(UIType.PlayerInfo), "player info open");
                    c.Info($"player exp {Player.GetExp():#,0} / {Player.GetMaxExpCurrentPhase():#,0} (rate of the save {Cfg.AddLevelupExpRate:+0.##%;-0.##%;0%})");
                    c.Snap("exp");
                }, 1000)
                .Step("restore", c =>
                {
                    CloseAll(UIType.PlayerInfo);
                    var item = NextLevel;
                    c.AssertEqual(ConfBalance.Scale(ConfBalance.OriginalLevelExp(item) ?? 0, ConfBalance.LevelExpRatio, Cfg.AddLevelupExpRate), item.exp, "back to the rate of the save");
                }, 1000);
        }
    }
}
