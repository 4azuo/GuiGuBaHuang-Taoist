using ModLib.Enum;
using ModLib.Helper;
using MOD_nE7UL2.Helper;
using MOD_nE7UL2.Mod;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Town
{
    /// <summary>
    /// TG-88 (ClanElderEvent): who can guard the player's clan (no kin, a realm high enough for the area, one post,
    /// room by the house); the invitation (refused, asked once a year, accepted); the Elders window; the monthly pay
    /// (paid: loyalty up; unpaid: loyalty down, the elder leaves at 0); the war council (the head's votes weigh more:
    /// 3 of 4 passes, 3 of 6 does not); a disloyal elder flees a war and is hunted, the hunter beats the deserter; an NPC
    /// clan invites the player, who accepts and guards it (its side in a fight, its salary, resign). Changes the save
    /// (a marriage, a death: restored by run-autotest.ps1); estate, relations, elders, money and places restored.
    /// </summary>
    public class TG88_ClanElders : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TG-88";

        private static Func<double> Rolls(params double[] v)
        {
            var i = 0;
            return () => i < v.Length ? v[i++] : 0.99;
        }

        /// <summary>Sets the intimacy of a unit with another (the save's own may be far below 0).</summary>
        private static void SetIntim(WorldUnitBase from, WorldUnitBase to, float value)
        {
            var r = from.data.unitData.relationData;
            FamilyEvent.ChangeIntim(r, to.GetUnitId(), value - r.GetIntim(to.GetUnitId()));
        }

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            MapBuildTown town = null;
            WorldUnitBase foe = null, elder = null;
            var conf = ClanElderEvent.Conf;
            return new Scenario { Id = "TG-88", Feature = "ClanElderEvent", Title = "Clan elders: invite, pay, war council, desertion, the player as an elder", Overlay = false }
                .Step("the player's clan", c =>
                {
                    if (!TravelTo(c, FindTown(t => t.IsSmallTown()) ?? FindTown(t => true))) return;
                    town = BagTown(c);
                    c.Bag["estate"] = JsonConvert.SerializeObject(EstateEvent.Get(town));
                    c.Bag["budget"] = MapBuildPropertyEvent.GetBuildProperty(town);
                    c.Bag["bases"] = JsonConvert.SerializeObject(ClanEvent.Instance.Bases);
                    c.Bag["elders"] = JsonConvert.SerializeObject(ClanElderEvent.Instance.Elders);
                    c.Bag["hunts"] = JsonConvert.SerializeObject(ClanElderEvent.Instance.Hunts);
                    c.Bag["asked"] = JsonConvert.SerializeObject(ClanElderEvent.Instance.Asked);
                    c.Bag["invited"] = ClanElderEvent.Instance.InvitedYear;
                    c.Bag["money"] = Player.GetUnitMoney();
                    c.Bag["rep"] = Player.GetProperty<int>(UnitPropertyEnum.Reputation);
                    c.Bag["gradeOver"] = conf.GradeOverArea;
                    ClanElderEvent.Instance.Elders.Clear();
                    ClanElderEvent.Instance.Hunts.Clear();
                    // the player: a spouse and a house in the town (a clan)
                    var spouse0 = FamilyEvent.Unit(FamilyEvent.Spouse(Player));
                    if (spouse0 == null || spouse0.isDie)
                    {
                        var mate = Npcs.FirstOrDefault(x => string.IsNullOrEmpty(FamilyEvent.Spouse(x)) && !HirePeopleEvent.IsHired(x) && FamilyEvent.IsMan(x) != FamilyEvent.IsMan(Player));
                        if (mate != null) FamilyEvent.SetMarried(Player, mate);
                    }
                    var e = EstateEvent.Get(town);
                    if (EstateEvent.HouseOf(town, Player) == null)
                    {
                        var plot = e.Plots.First(x => x.OwnerId == null && x.Level == 0);
                        plot.OwnerId = Player.GetUnitId();
                        plot.Level = 3;
                    }
                    EstateEvent.HouseOf(town, Player).Level = 3;
                    c.Assert(ClanEvent.Heads(town).Contains(Player.GetUnitId()), $"the player's family is a clan ({ClanEvent.Members(Player).Count} members)");
                    c.AssertEqual(2, ClanElderEvent.MaxElders(town, Player), "a house of level 3 keeps 2 elders");
                    // an NPC family settles: the foe
                    foe = Npcs.FirstOrDefault(x => !HirePeopleEvent.IsHired(x) && !FamilyEvent.IsFamilyOfPlayer(x) && !EstateEvent.HousesOf(x).Any()
                        && ClanEvent.Members(x).Count >= 2 && ClanEvent.Members(x).All(m => !m.IsPlayer()));
                    if (!c.Assert(foe != null, "an NPC family")) return;
                    c.Bag["foe"] = new object[] { foe.GetUnitPos(), foe.GetUnitMoney() };
                    foe.SetUnitPos(town.GetOrigiPoint());
                    foe.SetUnitMoney(int.MaxValue / 2);
                    c.AssertEqual("buy", EstateEvent.NpcHouse(foe, 1, () => 0, town), $"{ClanEvent.ClanName(foe)} settles");
                })
                .Step("who can serve", c =>
                {
                    if (foe == null) return;
                    // any realm first (the save's NPCs): the other reasons
                    conf.GradeOverArea = -20;
                    var spouse = FamilyEvent.Unit(FamilyEvent.Spouse(Player));
                    if (spouse != null) c.AssertEqual("elder_err_kin", ClanElderEvent.CannotServe(town, Player, spouse), "the spouse is kin");
                    c.AssertEqual("elder_err_head", ClanElderEvent.CannotServe(town, Player, foe), "the head of another clan of the town");
                    // a realm too low: the area asks for realm 10
                    conf.GradeOverArea = 20;
                    var weak = Npcs.FirstOrDefault(x => x.GetGradeLvl() < 10 && !ClanElderEvent.IsKin(Player, x));
                    if (weak != null) c.AssertEqual("elder_err_grade", ClanElderEvent.CannotServe(town, Player, weak), $"{weak.GetName()} (realm {weak.GetGradeLvl()}) too weak for realm 10");
                    // any realm from now on (the save's NPCs)
                    conf.GradeOverArea = -20;
                    elder = Npcs.Where(x => !HirePeopleEvent.IsHired(x) && ClanElderEvent.CannotServe(town, Player, x) == null && !ClanEvent.Members(foe).Contains(x))
                        .OrderBy(x => Math.Abs(x.GetGradeLvl() - Player.GetGradeLvl())).FirstOrDefault();
                    c.Assert(elder != null, $"an outsider can serve: {elder?.GetName()}");
                })
                .Step("invite", c =>
                {
                    if (elder == null) return;
                    ClanElderEvent.Instance.Asked.Remove(elder.GetUnitId());
                    SetIntim(elder, Player, 100);
                    // the save's reputation may be low: enough for the test
                    Player.SetProperty<int>(UnitPropertyEnum.Reputation, Math.Max(Player.GetProperty<int>(UnitPropertyEnum.Reputation), 1000));
                    c.Info($"chance {ClanElderEvent.ChanceOf(town, elder):P0}: intimacy {elder.data.unitData.relationData.GetIntim(Player.GetUnitId())}, reputation {Player.GetDynProperty(UnitDynPropertyEnum.Reputation).value} (base {Player.GetProperty<int>(UnitPropertyEnum.Reputation)}), house {ClanElderEvent.HouseLevel(town, Player)}, realms above {elder.GetGradeLvl() - Player.GetGradeLvl()}");
                    c.AssertEqual("elder_refused", ClanElderEvent.Invite(town, elder, () => 0.99), "a bad roll: declined");
                    c.AssertEqual("elder_err_asked", ClanElderEvent.Invite(town, elder, () => 0), "asked once a year");
                    ClanElderEvent.Instance.Asked.Remove(elder.GetUnitId());
                    c.AssertEqual("elder_accepted", ClanElderEvent.Invite(town, elder, () => 0), "accepted");
                    c.Assert(ClanElderEvent.IsKin(Player, elder) == false && ClanElderEvent.Of(town, Player).Any(x => x.UnitId == elder.GetUnitId()), "an elder of the player's clan");
                    c.AssertEqual("elder_err_busy", ClanElderEvent.CannotServe(town, foe, elder), "one post at most");
                    c.Assert(TaoistLogEvent.Instance.Entries.Any(x => x.Key == "log_clan_elder_join"), "Taoist log");
                    ClanElderEvent.OpenElders(town);
                })
                .Step("the Elders window", c =>
                {
                    if (elder == null) return;
                    c.Assert(g.ui.HasUI(UIType.TextInfoLong), "the window");
                    c.Assert(FindButtons(UIType.TextInfoLong, GameTool.LS("elder_dismiss")).Any(), "a Dismiss button");
                    // room: level 1 keeps one elder
                    var other = Npcs.FirstOrDefault(x => !HirePeopleEvent.IsHired(x) && x != elder && ClanElderEvent.CannotServe(town, Player, x, false) == null);
                    EstateEvent.HouseOf(town, Player).Level = 1;
                    if (other != null) c.AssertEqual("elder_err_full", ClanElderEvent.CannotServe(town, Player, other), "a small house: no room");
                    EstateEvent.HouseOf(town, Player).Level = 3;
                    CloseAll(UIType.TextInfoLong);
                }, 1500)
                .Step("pay", c =>
                {
                    if (elder == null) return;
                    var e = ClanElderEvent.PostOf(elder.GetUnitId()) ?? ClanElderEvent.Appoint(town, Player, elder);
                    var salary = ClanElderEvent.Salary(elder);
                    e.Loyalty = 50;
                    Player.SetUnitMoney(salary * 3);
                    var em = elder.GetUnitMoney();
                    ClanElderEvent.PayMonth();
                    c.AssertEqual(salary * 2, Player.GetUnitMoney(), $"the player paid {salary}");
                    c.AssertEqual(em + salary, elder.GetUnitMoney(), "the elder got it");
                    c.AssertEqual(50 + conf.PaidLoyalty, e.Loyalty, "paid: loyalty up");
                    Player.SetUnitMoney(0);
                    ClanElderEvent.PayMonth();
                    c.AssertEqual(50 + conf.PaidLoyalty - conf.UnpaidLoyalty, e.Loyalty, "unpaid: loyalty down");
                    e.Loyalty = 5;
                    ClanElderEvent.PayMonth();
                    c.Assert(ClanElderEvent.PostOf(elder.GetUnitId()) == null, "unpaid at loyalty 0: the elder leaves");
                    Player.SetUnitMoney(int.MaxValue / 4);
                })
                .Step("war council", c =>
                {
                    if (elder == null) return;
                    var e = ClanElderEvent.PostOf(elder.GetUnitId()) ?? ClanElderEvent.Appoint(town, Player, elder);
                    e.Loyalty = 100;
                    var ballots = ClanElderEvent.Vote(town, Player, foe, true, () => 0.99, out var passed);
                    c.AssertEqual(2, ballots.Count, "the head and one elder vote");
                    c.Assert(ballots[0].IsHead && ballots[0].Votes == conf.HeadVotes, $"the head has {conf.HeadVotes} votes");
                    c.Assert(passed, "the head and an elder against: 3 of 4, declared");
                    // two more elders (above the room, for the count): all three against -> 3 of 6, voted down
                    var more = Npcs.Where(x => !HirePeopleEvent.IsHired(x) && x != elder && ClanElderEvent.CannotServe(town, Player, x, false) == null && !ClanEvent.Members(foe).Contains(x)).Take(2).ToList();
                    foreach (var m in more)
                        ClanElderEvent.Instance.Elders.Add(new ClanElderEvent.Elder { TownId = town.buildData.id, Head = Player.GetUnitId(), UnitId = m.GetUnitId(), Loyalty = 50 });
                    if (more.Count == 2)
                    {
                        ClanElderEvent.Vote(town, Player, foe, true, () => 0.99, out passed);
                        c.Assert(!passed, "the head for, three elders against: 3 of 6, voted down");
                        ClanElderEvent.Vote(town, Player, foe, true, Rolls(0, 0.99, 0.99), out passed);
                        c.Assert(passed, "one elder for: 4 of 6, declared");
                    }
                    else c.Info($"elders for the count: {more.Count}");
                    ClanElderEvent.ShowBallots(town, Player, foe, ballots, true, () => { });
                    foreach (var m in more) ClanElderEvent.Instance.Elders.RemoveAll(x => x.UnitId == m.GetUnitId());
                }, 1500)
                .Step("the ballots", c =>
                {
                    if (elder == null) return;
                    c.Assert(g.ui.HasUI(InfoDialog.LastUIType), "the ballots window");
                    c.Assert(FindButtons(InfoDialog.LastUIType, GameTool.LS("elder_to_war")).Any(), "the button To war");
                    CloseAll(UIType.TextInfo, UIType.TextInfoLong);
                })
                .Step("desertion", c =>
                {
                    if (elder == null) return;
                    var e = ClanElderEvent.PostOf(elder.GetUnitId()) ?? ClanElderEvent.Appoint(town, Player, elder);
                    e.Loyalty = 0;
                    ClanElderEvent.BeforeWar(town, Player, foe, () => 0);
                    c.Assert(ClanElderEvent.PostOf(elder.GetUnitId()) == null, "a disloyal elder fled the war");
                    var h = ClanElderEvent.HuntsOn(elder.GetUnitId()).FirstOrDefault();
                    c.Assert(h != null && h.Head == Player.GetUnitId(), "the clan hunts the deserter");
                    c.AssertEqual("elder_err_hunted", ClanElderEvent.CannotServe(town, Player, elder), "a deserter is not invited again");
                    c.Assert(TaoistLogEvent.Instance.Entries.Any(x => x.Key == "log_clan_elder_fled"), "Taoist log");
                    // the hunter beats the deserter, who dies with HuntDeath
                    var hunter = ClanElderEvent.Hunter(h);
                    if (hunter != null)
                    {
                        ClanElderEvent.ResolveHunt(h, hunter, true, () => 0);
                        c.Assert(elder.isDie && !ClanElderEvent.Instance.Hunts.Contains(h), $"{hunter.GetName()} hunted the deserter down");
                    }
                    else c.Info("no hunter in the clan");
                })
                .Step("an NPC clan invites the player", c =>
                {
                    if (foe == null) return;
                    // the player leaves the town's clans aside: the foe trusts the player
                    ClanEvent.Instance.Bases[ClanEvent.Key(town.buildData.id, Player.GetUnitId(), foe.GetUnitId())] = 0;
                    SetIntim(foe, Player, 300);
                    Player.SetProperty<int>(UnitPropertyEnum.Reputation, Math.Max(Player.GetProperty<int>(UnitPropertyEnum.Reputation), conf.InviteReputation + 1));
                    c.AssertEqual("elder_err_head", ClanElderEvent.CannotServe(town, foe, Player), "the head of a clan of the town cannot guard another one there");
                    // the player's house given up for the test: no clan of its own here
                    c.Bag["myplot"] = EstateEvent.HouseOf(town, Player)?.Index ?? -1;
                    var mine = EstateEvent.HouseOf(town, Player);
                    if (mine != null) mine.OwnerId = null;
                    c.Info($"foe's intimacy {foe.data.unitData.relationData.GetIntim(Player.GetUnitId())}, reputation {Player.GetDynProperty(UnitDynPropertyEnum.Reputation).value}, can serve: {ClanElderEvent.CannotServe(town, foe, Player) ?? "yes"}, a clan: {ClanEvent.Heads(town).Contains(foe.GetUnitId())}, post: {ClanElderEvent.PlayerPost?.Head ?? "none"}, inviters {ClanElderEvent.Inviters().Count}");
                    c.Assert(ClanElderEvent.Inviters().Any(x => x.head.GetUnitId() == foe.GetUnitId()), $"{ClanEvent.ClanName(foe)} would invite the player");
                    ClanElderEvent.Instance.InvitedYear = -1;
                    ClanElderEvent.AskInvite(town, foe);
                }, 1500)
                .Step("accept", c =>
                {
                    if (foe == null) return;
                    c.Assert(DramaUI() != null, "the invitation");
                    c.Assert(DramaHelper.ChooseDramaOption(GameTool.LS("elder_accept")), "accept");
                }, 1500)
                .Step("the player guards the clan", c =>
                {
                    if (foe == null) return;
                    CloseAll(UIType.CheckPopup, UIType.TextInfo);
                    c.Assert(ClanElderEvent.IsPlayerElderOf(town, foe), $"the player is an elder of {ClanEvent.ClanName(foe)}");
                    c.Assert(ClanEvent.OnPlayersSide(town, foe), "its side in a fight");
                    var pm = Player.GetUnitMoney();
                    ClanElderEvent.PayMonth();
                    c.AssertEqual(pm + ClanElderEvent.Salary(Player), Player.GetUnitMoney(), "the salary");
                    ClanEvent.OpenClans(town);
                }, 1500)
                .Step("resign", c =>
                {
                    if (foe == null) return;
                    c.Assert(FindButtons(UIType.TextInfoLong, GameTool.LS("elder_resign")).Any(), "the Resign button in the Clans window");
                    ClanElderEvent.Leave(ClanElderEvent.PlayerPost, "log_clan_elder_resigned");
                    c.Assert(ClanElderEvent.PlayerPost == null && ClanElderEvent.HuntsOn(Player.GetUnitId()).Count == 0, "resigned in peace: no hunt");
                    CloseAll(UIType.TextInfoLong);
                })
                .Step("restore", c =>
                {
                    DismissDramas();
                    CloseAll(UIType.TextInfoLong, UIType.TextInfo, UIType.CheckPopup);
                    conf.GradeOverArea = c.Bag.ContainsKey("gradeOver") ? (int)c.Bag["gradeOver"] : conf.GradeOverArea;
                    if (town == null) return;
                    EstateEvent.Instance.Towns[town.buildData.id] = JsonConvert.DeserializeObject<EstateEvent.TownEstate>((string)c.Bag["estate"]);
                    MapBuildPropertyEvent.AddBuildProperty(town, (long)c.Bag["budget"] - MapBuildPropertyEvent.GetBuildProperty(town));
                    ClanEvent.Instance.Bases = JsonConvert.DeserializeObject<Dictionary<string, int>>((string)c.Bag["bases"]);
                    ClanElderEvent.Instance.Elders = JsonConvert.DeserializeObject<List<ClanElderEvent.Elder>>((string)c.Bag["elders"]);
                    ClanElderEvent.Instance.Hunts = JsonConvert.DeserializeObject<List<ClanElderEvent.Hunt>>((string)c.Bag["hunts"]);
                    ClanElderEvent.Instance.Asked = JsonConvert.DeserializeObject<Dictionary<string, int>>((string)c.Bag["asked"]);
                    ClanElderEvent.Instance.InvitedYear = (int)c.Bag["invited"];
                    Player.SetUnitMoney((int)c.Bag["money"]);
                    Player.SetProperty<int>(UnitPropertyEnum.Reputation, (int)c.Bag["rep"]);
                    if (c.Bag.ContainsKey("foe") && foe != null && !foe.isDie)
                    {
                        var v = (object[])c.Bag["foe"];
                        foe.SetUnitPos((UnityEngine.Vector2Int)v[0]);
                        foe.SetUnitMoney((int)v[1]);
                    }
                    c.AssertEqual((long)c.Bag["budget"], MapBuildPropertyEvent.GetBuildProperty(town), "budget restored");
                });
        }
    }
}
