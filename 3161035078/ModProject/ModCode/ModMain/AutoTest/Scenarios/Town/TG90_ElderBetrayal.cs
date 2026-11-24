using ModLib.AutoTest;
using ModLib.Helper;
using ModLib.Mod;
using MOD_nE7UL2.Mod;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Town
{
    /// <summary>
    /// TG-90 (ClanElderEvent, the battle of a clan war): the player's clan (a house and a spouse) is at war with an NPC
    /// clan; an elder of the player's clan (loyalty 0, a month unpaid) joins the real battle on the player's side; it does
    /// not betray while the player is healthy; with the player under half of its HP and the enemy as strong, it turns: its
    /// battle unit fights for the enemy, it leaves the clan and is hunted, the Taoist log tells it; the player wins, the
    /// turncoat does not join the losers. Changes the save (relations, hunts: restored by run-autotest.ps1); estate,
    /// relations of the clans, elders, hunts, money and places restored.
    /// </summary>
    public class TG90_ElderBetrayal : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TG-90";

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            MapBuildTown town = null;
            WorldUnitBase foe = null, elder = null;
            return new Scenario { Id = "TG-90", Feature = "ClanElderEvent", Title = "A clan elder betrays the player in the middle of a war battle", Overlay = false }
                .Step("setup", c =>
                {
                    if (!TravelTo(c, FindTown(t => t.IsSmallTown()) ?? FindTown(t => true))) return;
                    town = BagTown(c);
                    c.Bag["estate"] = JsonConvert.SerializeObject(EstateEvent.Get(town));
                    c.Bag["bases"] = JsonConvert.SerializeObject(ClanEvent.Instance.Bases);
                    c.Bag["elders"] = JsonConvert.SerializeObject(ClanElderEvent.Instance.Elders);
                    c.Bag["hunts"] = JsonConvert.SerializeObject(ClanElderEvent.Instance.Hunts);
                    // the player's clan: a house in the town and a spouse
                    if (string.IsNullOrEmpty(FamilyEvent.Spouse(Player)))
                    {
                        var mate = Npcs.FirstOrDefault(x => string.IsNullOrEmpty(FamilyEvent.Spouse(x)) && !HirePeopleEvent.IsHired(x) && FamilyEvent.IsMan(x) != FamilyEvent.IsMan(Player));
                        if (mate != null) FamilyEvent.SetMarried(Player, mate);
                    }
                    var e = EstateEvent.Get(town);
                    if (EstateEvent.HouseOf(town, Player) == null)
                    {
                        var plot = e.Plots.First(x => x.OwnerId == null && x.Level == 0);
                        plot.OwnerId = Player.GetUnitId(); plot.Level = 2;
                    }
                    if (!c.Assert(ClanEvent.Heads(town).Contains(Player.GetUnitId()), $"the player's family is a clan ({ClanEvent.Members(Player).Count} members)")) return;
                    // an NPC clan settles in the town, in a feud with the player
                    foe = Npcs.Where(x => !HirePeopleEvent.IsHired(x) && !FamilyEvent.IsFamilyOfPlayer(x) && !EstateEvent.HousesOf(x).Any())
                        .FirstOrDefault(x => ClanEvent.Members(x).Count >= 2 && ClanEvent.Members(x).All(m => !m.IsPlayer()));
                    if (!c.Assert(foe != null, "an NPC family")) return;
                    var saved = new Dictionary<string, object[]> { [foe.GetUnitId()] = new object[] { foe.GetUnitPos(), foe.GetUnitMoney() } };
                    foe.SetUnitPos(town.GetOrigiPoint());
                    foe.SetUnitMoney(int.MaxValue / 2);
                    c.AssertEqual("buy", EstateEvent.NpcHouse(foe, 1, () => 0, town), $"{ClanEvent.ClanName(foe)} settles");
                    ClanEvent.Instance.Bases[ClanEvent.Key(town.buildData.id, Player.GetUnitId(), foe.GetUnitId())] = -300;
                    // an elder of the player's clan: of no kin, loyalty 0, a month unpaid (a motive)
                    var foeIds = ClanEvent.Members(foe).Select(x => x.GetUnitId()).ToList();
                    elder = Npcs.FirstOrDefault(x => !HirePeopleEvent.IsHired(x) && !foeIds.Contains(x.GetUnitId()) && !ClanElderEvent.IsKin(Player, x)
                        && ClanElderEvent.PostOf(x.GetUnitId()) == null && !ClanEvent.Heads(town).Contains(x.GetUnitId()));
                    if (!c.Assert(elder != null, "an NPC for the elder")) return;
                    saved[elder.GetUnitId()] = new object[] { elder.GetUnitPos(), elder.GetUnitMoney() };
                    c.Bag["units"] = saved;
                    var post = ClanElderEvent.Appoint(town, Player, elder);
                    post.Loyalty = 0;
                    post.Unpaid = 1;
                    c.Info($"elder {elder.GetName()} (loyalty 0, unpaid 1), enemy {ClanEvent.ClanName(foe)}");
                    // the war (the flight roll before it is skipped: the elder stays)
                    var their = ClanEvent.Fighter(foe, true);
                    c.Bag["their"] = their.GetUnitId();
                    ClanEvent.Instance.Pending = new ClanEvent.Fight
                    {
                        TownId = town.buildData.id, War = true, Ours = Player.GetUnitId(), Theirs = foe.GetUnitId(),
                        OurFighter = Player.GetUnitId(), TheirFighter = their.GetUnitId(),
                    };
                    ClanEvent.OnAnswer(0);
                })
                .Wait("battle loaded", c => { StartBattle(); ProtectPlayer(); return InBattle && ModBattleEvent.BattleUnits.ToArray().Any(x => x.IsWorldUnit() && !x.IsPlayer()); }, 90000, 1000)
                .Wait("battle started", c => { ProtectPlayer(); return StartBattle(); }, 60000, 1000)
                .Wait("the elder joins", c => { ProtectPlayer(); return elder == null || ClanElderEvent.BattleUnitOf(elder.GetUnitId()) != null; }, 30000, 1000)
                .Step("no betrayal while the player is healthy", c =>
                {
                    if (elder == null || !InBattle) return;
                    c.Assert(ClanElderEvent.InWarBattle, "a clan war battle");
                    var unit = ClanElderEvent.BattleUnitOf(elder.GetUnitId());
                    if (!c.Assert(unit != null, $"{elder.GetName()} fights in the battle")) return;
                    c.Assert(unit.data.unitType == UnitType.PlayerNPC, "on the player's side");
                    ProtectPlayer();
                    ModBattleEvent.PlayerUnit.data.hp = ModBattleEvent.PlayerUnit.data.maxHP.value;
                    c.Bag["logs"] = TaoistLogEvent.Instance.Entries.Count;
                    ClanElderEvent.WarSeconds = ClanElderEvent.Conf.BetrayAfterSeconds;
                    ClanElderEvent.PowerTheirs = ClanElderEvent.PowerOurs;
                    c.Assert(!ClanElderEvent.TryBetray(() => 0), "the player at full HP: no betrayal");
                    c.Assert(unit.data.unitType == UnitType.PlayerNPC, "still on the player's side");
                }, 1000)
                .Step("betrayal", c =>
                {
                    if (elder == null || !InBattle) return;
                    var unit = ClanElderEvent.BattleUnitOf(elder.GetUnitId());
                    if (unit == null) return;
                    var logs = (int)c.Bag["logs"];
                    // the enemy may have hurt the player meanwhile: the second roll of the event itself may have come first
                    var already = unit.data.unitType == UnitType.Monst;
                    ModBattleEvent.PlayerUnit.data.hp = ModBattleEvent.PlayerUnit.data.maxHP.value * 2 / 5;
                    c.Assert(already || ClanElderEvent.TryBetray(() => 0), already ? "the elder already betrayed (the player under half of its HP)" : "the player at 40 % of its HP, the enemy as strong: the elder betrays");
                    c.Assert(unit.data.unitType == UnitType.Monst, "its battle unit fights for the enemy");
                    c.Assert(ClanElderEvent.PostOf(elder.GetUnitId()) == null, "it left the player's clan");
                    c.Assert(ClanElderEvent.HuntsOn(elder.GetUnitId()).Any(x => x.Head == Player.GetUnitId()), "the clan hunts the traitor");
                    c.Assert(TaoistLogEvent.Instance.Entries.Skip(logs).Any(x => x.Key == "log_clan_elder_betrayed"), "the Taoist log tells it");
                    c.Assert(DramaUI() != null, "the drama in the battle");
                    c.Assert(!ClanElderEvent.TryBetray(() => 0), "BetrayMax: one betrayal a battle");
                }, 1500)
                .Step("win", c =>
                {
                    DismissDramas();
                    ProtectPlayer();
                    if (InBattle) EndBattle(true);
                }, 1500)
                .Wait("back to the map", c => LeaveBattle(), 90000, 2000)
                .Step("after the battle", c =>
                {
                    if (elder == null) return;
                    c.Assert(OnMap, "back on the map");
                    c.Assert(!ClanElderEvent.InWarBattle, "the battle state cleared");
                    c.Assert(ClanElderEvent.PostOf(elder.GetUnitId()) == null, "the turncoat's side lost: it serves no clan");
                    c.Assert(ClanEvent.Instance.Pending == null, "the war decided");
                }, 1500)
                .Step("restore", c =>
                {
                    DismissDramas();
                    CloseAll(UIType.TextInfoLong, UIType.CheckPopup);
                    ClanEvent.Instance.Pending = null;
                    if (town == null) return;
                    EstateEvent.Instance.Towns[town.buildData.id] = JsonConvert.DeserializeObject<EstateEvent.TownEstate>((string)c.Bag["estate"]);
                    ClanEvent.Instance.Bases = JsonConvert.DeserializeObject<Dictionary<string, int>>((string)c.Bag["bases"]);
                    ClanElderEvent.Instance.Elders = JsonConvert.DeserializeObject<List<ClanElderEvent.Elder>>((string)c.Bag["elders"]);
                    ClanElderEvent.Instance.Hunts = JsonConvert.DeserializeObject<List<ClanElderEvent.Hunt>>((string)c.Bag["hunts"]);
                    if (c.Bag.ContainsKey("units"))
                        foreach (var kv in (Dictionary<string, object[]>)c.Bag["units"])
                        {
                            var u = FamilyEvent.Unit(kv.Key);
                            if (u == null || u.isDie) continue;
                            u.SetUnitPos((UnityEngine.Vector2Int)kv.Value[0]);
                            u.SetUnitMoney((int)kv.Value[1]);
                        }
                    c.Assert(ClanElderEvent.PostOf(elder?.GetUnitId() ?? "") == null, "elders and hunts restored");
                });
        }
    }
}
