using ModLib.Enum;
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
    /// TG-89 (ClanEvent succession): the head of an NPC clan dies; at the month of the town its heir (a grown child, the
    /// strongest) takes the family house and heads the clan; the relations with the other clans and the elders go with
    /// it (the elders lose some loyalty); with no child a brother or sister is the heir; no one left: the mansion stands in
    /// ruins, a clan of the town takes it as its seat, or it is split into plots for the mortals after RuinMonths. Changes the save (a death:
    /// restored by run-autotest.ps1); estate, relations, elders, money and places restored.
    /// </summary>
    public class TG89_ClanSuccession : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TG-89";

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            MapBuildTown town = null;
            WorldUnitBase head = null, heir = null, elder = null;
            return new Scenario { Id = "TG-89", Feature = "ClanEvent", Title = "Clan succession: the heir heads the clan when the head dies", Overlay = false }
                .Step("a clan with a grown heir", c =>
                {
                    if (!TravelTo(c, FindTown(t => t.IsSmallTown()) ?? FindTown(t => true))) return;
                    town = BagTown(c);
                    c.Bag["estate"] = JsonConvert.SerializeObject(EstateEvent.Get(town));
                    c.Bag["budget"] = MapBuildPropertyEvent.GetBuildProperty(town);
                    c.Bag["bases"] = JsonConvert.SerializeObject(ClanEvent.Instance.Bases);
                    c.Bag["elders"] = JsonConvert.SerializeObject(ClanElderEvent.Instance.Elders);
                    c.Bag["gradeOver"] = ClanElderEvent.Conf.GradeOverArea;
                    head = Npcs.FirstOrDefault(x => !HirePeopleEvent.IsHired(x) && !FamilyEvent.IsFamilyOfPlayer(x) && !EstateEvent.HousesOf(x).Any()
                        && ClanEvent.Members(x).All(m => !m.IsPlayer())
                        && FamilyEvent.Family(x).Children.Select(FamilyEvent.Unit).Any(ch => ch != null && !ch.isDie && FamilyEvent.AgeYears(ch) >= ClanEvent.Conf.HeirAge && !EstateEvent.HousesOf(ch).Any()));
                    if (!c.Assert(head != null, "an NPC with a grown child")) return;
                    c.Bag["head"] = new object[] { head.GetUnitPos(), head.GetUnitMoney() };
                    head.SetUnitPos(town.GetOrigiPoint());
                    head.SetUnitMoney(int.MaxValue / 2);
                    c.AssertEqual("buy", EstateEvent.NpcHouse(head, 1, () => 0, town), $"{ClanEvent.ClanName(head)} settles ({ClanEvent.Members(head).Count} members)");
                    heir = ClanEvent.Successor(town, head.GetUnitId());
                    c.Assert(heir != null && FamilyEvent.Family(heir).Parents.Contains(head.GetUnitId()) && FamilyEvent.AgeYears(heir) >= ClanEvent.Conf.HeirAge,
                        $"the heir: a grown child, {heir?.GetName()} ({FamilyEvent.AgeYears(heir ?? head)} years, realm {heir?.GetGradeLvl()})");
                    // an elder and a feud with the player
                    ClanElderEvent.Conf.GradeOverArea = -20;
                    elder = Npcs.FirstOrDefault(x => !HirePeopleEvent.IsHired(x) && ClanElderEvent.CannotServe(town, head, x) == null);
                    if (elder != null) ClanElderEvent.Appoint(town, head, elder).Loyalty = 60;
                    ClanEvent.Instance.Bases[ClanEvent.Key(town.buildData.id, head.GetUnitId(), Player.GetUnitId())] = -55;
                })
                .Step("the head dies", c =>
                {
                    if (heir == null) return;
                    var plot = EstateEvent.HouseOf(town, head);
                    c.Bag["plot"] = plot.Index;
                    head.Die();
                    EstateEvent.MonthTown(town, 1, () => 0.99);
                    var p = EstateEvent.Get(town).Plots.First(x => x.Index == (int)c.Bag["plot"]);
                    c.AssertEqual(heir.GetUnitId(), p.OwnerId, $"the house goes to {heir.GetName()}");
                    c.Assert(p.Level > 0, "the house stands (not given to the mortals)");
                    c.Assert(ClanEvent.Heads(town).Contains(heir.GetUnitId()) || ClanEvent.Members(heir).Count < ClanEvent.Conf.MinMembers,
                        $"{ClanEvent.ClanName(heir)} heads the clan ({ClanEvent.Members(heir).Count} members)");
                    c.AssertEqual(-55, ClanEvent.Base(town.buildData.id, heir.GetUnitId(), Player.GetUnitId()), "the feud goes on with the heir");
                    c.Assert(!ClanEvent.Instance.Bases.Keys.Any(k => k.Contains(head.GetUnitId())), "no relation left on the dead head");
                    if (elder != null)
                    {
                        var e = ClanElderEvent.PostOf(elder.GetUnitId());
                        c.Assert(e != null && e.Head == heir.GetUnitId(), "the elder guards the heir's clan");
                        c.AssertEqual(60 - ClanElderEvent.Conf.SuccessionLoyalty, e?.Loyalty ?? 0, "a new head: the elder's loyalty falls");
                    }
                    c.Assert(TaoistLogEvent.Instance.Entries.Any(x => x.Key == "log_clan_succession") || Player.GetMapBuild<MapBuildTown>()?.buildData.id != town.buildData.id, "Taoist log");
                })
                .Step("a brother or sister", c =>
                {
                    if (town == null) return;
                    // an NPC with no child and a living brother or sister: the sibling heads the clan
                    var lone = Npcs.FirstOrDefault(x => !FamilyEvent.Family(x).Children.Select(FamilyEvent.Unit).Any(ch => ch != null && !ch.isDie)
                        && FamilyEvent.Family(x).Siblings.Select(FamilyEvent.Unit).Any(sb => sb != null && !sb.isDie && EstateEvent.HouseOf(town, sb) == null));
                    if (lone == null) { c.Info("no NPC without children and with a brother or sister in this save"); return; }
                    var next = ClanEvent.Successor(town, lone.GetUnitId());
                    c.Assert(next != null && FamilyEvent.Family(lone).Siblings.Contains(next.GetUnitId()), $"no child: {lone.GetName()}'s brother or sister {next?.GetName()} is the heir");
                })
                .Step("wiped out: a ruin", c =>
                {
                    if (town == null) return;
                    c.Assert(ClanEvent.Successor(town, "no-such-unit") == null, "no one left: no heir");
                    var e = EstateEvent.Get(town);
                    var plot = e.Plots.First(x => x.OwnerId == null && x.Level == 0);
                    plot.OwnerId = "no-such-unit"; plot.Level = 3;
                    EstateEvent.MonthTown(town, 1, () => 0.99);
                    c.Assert(EstateEvent.IsRuin(plot) && plot.Level == 3 && plot.RuinSince >= 0, "the mansion stands in ruins (its size kept)");
                    c.Assert(!ClanEvent.Heads(town).Contains(EstateEvent.RUIN), "the clan is gone");
                    c.Assert(ClanEvent.Ruins(town).Contains(plot), "listed as a ruin of the town");
                    c.Bag["ruin"] = plot.Index;
                    // a clan of the town with a smaller house takes it as its seat
                    var taker = ClanEvent.Heads(town).Select(FamilyEvent.Unit).FirstOrDefault(h => h != null && !h.IsPlayer() && (EstateEvent.HouseOf(town, h)?.Level ?? 0) < plot.Level);
                    if (taker != null)
                    {
                        var money = taker.GetUnitMoney();
                        taker.SetUnitMoney(int.MaxValue / 2);
                        var budget = MapBuildPropertyEvent.GetBuildProperty(town);
                        ClanEvent.YearRuins(town, () => 0);
                        c.AssertEqual(taker.GetUnitId(), plot.OwnerId, $"{ClanEvent.ClanName(taker)} took the ruin as its seat");
                        c.Assert(MapBuildPropertyEvent.GetBuildProperty(town) > budget, "the town got the price");
                        c.AssertEqual(1, EstateEvent.HousesOf(taker).Count(x => e.Plots.Contains(x)), "one house in the town: the old one left to the mortals");
                        taker.SetUnitMoney(money);
                    }
                    else c.Info("no clan with a smaller house to take the ruin");
                })
                .Step("a ruin split for the mortals", c =>
                {
                    if (town == null) return;
                    var e = EstateEvent.Get(town);
                    var plot = e.Plots.First(x => x.OwnerId == null && x.Level == 0);
                    plot.OwnerId = "no-such-unit-2"; plot.Level = 2;
                    EstateEvent.MonthTown(town, 1, () => 0.99);
                    c.Assert(EstateEvent.IsRuin(plot), "a ruin");
                    plot.RuinSince = GameHelper.GetGameTotalMonth() - ClanEvent.Conf.RuinMonths;
                    ClanEvent.YearRuins(town, () => 0.99);
                    c.Assert(plot.OwnerId == null && plot.Level == EstateEvent.DistrictOf(plot.District).MortalLevel, "after RuinMonths: split into plots for the mortals");
                })
                .Step("restore", c =>
                {
                    ClanElderEvent.Conf.GradeOverArea = c.Bag.ContainsKey("gradeOver") ? (int)c.Bag["gradeOver"] : ClanElderEvent.Conf.GradeOverArea;
                    if (town == null) return;
                    EstateEvent.Instance.Towns[town.buildData.id] = JsonConvert.DeserializeObject<EstateEvent.TownEstate>((string)c.Bag["estate"]);
                    MapBuildPropertyEvent.AddBuildProperty(town, (long)c.Bag["budget"] - MapBuildPropertyEvent.GetBuildProperty(town));
                    ClanEvent.Instance.Bases = JsonConvert.DeserializeObject<Dictionary<string, int>>((string)c.Bag["bases"]);
                    ClanElderEvent.Instance.Elders = JsonConvert.DeserializeObject<List<ClanElderEvent.Elder>>((string)c.Bag["elders"]);
                    c.AssertEqual((long)c.Bag["budget"], MapBuildPropertyEvent.GetBuildProperty(town), "budget restored");
                });
        }
    }
}
