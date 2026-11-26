using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOD_nE7UL2.Mod;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;

namespace UnitTests.Taoist
{
    /// <summary>Formulas of the balance pass of 6.3 (monster basis, damage over time, MP block, regeneration, skills and pills, pill body).</summary>
    [TestClass]
    public class BalanceTests
    {
        private static string ModConf => TestSetup.Repo("3161035078", "ModProject", "ModConf");

        [TestMethod, Description("MonstStrongerConfig.BasisMultiplier = (1 + 0.5 x (grade - 1)) x (1 + 0.25 x (game level - 1))")]
        public void MonsterBasis_Multiplier()
        {
            var file = JObject.Parse(File.ReadAllText(Path.Combine(ModConf, MonstStrongerConfig.FILE)));
            Check.Eq(0.5f, (float)file["BasisGradeRate"], "monst_stronger_configs.json BasisGradeRate");
            Check.Eq(0.25f, (float)file["BasisGameLvlRate"], "monst_stronger_configs.json BasisGameLvlRate");
            System.Func<int, int, double> m = (gr, gl) => MonstStrongerConfig.BasisMultiplier(0.5, 0.25, gr, gl);
            Check.Eq(1d, m(1, 1), 1e-9, "grade 1, Normal");
            Check.Eq(2d, m(1, 5), 1e-9, "grade 1, Chaos");
            Check.Eq(2d, m(3, 1), 1e-9, "grade 3, Normal");
            Check.Eq(11d, m(10, 5), 1e-9, "grade 10, Chaos (was x50)");
            Check.Eq(5.5d, m(10, 1), 1e-9, "grade 10, Normal (was x10)");
            for (var gl = 1; gl <= 5; gl++)
                for (var gr = 1; gr <= 10; gr++)
                    Check.True(m(gr, gl) <= gr * gl, $"never above the old x grade x level (grade {gr}, level {gl})", m(gr, gl));
        }

        [TestMethod, Description("BattleModifyEvent.TimeBonus = (multiplier - 1) x grade / 3: nothing at the start of the battle")]
        public void TimeBonus_StartsAtZero()
        {
            for (var g = 1; g <= 10; g++)
                Check.Eq(0d, BattleModifyEvent.TimeBonus(1d, g), 1e-12, $"multiplier x1, grade {g}");
            Check.Eq(0.04d, BattleModifyEvent.TimeBonus(1.04d, 3), 1e-9, "x1.04 at grade 3: +4 %");
            Check.Eq(0.4d, BattleModifyEvent.TimeBonus(1.12d, 10), 1e-9, "x1.12 at grade 10: +40 %");
            Check.Eq(0d, BattleModifyEvent.TimeBonus(0.9d, 5), 1e-12, "never negative");
        }

        [TestMethod, Description("BattleModifyEvent.BlockMpCostOf(blocked, blockcost) = ceil(blocked / blockcost), at least 1; nothing blocked costs nothing")]
        public void MpBlockCost_ByDamageBlocked()
        {
            Check.Eq(15, BattleModifyEvent.BlockMpCostOf(60, 4f), "60 blocked, physical basis 200 (blockcost 4)");
            Check.Eq(1, BattleModifyEvent.BlockMpCostOf(1, 50f), "at least 1");
            Check.Eq(0, BattleModifyEvent.BlockMpCostOf(0, 4f), "nothing blocked");
            Check.Eq(int.MaxValue, BattleModifyEvent.BlockMpCostOf(10, 0f), "no physical basis: no block (cost too high)");
            Check.Eq(int.MaxValue, BattleModifyEvent.BlockMpCostOf(int.MaxValue, 0.0001f), "huge block clamped");
        }

        [TestMethod, Description("BattleModifyEvent.GuardCap: the MP, SP and DP blocks together take at most 80 % of a hit; GuardBlock never over what is left")]
        public void Guard_CommonCap()
        {
            Check.Eq(0.8d, BattleModifyEvent.GUARD_MAX, 1e-12, "80 %");
            Check.Eq(800, BattleModifyEvent.GuardCap(1000), "1000 damage: 800 at most");
            Check.Eq(0, BattleModifyEvent.GuardCap(-5), "no negative cap");
            Check.True(BattleModifyEvent.MpBlockCap(1000) <= BattleModifyEvent.GuardCap(1000), "the MP cap is inside the common one");
            // a hit of 1000: MP takes 600, SP would take 5000 -> only 200 left, DP nothing
            var left = BattleModifyEvent.GuardCap(1000) - BattleModifyEvent.MpBlockCap(1000);
            var sp = BattleModifyEvent.GuardBlock(5000d, left); left -= sp;
            var dp = BattleModifyEvent.GuardBlock(5000d, left);
            Check.Eq(200, sp, "SP takes what is left");
            Check.Eq(0, dp, "DP: nothing left");
            Check.Eq(200, 1000 - 600 - sp - dp, "the hit keeps 20 %");
            Check.Eq(0, BattleModifyEvent.GuardBlock(-10d, 100), "never negative");
            Check.Eq(30, BattleModifyEvent.GuardBlock(30.4d, 100), "below the cap: what it blocks");
        }

        [TestMethod, Description("BattleModifyEvent.Regen keeps the fraction: 0.44 a tick gives 2 points in 5 ticks (was 0)")]
        public void Regen_KeepsFractions()
        {
            double left = 0;
            var sum = 0;
            for (var i = 0; i < 5; i++) sum += BattleModifyEvent.Regen(ref left, 0.44);
            Check.Eq(2, sum, "5 ticks of 0.44");
            Check.Eq(0.2d, left, 1e-9, "0.2 kept");
            left = 0;
            Check.Eq(3, BattleModifyEvent.Regen(ref left, 3.5), "3.5: 3 now");
            Check.Eq(4, BattleModifyEvent.Regen(ref left, 3.5), "3.5 again: 4 (the half kept)");
            left = 0;
            Check.Eq(0, BattleModifyEvent.Regen(ref left, double.NaN), "NaN gives nothing");
            Check.Eq(0d, left, 1e-12, "NaN not kept");
        }

        [TestMethod, Description("ConfBalance: battleSkillValue kinds, value x ratio x (1 + S&M rate)")]
        public void ConfBalance_ScaleAndKinds()
        {
            Check.Eq(ConfBalance.Kind.MpCost, ConfBalance.KindOfSkillValue("1001_mpCost").Value, "_mpCost");
            Check.Eq(ConfBalance.Kind.Cd, ConfBalance.KindOfSkillValue("1001_cd").Value, "_cd");
            Check.Eq(ConfBalance.Kind.ZizhiBase, ConfBalance.KindOfSkillValue("&zizhiBase_1").Value, "&zizhiBase_");
            Check.Eq(ConfBalance.Kind.ZizhiAdd, ConfBalance.KindOfSkillValue("&zizhiAdd_1").Value, "&zizhiAdd_");
            Check.Eq(ConfBalance.Kind.DaodianBase, ConfBalance.KindOfSkillValue("&daodianBase_1").Value, "&daodianBase_");
            Check.Eq(ConfBalance.Kind.DaodianAdd, ConfBalance.KindOfSkillValue("&daodianAdd_1").Value, "&daodianAdd_");
            Check.Null(ConfBalance.KindOfSkillValue("1001_tsgj"), "power-up values are added by ModMain, not by key");
            Check.Null(ConfBalance.KindOfSkillValue(null), "null key");
            Check.Eq(7125, ConfBalance.Scale(285, 25, 0), "285 x 25 (old grade 10 basis requirement)");
            Check.Eq(3990, ConfBalance.Scale(285, 14, 0), "285 x 14 (new default)");
            Check.Eq(1995, ConfBalance.Scale(285, 14, -0.5), "S&M -50 %");
            Check.Eq(0, ConfBalance.Scale(100, 2, -2), "never below 0");
            Check.Eq(8, ConfBalance.RATIO_PREFIX.Count, "8 kinds");
        }

        [TestMethod, Description("mod_configs.json: every ratio of the 8 kinds for grades 1-10; the new defaults of MP cost, basis requirement and power-up pills")]
        public void ModConfigs_Ratios()
        {
            var b = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(ModConf, "mod_configs.json")))["BaseConfigs"];
            foreach (var p in ConfBalance.RATIO_PREFIX.Values)
                for (var i = 1; i <= 10; i++)
                    Check.True(b[p + i] != null && (float)b[p + i] > 0, $"{p}{i} > 0", b[p + i]);
            Check.Eq(14f, (float)b["SkillMpCostRatio10"], "MP cost grade 10 (was 25)");
            Check.Eq(14f, (float)b["SkillZizhiBaseRatio10"], "basis requirement grade 10 (was 25)");
            Check.Eq(55f, (float)b["PowerUpPillEfxRatio10"], "power-up pill grade 10 (was 100)");
        }

        [TestMethod, Description("S&M page 4: points of the skill sliders (+100 %: MP cost +3000, power-up pills -2000)")]
        public void SkillSliders_Score()
        {
            Check.Eq(8, SMGlobalConfigsEvent.SKILL_SLIDERS.Length, "8 sliders");
            Check.Eq(8, SMGlobalConfigsEvent.SKILL_SLIDERS.Select(x => x.kind).Distinct().Count(), "one a kind");
            var mp = SMGlobalConfigsEvent.SKILL_SLIDERS.First(x => x.kind == ConfBalance.Kind.MpCost);
            var pill = SMGlobalConfigsEvent.SKILL_SLIDERS.First(x => x.kind == ConfBalance.Kind.PowerUpPill);
            Check.Eq(3000, SMGlobalConfigsEvent.SkillSliderScore(mp.score, 1f), "MP cost +100 %");
            Check.Eq(-1500, SMGlobalConfigsEvent.SkillSliderScore(mp.score, -0.5f), "MP cost -50 %");
            Check.Eq(-2000, SMGlobalConfigsEvent.SkillSliderScore(pill.score, 1f), "power-up pills +100 % (easier)");
            Check.Eq(0, SMGlobalConfigsEvent.SkillSliderScore(pill.score, 0f), "default 0 P");
            foreach (var s in SMGlobalConfigsEvent.SKILL_SLIDERS)
                Check.True(s.min < 0 && s.max > 0, $"range of {s.kind} around 0", $"{s.min}..{s.max}");
            Check.Eq(0f, ConfBalance.Rate(null, ConfBalance.Kind.MpCost), "no save: 0");
        }

        [TestMethod, Description("Focus of a recovery potion: x (1 + 0.1 x grade)")]
        public void PotionSpCost()
        {
            Check.Eq(1.1f, MOD_nE7UL2.ModMain.PotionSpCostRatio(1), "grade 1");
            Check.Eq(1.3f, MOD_nE7UL2.ModMain.PotionSpCostRatio(3), "grade 3 (was 1.95 at level 1)");
            Check.Eq(2.0f, MOD_nE7UL2.ModMain.PotionSpCostRatio(10), "grade 10");
        }

        [TestMethod, Description("PillBodyEvent: tier 1 (awakening) at 100 points, tier n >= 2 at 50 n², capped by the realm; bonus = tiers x 10 % + alchemy ranks x 5 % (own potion) + 30 % (storyline S)")]
        public void PillBody_Tiers()
        {
            Check.Eq(100, PillBodyEvent.PointsOf(1), "tier 1 (the awakening)");
            Check.Eq(200, PillBodyEvent.PointsOf(2), "tier 2");
            Check.Eq(5000, PillBodyEvent.PointsOf(10), "tier 10");
            Check.Eq(0, PillBodyEvent.TierOf(99, 10), "99 points");
            Check.Eq(1, PillBodyEvent.TierOf(100, 10), "100 points");
            Check.Eq(10, PillBodyEvent.TierOf(int.MaxValue, 10), "max tier 10");
            Check.Eq(3, PillBodyEvent.TierOf(50000, 4), "realm 4: cap 3");
            Check.Eq(6, PillBodyEvent.TierOf(50000, 5), "realm 5: cap 6");
            Check.Eq(9, PillBodyEvent.TierOf(50000, 8), "realm 8: cap 9");
            Check.Eq(10, PillBodyEvent.TierOf(50000, 9), "realm 9: cap 10");
            Check.Eq(0.3d, PillBodyEvent.Bonus(3, 5, false, false), 1e-6, "tier 3, alchemy 5 but not own potion");
            Check.Eq(0.55d, PillBodyEvent.Bonus(3, 5, true, false), 1e-6, "tier 3, alchemy 5, own potion");
            Check.Eq(1.55d, PillBodyEvent.Bonus(10, 5, true, true), 1e-6, "everything");
            Check.Eq(300, PillBodyEvent.Extra(1000, 50000, 0.3, false), "+30 % of 1,000");
            Check.Eq(5300, PillBodyEvent.Extra(1000, 50000, 0.3, true), "+ 10 % of 50,000 max (Nine-Turn)");
            Check.Eq(0, PillBodyEvent.Extra(1000, 50000, 0, false), "no bonus");
        }

        [TestMethod, Description("6.3 tuning: MP block at most 60 % of a hit; monster growth counter^0.75 x (1 + 0.5 (g-1)) x (1 + 0.25 (l-1)); potion heal x (1 + 1.5 g + level)")]
        public void Tuning_BlockGrowthPotions()
        {
            Check.Eq(600, BattleModifyEvent.MpBlockCap(1000), "60 % of 1,000");
            Check.Eq(0, BattleModifyEvent.MpBlockCap(-5), "no negative cap");
            System.Func<int, int, int, double> grow = (counter, grade, lvl) => MonstStrongerConfig.Growth(counter, 0, 0.004, 0, grade, lvl, 0.75, 0.5, 0.25);
            Check.Eq(0d, grow(0, 10, 5), 1e-9, "no growth at the start");
            var g15 = grow(59, 10, 5);
            Check.Info($"common monster, grade 10, Chaos, counter 59 (15 years, GrowUpSpeed 3): +{g15:P0} (was +1,180 %)");
            Check.True(g15 > 0.5 && g15 < 1.5, "15 years at grade 10 Chaos: +50..150 %", g15);
            Check.True(grow(400, 10, 5) < 400 * 0.004 * 50, "100 years: far below the old linear growth", grow(400, 10, 5));
            Check.True(grow(59, 1, 1) < 59 * 0.004, "grade 1 Normal: slower than before", grow(59, 1, 1));
            Check.Eq(3.5f, MOD_nE7UL2.ModMain.PotionHealRatio(1, 1, null), "grade 1 level 1: x3.5 (was x5)");
            Check.Eq(15.5f, MOD_nE7UL2.ModMain.PotionHealRatio(9, 1, null), "grade 9 level 1: x15.5 (was x55)");
            Check.Eq(20.5f, MOD_nE7UL2.ModMain.PotionHealRatio(9, 6, null), "grade 9 level 6: x20.5 (was x60)");
            var b = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(ModConf, "mod_configs.json")))["BaseConfigs"];
            Check.Eq(1.5f, (float)b["PotionHealGradeRatio"], "mod_configs.json PotionHealGradeRatio");
        }

        [TestMethod, Description("ConstitutionEvent: born roll stable by unit id, innate tier = realm, martial kinds, tiers x scale")]
        public void Constitution_Rules()
        {
            Check.Eq(7, ConstitutionEvent.DEFS.Length, "7 constitutions");
            Check.Eq(7, ConstitutionEvent.DEFS.Select(x => x.Key).Distinct().Count(), "distinct text keys");
            var r = ConstitutionEvent.Roll("unit-123", ConstitutionEvent.Kind.InnateSpirit);
            Check.Eq(r, ConstitutionEvent.Roll("unit-123", ConstitutionEvent.Kind.InnateSpirit), "same unit, same roll");
            Check.True(r >= 0 && r < 100, "roll 0..100", r);
            var born = Enumerable.Range(0, 2000).Count(i => ConstitutionEvent.Roll("u" + i, ConstitutionEvent.Kind.InnateSpirit) < 5f);
            Check.True(born > 40 && born < 200, "about 5 % of 2,000 NPCs born with the Innate Spirit Body", born);
            var innate = ConstitutionEvent.Of(ConstitutionEvent.Kind.InnateSpirit);
            Check.Eq(7, ConstitutionEvent.TierOf(innate, 0, 7, true), "innate: tier = realm 7");
            Check.Eq(0, ConstitutionEvent.TierOf(innate, 0, 7, false), "innate, not born: 0");
            var sword = ConstitutionEvent.Of(ConstitutionEvent.Kind.SwordBone);
            Check.Eq(0, ConstitutionEvent.TierOf(sword, 1000000, 10, false), "not awakened: tier 0 whatever the points");
            Check.Eq(1, ConstitutionEvent.TierOf(sword, 0, 10, true), "awakened: at least tier 1");
            Check.Eq(1, ConstitutionEvent.TierOf(sword, 9999, 10, true), "sword bone: 9,999 hits, tier 1 (10,000 for tier 2)");
            Check.Eq(2, ConstitutionEvent.TierOf(sword, 10000, 10, true), "10,000 hits: tier 2");
            Check.Eq(10, ConstitutionEvent.TierOf(sword, 250000, 10, true), "250,000 hits: tier 10");
            Check.Eq(3, ConstitutionEvent.TierOf(sword, 10000000, 2, true), "realm 2: cap 3");
            Check.Eq(0d, ConstitutionEvent.AwakenChance(99), 1e-9, "awakening: no chance below the points of tier 1");
            Check.Eq(0.3d, ConstitutionEvent.AwakenChance(100), 1e-9, "0.3 % at the points of tier 1");
            Check.Eq(0.6d, ConstitutionEvent.AwakenChance(200), 1e-9, "0.6 % with twice the points");
            Check.Eq(0.99d, ConstitutionEvent.AwakenChance(100000), 1e-9, "at most 0.99 % (under 1 %)");
            Check.Eq(ConstitutionEvent.Kind.SwordBone, ConstitutionEvent.MartialOf("Sword").Value, "sword hits");
            Check.Eq(ConstitutionEvent.Kind.Vajra, ConstitutionEvent.MartialOf("Palm").Value, "palm hits");
            Check.Null(ConstitutionEvent.MartialOf("Fire"), "a fire spell grows none");
        }

        [TestMethod, Description("TaoistStats: the keys of the balance numbers (hit/<player|on-player>/<kind>/r<realm>, r<realm>) and the shares")]
        public void BalanceStats_Keys()
        {
            Check.Eq("player/monster/r3", MOD_nE7UL2.Helper.TaoistStats.HitKey(true, "monster", 3), "a hit of the player on a monster of realm 3");
            Check.Eq("on-player/cultivator/r10", MOD_nE7UL2.Helper.TaoistStats.HitKey(false, "cultivator", 10), "a hit of a realm 10 cultivator on the player");
            Check.Eq("r0", MOD_nE7UL2.Helper.TaoistStats.Realm(-2), "no negative realm");
            Check.Eq(25d, MOD_nE7UL2.Helper.TaoistStats.Percent(250, 1000), 1e-9, "250 of 1000: 25 %");
            Check.Eq(0d, MOD_nE7UL2.Helper.TaoistStats.Percent(5, 0), 1e-12, "no whole: 0");
        }
    }
}
