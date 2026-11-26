using ModLib.AutoTest;
using ModLib.Enum;
using ModLib.Helper;
using ModLib.Mod;
using MOD_nE7UL2.Mod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Battle
{
    /// <summary>
    /// TB-19 (BattleModifyEvent, real battle): two NPCs of the same realm fight each other for a while, one on each side
    /// (BattleEvent.NPCJoin), for every realm 1-10 found in the world: their own skills, items and AI, the real damage
    /// pipeline. Every hit between them is recorded (BattleModifyEvent.RecordedHits): the damage before the MP / SP / DP
    /// blocks, the part they took (never over GUARD_MAX = 80 %), the final damage against the max HP of the target, the
    /// hits at the minimum damage, the hits to kill. The table is written in the evidence folder (duels-by-realm.csv).
    /// Both NPCs cannot die (the save keeps them); the pair is removed from the battle before the next one.
    /// </summary>
    public class TB19_NpcDuelsByRealm : IScenarioDefinition
    {
        public string Id => "TB-19";

        /// <summary>Time a pair fights (ms).</summary>
        public const int FIGHT_MS = 20000;

        public static readonly string[] COLUMNS = { "realm", "a", "b", "atkA", "defA", "hpA", "atkB", "defB", "hpB", "hits", "crits",
            "avgRaw", "avgPreGuard", "avgGuardShare", "cappedShare", "avgFinal", "avgFinalHpShare", "minShare", "hitsToKill", "timeMult" };

        private class Pair { public WorldUnitBase A, B; public UnitCtrlBase CA, CB; }

        public Scenario Create()
        {
            WorldUnitBase opponent = null;
            Pair current = null;
            var rows = new List<string>();
            var s = new Scenario { Id = "TB-19", Feature = "BattleModifyEvent", Title = "NPC duels of every realm: real hits, guard cap, hits to kill", Overlay = false }
                .Step("start duel", c =>
                {
                    opponent = Npcs.Where(x => !HirePeopleEvent.IsHired(x) && !x.isDie).OrderBy(x => x.GetGradeLvl()).FirstOrDefault();
                    if (!c.Assert(opponent != null, "an NPC to start a battle")) return;
                    g.world.battle.IntoBattle(opponent, MapBuildPropertyEvent.CATCH_SNEAKY_DUNGEON_ID, battleData: new WorldBattleData { npcUnit = opponent });
                })
                .Wait("battle loaded", c => { StartBattle(); ProtectPlayer(); return opponent == null || InBattle && ModBattleEvent.PlayerUnit != null; }, 90000, 1000)
                .Wait("battle started", c => { ProtectPlayer(); return opponent == null || StartBattle(); }, 60000, 1000)
                .Step("keep the opponent", c =>
                {
                    if (!InBattle) return;
                    // the first opponent stays (the battle does not end between two pairs), it cannot die
                    opponent?.GetBattleUnit()?.AddState(UnitStateType.NotDie, float.MaxValue);
                    c.Info($"opponent {opponent?.GetName()} (realm {opponent?.GetGradeLvl()})");
                }, 500);

            for (var realm = 1; realm <= 10; realm++)
            {
                var grade = realm;
                s = s.Step($"realm {grade}: pair", c =>
                    {
                        if (!InBattle) return;
                        Remove(current);
                        current = null;
                        ProtectPlayer();
                        var used = new HashSet<string> { opponent?.GetUnitId() ?? "" };
                        var list = Npcs.Where(x => x != null && !x.isDie && x.GetGradeLvl() == grade && !HirePeopleEvent.IsHired(x) && !used.Contains(x.GetUnitId()))
                            .OrderBy(x => x.GetDynProperty(UnitDynPropertyEnum.Attack).value).ToList();
                        if (list.Count < 2) { c.Skip($"realm {grade}: {list.Count} NPC(s)"); return; }
                        var a = list[list.Count / 2]; var b = list[list.Count / 2 - 1];
                        foreach (var w in new[] { a, b })
                        {
                            w.SetProperty<int>(UnitPropertyEnum.Hp, w.GetDynProperty(UnitDynPropertyEnum.HpMax).value);
                            w.SetProperty<int>(UnitPropertyEnum.Mp, w.GetDynProperty(UnitDynPropertyEnum.MpMax).value);
                            w.SetProperty<int>(UnitPropertyEnum.Sp, w.GetDynProperty(UnitDynPropertyEnum.SpMax).value);
                        }
                        var ca = BattleEvent.NPCJoin(UnitType.PlayerNPC, a).FirstOrDefault();
                        var cb = BattleEvent.NPCJoin(UnitType.Monst, b).FirstOrDefault();
                        if (!c.Assert(ca != null && cb != null, $"realm {grade}: {a.GetName()} and {b.GetName()} in the battle")) { Remove(new Pair { CA = ca, CB = cb }); return; }
                        ca.AddState(UnitStateType.NotDie, float.MaxValue);
                        cb.AddState(UnitStateType.NotDie, float.MaxValue);
                        // face to face, next to each other (the player and the first opponent elsewhere)
                        var pos = (UnityEngine.Vector2)cb.transform.position;
                        ca.move.SetPosition(pos + new UnityEngine.Vector2(3, 0));
                        current = new Pair { A = a, B = b, CA = ca, CB = cb };
                        BattleModifyEvent.RecordedHits = new List<BattleModifyEvent.HitRecord>();
                        c.Info($"realm {grade}: {a.GetName()} (ATK {ca.data.attack.baseValue}, DEF {ca.data.defense.baseValue}, HP {ca.data.maxHP.value}) vs " +
                            $"{b.GetName()} (ATK {cb.data.attack.baseValue}, DEF {cb.data.defense.baseValue}, HP {cb.data.maxHP.value})");
                    }, FIGHT_MS)
                    .Step($"realm {grade}: hits", c =>
                    {
                        var p = current;
                        var hits = BattleModifyEvent.RecordedHits ?? new List<BattleModifyEvent.HitRecord>();
                        BattleModifyEvent.RecordedHits = null;
                        if (p == null || !InBattle) return;
                        var ida = p.A.GetUnitId(); var idb = p.B.GetUnitId();
                        var duel = hits.Where(x => (x.AtkId == ida && x.HitId == idb) || (x.AtkId == idb && x.HitId == ida)).ToList();
                        c.Snap($"realm-{grade}");
                        if (duel.Count == 0)
                        {
                            string N(string id) => id == ida ? p.A.GetName() : id == idb ? p.B.GetName() : id == Player.GetUnitId() ? "player" : id == opponent?.GetUnitId() ? "opponent" : id;
                            c.Info($"realm {grade}: no hit between them in {FIGHT_MS / 1000} s; other hits: {string.Join(", ", hits.GroupBy(x => N(x.AtkId) + " -> " + N(x.HitId)).Select(x => $"{x.Key} x{x.Count()}"))}; " +
                                $"A dead {p.CA.isDie} at {(UnityEngine.Vector2)p.CA.transform.position}, B dead {p.CB.isDie} at {(UnityEngine.Vector2)p.CB.transform.position}, player at {(UnityEngine.Vector2)(ModBattleEvent.PlayerUnit?.transform.position ?? UnityEngine.Vector3.zero)}");
                            return;
                        }
                        double Avg(Func<BattleModifyEvent.HitRecord, double> f) => duel.Average(f);
                        var avgFinal = Avg(x => x.Final);
                        var avgHp = Avg(x => x.HitHpMax);
                        var guardShare = Avg(x => x.PreGuard <= 0 ? 0 : (double)x.Guard / x.PreGuard);
                        var capped = duel.Count(x => x.PreGuard > 0 && x.Guard >= BattleModifyEvent.GuardCap(x.PreGuard) - 1) / (double)duel.Count;
                        var atMin = duel.Count(x => x.Final <= x.Min) / (double)duel.Count;
                        var toKill = avgFinal <= 0 ? 0 : Math.Ceiling(avgHp / avgFinal);
                        c.Assert(duel.All(x => x.Guard <= BattleModifyEvent.GuardCap(x.PreGuard)), $"realm {grade}: the MP / SP / DP blocks never take over {BattleModifyEvent.GUARD_MAX:P0} of a hit");
                        c.Assert(duel.All(x => x.Final >= x.Min), $"realm {grade}: never under the minimum damage");
                        c.Info($"realm {grade}: {duel.Count} hits ({duel.Count(x => x.Crit)} crit), raw {Avg(x => x.Raw):0}, before the blocks {Avg(x => x.PreGuard):0}, " +
                            $"blocks {guardShare:P0} ({capped:P0} at the cap), final {avgFinal:0} = {avgFinal / Math.Max(1, avgHp):P1} of the max HP, at the minimum {atMin:P0}, " +
                            $"about {toKill} hits to kill, time x{BattleModifyEvent.Instance.GetDamageMultiplier():0.00}");
                        rows.Add(string.Join(",", new object[] { grade, "\"" + p.A.GetName() + "\"", "\"" + p.B.GetName() + "\"",
                            p.CA.data.attack.baseValue, p.CA.data.defense.baseValue, p.CA.data.maxHP.value, p.CB.data.attack.baseValue, p.CB.data.defense.baseValue, p.CB.data.maxHP.value,
                            duel.Count, duel.Count(x => x.Crit), Avg(x => x.Raw).ToString("0"), Avg(x => x.PreGuard).ToString("0"), guardShare.ToString("0.000"), capped.ToString("0.000"),
                            avgFinal.ToString("0"), (avgFinal / Math.Max(1, avgHp)).ToString("0.0000"), atMin.ToString("0.000"), toKill, BattleModifyEvent.Instance.GetDamageMultiplier().ToString("0.000") }));
                    }, 500);
            }

            return s
                .Step("table", c =>
                {
                    Remove(current);
                    current = null;
                    BattleModifyEvent.RecordedHits = null;
                    c.Assert(rows.Count > 0, $"{rows.Count} realm(s) measured");
                    var dir = AutoTestRunner.OutDir;
                    if (!string.IsNullOrEmpty(dir))
                    {
                        File.WriteAllText(Path.Combine(dir, "duels-by-realm.csv"), string.Join(",", COLUMNS) + "\n" + string.Join("\n", rows) + "\n", Encoding.UTF8);
                        c.Info("duels-by-realm.csv written");
                    }
                }, 500)
                .Step("win", c => { if (InBattle) { opponent?.GetBattleUnit()?.DelState(UnitStateType.NotDie); EndBattle(true); } }, 1500)
                .Wait("back to the map", c => LeaveBattle(), 90000, 2000)
                .Step("map", c => c.Assert(OnMap, "back on the map"));
        }

        private static void Remove(Pair p)
        {
            if (p == null) return;
            foreach (var u in new[] { p.CA, p.CB })
            {
                if (u == null) continue;
                try { u.DelState(UnitStateType.NotDie); } catch { }
                try { ModBattleEvent.SceneBattle.unit.RemoveUnit(u); } catch { }
                try { u.Destroy(false); } catch { }
            }
        }
    }
}
