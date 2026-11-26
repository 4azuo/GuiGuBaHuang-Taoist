using ModLib.AutoTest;
using ModLib.Helper;
using ModLib.Mod;
using MOD_nE7UL2.Mod;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Battle
{
    /// <summary>
    /// TB-05 (SpiritlockEvent, started by code): in the spiritlock circle dungeon the venerable tortoises and the
    /// circle cannot move.
    /// </summary>
    public class TB05_SpiritlockBattle : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TB-05";

        private static bool IsSpiritlockUnit(UnitCtrlBase x)
        {
            var id = x.data.TryCast<UnitDataMonst>()?.unitAttrItem?.id ?? 0;
            return SpiritlockEvent.VENERABLE_TORTOISES.Contains(id) || id == SpiritlockEvent.SPIRITLOCK_CIRCLE;
        }

        private static bool IsTortoise(UnitCtrlBase x) => SpiritlockEvent.VENERABLE_TORTOISES.Contains(x.data.TryCast<UnitDataMonst>()?.unitAttrItem?.id ?? 0);
        private static UnitCtrlBase Tortoise => ModBattleEvent.BattleMonsters.FirstOrDefault(x => IsTortoise(x) && !x.isDie);

        private static void StartTurning(AutoTestContext c, bool locked)
        {
            c.Bag["turns"] = 0; c.Bag["setDire"] = false; c.Bag["moved"] = 0f; c.Bag["lockKept"] = true; c.Bag["n"] = 0;
            c.Bag["locked"] = locked;
            var t = Tortoise;
            if (!c.Assert(t != null, "a tortoise alive")) return;
            t.move.isLockDire = locked;
            var left = t.move.isLeft;
            t.move.SetDire(!left);
            c.Bag["setDire"] = t.move.isLeft != left;
            c.Bag["left"] = t.move.isLeft;
            c.Bag["pos"] = t.transform.position;
        }

        private static bool WatchTurning(AutoTestContext c)
        {
            var t = Tortoise;
            if (t == null || !c.Bag.ContainsKey("left")) return true;
            var n = (int)c.Bag["n"] + 1;
            c.Bag["n"] = n;
            if (t.move.isLeft != (bool)c.Bag["left"]) { c.Bag["turns"] = (int)c.Bag["turns"] + 1; c.Bag["left"] = t.move.isLeft; }
            if (t.move.isLockDire != (bool)c.Bag["locked"]) c.Bag["lockKept"] = false;
            c.Bag["moved"] = System.Math.Max((float)c.Bag["moved"], UnityEngine.Vector3.Distance(t.transform.position, (UnityEngine.Vector3)c.Bag["pos"]));
            if (n % 10 == 1) // the player on the other side of it
            {
                var p = t.transform.position;
                ModBattleEvent.PlayerUnit.move.SetPosition(new UnityEngine.Vector2(p.x + (n % 20 == 1 ? -3f : 3f), p.y));
            }
            return n >= 40;
        }

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            return new Scenario { Id = "TB-05", Feature = "SpiritlockEvent", Title = "Spiritlock circle: tortoises cannot move", Overlay = false }
                .Step("enter dungeon", c =>
                {
                    var level = (CurrentTown?.gridData.areaBaseID ?? 1) * 5;
                    c.Info($"spiritlock dungeon 7007, level {level}");
                    g.world.battle.IntoBattle(new DataMap.MonstData { id = 7007, level = level });
                })
                .Wait("battle loaded", c => { StartBattle(); return InBattle; }, 90000, 1000)
                .Wait("battle started", c => StartBattle(), 60000, 1000)
                .Wait("spiritlock units", c => InBattle && ModBattleEvent.BattleMonsters.Any(IsSpiritlockUnit), 30000, 1000)
                .Step("units cannot move", c =>
                {
                    if (!InBattle) return;
                    var units = ModBattleEvent.BattleMonsters.Where(IsSpiritlockUnit).ToList();
                    c.Assert(units.Count > 0, $"{units.Count} tortoise/circle units");
                    c.Assert(units.All(x => x.data.moveSpeed.baseValue == 0), "move speed 0 for all of them");
                    foreach (var x in units.Where(IsTortoise))
                        c.Assert(x.data.hp == x.data.maxHP.value && x.move.isLockDire, $"tortoise HP {x.data.hp}/{x.data.maxHP.value}, direction locked {x.move.isLockDire}");
                }, 2000)
                // turning: first with the lock off (what the game does), then on; the game is asked to turn it both ways,
                // and the player stands on its left then right
                .Step("turning, lock off", c => StartTurning(c, false), 500)
                .Wait("watch, lock off", c => WatchTurning(c), 30000, 250)
                .Step("turning, lock on", c =>
                {
                    c.Info($"lock off: {c.Bag["turns"]} turns seen, SetDire turns it: {c.Bag["setDire"]}, moved {c.Bag["moved"]:0.###}");
                    StartTurning(c, true);
                }, 500)
                .Wait("watch, lock on", c => WatchTurning(c), 30000, 250)
                .Step("cannot turn", c =>
                {
                    c.Info($"lock on: {c.Bag["turns"]} turns seen, SetDire turns it: {c.Bag["setDire"]}, moved {c.Bag["moved"]:0.###}, lock kept {c.Bag["lockKept"]}");
                    c.AssertEqual(0, (int)c.Bag["turns"], "the tortoise does not turn");
                    c.AssertEqual(false, (bool)c.Bag["setDire"], "asked to turn, it does not");
                    c.AssertEqual(true, (bool)c.Bag["lockKept"], "its direction stays locked");
                }, 500)
                .Step("leave", c => { if (InBattle) EndBattle(true); }, 1500)
                .Wait("back to the map", c => LeaveBattle(), 90000, 2000)
                .Step("after battle", c => c.Assert(OnMap, "player back on the world map"), 2000);
        }
    }
}
