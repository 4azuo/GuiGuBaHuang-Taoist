using ModLib.Helper;
using ModLib.Mod;
using MOD_nE7UL2.Const;
using MOD_nE7UL2.Helper;
using MOD_nE7UL2.Mod;
using System;
using System.Collections.Generic;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Tool
{
    /// <summary>
    /// TR-04 (in the game): the pictures of the 6.0 video (marketing/video-6.0.0), in the order of its chapters. Build a
    /// city (master, palace, wall, the town map and its buildings), buy a mortal house, a child is born (the family
    /// tree), the player's clan among the clans of the city, its elders (the Elders window, the war council, an NPC clan
    /// inviting the player, a deserter hunted), a clan war: the call, the battle stepped in, the victory
    /// and the enemy clan wiped out (Taoist log); the new paths of cultivation: body refining, spirit sense and its scan,
    /// Dao heart, merit, the Artisan Dao, the massacre of a town (karma). The save is restored after the run.
    /// </summary>
    public class TR04_Promo60 : IScenarioDefinition
    {
        public string Id => "TR-04";

        /// <summary>Clicks a button of the cultivation screen (the paths of cultivation), the screen opened when needed.</summary>
        private static bool OpenPath(string labelKey)
        {
            OpenCultivation();
            var btn = FindButtons(UIType.FateFeature, GameTool.LS(labelKey)).FirstOrDefault();
            if (btn == null) return false;
            btn.Item.onClick.Invoke();
            return g.ui.HasUI(UIType.TextInfoLong);
        }

        /// <summary>The player alive (NotDie) with full vitality in the battle.</summary>
        private static void HealPlayer()
        {
            if (!ProtectPlayer()) return;
            var u = ModBattleEvent.PlayerUnit;
            // the game's heal (AddHP: the vitality bar follows; a plain hp write left the bar at 0)
            var missing = u.data.maxHP.value - u.data.hp;
            if (missing > 0) u.data.AddHP(new MartialTool.HitData(u, null, 0, 0, missing));
        }

        /// <summary>Opens the cultivation screen (FateFeature) from the map when it is not open.</summary>
        private static void OpenCultivation()
        {
            if (g.ui.HasUI(UIType.FateFeature)) return;
            var ui = g.ui.GetUI<UIMapMain>(UIType.MapMain);
            (ui?.playerInfo.btnFateFeature ?? ui?.playerInfo.btnFateFeature_En)?.onClick.Invoke();
        }

        public Scenario Create()
        {
            MapBuildTown town = null;
            EstateEvent.Plot house = null;
            var heads = new List<WorldUnitBase>();
            float warDeath = -1;
            WorldUnitBase warOurs = null, warTheirs = null;
            var elderGrade = int.MinValue;
            ClanElderEvent.Hunt hunt = null;
            var s = new Scenario { Id = "TR-04", Feature = "Promo", Title = "Promotion pictures: Taoist 6.0", Kind = ScenarioKind.Tool, Overlay = false, SnapSteps = false }
                .Step("travel", c =>
                {
                    // the artifact window the save opens on load: not behind the pictures
                    CloseAll(UIType.GodEye, UIType.PiscesPendant, UIType.DevilDemon);
                    TravelTo(c, FindTown(x => x.IsCity() && EstateEvent.Get(x) != null) ?? FindTown(x => EstateEvent.Get(x) != null) ?? FindTown(x => true));
                    town = BagTown(c);
                    c.Info($"town: {town?.name} (city {town?.IsCity()})");
                    var trainer = FeatureEvent<TrainerEvent>(ModConst.TRAINER_EVENT);
                    for (var i = 0; i < 40 && Player.GetGradeLvl() < TR02_PromoGame.PROMO_GRADE; i++) trainer.Levelup();
                    if (Player.GetUnitMoney() < 1000000) Player.AddUnitMoney(1000000);
                    Player.AddUnitProp(SpiritNoteEvent.NOTE_ID, 120);
                    if (town != null && !MapBuildPropertyEvent.IsTownMaster(town, Player)) trainer.BecomeTownMaster();
                }, 1500).Confirm()

                // ---- build a city: palace, wall, a busy city with cultivators' houses and clans
                .Step("the city grows", c =>
                {
                    var e = town == null ? null : EstateEvent.Get(town);
                    if (e == null) { c.Info("no estate in this town"); return; }
                    MapBuildPropertyEvent.AddBuildProperty(town, 50000000);
                    e.PalaceLevel = 4;
                    e.WallLevel = 3;
                    // the player starts without a house here: the one bought below is its own
                    foreach (var p in e.Plots.Where(x => x.OwnerId == Player.GetUnitId() && x.Level > 0).ToList()) EstateEvent.Release(e, p);
                    e.Population = EstateEvent.BasePopulation(town) * 3 / 2;
                    var k = 0;
                    EstateEvent.GrowMortals(town, e, 120, () => k++ % 3 == 0 ? 0 : 0.99);
                    // NPC clans (families of 2 or more) and other cultivators with their houses
                    var families = Npcs.Where(x => !HirePeopleEvent.IsHired(x) && !EstateEvent.HousesOf(x).Any() && ClanEvent.Members(x).Count >= 2
                            && ClanEvent.Members(x).All(m => !m.IsPlayer() && !FamilyEvent.IsFamilyOfPlayer(m)))
                        .OrderByDescending(x => x.GetGradeLvl()).ToList();
                    foreach (var h in families)
                    {
                        if (heads.Count == 3) break;
                        var ids = ClanEvent.Members(h).Select(x => x.GetUnitId()).ToList();
                        if (heads.Any(o => ClanEvent.Members(o).Any(m => ids.Contains(m.GetUnitId())))) continue;
                        heads.Add(h);
                    }
                    var others = Npcs.Where(x => !HirePeopleEvent.IsHired(x) && !EstateEvent.HousesOf(x).Any() && !heads.Contains(x)).OrderByDescending(x => x.GetGradeLvl()).Take(5);
                    foreach (var u in heads.Concat(others))
                    {
                        u.SetUnitPos(town.GetOrigiPoint());
                        u.SetUnitMoney(int.MaxValue / 2);
                        EstateEvent.NpcHouse(u, 1, () => 0, town);
                        for (var i = 0; i < EstateEvent.MAX_LEVEL; i++) EstateEvent.NpcHouse(u, 1, () => 0);
                    }
                    c.Info($"clans {heads.Count}, NPC houses {e.Plots.Count(x => x.OwnerId != null && x.MainIndex == null)}, grown {EstateEvent.GrownLevels(e)}");
                }, PromoShot.RENDER_MS);

            s.Shot("city-map", c => { if (town == null) return false; EstateEvent.OpenPlots(town); return g.ui.HasUI(UIType.TextInfoLong); })
                .Close(() => CloseAll(UIType.TextInfoLong));
            s.Shot("city-buildings", c =>
            {
                if (town == null || !MapBuildPropertyEvent.IsTownMaster(town, Player)) return false;
                // a building taken down: its "Build" button shows (the save is restored after the run)
                var down = MOD_nE7UL2.Enum.BuildingCostEnum.GetAllEnums<MOD_nE7UL2.Enum.BuildingCostEnum>().FirstOrDefault(x => !x.IsSchool && BuildingArrangeEvent.IsDestroyable(town, x));
                if (down != null) BuildingArrangeEvent.Destroy(town, down);
                MapBuildPropertyEvent.OpenUITownManage(town);
                return g.ui.HasUI(UIType.NPCSearch);
            }).Close(() => CloseAll(UIType.NPCSearch, UIType.TextInfoLong, UIType.TextInfo));

            // ---- buy a house: the best mortal house of the city, paid with banknotes
            s.Shot("house-buy", c =>
            {
                var e = town == null ? null : EstateEvent.Get(town);
                house = e?.Plots.Where(EstateEvent.IsMortalHome).OrderByDescending(x => x.Level).ThenByDescending(x => x.Prosperity).FirstOrDefault();
                if (house == null) return false;
                var notes = SpiritNoteEvent.NotesFor(EstateEvent.MortalPrice(town, house)) - SpiritNoteEvent.Notes(Player);
                if (notes > 0) Player.AddUnitProp(SpiritNoteEvent.NOTE_ID, (int)notes + 10);
                EstateEvent.ClickPlot(town, house);
                return g.ui.HasUI(InfoDialog.LastUIType);
            });
            s.Step("buy", c =>
            {
                if (house == null) return;
                var label = string.Format(GameTool.LS("estate_mortal_buy"), SpiritNoteEvent.NotesFor(EstateEvent.MortalPrice(town, house)));
                var btn = FindButtons(InfoDialog.LastUIType, label).FirstOrDefault();
                if (btn != null) btn.Item.onClick.Invoke();
                else c.Info($"no '{label}' button: {EstateEvent.BuyMortal(town, house, Player)}");
                CloseAll(UIType.TextInfo, UIType.TextInfoLong);
                c.Info($"house: {EstateEvent.HouseName(house.Level)}, owner {(house.OwnerId == Player.GetUnitId() ? "the player" : house.OwnerId ?? "-")}");
            }, PromoShot.RENDER_MS);
            s.Shot("house-bought", c =>
            {
                if (house == null || house.OwnerId != Player.GetUnitId()) return false;
                EstateEvent.OpenHouse(town, house);
                return g.ui.HasUI(UIType.TextInfoLong);
            }).Close(() => CloseAll(UIType.TextInfoLong));

            // ---- children: married, a grown-up child (a clan of the player), then a birth
            s.Step("family", c =>
            {
                var F = FamilyEvent.Instance;
                if (F == null || FamilyEvent.Disabled) { c.Info("family off"); return; }
                if (string.IsNullOrEmpty(FamilyEvent.Spouse(Player)))
                {
                    var mate = Npcs.Where(x => string.IsNullOrEmpty(FamilyEvent.Spouse(x)) && !HirePeopleEvent.IsHired(x) && !heads.Any(h => ClanEvent.Members(h).Contains(x))
                            && FamilyEvent.IsMan(x) != FamilyEvent.IsMan(Player))
                        .OrderByDescending(x => x.GetGradeLvl()).FirstOrDefault();
                    if (mate == null) { c.Info("no one to marry"); return; }
                    FamilyEvent.SetMarried(Player, mate);
                }
                var spouse = FamilyEvent.Unit(FamilyEvent.Spouse(Player));
                if (FamilyEvent.Family(Player).Children.Count == 0)
                {
                    var father = FamilyEvent.IsMan(Player) ? Player : spouse;
                    var mother = FamilyEvent.IsMan(Player) ? spouse : Player;
                    F.Birth(father, mother, GameHelper.GetGameTotalMonth() - FamilyEvent.ADULT_YEARS * 12 - 24);
                    F.OnMonthly();
                }
                if (town != null)
                    foreach (var m in ClanEvent.Members(Player).Where(x => !x.IsPlayer())) m.SetUnitPos(town.GetOrigiPoint());
                DismissDramas();
                c.Info($"married to {spouse?.GetName()}, children: {FamilyEvent.Family(Player).Children.Count}, clan members {ClanEvent.Members(Player).Count}");
            }, PromoShot.RENDER_MS);
            s.Shot("birth", c =>
            {
                if (FamilyEvent.Disabled || string.IsNullOrEmpty(FamilyEvent.Spouse(Player))) return false;
                var spouse = FamilyEvent.Unit(FamilyEvent.Spouse(Player));
                DismissDramas();
                FamilyEvent.Instance.Birth(FamilyEvent.IsMan(Player) ? Player : spouse, FamilyEvent.IsMan(Player) ? spouse : Player, GameHelper.GetGameTotalMonth());
                return true;
            }, () => DramaUI() != null).Close(() => DismissDramas());
            s.Shot("family", c =>
            {
                if (FamilyEvent.Disabled || string.IsNullOrEmpty(FamilyEvent.Spouse(Player))) return false;
                DismissDramas();
                FamilyEvent.OpenFamily(Player);
                return g.ui.HasUI(UIType.NPCInfoRelation);
            }).Close(() => CloseAll(UIType.NPCInfoRelation));

            // ---- the player's clan among the clans of the city: an ally, a blood feud
            s.Step("clans", c =>
            {
                if (town == null || heads.Count < 2) { c.Info($"clans: {heads.Count}"); return; }
                var tid = town.buildData.id;
                var me = Player.GetUnitId();
                ClanEvent.Instance.Bases[ClanEvent.Key(tid, me, heads[0].GetUnitId())] = 90;
                ClanEvent.Instance.Bases[ClanEvent.Key(tid, me, heads[1].GetUnitId())] = -100;
                ClanEvent.Instance.Bases[ClanEvent.Key(tid, heads[0].GetUnitId(), heads[1].GetUnitId())] = -60;
                if (heads.Count > 2)
                {
                    ClanEvent.Instance.Bases[ClanEvent.Key(tid, me, heads[2].GetUnitId())] = 20;
                    ClanEvent.Instance.Bases[ClanEvent.Key(tid, heads[1].GetUnitId(), heads[2].GetUnitId())] = -40;
                }
                c.Info($"clans of the town: {ClanEvent.Heads(town).Count}, the player's: {ClanEvent.Heads(town).Contains(me)}");
            });
            // ---- clan elders: two outsiders guard the player's clan (one of them not so loyal)
            s.Step("elders", c =>
            {
                if (town == null || !ClanElderEvent.Enabled || !ClanEvent.Heads(town).Contains(Player.GetUnitId())) return;
                // the realm asked of the elders: lowered until the area has cultivators to show (the save's NPCs)
                elderGrade = ClanElderEvent.Conf.GradeOverArea;
                while (ClanElderEvent.Candidates(town, Player).Count < 5 && ClanElderEvent.MinGrade(town) > 1) ClanElderEvent.Conf.GradeOverArea--;
                var mine = EstateEvent.HouseOf(town, Player);
                if (mine != null && ClanElderEvent.MaxElders(mine.Level) < 2) mine.Level = Math.Max(mine.Level, 3);
                ClanElderEvent.Instance.Elders.RemoveAll(x => x.Head == Player.GetUnitId());
                var loyalty = new[] { 86, 34 };
                foreach (var (u, i) in ClanElderEvent.Candidates(town, Player).Take(2).Select((u, i) => (u, i)))
                    ClanElderEvent.Appoint(town, Player, u).Loyalty = loyalty[i];
                foreach (var u in ClanElderEvent.Candidates(town, Player))
                    FamilyEvent.ChangeIntim(u.data.unitData.relationData, Player.GetUnitId(), 60);
                c.Info($"elders: {ClanElderEvent.Of(town, Player).Count}, realm {ClanElderEvent.MinGrade(town)}+, candidates {ClanElderEvent.Candidates(town, Player).Count}");
            });
            s.Shot("clan", c => { if (town == null || ClanEvent.Heads(town).Count < 2) return false; ClanEvent.OpenClans(town); return g.ui.HasUI(UIType.TextInfoLong); })
                .Close(() => CloseAll(UIType.TextInfoLong));
            s.Shot("elders", c =>
            {
                if (town == null || ClanElderEvent.Of(town, Player).Count == 0) return false;
                ClanElderEvent.OpenElders(town);
                return g.ui.HasUI(UIType.TextInfoLong);
            }).Close(() => CloseAll(UIType.TextInfoLong));
            // the war council on the blood feud: the head's 3 votes and one loyal elder for, the other against
            s.Shot("elder-council", c =>
            {
                if (town == null || heads.Count < 2 || ClanElderEvent.Of(town, Player).Count == 0) return false;
                var rolls = new Queue<double>(new[] { 0.0, 0.99 });
                var ballots = ClanElderEvent.Vote(town, Player, heads[1], true, () => rolls.Count > 0 ? rolls.Dequeue() : 0.99, out var passed);
                ClanElderEvent.ShowBallots(town, Player, heads[1], ballots, passed, () => { });
                return true;
            }, () => g.ui.HasUI(InfoDialog.LastUIType)).Close(() => CloseAll(UIType.TextInfo, UIType.TextInfoLong));
            // an NPC clan that trusts the player invites it to guard it
            s.Shot("elder-invite", c =>
            {
                if (town == null || heads.Count < 1 || !ClanElderEvent.Enabled) return false;
                ClanElderEvent.AskInvite(town, heads[0]);
                return true;
            }, () => DramaUI() != null).Close(() => DismissDramas());
            // a deserter hunted: the feuding clan's hunter blocks the player's way
            s.Shot("elder-hunt", c =>
            {
                if (town == null || heads.Count < 2 || !ClanElderEvent.Enabled) return false;
                hunt = new ClanElderEvent.Hunt { TownId = town.buildData.id, Head = heads[1].GetUnitId(), TargetId = Player.GetUnitId(), Salary = ClanElderEvent.Salary(Player), Wins = 1 };
                ClanElderEvent.Instance.Hunts.Add(hunt);
                var hunter = ClanElderEvent.Hunter(hunt);
                if (hunter == null) return false;
                ClanElderEvent.Ambush(hunt, hunter);
                return true;
            }, () => DramaUI() != null).Close(() =>
            {
                // the hunt ended first: the drama closed without a fight
                if (hunt != null) ClanElderEvent.Instance.Hunts.Remove(hunt);
                DismissDramas();
                if (elderGrade != int.MinValue) ClanElderEvent.Conf.GradeOverArea = elderGrade;
            });

            // ---- clan war: the feud marches on the player's clan; the player steps in, wins, the enemy clan is wiped out
            s.Shot("clan-war-call", c =>
            {
                if (town == null || heads.Count < 2) return false;
                var ours = ClanEvent.Heads(town).Contains(Player.GetUnitId()) ? Player : heads[0];
                var ourFighter = ClanEvent.Fighter(ours, true) ?? ours;
                var theirFighter = ClanEvent.Fighter(heads[1], true);
                if (theirFighter == null) return false;
                warOurs = ours; warTheirs = heads[1];
                ClanEvent.Instance.Pending = null;
                ClanEvent.Ask(town, new ClanEvent.Fight
                {
                    TownId = town.buildData.id, War = true, Ours = ours.GetUnitId(), Theirs = heads[1].GetUnitId(),
                    OurFighter = ourFighter.GetUnitId(), TheirFighter = theirFighter.GetUnitId(),
                });
                return true;
            }, () => DramaUI() != null);
            s.Step("step in", c =>
            {
                DismissDramas();
                if (ClanEvent.Instance.Pending == null) return;
                // every loser of this war falls: the enemy clan is wiped out (the save is restored after the run)
                warDeath = ClanEvent.Conf.WarDeath;
                ClanEvent.Conf.WarDeath = 1f;
                ClanEvent.OnAnswer(0);
            })
                // no battle (nobody stepped in): the waits end at once
                .Wait("battle loaded", c =>
                {
                    if (ClanEvent.Instance.Pending == null) return true;
                    StartBattle(); ProtectPlayer();
                    return InBattle && ModBattleEvent.BattleUnits.ToArray().Any(x => x.IsWorldUnit() && !x.IsPlayer());
                }, 90000, 1000)
                .Wait("battle started", c => { if (!InBattle) return true; ProtectPlayer(); return StartBattle(); }, 60000, 1000)
                .Step("the fight", c => ProtectPlayer(), 2500);
            // the player's vitality full in the picture (KeepAlive refills it only under half: the bar showed 0)
            s.Shot("clan-battle", c => { HealPlayer(); return InBattle; }, () => { HealPlayer(); return true; });
            s.Step("win", c =>
            {
                if (!InBattle) return;
                ProtectPlayer();
                var enemy = ModBattleEvent.BattleUnits.ToArray().FirstOrDefault(x => x.IsWorldUnit() && !x.IsPlayer());
                for (var i = 0; i < 10 && enemy != null && enemy.data.hp > 1 && !enemy.isDie; i++)
                    PlayerHit(enemy, (enemy.data.hp + enemy.data.maxHP.value) * 3);
                EndBattle(true);
            }, 1500)
                .Wait("back to the map", c => LeaveBattle(), 90000, 2000);
            s.Shot("clan-victory", c =>
            {
                if (warDeath < 0) return false;
                ClanEvent.Conf.WarDeath = warDeath;
                // the message of the victory (ClanEvent.Resolve: a one-button message box) is answered by the runner
                // before the next step (stale popups): shown again, the same text, in a text window it leaves open, when
                // the player's side won (the enemy clan gone from the town)
                if (warOurs == null || warTheirs == null || ClanEvent.Heads(town).Contains(warTheirs.GetUnitId())) return false;
                CloseAll(UIType.CheckPopup, UIType.TextInfo);
                InfoDialog.Show(town.name, string.Format(GameTool.LS("clan_won"), ClanEvent.ClanName(warOurs), ClanEvent.ClanName(warTheirs)));
                return true;
            }, () => g.ui.HasUI(UIType.TextInfo))
                .Close(() => CloseAll(UIType.CheckPopup, UIType.TextInfo));
            s.Shot("clan-extinct", c =>
            {
                if (warDeath < 0 || TaoistLogEvent.Instance == null) return false;
                TaoistLogEvent.Instance.OpenLog();
                return true;
            }).Close(() => CloseAll(UIType.TextInfoLong));

            // ---- the new paths of cultivation, each well along
            s.Step("the paths", c =>
            {
                if (BodyCulEvent.Enabled)
                {
                    var d = BodyCulEvent.Get(Player) ?? BodyCulEvent.Start(Player);
                    d.Level = BodyCulEvent.LAYERS * 3 + 1;
                    d.Exp = BodyCulEvent.NeedExp(d.Level) * 2 / 3;
                    d.InjuryMonths = 0;
                }
                if (MeritEvent.Enabled) MeritEvent.Add(Player, MeritEvent.MaxMerit * 2 / 3 - MeritEvent.MeritOf(Player), "merit_src_donate");
                if (DaoHeartEvent.Enabled) DaoHeartEvent.Add(Player, DaoHeartEvent.MaxValue * 3 / 4, "heart_src_seclusion");
                if (SpiritSenseEvent.Enabled)
                {
                    SpiritSenseEvent.AddExp(Player, SpiritSenseEvent.MaxExp * 3 / 4);
                    SpiritSenseEvent.Get(Player).LastScanMonth = -1;
                }
                if (ArtisanEvent.Enabled)
                {
                    var ranks = new[] { 5, 4, 3, 2, 3, 4 };
                    for (var i = 0; i < ArtisanEvent.CRAFTS.Length; i++)
                    {
                        var d = ArtisanEvent.Get(Player, ArtisanEvent.CRAFTS[i], true);
                        d.Rank = ranks[i % ranks.Length];
                        d.Exp = ArtisanEvent.NeedExp(d.Rank) / 2;
                        ArtisanEvent.Sync(Player, ArtisanEvent.CRAFTS[i]);
                    }
                }
                UnitModifyEvent.Instance.CachedValues.Clear();
                c.Info($"body {BodyCulEvent.GetLevel(Player)}, merit {MeritEvent.Level(Player)}, heart {DaoHeartEvent.Level(Player)}, sense {SpiritSenseEvent.Level(Player)}, artisan {(ArtisanEvent.Enabled ? ArtisanEvent.BestRank(Player) : 0)}");
            }, 1000);
            // the cultivation screen opened first: the paths add their buttons when it has opened (OnOpenUIEnd), not in
            // the frame of the click
            s.Step("cultivation screen", c => OpenCultivation())
                .Wait("path buttons", c => FindButtons(UIType.FateFeature, GameTool.LS("body_button")).Any() || !BodyCulEvent.Enabled, 10000, 300);
            s.Shot("path-body",c => BodyCulEvent.Enabled && OpenPath("body_button")).Close(() => CloseAll(UIType.TextInfoLong));
            s.Shot("path-sense", c => SpiritSenseEvent.Enabled && OpenPath("sense_button"));
            s.Shot("path-scan", c =>
            {
                if (!g.ui.HasUI(UIType.TextInfoLong)) return false;
                var btn = FindButtons(UIType.TextInfoLong, GameTool.LS("sense_scan_button").Split('{')[0]).FirstOrDefault();
                if (btn == null) return false;
                btn.Item.onClick.Invoke();
                return SpiritSenseEvent.LastScan != null;
            }).Close(() => CloseAll(UIType.TextInfoLong, UIType.TextInfo));
            s.Shot("path-heart", c => DaoHeartEvent.Enabled && OpenPath("heart_button")).Close(() => CloseAll(UIType.TextInfoLong));
            s.Shot("path-merit", c => MeritEvent.Enabled && OpenPath("merit_button")).Close(() => CloseAll(UIType.TextInfoLong));
            s.Shot("path-artisan", c => ArtisanEvent.Enabled && OpenPath("artisan_button")).Close(() => CloseAll(UIType.TextInfoLong, UIType.FateFeature));

            // ---- the evil way: karma, the massacre of a small town offered
            s.Shot("path-massacre", c =>
            {
                if (!MeritEvent.Enabled) return false;
                if (!TravelTo(c, FindTown(t => t.IsSmallTown() && MapBuildPropertyEvent.GetTownMaster(t) != null && !MapBuildPropertyEvent.IsTownMaster(t, Player)) ?? FindTown(t => t.IsSmallTown()))) return false;
                var target = Player.GetMapBuild<MapBuildTown>();
                MeritEvent.Add(Player, -600 - MeritEvent.MeritOf(Player));
                if (target == null || !MeritEvent.CanMassacre(Player, target)) { c.Info($"no massacre in {target?.name}"); return false; }
                MeritEvent.OpenWindow();
                var btn = FindButtons(UIType.TextInfoLong, string.Format(GameTool.LS("merit_massacre_button"), target.name)).FirstOrDefault();
                if (btn == null) return false;
                btn.Item.onClick.Invoke();
                return true;
            }, () => DramaUI() != null).Close(() => { DismissDramas(); CloseAll(UIType.TextInfoLong); });

            return s.Step("cleanup", c =>
            {
                if (warDeath >= 0) ClanEvent.Conf.WarDeath = warDeath;
                ClanEvent.Instance.Pending = null;
                DismissDramas();
                CloseAll(UIType.NPCSearch, UIType.TextInfoLong, UIType.TextInfo, UIType.NPCInfoRelation, UIType.FateFeature, UIType.CheckPopup);
                c.Info("done (the save is restored by run-autotest.ps1)");
            });
        }
    }
}
