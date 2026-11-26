using MOD_nE7UL2.Object;
using ModLib.Helper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// The values of the game configs that Taoist scales by mod_configs.json AND by the S&amp;M configs of the save: the level-up
    /// exp, the skill mastery exp, the skill MP cost / cooldown / requirements (basis "zizhi", Dao points "daodian") and the effect
    /// of the power-up pills. The configs of the game are changed in place and the S&amp;M configs belong to a save, so the values of
    /// the game are kept once (Init, at the start of the game) and every value is worked out again from them (Apply): at the
    /// start (no save: the S&amp;M rates at 0) and each time a save is loaded; loading other saves never multiplies twice.
    /// Value = value of the game x ratio of mod_configs.json (by grade 1-10) x (1 + S&amp;M rate).
    /// </summary>
    public static class ConfBalance
    {
        public enum Kind { MasteryExp, MpCost, Cd, ZizhiBase, ZizhiAdd, DaodianBase, DaodianAdd, PowerUpPill }

        /// <summary>Prefix of the ratios of each kind in mod_configs.json (BaseConfigs.&lt;prefix&gt;1..10).</summary>
        public static readonly Dictionary<Kind, string> RATIO_PREFIX = new Dictionary<Kind, string>
        {
            [Kind.MasteryExp] = "SkillMasteryExpRatio", [Kind.MpCost] = "SkillMpCostRatio", [Kind.Cd] = "SkillCdRatio",
            [Kind.ZizhiBase] = "SkillZizhiBaseRatio", [Kind.ZizhiAdd] = "SkillZizhiAddRatio",
            [Kind.DaodianBase] = "SkillDaodianBaseRatio", [Kind.DaodianAdd] = "SkillDaodianAddRatio",
            [Kind.PowerUpPill] = "PowerUpPillEfxRatio",
        };

        /// <summary>S&amp;M rate of each kind (added to x1).</summary>
        public static float Rate(SMGlobalConfigsEvent c, Kind k)
        {
            if (c == null) return 0f;
            switch (k)
            {
                case Kind.MasteryExp: return c.SkillMasteryExpRate;
                case Kind.MpCost: return c.SkillMpCostRate;
                case Kind.Cd: return c.SkillCdRate;
                case Kind.ZizhiBase: return c.SkillZizhiBaseRate;
                case Kind.ZizhiAdd: return c.SkillZizhiAddRate;
                case Kind.DaodianBase: return c.SkillDaodianBaseRate;
                case Kind.DaodianAdd: return c.SkillDaodianAddRate;
                case Kind.PowerUpPill: return c.PowerUpPillEfxRate;
                default: return 0f;
            }
        }

        /// <summary>Kind of a battleSkillValue key (null: not scaled).</summary>
        public static Kind? KindOfSkillValue(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (key.EndsWith("_mpCost")) return Kind.MpCost;
            if (key.EndsWith("_cd")) return Kind.Cd;
            if (key.StartsWith("&zizhiBase_")) return Kind.ZizhiBase;
            if (key.StartsWith("&zizhiAdd_")) return Kind.ZizhiAdd;
            if (key.StartsWith("&daodianBase_")) return Kind.DaodianBase;
            if (key.StartsWith("&daodianAdd_")) return Kind.DaodianAdd;
            return null;
        }

        /// <summary>Value of the grade (1-10) from the value of the game: x ratio x (1 + rate), not below 0.</summary>
        public static int Scale(double value, double ratio, double rate) => Math.Max(0d, value * ratio * (1d + rate)).ToIntClamp();

        private class ValueRow
        {
            public ConfBattleSkillValueItem Item;
            public Kind Kind;
            public float[] Orig;
        }

        private static ModStts._BaseConfigs Settings => ModMain.ModObj?.ModSettings?.BaseConfigs;
        private static readonly List<ValueRow> values = new List<ValueRow>();
        private static readonly List<Tuple<ConfBattleSkillMasteryItem, int[]>> mastery = new List<Tuple<ConfBattleSkillMasteryItem, int[]>>();
        private static readonly List<Tuple<ConfRoleGradeItem, int>> levelExp = new List<Tuple<ConfRoleGradeItem, int>>();
        private static readonly HashSet<string> powerUpKeys = new HashSet<string>();
        private static bool initDone;

        /// <summary>The ratio of mod_configs.json of the kind at the grade (1-10; 1 when missing).</summary>
        public static float Ratio(Kind k, int grade)
        {
            var s = Settings;
            if (s == null) return 1f;
            var p = typeof(ModStts._BaseConfigs).GetProperty(RATIO_PREFIX[k] + Math.Max(1, Math.Min(10, grade)));
            return p == null ? 1f : (float)p.GetValue(s);
        }

        /// <summary>Keeps the values of the game (once, at the start: before any change of Taoist).</summary>
        public static void Init()
        {
            if (initDone) return;
            initDone = true;
            foreach (var item in g.conf.roleGrade._allConfList)
                levelExp.Add(Tuple.Create(item, item.exp));
            foreach (var item in g.conf.battleSkillMastery._allConfList)
                mastery.Add(Tuple.Create(item, new[] { item.grade1, item.grade2, item.grade3, item.grade4, item.grade5, item.grade6, item.grade7, item.grade8, item.grade9, item.grade10 }));
            foreach (var item in g.conf.battleSkillValue._allConfList)
            {
                var k = KindOfSkillValue(item.key);
                if (k != null) values.Add(new ValueRow { Item = item, Kind = k.Value, Orig = Read(item) });
            }
        }

        /// <summary>A value of a power-up pill effect (battleSkillValue "_tsgj" / "_tsfy"), found by ModMain (once each).</summary>
        public static void AddPowerUpValue(ConfBattleSkillValueItem item)
        {
            if (item == null || !powerUpKeys.Add(item.key)) return;
            values.Add(new ValueRow { Item = item, Kind = Kind.PowerUpPill, Orig = Read(item) });
        }

        /// <summary>Works out every value from the values of the game, mod_configs.json and the S&amp;M configs (null: rates at 0).</summary>
        public static void Apply(SMGlobalConfigsEvent cfg)
        {
            if (!initDone) return;
            var expRatio = Settings?.LevelExpRatio ?? 1f;
            var expRate = cfg?.AddLevelupExpRate ?? 0f;
            foreach (var x in levelExp)
                x.Item1.exp = Scale(x.Item2, expRatio, expRate);
            var r = Enumerable.Range(1, 10).Select(i => Ratio(Kind.MasteryExp, i)).ToArray();
            var rate = Rate(cfg, Kind.MasteryExp);
            foreach (var x in mastery)
            {
                var o = x.Item2; var m = x.Item1;
                m.grade1 = Scale(o[0], r[0], rate); m.grade2 = Scale(o[1], r[1], rate); m.grade3 = Scale(o[2], r[2], rate);
                m.grade4 = Scale(o[3], r[3], rate); m.grade5 = Scale(o[4], r[4], rate); m.grade6 = Scale(o[5], r[5], rate);
                m.grade7 = Scale(o[6], r[6], rate); m.grade8 = Scale(o[7], r[7], rate); m.grade9 = Scale(o[8], r[8], rate);
                m.grade10 = Scale(o[9], r[9], rate);
            }
            var ratios = RATIO_PREFIX.Keys.ToDictionary(k => k, k => Enumerable.Range(1, 10).Select(i => Ratio(k, i)).ToArray());
            foreach (var v in values)
                Write(v.Item, v.Orig, ratios[v.Kind], Rate(cfg, v.Kind));
            DebugHelper.WriteLine($"ConfBalance: {values.Count} skill/pill values, {mastery.Count} mastery rows, {levelExp.Count} levels; S&M rates " +
                string.Join(", ", RATIO_PREFIX.Keys.Select(k => $"{k} {Rate(cfg, k):+0.##;-0.##;0}")) + $", level exp {expRate:+0.##;-0.##;0}");
        }

        /// <summary>The value of the game of a skill value at the grade (tests), null when it is not scaled.</summary>
        public static float? Original(string key, int grade)
        {
            var v = values.FirstOrDefault(x => x.Item.key == key);
            return v == null ? (float?)null : v.Orig[Math.Max(1, Math.Min(10, grade)) - 1];
        }

        /// <summary>The value of the game of the skill mastery exp of a row at the grade (tests).</summary>
        public static int? OriginalMastery(ConfBattleSkillMasteryItem item, int grade)
        {
            var v = mastery.FirstOrDefault(x => x.Item1 == item);
            return v == null ? (int?)null : v.Item2[Math.Max(1, Math.Min(10, grade)) - 1];
        }

        private static float[] Read(ConfBattleSkillValueItem i) => new[] { i.value1, i.value2, i.value3, i.value4, i.value5, i.value6, i.value7, i.value8, i.value9, i.value10 }
            .Select(x => x.Parse<float>()).ToArray();

        private static void Write(ConfBattleSkillValueItem i, float[] o, float[] r, float rate)
        {
            i.value1 = Scale(o[0], r[0], rate).ToString(); i.value2 = Scale(o[1], r[1], rate).ToString();
            i.value3 = Scale(o[2], r[2], rate).ToString(); i.value4 = Scale(o[3], r[3], rate).ToString();
            i.value5 = Scale(o[4], r[4], rate).ToString(); i.value6 = Scale(o[5], r[5], rate).ToString();
            i.value7 = Scale(o[6], r[6], rate).ToString(); i.value8 = Scale(o[7], r[7], rate).ToString();
            i.value9 = Scale(o[8], r[8], rate).ToString(); i.value10 = Scale(o[9], r[9], rate).ToString();
        }
    }
}
