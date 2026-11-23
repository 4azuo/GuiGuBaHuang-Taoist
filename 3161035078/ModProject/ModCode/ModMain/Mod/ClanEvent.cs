using MOD_nE7UL2.Helper;
using EBattleTypeData;
using MOD_nE7UL2.Const;
using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Helper;
using ModLib.Mod;
using ModLib.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Cultivator clans of the towns (settings: "Clans" of ModConf/estate_configs.json):
    /// - A clan: a cultivator with a house in a town (EstateEvent; the player too) and its living family (spouse, children,
    ///   grandchildren: FamilyEvent), at least MinMembers.
    /// - Two clans of a town have a relation -100..100: a base (random at first) moved by their fights, + the mean
    ///   intimacy between their members x MemberWeight, + MarriageBonus a marriage between them.
    /// - Yearly, rarely: hostile clans brawl (a descendant against another: the loser hurt, sometimes dead); bitter enemies
    ///   go to war (every member fights; the losers may die and lose their house; a clan left without members is wiped
    ///   out). When the player's family or an ally of it is in a fight, the player is asked to step in: a real battle
    ///   against the enemy's champion decides it.
    /// - Elders (ClanElderEvent): outsiders who guard a clan; they vote on its wars and fight in them.
    /// - Succession: when the head dies, a son or daughter takes the family house and heads the clan (a grown one, the
    ///   strongest first; else the eldest child; else a grandchild, a brother or sister, the spouse); the family of the old
    ///   head stays in the clan (Kin), the relations and the elders go with it. No one left (wiped out): the clan is gone,
    ///   its mansion stands in ruins (RuinMonths), then is split into plots for the mortals, unless another clan of the
    ///   town takes it as its seat (or the player buys it). The player
    ///   who goes on as a child at the end of its life (FamilyEvent.Inherit): the next head goes on heading the clan;
    ///   another child is a member of the clan of the next head (the houses go to it).
    /// - "Clans" button on the town map: the clans of the town and their relations.
    /// </summary>
    [Cache(ModConst.CLAN_EVENT)]
    public class ClanEvent : ModEvent
    {
        public static ClanEvent Instance { get; set; }

        public static EstateConfig._Clans Conf => EstateEvent.Conf.Clans;

        /// <summary>Base relation of two clans of a town: "townId|headA|headB" (heads sorted).</summary>
        public Dictionary<string, int> Bases { get; set; } = new Dictionary<string, int>();
        /// <summary>A fight the player was asked to step in (resolved after the battle).</summary>
        public Fight Pending { get; set; }
        /// <summary>Head id -> the family of the former heads who stay in its clan after a succession (their children count too).</summary>
        public Dictionary<string, List<string>> Kin { get; set; } = new Dictionary<string, List<string>>();

        public class Fight
        {
            public string TownId { get; set; }
            public bool War { get; set; }
            /// <summary>Heads of the clans: the side the player helps, the other.</summary>
            public string Ours { get; set; }
            public string Theirs { get; set; }
            public string OurFighter { get; set; }
            public string TheirFighter { get; set; }
        }

        private static WorldUnitBase Player => g.world.playerUnit;

        // ------------------------------------------------------------------ formulas (pure, unit tests)

        /// <summary>Relation of two clans: base + mean intimacy of the members x weight + a bonus a marriage, -100..100.</summary>
        public static int Relation(int baseRel, double memberIntim, int marriages) =>
            (int)Math.Max(-100, Math.Min(100, Math.Round(baseRel + memberIntim * Conf.MemberWeight + marriages * Conf.MarriageBonus)));

        /// <summary>Chance (0-1) of side A to win: its power against the other's.</summary>
        public static double WinChance(double powerA, double powerB) => powerA <= 0 && powerB <= 0 ? 0.5 : powerA / Math.Max(1.0, powerA + powerB);

        public static string Key(string townId, string a, string b) => string.CompareOrdinal(a, b) < 0 ? $"{townId}|{a}|{b}" : $"{townId}|{b}|{a}";

        // ------------------------------------------------------------------ clans

        private static WorldUnitBase Unit(string id) => FamilyEvent.Unit(id);
        private static bool Alive(WorldUnitBase u) => u != null && !u.isDie;

        /// <summary>The heads of the clans of a town: the owners of its houses (estates already made).</summary>
        public static List<string> Heads(MapBuildTown town)
        {
            if (town == null || EstateEvent.Instance == null || !EstateEvent.Instance.Towns.TryGetValue(town.buildData.id, out var e))
                return new List<string>();
            return e.Plots.Where(x => x.OwnerId != null && x.Level > 0 && x.MainIndex == null).Select(x => x.OwnerId).Distinct()
                .Where(id => Alive(Unit(id)) && Members(Unit(id)).Count >= Conf.MinMembers).ToList();
        }

        /// <summary>The living members of a clan: the head, the spouse, the children and grandchildren.</summary>
        public static List<WorldUnitBase> Members(WorldUnitBase head)
        {
            var rs = new List<WorldUnitBase>();
            if (!Alive(head)) return rs;
            rs.Add(head);
            var f = FamilyEvent.Family(head);
            var spouse = Unit(f.Spouse);
            if (Alive(spouse)) rs.Add(spouse);
            foreach (var cid in f.Children)
            {
                var child = Unit(cid);
                if (!Alive(child)) continue;
                rs.Add(child);
                foreach (var gid in FamilyEvent.Family(child).Children)
                {
                    var gc = Unit(gid);
                    if (Alive(gc)) rs.Add(gc);
                }
            }
            // the family of the former heads (a succession): they and their children
            if (Instance?.Kin != null && Instance.Kin.TryGetValue(head.GetUnitId(), out var kin))
                foreach (var id in kin)
                {
                    var u = Unit(id);
                    if (!Alive(u)) continue;
                    rs.Add(u);
                    foreach (var cid in FamilyEvent.Family(u).Children)
                    {
                        var ch = Unit(cid);
                        if (Alive(ch)) rs.Add(ch);
                    }
                }
            return rs.GroupBy(x => x.GetUnitId()).Select(x => x.First()).ToList();
        }

        public static string ClanName(WorldUnitBase head) => string.Format(GameTool.LS("clan_name"), head?.GetName() ?? "-");

        /// <summary>Relation of two clans of a town (their base, the intimacy of their members, marriages between them).</summary>
        public static int Relation(MapBuildTown town, WorldUnitBase headA, WorldUnitBase headB)
        {
            var a = Members(headA);
            var b = Members(headB);
            var sum = 0.0;
            var n = 0;
            foreach (var x in a.Take(8))
                foreach (var y in b.Take(8))
                {
                    sum += (x.data.unitData.relationData.GetIntim(y.GetUnitId()) + y.data.unitData.relationData.GetIntim(x.GetUnitId())) / 2.0;
                    n++;
                }
            var ids = new HashSet<string>(b.Select(x => x.GetUnitId()));
            var marriages = a.Count(x => ids.Contains(FamilyEvent.Spouse(x) ?? ""));
            return Relation(Base(town.buildData.id, headA.GetUnitId(), headB.GetUnitId()), n == 0 ? 0 : sum / n, marriages);
        }

        public static int Base(string townId, string a, string b)
        {
            var key = Key(townId, a, b);
            if (!Instance.Bases.TryGetValue(key, out var v))
            {
                v = CommonTool.Random(Conf.StartRelation[0], Conf.StartRelation[1] + 1);
                Instance.Bases[key] = v;
            }
            return v;
        }

        public static void AddBase(string townId, string a, string b, int delta)
        {
            var key = Key(townId, a, b);
            Instance.Bases[key] = Math.Max(-100, Math.Min(100, Base(townId, a, b) + delta));
        }

        /// <summary>The clan of the player in a town (the player's head id) when it has a house there.</summary>
        public static bool IsPlayerClan(string head) => Player != null && head == Player.GetUnitId();

        private static double Power(IEnumerable<WorldUnitBase> units) => units.Sum(x => (double)UnitPowerCalHelper.CalWUnitBattlePower(x));

        // ------------------------------------------------------------------ yearly

        [EventCondition(IsInGame = HandleEnum.True)]
        public override void OnYearly()
        {
            base.OnYearly();
            if (!Conf.Enabled)
                return;
            for (var y = 0; y < Math.Max(1, ModMaster.YearsPassed); y++)
                foreach (var town in ModMain.Store.Towns)
                {
                    try { YearTown(town, () => CommonTool.Random(0f, 1f)); }
                    catch (Exception ex) { DebugHelper.WriteLine($"ClanEvent.YearTown {town?.name}: {ex}"); }
                    try { YearRuins(town, () => CommonTool.Random(0f, 1f)); }
                    catch (Exception ex) { DebugHelper.WriteLine($"ClanEvent.YearRuins {town?.name}: {ex}"); }
                }
        }

        /// <summary>A year of the clans of a town: hostile pairs may brawl, bitter enemies may go to war. Returns the fights.</summary>
        public static int YearTown(MapBuildTown town, Func<double> roll)
        {
            var heads = Heads(town);
            var fights = 0;
            for (var i = 0; i < heads.Count; i++)
                for (var j = i + 1; j < heads.Count; j++)
                {
                    var a = Unit(heads[i]); var b = Unit(heads[j]);
                    if (!Alive(a) || !Alive(b)) continue;
                    var rel = Relation(town, a, b);
                    if (rel <= Conf.WarAt && roll() < Conf.WarChance)
                    {
                        // the elders of the clan that goes to war vote on it (ClanElderEvent)
                        if (ClanElderEvent.WarAllowed(town, a, b, roll)) Start(town, a, b, true, roll);
                        fights++;
                    }
                    else if (rel <= Conf.HostileAt && roll() < Conf.BrawlChance) { Start(town, a, b, false, roll); fights++; }
                }
            return fights;
        }

        /// <summary>The clans of the player's side: its own, and its allies in the town.</summary>
        public static bool OnPlayersSide(MapBuildTown town, WorldUnitBase head)
        {
            if (Player == null || head == null) return false;
            if (IsPlayerClan(head.GetUnitId())) return true;
            if (ClanElderEvent.IsPlayerElderOf(town, head)) return true;
            if (Members(head).Any(x => x.IsPlayer())) return true;
            if (!EstateEvent.HasHouse(town, Player) || Members(Player).Count < Conf.MinMembers) return false;
            return Relation(town, Player, head) >= Conf.AllyAt;
        }

        /// <summary>A fight starts: the player is asked to step in when its side is in it, else it is decided at once.</summary>
        public static void Start(MapBuildTown town, WorldUnitBase a, WorldUnitBase b, bool war, Func<double> roll)
        {
            // elders of low loyalty may flee a war (deserters)
            if (war) ClanElderEvent.BeforeWar(town, a, b, roll);
            var fa = Fighter(a, war);
            var fb = Fighter(b, war);
            if (fa == null || fb == null) return;
            var mineA = OnPlayersSide(town, a);
            var mineB = OnPlayersSide(town, b);
            if ((mineA ^ mineB) && Instance != null && CanAsk())
            {
                var ours = mineA ? a : b; var theirs = mineA ? b : a;
                var fight = new Fight
                {
                    TownId = town.buildData.id, War = war,
                    Ours = ours.GetUnitId(), Theirs = theirs.GetUnitId(),
                    OurFighter = (mineA ? fa : fb).GetUnitId(), TheirFighter = (mineA ? fb : fa).GetUnitId(),
                };
                Ask(town, fight);
                return;
            }
            Resolve(town, a, b, fa, fb, war, roll() < WinChance(SidePower(town, a, fa, war), SidePower(town, b, fb, war)), roll);
        }

        public static bool CanAsk() => !GameHelper.IsInBattlle() && !(TimeSkipEvent.Instance?.IsProcessing ?? false) && Instance.Pending == null;

        /// <summary>The one who fights for a clan: in a brawl a descendant (the youngest grown member, not the player), in a war the strongest.</summary>
        public static WorldUnitBase Fighter(WorldUnitBase head, bool war)
        {
            var m = Members(head).Where(x => !x.IsPlayer()).ToList();
            if (m.Count == 0) return head.IsPlayer() ? null : head;
            return war ? m.OrderByDescending(x => UnitPowerCalHelper.CalWUnitBattlePower(x)).First()
                       : m.Where(x => x.GetUnitId() != head.GetUnitId()).OrderBy(x => FamilyEvent.AgeYears(x)).FirstOrDefault() ?? m.First();
        }

        /// <summary>The power of a side: in a war its members and its elders, in a brawl its fighter.</summary>
        public static double SidePower(MapBuildTown town, WorldUnitBase head, WorldUnitBase fighter, bool war) =>
            war ? Power(Members(head)) + ClanElderEvent.Power(town, head) : UnitPowerCalHelper.CalWUnitBattlePower(fighter);

        /// <summary>The player is asked: step in (a real battle against the enemy's fighter) or stay out.</summary>
        public static void Ask(MapBuildTown town, Fight f)
        {
            Instance.Pending = f;
            var ours = Unit(f.Ours); var theirs = Unit(f.Theirs);
            var our = Unit(f.OurFighter); var their = Unit(f.TheirFighter);
            var text = string.Format(GameTool.LS(f.War ? "clan_war_ask" : "clan_brawl_ask"), town.name, ClanName(theirs), their.GetName(), ClanName(ours), our.GetName());
            DramaHelper.OpenDrama1(text, new List<string> { GameTool.LS("clan_step_in"), GameTool.LS("clan_stay_out") },
                x => OnAnswer(x.id % 10), their, Player);
        }

        /// <summary>0: step in (a real battle against the enemy's fighter); 1: stay out (decided at once).</summary>
        public static void OnAnswer(int opt)
        {
            var f = Instance.Pending;
            if (f == null) return;
            var town = Town(f.TownId);
            var their = Unit(f.TheirFighter);
            if (opt == 0 && town != null && Alive(their) && !GameHelper.IsInBattlle())
            {
                their.SetUnitPos(Player.GetUnitPos());
                g.world.battle.IntoBattle(their, BattleHelper.NPC_FIGHT_DUNGEON_ID, null, new WorldBattleData
                {
                    npcUnit = their,
                    isRealBattle = true,
                    npcWinNotKillPlayerUnit = true,
                    npcWinInsultPlayerUnit = false,
                });
                return;
            }
            Instance.Pending = null;
            if (town == null) return;
            Func<double> roll = () => CommonTool.Random(0f, 1f);
            var ours = Unit(f.Ours); var theirs = Unit(f.Theirs);
            var our = Unit(f.OurFighter);
            // an elder who stays out of a war of the clan it guards is a deserter
            if (f.War) ClanElderEvent.OnPlayerStayedOut(town, ours);
            if (Alive(ours) && Alive(theirs) && Alive(our) && Alive(their))
                Resolve(town, ours, theirs, our, their, f.War, roll() < WinChance(SidePower(town, ours, our, f.War), SidePower(town, theirs, their, f.War)), roll);
        }

        /// <summary>The battle of the player ended: its side won or lost the fight.</summary>
        public override void OnBattleEndHandler(BattleEndHandler e)
        {
            base.OnBattleEndHandler(e);
            var f = Instance?.Pending;
            if (f == null) return;
            Instance.Pending = null;
            var town = Town(f.TownId);
            var ours = Unit(f.Ours); var theirs = Unit(f.Theirs);
            var our = Unit(f.OurFighter); var their = Unit(f.TheirFighter);
            if (town == null || ours == null || theirs == null) return;
            Resolve(town, ours, theirs, Alive(our) ? our : Player, their ?? theirs, f.War, e.isWin, () => CommonTool.Random(0f, 1f), true);
        }

        private static MapBuildTown Town(string id) => ModMain.Store.Towns.FirstOrDefault(x => x.buildData.id == id);

        /// <summary>
        /// The end of a fight (aWon: side A won). Brawl: the loser's fighter hurt, dead with BrawlDeath; war: each member of
        /// the losers dies with WarDeath (the winners' with a quarter of it), the losers' house goes to the mortals, a clan
        /// left without members is wiped out. The player never dies here. The base relation falls.
        /// </summary>
        public static void Resolve(MapBuildTown town, WorldUnitBase a, WorldUnitBase b, WorldUnitBase fa, WorldUnitBase fb, bool war, bool aWon, Func<double> roll, bool playerFought = false)
        {
            var winner = aWon ? a : b; var loser = aWon ? b : a;
            var wf = aWon ? fa : fb; var lf = aWon ? fb : fa;
            var tid = town.buildData.id;
            var related = OnPlayersSide(town, a) || OnPlayersSide(town, b) || Player.GetMapBuild<MapBuildTown>()?.buildData.id == tid;
            var winnerName = ClanName(winner); var loserName = ClanName(loser);
            if (!war)
            {
                Hurt(lf);
                if (wf != null && lf != null)
                {
                    FamilyEvent.ChangeIntim(lf.data.unitData.relationData, wf.GetUnitId(), -30);
                    FamilyEvent.ChangeIntim(wf.data.unitData.relationData, lf.GetUnitId(), -30);
                }
                var died = Alive(lf) && !lf.IsPlayer() && roll() < Conf.BrawlDeath;
                if (died) lf.Die();
                died = died || lf.isDie;   // or killed in the player's battle
                AddBase(tid, a.GetUnitId(), b.GetUnitId(), died ? Conf.DeathRelation : Conf.BrawlRelation);
                TaoistLogEvent.LogIf(related || died, died ? "log_clan_brawl_death" : "log_clan_brawl", town.name, wf.GetName(), winnerName, lf.GetName(), loserName);
            }
            else
            {
                var fallen = 0;
                foreach (var m in Members(loser).Where(x => !x.IsPlayer()))
                    if (roll() < Conf.WarDeath) { m.Die(); fallen++; }
                    else Hurt(m);
                foreach (var m in Members(winner).Where(x => !x.IsPlayer()))
                    if (roll() < Conf.WarDeath / 4) { m.Die(); fallen++; }
                fallen += ClanElderEvent.AfterWar(town, winner, loser, roll);
                // the losers lose their house; a clan wiped out (no one left to carry it on) leaves its mansion in ruins
                var wiped = Members(loser).Count == 0;
                if (EstateEvent.Instance != null && EstateEvent.Instance.Towns.TryGetValue(tid, out var e))
                    foreach (var p in e.Plots.Where(x => x.OwnerId == loser.GetUnitId() && x.Level > 0 && x.MainIndex == null).ToList())
                        if (wiped) EstateEvent.Ruin(e, p, loserName);
                        else EstateEvent.Release(e, p, true);
                AddBase(tid, a.GetUnitId(), b.GetUnitId(), Conf.WarRelation);
                TaoistLogEvent.Log("log_clan_war", town.name, winnerName, loserName, fallen);
                if (Members(loser).Count == 0)
                    TaoistLogEvent.Log("log_clan_extinct", town.name, loserName);
            }
            if (playerFought)
                InfoDialog.Msg(town.name, string.Format(GameTool.LS(aWon ? "clan_won" : "clan_lost"), winnerName, loserName));
        }

        public static void Hurt(WorldUnitBase u)
        {
            if (!Alive(u) || u.IsPlayer()) return;
            var max = u.GetDynProperty(UnitDynPropertyEnum.HpMax).value;
            u.SetProperty<int>(UnitPropertyEnum.Hp, Math.Max(1, Math.Min(u.GetProperty<int>(UnitPropertyEnum.Hp), max / 5)));
        }

        // ------------------------------------------------------------------ succession

        /// <summary>The living family of a head (alive or dead): spouse, children, grandchildren, the former heads' family (Kin).</summary>
        private static (List<WorldUnitBase> children, List<WorldUnitBase> grand, List<WorldUnitBase> others, List<WorldUnitBase> siblings) Family(string headId)
        {
            var living = (ModMain.Store?.WUnits ?? new WorldUnitBase[0]).Where(Alive).ToList();
            if (Alive(Player) && living.All(x => x.GetUnitId() != Player.GetUnitId())) living.Add(Player);
            var children = living.Where(u => u.GetUnitId() != headId && FamilyEvent.Family(u).Parents.Contains(headId)).ToList();
            var childIds = new HashSet<string>(children.Select(x => x.GetUnitId()));
            var grand = living.Where(u => !childIds.Contains(u.GetUnitId()) && u.GetUnitId() != headId && FamilyEvent.Family(u).Parents.Any(childIds.Contains)).ToList();
            var others = living.Where(x => FamilyEvent.Spouse(x) == headId && x.GetUnitId() != headId).ToList();
            if (Instance?.Kin != null && Instance.Kin.TryGetValue(headId, out var kin))
                others.AddRange(kin.Select(Unit).Where(Alive));
            // brothers and sisters: of blood first (a parent in common), then sworn ones
            var siblings = living.Where(u => u.GetUnitId() != headId && !childIds.Contains(u.GetUnitId()) && FamilyEvent.Family(u).Siblings.Contains(headId))
                .OrderByDescending(u => FamilyEvent.Family(u).Parents.Any(p => { var pu = Unit(p); return pu != null && FamilyEvent.Family(pu).Children.Contains(headId); }) ? 1 : 0)
                .ToList();
            return (children, grand, others, siblings);
        }

        /// <summary>
        /// The next head of a clan whose head (headId) dies, in the town: a son or daughter - a grown one (HeirAge), the
        /// strongest first, else the eldest child -, else a grown grandchild, else a brother or sister (of blood first),
        /// else the spouse, else a young grandchild; never one who has a house of its own in the town. Null: no one left.
        /// </summary>
        public static WorldUnitBase Successor(MapBuildTown town, string headId)
        {
            var (children, grand, others, siblings) = Family(headId);
            bool Free(WorldUnitBase u) => u.GetUnitId() != headId && EstateEvent.HouseOf(town, u) == null;
            var strongest = new Func<IEnumerable<WorldUnitBase>, WorldUnitBase>(xs => xs.Where(Free)
                .OrderByDescending(x => x.GetGradeLvl()).ThenByDescending(x => FamilyEvent.AgeYears(x)).FirstOrDefault());
            return strongest(children.Where(x => FamilyEvent.AgeYears(x) >= Conf.HeirAge))
                ?? children.Where(Free).OrderByDescending(x => FamilyEvent.AgeYears(x)).FirstOrDefault()
                ?? strongest(grand.Where(x => FamilyEvent.AgeYears(x) >= Conf.HeirAge))
                ?? siblings.Where(Free).FirstOrDefault(x => FamilyEvent.AgeYears(x) >= Conf.HeirAge)
                ?? siblings.FirstOrDefault(Free)
                ?? others.FirstOrDefault(x => FamilyEvent.Spouse(x) == headId && Free(x))
                ?? strongest(grand);
        }

        /// <summary>
        /// The clan of a head (oldId) in the town passes to a new head: its house (all its plots), its relations with the
        /// other clans, its elders and hunts; the family of the old head stays in the clan (Kin: the members given).
        /// </summary>
        public static void Transfer(MapBuildTown town, EstateEvent.TownEstate e, string oldId, WorldUnitBase heir, IEnumerable<string> members)
        {
            var hid = heir.GetUnitId();
            foreach (var p in e.Plots.Where(x => x.OwnerId == oldId))
            {
                p.OwnerId = hid;
                p.Unpaid = 0;
            }
            var tid = town.buildData.id;
            foreach (var key in Instance.Bases.Keys.ToList())
            {
                var parts = key.Split('|');
                if (parts.Length != 3 || parts[0] != tid || (parts[1] != oldId && parts[2] != oldId)) continue;
                var other = parts[1] == oldId ? parts[2] : parts[1];
                var v = Instance.Bases[key];
                Instance.Bases.Remove(key);
                if (other != hid) Instance.Bases[Key(tid, hid, other)] = v;
            }
            var kin = Instance.Kin.TryGetValue(hid, out var k) ? k : new List<string>();
            kin.AddRange(members.Where(x => !string.IsNullOrEmpty(x) && x != hid && x != oldId && !kin.Contains(x)));
            Instance.Kin[hid] = kin;
            ClanElderEvent.OnSuccession(tid, oldId, hid);
        }

        /// <summary>
        /// The head of the house died: its heir takes the house (all its plots) and heads the clan; the relations with the
        /// other clans and the elders go with it. False when there is no heir (the caller gives the house to the mortals).
        /// </summary>
        public static bool Succeed(MapBuildTown town, EstateEvent.TownEstate e, EstateEvent.Plot main, string deadId)
        {
            if (!Conf.Enabled || town == null || main == null || string.IsNullOrEmpty(deadId)) return false;
            var heir = Successor(town, deadId);
            if (heir == null) return false;
            var tid = town.buildData.id;
            // the family of the old head stays in the clan
            var (children, grand, others, siblings) = Family(deadId);
            Transfer(town, e, deadId, heir, children.Concat(grand).Concat(others).Concat(siblings).Select(x => x.GetUnitId()));
            if (Instance.Kin.ContainsKey(deadId) && !EstateEvent.HousesOf(Unit(deadId)).Any()) Instance.Kin.Remove(deadId);
            TaoistLogEvent.LogIf(heir.IsPlayer() || OnPlayersSide(town, heir) || Player.GetMapBuild<MapBuildTown>()?.buildData.id == tid,
                "log_clan_succession", town.name, heir.GetName(), ClanName(heir));
            if (heir.IsPlayer())
                InfoDialog.Msg(town.name, string.Format(GameTool.LS("clan_succession_player"), EstateEvent.HouseName(main.Level), ClanName(heir)));
            return true;
        }

        /// <summary>
        /// The player goes on as its child (FamilyEvent.Inherit, before the swap: the player's unit keeps its id). In each
        /// town where the player heads a clan: the chosen child is the next head (Successor) -> the player goes on heading
        /// it, its old family stays in the clan; another child -> the clan (its house) passes to the next head, the player
        /// is one of its members. Returns the messages for the player.
        /// </summary>
        public static List<string> OnPlayerInherit(WorldUnitBase chosen)
        {
            var msgs = new List<string>();
            if (!Conf.Enabled || EstateEvent.Instance == null || Instance == null || chosen == null) return msgs;
            var pid = Player.GetUnitId();
            var cid = chosen.GetUnitId();
            var family = Members(Player).Select(x => x.GetUnitId()).Where(x => x != pid && x != cid).ToList();
            foreach (var kv in EstateEvent.Instance.Towns.ToList())
            {
                var town = Town(kv.Key);
                var main = kv.Value.Plots.FirstOrDefault(x => x.OwnerId == pid && x.Level > 0 && x.MainIndex == null);
                if (town == null || main == null) continue;
                var next = Successor(town, pid);
                if (next == null || next.GetUnitId() == cid)
                {
                    // the chosen child is the next head: the player's unit goes on heading the clan, with its old family
                    var kin = Instance.Kin.TryGetValue(pid, out var k) ? k : new List<string>();
                    kin.AddRange(family.Where(x => !kin.Contains(x)));
                    Instance.Kin[pid] = kin;
                    msgs.Add(string.Format(GameTool.LS("clan_inherit_head"), town.name, chosen.GetName()));
                    continue;
                }
                // another child is the next head: the house and the clan pass to it, the player is a member of its clan
                Transfer(town, kv.Value, pid, next, family.Concat(new[] { pid }));
                TaoistLogEvent.Log("log_clan_succession", town.name, next.GetName(), ClanName(next));
                msgs.Add(string.Format(GameTool.LS("clan_inherit_member"), town.name, next.GetName(), ClanName(next), EstateEvent.HouseName(main.Level)));
            }
            // the posts of elder and the hunts: the old player's end, the child's are the player's
            ClanElderEvent.OnPlayerInherit(pid, cid);
            // the chosen child's own houses are the player's now (not in a town where the player keeps a house)
            foreach (var kv in EstateEvent.Instance.Towns)
                if (!kv.Value.Plots.Any(x => x.OwnerId == pid))
                    foreach (var p in kv.Value.Plots.Where(x => x.OwnerId == cid)) p.OwnerId = pid;
            if (Instance.Kin.TryGetValue(cid, out var ck))
            {
                Instance.Kin.Remove(cid);
                var kin = Instance.Kin.TryGetValue(pid, out var k2) ? k2 : new List<string>();
                kin.AddRange(ck.Where(x => x != pid && !kin.Contains(x)));
                if (kin.Count > 0) Instance.Kin[pid] = kin;
            }
            return msgs;
        }

        // ------------------------------------------------------------------ ruins

        /// <summary>The ruined mansions of a town (their main plots).</summary>
        public static List<EstateEvent.Plot> Ruins(MapBuildTown town) =>
            EstateEvent.Get(town)?.Plots.Where(x => EstateEvent.IsRuin(x) && x.MainIndex == null).ToList() ?? new List<EstateEvent.Plot>();

        /// <summary>Price of a ruined mansion: RuinPrice x the land price of its plots.</summary>
        public static long RuinPrice(MapBuildTown town, EstateEvent.TownEstate e, EstateEvent.Plot main) =>
            (long)Math.Round(EstateEvent.PartsOf(e, main).Sum(x => (double)EstateEvent.LandPrice(town, x)) * Conf.RuinPrice);

        /// <summary>Months a ruin still stands before it is split into plots.</summary>
        public static int RuinMonthsLeft(EstateEvent.Plot p) => Math.Max(0, Conf.RuinMonths - (GameHelper.GetGameTotalMonth() - p.RuinSince));

        /// <summary>
        /// A year of the ruins of a town: a ruin past RuinMonths is split into plots for the mortals; else, with
        /// RuinTakeChance, the richest NPC clan of the town with a smaller house that can pay takes it as its seat (its old
        /// house to the mortals). Returns the ruins taken or split.
        /// </summary>
        public static int YearRuins(MapBuildTown town, Func<double> roll)
        {
            var e = EstateEvent.Get(town);
            if (e == null) return 0;
            var n = 0;
            foreach (var r in Ruins(town))
            {
                if (RuinMonthsLeft(r) <= 0)
                {
                    TaoistLogEvent.LogIf(Player.GetMapBuild<MapBuildTown>()?.buildData.id == town.buildData.id, "log_clan_ruin_split", town.name, r.RuinOf ?? "-");
                    EstateEvent.Release(e, r, true);
                    n++;
                    continue;
                }
                if (roll() >= Conf.RuinTakeChance) continue;
                var price = RuinPrice(town, e, r);
                var head = Heads(town).Select(Unit).Where(h => Alive(h) && !h.IsPlayer() && (EstateEvent.HouseOf(town, h)?.Level ?? 0) < r.Level && h.GetUnitMoney() >= price)
                    .OrderByDescending(h => h.GetUnitMoney()).FirstOrDefault();
                if (head == null) continue;
                TakeRuin(town, e, r, head, price);
                n++;
            }
            return n;
        }

        /// <summary>A clan takes a ruined mansion as its seat: it pays the town, leaves its old house to the mortals.</summary>
        public static void TakeRuin(MapBuildTown town, EstateEvent.TownEstate e, EstateEvent.Plot main, WorldUnitBase head, long price)
        {
            var old = EstateEvent.HouseOf(town, head);
            if (old != null) EstateEvent.Release(e, old, true);
            var fallen = main.RuinOf;
            foreach (var p in EstateEvent.PartsOf(e, main))
            {
                p.OwnerId = head.GetUnitId(); p.Unpaid = 0; p.RuinSince = -1; p.RuinOf = null;
            }
            if (head.IsPlayer()) SpiritNoteEvent.Pay(head, price);
            else head.AddUnitMoney((int)-Math.Min(int.MaxValue, price));
            MapBuildPropertyEvent.AddBuildProperty(town, price);
            TaoistLogEvent.LogIf(head.IsPlayer() || Player.GetMapBuild<MapBuildTown>()?.buildData.id == town.buildData.id, "log_clan_ruin_taken", town.name, ClanName(head), fallen ?? "-", EstateEvent.HouseName(main.Level));
        }

        /// <summary>A click on a ruin: whose it was, when it is split; the player without a house in the town may buy it.</summary>
        public static void ClickRuin(MapBuildTown town, EstateEvent.TownEstate e, EstateEvent.Plot main)
        {
            var title = string.Format(GameTool.LS("estate_title"), town.name);
            var price = RuinPrice(town, e, main);
            var text = EstateEvent.PlotWhere(main) + "\n" + string.Format(GameTool.LS("clan_ruin_info"), EstateEvent.HouseName(main.Level), main.RuinOf ?? "-",
                (GameHelper.GetGameTotalMonth() - main.RuinSince) / 12, RuinMonthsLeft(main));
            if (EstateEvent.HasHouse(town, Player))
            {
                InfoDialog.Show(title, text);
                return;
            }
            var notes = SpiritNoteEvent.NotesFor(price);
            InfoDialog.Show(title, text + "\n" + string.Format(GameTool.LS("clan_ruin_buy_info"), notes, SpiritNoteEvent.Notes(Player)),
                (string.Format(GameTool.LS("clan_ruin_buy"), notes), () =>
                {
                    if (SpiritNoteEvent.Notes(Player) < notes) { InfoDialog.Msg(title, EstateEvent.NoNotesText(price)); return; }
                    TakeRuin(town, e, main, Player, price);
                    EstateEvent.OpenPlots(town);
                    InfoDialog.Msg(title, string.Format(GameTool.LS("clan_ruin_bought"), EstateEvent.HouseName(main.Level)));
                }),
                (GameTool.LS("dlg_close"), null));
        }

        // ------------------------------------------------------------------ window

        /// <summary>
        /// The clans of a town: a card a clan (the picture of its mansion, its name, its members and house, its elders),
        /// the player's clan first; then the relation of every pair (the worst first), in three columns.
        /// </summary>
        public static void OpenClans(MapBuildTown town)
        {
            var ui = new UICustom1(string.Format(GameTool.LS("clan_title"), town.name));
            var heads = Heads(town).Select(Unit).ToList();
            var row = ui.FirstRow + 1;
            if (heads.Count == 0)
            {
                ui.AddText(ui.MidCol, row, GameTool.LS("clan_none")).Format(Color.gray, 15);
                ClanElderEvent.AddClansButtons(ui, town);
                return;
            }
            var dy = InfoSheet.RowHeight(ui);
            var left = ui.Columns[ui.FirstCol];
            var width = ui.Columns[ui.LastCol] - left;
            var shown = heads.OrderByDescending(x => IsPlayerClan(x.GetUnitId())).Take(10).ToList();
            var pairs = new List<(int rel, string text)>();
            for (var i = 0; i < heads.Count; i++)
                for (var j = i + 1; j < heads.Count; j++)
                {
                    var rel = Relation(town, heads[i], heads[j]);
                    pairs.Add((rel, string.Format(GameTool.LS("clan_relation"), ClanName(heads[i]), ClanName(heads[j]), rel, GameTool.LS(RelationKey(rel)))));
                }
            var relCols = Math.Min(3, Math.Min(9, pairs.Count));
            var relRows = relCols == 0 ? 0 : (Math.Min(9, pairs.Count) + relCols - 1) / relCols;
            // the cards: 5 a row; at most 7.5 rows high, less when the relations, the buttons and the hint need the room
            var perRow = Math.Min(5, shown.Count);
            var cardRows = (shown.Count + perRow - 1) / perRow;
            var top = ui.Rows[row] + dy / 2f;
            var bottom = ui.Rows[Math.Max(row + 1, ui.LastRow - 4 - relRows - (relRows > 0 ? 1 : 0))];
            var cardH = Math.Min(dy * 7.5f, (top - bottom) / cardRows);
            var cardW = width / perRow;
            var picH = Math.Max(dy * 1.5f, cardH - dy * 3.3f);
            for (var k = 0; k < shown.Count; k++)
            {
                var h = shown[k];
                var cx = left + cardW * (k % perRow + 0.5f);
                var cardTop = top - cardH * (k / perRow);
                var level = EstateEvent.HouseOf(town, h)?.Level ?? 1;
                var mine = IsPlayerClan(h.GetUnitId());
                EstateEvent.Picture(ui, EstateEvent.HouseSprite(level), cx, cardTop - picH / 2f, cardW - 24f, picH);
                var y = cardTop - picH - dy * 0.6f;
                ui.AddText(cx, y, ClanName(h)).Format(mine ? new Color(0.55f, 0.35f, 0f) : Color.black, 15, FontStyle.Bold);
                ui.AddText(cx, y - dy, string.Format(GameTool.LS("clan_card_members"), Members(h).Count, EstateEvent.HouseName(level))).Format(new Color(0.3f, 0.3f, 0.3f), 13);
                var elders = ClanElderEvent.ElderNames(town, h);
                if (elders.Count > 0)
                    ui.AddText(cx, y - dy * 2f, string.Format(GameTool.LS("clan_card_elders"), elders.Count > 2 ? string.Join(", ", elders.Take(2)) + ", ..." : string.Join(", ", elders)))
                        .Format(new Color(0.1f, 0.35f, 0.3f), 12);
            }
            // the relations: right under the cards, up to three columns
            if (relRows > 0)
            {
                var cardsBottom = top - cardH * cardRows;
                var relRow = row;
                while (relRow < ui.LastRow - 4 - relRows && ui.Rows[relRow] > cardsBottom - dy * 0.2f) relRow++;
                ui.AddText(ui.MidCol, relRow, GameTool.LS("clan_relations")).Format(new Color(0.55f, 0.35f, 0f), 14);
                var list = pairs.OrderBy(x => x.rel).Take(9).ToList();
                for (var i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    ui.AddText(left + width * (i % relCols + 0.5f) / relCols, ui.Rows[relRow + 1 + i / relCols], p.text)
                        .Format(p.rel <= Conf.HostileAt ? new Color(0.6f, 0.1f, 0.1f) : p.rel >= Conf.AllyAt ? new Color(0.1f, 0.45f, 0.1f) : Color.black, 13);
                }
            }
            ui.AddText(ui.MidCol, ui.LastRow - 1, GameTool.LS("clan_hint")).Format(Color.gray, 13);
            // the elders of the player's clan, its war council, the player's post (ClanElderEvent)
            ClanElderEvent.AddClansButtons(ui, town);
        }

        public static string RelationKey(int rel) =>
            rel <= Conf.WarAt ? "clan_rel_feud" : rel <= Conf.HostileAt ? "clan_rel_hostile" : rel >= Conf.AllyAt ? "clan_rel_allied" : "clan_rel_neutral";
    }
}
