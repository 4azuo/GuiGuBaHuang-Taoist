using ModLib.AutoTest;
using ModLib.Enum;
using ModLib.Helper;
using MOD_nE7UL2.Mod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Battle
{
    /// <summary>
    /// TG-91 (balance by realm, over the years): the NPCs of the world are the test cultivators (their own stats, skills and
    /// items, much more varied than the test player). For each realm 1-10 three of them (the weakest, the middle and the
    /// strongest by attack) are compared now, after 5 years and after 10 more years (time skips): an example hit between
    /// two NPCs of the realm (DamageInfo pipeline: hits to kill, MP block), the regeneration a second, the MP cost of their
    /// skill against their MP, the potions of the realm (recovery against the max HP, potions until the focus is gone), the
    /// basis required by the skills of the realm against their basis, the basis multiplier of the monsters (new / old) and
    /// the growth of the monsters (counter of MonstStrongerEvent). The tables are written in the evidence folder
    /// (balance-&lt;checkpoint&gt;.csv) and in the report; the checks are sanity checks (no 0, no overflow, no compounding
    /// of the configs). Changes the save (time passes; restored by run-autotest.ps1).
    /// </summary>
    public class TG91_BalanceByRealm : IScenarioDefinition
    {
        public string Id => "TG-91";

        public const int FIRST_SKIP = 60, SECOND_SKIP = 120;
        public const int SKIP_TIMEOUT_MS = 2400000;

        public class Row
        {
            public string Checkpoint, Role, Name;
            public int Grade, Atk, Def, HpMax, MpMax, SpMax, MaxBasis, Items, Potions, Artifacts;
            public int HitDmg, HitsToKill, BlockedByMp, SkillMp, CastsOfMp;
            public double HpRegenSec, MpRegenSec;
            public int PotionHp, PotionHpMax, PotionSp; public double PotionHpShare, PotionHpMaxShare; public int PotionsOfFocus;
            public int ZizhiBase; public double MonstBasisNew, MonstBasisOld, MonstGrowth;
            public string Constitutions;
        }

        private static readonly List<Row> all = new List<Row>();

        /// <summary>The three NPCs of a realm: weakest, middle and strongest attack.</summary>
        public static List<(string role, WorldUnitBase w)> Sample(int grade)
        {
            var list = Npcs.Where(x => x != null && !x.isDie && x.GetGradeLvl() == grade)
                .OrderBy(x => x.GetDynProperty(UnitDynPropertyEnum.Attack).value).ToList();
            var rs = new List<(string, WorldUnitBase)>();
            if (list.Count == 0) return rs;
            rs.Add(("weak", list[0]));
            if (list.Count > 2) rs.Add(("mid", list[list.Count / 2]));
            if (list.Count > 1) rs.Add(("strong", list[list.Count - 1]));
            return rs;
        }

        /// <summary>MP of a cast of the attack skill of the unit (the equipped one, else its dearest one: NPCs choose theirs in the battle).</summary>
        public static int SkillMp(WorldUnitBase w)
        {
            var list = new List<DataProps.PropsData>();
            var left = w.GetMartialAttack()?.data;
            if (left != null) list.Add(left);
            else
                foreach (var m in w.data.unitData.allActionMartial)
                    if (m.Value?.data != null) list.Add(m.Value.data);
            var best = 0;
            foreach (var s in list)
            {
                try { best = Math.Max(best, s.GetSkillMpCost() + BattleModifyEvent.GetSkillAdjMpCost(w, s)); }
                catch { /* a step or an ability: no attack MP cost */ }
            }
            return best;
        }

        /// <summary>Grade of the recovery potions for a realm: the realm, else the grade below (the game has potions of grades 1, 3, 5, 7, 9).</summary>
        public static int PotionGrade(int grade)
        {
            var grades = new HashSet<int>(g.conf.itemProps._allConfList.ToArray().Select(p => p.IsPotion()).Where(p => p != null && PillBodyEvent.Recovery(p).hp > 0).Select(p => p.grade));
            for (var x = grade; x >= 1; x--) if (grades.Contains(x)) return x;
            return grade;
        }

        /// <summary>Recovery potions of the realm in the configs (PotionGrade): their mean HP recovery and focus cost.</summary>
        public static (int hp, int hpMax, int sp) Potions(int grade)
        {
            var pg = PotionGrade(grade);
            // HP potions of the grade, by item level: the lowest one (bought everywhere) and the highest one
            var pills = g.conf.itemProps._allConfList.ToArray().Where(p => p.IsPotion() != null && p.IsPotion().grade == pg && PillBodyEvent.Recovery(p.IsPotion()).hp > 0)
                .OrderBy(p => p.level).ThenBy(p => PillBodyEvent.Recovery(p.IsPotion()).hp).ToList();
            if (pills.Count == 0) return (0, 0, 0);
            return (PillBodyEvent.Recovery(pills.First().IsPotion()).hp, PillBodyEvent.Recovery(pills.Last().IsPotion()).hp, pills.First().IsPotion().spCost);
        }

        /// <summary>Mean basis required at a realm by the skills ("&amp;zizhiBase_", value of the grade).</summary>
        public static int ZizhiBase(int grade)
        {
            var v = g.conf.battleSkillValue._allConfList.ToArray().Where(x => x.key.StartsWith("&zizhiBase_"))
                .Select(x => (new[] { x.value1, x.value2, x.value3, x.value4, x.value5, x.value6, x.value7, x.value8, x.value9, x.value10 })[grade - 1].Parse<float>()).ToList();
            return v.Count == 0 ? 0 : (int)v.Average();
        }

        public static Row Measure(string checkpoint, string role, WorldUnitBase w, WorldUnitBase foe)
        {
            var grade = w.GetGradeLvl();
            var gameLvl = GameHelper.GetGameLevel().Parse<int>();
            var a = DamageInfo.FromWorld(w);
            var d = DamageInfo.FromWorld(foe ?? w);
            var steps = DamageInfo.Example(a, d, false);
            var hit = DamageInfo.Final(steps);
            var beforeMp = steps.FirstOrDefault(x => x.Item1 == "basisdef")?.Item2 ?? hit;
            var afterMp = steps.FirstOrDefault(x => x.Item1 == "mpblock")?.Item2 ?? beforeMp;
            var props = w.GetUnitProps();
            var skillMp = SkillMp(w);
            var (potHp, potHpMax, potSp) = Potions(grade);
            var cfg = MonstStrongerEvent.Configs;
            var counter = MonstStrongerEvent.Instance?.Counter ?? 0;
            return new Row
            {
                Checkpoint = checkpoint, Role = role, Name = w.GetName(), Grade = grade,
                Atk = a.Atk, Def = a.Def, HpMax = a.HpMax, MpMax = a.MpMax, SpMax = a.SpMax, MaxBasis = a.Basis.Values.DefaultIfEmpty(0).Max(),
                Items = props.Count, Potions = props.Where(x => x.propsItem?.IsPotion() != null).Sum(x => x.propsCount),
                Artifacts = w.GetEquippedArtifacts()?.Length ?? 0,
                HitDmg = hit, HitsToKill = DamageInfo.HitsToKill(hit, d.HpMax), BlockedByMp = Math.Max(0, beforeMp - afterMp),
                SkillMp = skillMp, CastsOfMp = skillMp <= 0 ? 0 : a.MpMax / skillMp,
                HpRegenSec = (BattleModifyEvent.GetHpRecoveryBase(w) + BattleModifyEvent.GetHpRecoveryPlus(w)) * 5,
                MpRegenSec = (BattleModifyEvent.GetMpRecoveryBase(w) + BattleModifyEvent.GetMpRecoveryPlus(w)) * 5,
                PotionHp = potHp, PotionHpMax = potHpMax, PotionSp = potSp, PotionHpShare = a.HpMax <= 0 ? 0 : potHp / (double)a.HpMax, PotionHpMaxShare = a.HpMax <= 0 ? 0 : potHpMax / (double)a.HpMax,
                PotionsOfFocus = potSp <= 0 ? 0 : a.SpMax / potSp,
                ZizhiBase = ZizhiBase(grade),
                MonstBasisNew = cfg.BasisMultiplier(grade, gameLvl), MonstBasisOld = grade * gameLvl,
                MonstGrowth = cfg.Growth(counter, MonstStrongerEvent.Instance?.KillCounter.TryGetValue(MonstType.Common, out var kc) == true ? kc : 0,
                    cfg.GrowRate.TryGetValue(MonstType.Common, out var gr) ? gr : 0f, cfg.KillGrowRate.TryGetValue(MonstType.Common, out var kr) ? kr : 0f, grade, gameLvl),
                Constitutions = ConstitutionEvent.Instance == null ? "" : string.Join(" ", ConstitutionEvent.DEFS.Where(k => ConstitutionEvent.Instance.Tier(w, k.Kind) > 0).Select(k => $"{k.Kind}{ConstitutionEvent.Instance.Tier(w, k.Kind)}")),
            };
        }

        private static readonly string[] COLUMNS = { "checkpoint", "role", "name", "grade", "atk", "def", "hpMax", "mpMax", "spMax", "maxBasis", "items", "potions", "artifacts",
            "hitDmg", "hitsToKill", "blockedByMp", "skillMp", "castsOfMp", "hpRegen/s", "mpRegen/s", "potionHp", "potionHp/hpMax", "potionHpTop", "potionHpTop/hpMax", "potionSp", "potionsOfFocus",
            "zizhiBase", "monstBasisNew", "monstBasisOld", "monstGrowth", "constitutions" };

        private static string Csv(Row r) => string.Join(",", new object[] { r.Checkpoint, r.Role, "\"" + r.Name + "\"", r.Grade, r.Atk, r.Def, r.HpMax, r.MpMax, r.SpMax, r.MaxBasis, r.Items, r.Potions, r.Artifacts,
            r.HitDmg, r.HitsToKill, r.BlockedByMp, r.SkillMp, r.CastsOfMp, r.HpRegenSec.ToString("0.##"), r.MpRegenSec.ToString("0.##"), r.PotionHp, r.PotionHpShare.ToString("0.###"), r.PotionHpMax, r.PotionHpMaxShare.ToString("0.###"), r.PotionSp, r.PotionsOfFocus,
            r.ZizhiBase, r.MonstBasisNew.ToString("0.##"), r.MonstBasisOld, r.MonstGrowth.ToString("0.###"), r.Constitutions });

        /// <summary>Measures every realm at a checkpoint: rows in the report and the CSV, sanity checks.</summary>
        public static void Checkpoint(AutoTestContext c, string name)
        {
            var rows = new List<Row>();
            var grades = 0;
            for (var grade = 1; grade <= 10; grade++)
            {
                var sample = Sample(grade);
                if (sample.Count == 0) { c.Info($"{name}: no NPC of realm {grade}"); continue; }
                grades++;
                var mid = sample[sample.Count / 2].w;
                foreach (var (role, w) in sample)
                    rows.Add(Measure(name, role, w, mid));
            }
            all.AddRange(rows);
            c.Info($"{name}: {GameHelper.GetGameYear()}/{GameHelper.GetGameMonth()}, {rows.Count} NPCs of {grades} realms, monster growth counter {MonstStrongerEvent.Instance?.Counter}");
            foreach (var r in rows.Where(x => x.Role == "mid" || x.Role == "weak" && rows.Count(y => y.Grade == x.Grade) == 1))
                c.Info($"realm {r.Grade} {r.Name}: atk {r.Atk:#,0} def {r.Def:#,0} HP {r.HpMax:#,0} MP {r.MpMax:#,0}, basis {r.MaxBasis:#,0}, {r.Items} items ({r.Potions} potions, {r.Artifacts} artifacts); " +
                    $"hit {r.HitDmg:#,0} = {r.HitsToKill} hits to kill (MP block -{r.BlockedByMp:#,0}); skill {r.SkillMp:#,0} MP = {r.CastsOfMp} casts; regen {r.HpRegenSec:0.#} HP/s {r.MpRegenSec:0.#} MP/s; " +
                    $"potion {r.PotionHp:#,0} HP = {r.PotionHpShare:P1} of max (top level {r.PotionHpMax:#,0} = {r.PotionHpMaxShare:P0}), {r.PotionsOfFocus} potions of focus; skill basis {r.ZizhiBase:#,0}; monster basis x{r.MonstBasisNew:0.##} (was x{r.MonstBasisOld}), growth +{r.MonstGrowth:P1}; constitutions [{r.Constitutions}]");
            c.Assert(grades >= 3, $"NPCs of {grades} realms (at least 3)");
            c.Assert(rows.All(r => r.Atk > 0 && r.HpMax > 0), "every NPC has attack and max HP");
            c.Assert(rows.All(r => r.HitDmg > 0 && r.HitsToKill >= 1), "every example hit does damage");
            c.Assert(rows.All(r => r.HitDmg < int.MaxValue / 2), "no damage overflow");
            c.Assert(rows.All(r => r.PotionHp > 0 || r.Grade > 10), $"recovery potions for every realm measured ({string.Join(", ", rows.Where(r => r.PotionHp <= 0).Select(r => r.Grade).Distinct())})");
            c.Assert(rows.All(r => r.MonstBasisNew <= r.MonstBasisOld), "monster basis never above the old x grade x level");
            try
            {
                var dir = AutoTestRunner.OutDir;
                if (!string.IsNullOrEmpty(dir))
                {
                    var file = Path.Combine(dir, $"balance-{name}.csv");
                    File.WriteAllText(file, string.Join(",", COLUMNS) + "\n" + string.Join("\n", rows.Select(Csv)) + "\n", Encoding.UTF8);
                    c.Info($"table: {file}");
                }
            }
            catch (Exception ex) { c.Info($"CSV not written: {ex.Message}"); }
        }

        /// <summary>A skill value (MP cost) to check that the configs are not multiplied twice.</summary>
        private static string ProbeKey() => g.conf.battleSkillValue._allConfList.ToArray().FirstOrDefault(x => ConfBalance.KindOfSkillValue(x.key) == ConfBalance.Kind.MpCost && x.value5.Parse<float>() > 0)?.key;
        private static string Probe(string key) { var i = g.conf.battleSkillValue.GetItem(key); return i == null ? null : $"{i.value1}/{i.value5}/{i.value10}"; }

        private static Scenario Skip(Scenario s, string label, int months) => s
            .Step(label, c =>
            {
                c.Bag["month"] = GameHelper.GetGameTotalMonth();
                TimeSkipEvent.NoInterrupt = true;
                TimeSkipEvent.Instance.SkipTime(months);
                c.Info($"skipping {months} months from {GameHelper.GetGameYear()}/{GameHelper.GetGameMonth()}");
            })
            .Wait($"{label}: done", c => !TimeSkipEvent.Instance.IsProcessing && GameHelper.GetGameTotalMonth() >= (int)c.Bag["month"] + months, SKIP_TIMEOUT_MS, 5000)
            .Step($"{label}: after", c =>
            {
                ClickButton(CONFIRM);
                TimeSkipEvent.NoInterrupt = false;
                c.Info(TimeSkipEvent.Instance.LastTiming.ToString());
                c.AssertEqual((int)c.Bag["month"] + months, GameHelper.GetGameTotalMonth(), $"{months} months passed");
            }, 3000);

        public Scenario Create()
        {
            var s = new Scenario { Id = "TG-91", Feature = "MonstStrongerEvent/BattleModifyEvent/ConfBalance/PillBodyEvent", Title = "Balance by realm with NPCs, over 15 years", Overlay = false }
                .Step("now", c =>
                {
                    all.Clear();
                    var key = ProbeKey();
                    c.Bag["probe"] = key; c.Bag["probeValue"] = Probe(key);
                    c.Info($"skill value {key}: {Probe(key)} (grades 1/5/10)");
                    Checkpoint(c, "y0");
                });
            s = Skip(s, "5 years", FIRST_SKIP);
            s = s.Step("after 5 years", c => Checkpoint(c, "y5"));
            s = Skip(s, "10 more years", SECOND_SKIP);
            return s.Step("after 15 years", c =>
            {
                Checkpoint(c, "y15");
                var key = (string)c.Bag["probe"];
                c.AssertEqual((string)c.Bag["probeValue"], Probe(key), $"skill value {key} the same after 15 years (configs not multiplied again)");
                // growth by realm: middle NPC of each realm, first and last checkpoint
                foreach (var grade in all.Select(x => x.Grade).Distinct().OrderBy(x => x))
                {
                    var a = all.FirstOrDefault(x => x.Checkpoint == "y0" && x.Grade == grade && x.Role != "strong");
                    var b = all.FirstOrDefault(x => x.Checkpoint == "y15" && x.Grade == grade && x.Role != "strong");
                    if (a == null || b == null) continue;
                    c.Info($"realm {grade} over 15 years: NPC atk {a.Atk:#,0} -> {b.Atk:#,0}, HP {a.HpMax:#,0} -> {b.HpMax:#,0}, hits to kill {a.HitsToKill} -> {b.HitsToKill}; " +
                        $"monster growth +{a.MonstGrowth:P1} -> +{b.MonstGrowth:P1}");
                }
                try
                {
                    var dir = AutoTestRunner.OutDir;
                    if (!string.IsNullOrEmpty(dir))
                        File.WriteAllText(Path.Combine(dir, "balance-all.csv"), string.Join(",", COLUMNS) + "\n" + string.Join("\n", all.Select(Csv)) + "\n", Encoding.UTF8);
                }
                catch (Exception ex) { c.Info($"CSV not written: {ex.Message}"); }
            });
        }
    }
}
