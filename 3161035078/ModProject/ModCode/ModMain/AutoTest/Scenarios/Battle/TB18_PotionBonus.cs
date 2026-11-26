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
    /// TB-18 (PillBodyEvent/BattleModifyEvent, NPC duel started by code): in a battle, a recovery potion of the player gets the
    /// bonus of the pill body (tiers), the Nine-Turn Golden Pill (10 % of the max HP) and the focus of storyline S back;
    /// the pill points grow by the grade of the potion. The time bonus of the damage is 0 at the start of the battle and
    /// the HP regeneration keeps its fractions. The flags of the save are put back.
    /// </summary>
    public class TB18_PotionBonus : IScenarioDefinition
    {
        public string Id => "TB-18";

        public Scenario Create()
        {
            WorldUnitBase npc = null;
            (int points, bool nine, bool heal, bool focus) saved = default;
            return new Scenario { Id = "TB-18", Feature = "PillBodyEvent/BattleModifyEvent", Title = "Potion bonus of the pill body in a battle", Overlay = false }
                .Step("start duel", c =>
                {
                    npc = Npcs.Where(x => !HirePeopleEvent.IsHired(x) && !x.isDie).OrderBy(x => x.GetGradeLvl()).FirstOrDefault();
                    if (!c.Assert(npc != null, "an NPC to fight")) return;
                    g.world.battle.IntoBattle(npc, MapBuildPropertyEvent.CATCH_SNEAKY_DUNGEON_ID, battleData: new WorldBattleData { npcUnit = npc });
                })
                .Wait("battle loaded", c => { StartBattle(); ProtectPlayer(); return npc == null || InBattle && ModBattleEvent.PlayerUnit != null; }, 90000, 1000)
                .Wait("battle started", c => { ProtectPlayer(); return npc == null || StartBattle(); }, 60000, 1000)
                .Step("time bonus", c =>
                {
                    c.Info($"battle time {ModBattleEvent.BattleStayTime:0.0} s, multiplier x{BattleModifyEvent.Instance.GetDamageMultiplier():0.000}");
                    c.Assert(BattleModifyEvent.TimeBonus(1d, Player.GetGradeLvl()) == 0d, "no time bonus at x1");
                    c.Assert(BattleModifyEvent.TimeBonus(BattleModifyEvent.Instance.GetDamageMultiplier(), Player.GetGradeLvl()) < 0.5, "a small time bonus at the start of the battle");
                }, 1000)
                .Step("potion", c =>
                {
                    if (!InBattle) { c.Assert(false, "in the battle"); return; }
                    var p = PillBodyEvent.Instance;
                    saved = (p.Points, p.NineTurn, p.KingHeal, p.KingFocus);
                    var unit = ModBattleEvent.PlayerUnit;
                    var props = g.conf.itemProps._allConfList.ToArray().Where(x => x.IsPotion() != null && PillBodyEvent.Recovery(x.IsPotion()).hp > 0)
                        .OrderBy(x => System.Math.Abs(x.IsPotion().grade - Player.GetGradeLvl())).FirstOrDefault();
                    if (!c.Assert(props != null, "a recovery potion near the realm of the player")) return;
                    var pill = props.IsPotion();
                    var (baseHp, _) = PillBodyEvent.Recovery(pill);
                    try
                    {
                        p.Points = PillBodyEvent.PointsOf(3); p.NineTurn = false; p.KingHeal = false; p.KingFocus = false;
                        ConstitutionEvent.Instance.Awaken(Player, ConstitutionEvent.Kind.Pill);
                        var tier = ConstitutionEvent.Enabled ? System.Math.Max(1, PillBodyEvent.TierOf(p.Points + pill.grade, Player.GetGradeLvl())) : 0;
                        var (alch, own) = PillBodyEvent.Alchemy(props.id);
                        unit.data.hp = 1;
                        var (addHp, _) = p.Use(unit, props, pill);
                        var expect = PillBodyEvent.Extra(baseHp, unit.data.maxHP.value, PillBodyEvent.Bonus(tier, alch, own, false), false);
                        c.AssertEqual(expect, addHp, $"tier {tier}: +{tier * 10} % of {baseHp} HP (alchemy {alch}, own {own})");
                        c.Assert(unit.data.hp > 1, $"HP raised: {unit.data.hp}");
                        c.AssertEqual(PillBodyEvent.PointsOf(3) + pill.grade, p.Points, "pill points + the grade of the potion");

                        p.NineTurn = true;
                        unit.data.hp = 1;
                        var (nine, _) = p.Use(unit, props, pill);
                        c.Assert(nine >= unit.data.maxHP.value / 10, $"Nine-Turn Golden Pill: +{nine} HP, at least 10 % of {unit.data.maxHP.value}");

                        p.NineTurn = false; p.KingFocus = true;
                        unit.data.sp = 0;
                        p.Use(unit, props, pill);
                        c.AssertEqual((pill.spCost * PillBodyEvent.KING_FOCUS).ToIntClamp(), unit.data.sp, $"storyline S: half of the focus ({pill.spCost}) back");
                    }
                    finally { p.Points = saved.points; p.NineTurn = saved.nine; p.KingHeal = saved.heal; p.KingFocus = saved.focus; }
                }, 1000)
                .Step("constitution stats", c =>
                {
                    if (!InBattle) return;
                    var ev = ConstitutionEvent.Instance;
                    var unit = ModBattleEvent.PlayerUnit;
                    var h = unit.data.TryCast<UnitDataHuman>();
                    var id = Player.GetUnitId();
                    var savedPts = ev.Points.TryGetValue(id, out var p) ? new System.Collections.Generic.Dictionary<ConstitutionEvent.Kind, int>(p) : null;
                    var savedAwake = ev.Awakened.TryGetValue(id, out var aw) ? new System.Collections.Generic.HashSet<ConstitutionEvent.Kind>(aw) : null;
                    var atk0 = h.attack.baseValue; var hp0 = h.maxHP.baseValue; var sword0 = h.basisSword.baseValue;
                    try
                    {
                        ev.Points[id] = new System.Collections.Generic.Dictionary<ConstitutionEvent.Kind, int> { [ConstitutionEvent.Kind.SwordBone] = PillBodyEvent.PointsOf(1) * 50, [ConstitutionEvent.Kind.DragonBlood] = PillBodyEvent.PointsOf(1) * 2 };
                        ev.Awaken(Player, ConstitutionEvent.Kind.SwordBone); ev.Awaken(Player, ConstitutionEvent.Kind.DragonBlood);
                        ev.OnBattleUnitInto(unit);
                        c.Info($"attack {atk0} -> {h.attack.baseValue}, max HP {hp0} -> {h.maxHP.baseValue}, sword basis {sword0} -> {h.basisSword.baseValue}");
                        c.Assert(System.Math.Abs(h.attack.baseValue - atk0 * 1.02) <= 1, "Sword Bone 1 + Dragon Blood 1: attack +2 %");
                        c.Assert(System.Math.Abs(h.maxHP.baseValue - hp0 * 1.04) <= 1, "Dragon Blood 1: max HP +4 %");
                        c.Assert(h.basisSword.baseValue >= sword0, "sword basis not lower (+5 %)");
                    }
                    finally
                    {
                        h.attack.baseValue = atk0; h.maxHP.baseValue = hp0; h.basisSword.baseValue = sword0;
                        if (savedPts == null) ev.Points.Remove(id); else ev.Points[id] = savedPts;
                        if (savedAwake == null) ev.Awakened.Remove(id); else ev.Awakened[id] = savedAwake;
                    }
                }, 500)
                .Step("regeneration", c =>
                {
                    if (!InBattle) return;
                    var w = Player;
                    var tick = BattleModifyEvent.GetHpRecoveryBase(w) + BattleModifyEvent.GetHpRecoveryPlus(w);
                    var unit = ModBattleEvent.PlayerUnit;
                    unit.data.hp = System.Math.Max(1, unit.data.maxHP.value / 2);
                    c.Bag["hp"] = unit.data.hp;
                    c.Bag["tick"] = tick;
                    c.Info($"HP recovery {tick:0.###} a tick ({tick * 5:0.##} a second)");
                }, 3000)
                .Step("regenerated", c =>
                {
                    if (!InBattle) return;
                    var tick = (double)c.Bag["tick"];
                    var gained = ModBattleEvent.PlayerUnit.data.hp - (int)c.Bag["hp"];
                    c.Info($"HP gained in ~3 s: {gained} (expected about {tick * 15:0.#})");
                    if (tick > 0 && tick < 1) c.Info(gained > 0 ? "a recovery below 1 a tick still heals (fractions kept)" : "no HP gained (hit by the opponent meanwhile?)");
                })
                .Step("win", c => { if (InBattle) EndBattle(true); }, 1500)
                .Wait("back to the map", c => LeaveBattle(), 90000, 2000)
                .Step("after battle", c => c.Assert(OnMap, "player back on the world map"), 2000);
        }
    }
}
