using MOD_nE7UL2.Helper;
using EBattleTypeData;
using MOD_nE7UL2.Const;
using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Helper;
using ModLib.Mod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Clan elders (settings: "Elders" of ModConf/estate_configs.json), on the clans of ClanEvent:
    /// - An elder: a cultivator of no kin to the clan (not of the head's blood, not a member), of a grade high enough for
    ///   the area of the clan's town, who guards it; a clan keeps a few, by the level of its house. A unit is the elder of
    ///   one clan at most.
    /// - Paid by the clan: a salary a month by its grade, an offering a year. Loyalty 0..100 follows the pay and the wars;
    ///   an unpaid elder leaves at 0.
    /// - No say in the clan's affairs, but a vote on its wars: the head has HeadVotes votes, an elder 1; a war needs more
    ///   than half. The elders fight in the clan's wars (their power counts; they may fall in a lost one).
    /// - An elder who flees a war (low loyalty; the player who stays out of a war of its clan) is a deserter: the clan
    ///   hunts it for years.
    /// - The player invites elders (the candidates of the area, in the Elders window of its clan); NPC clans take NPC
    ///   elders and invite the player when its head trusts it (intimacy, reputation); the player may resign. The player's
    ///   clan declares war through a war council.
    /// </summary>
    [Cache(ModConst.CLAN_ELDER_EVENT)]
    public partial class ClanElderEvent : ModEvent
    {
        public static ClanElderEvent Instance { get; set; }

        public static EstateConfig._Elders Conf => EstateEvent.Conf.Elders;
        public static bool Enabled => ClanEvent.Conf.Enabled && Conf.Enabled;

        public class Elder
        {
            public string TownId { get; set; }
            /// <summary>Head of the clan guarded.</summary>
            public string Head { get; set; }
            public string UnitId { get; set; }
            /// <summary>Game month it joined.</summary>
            public int Since { get; set; }
            public int Loyalty { get; set; }
            /// <summary>Months not paid in a row.</summary>
            public int Unpaid { get; set; }
        }

        /// <summary>A deserter hunted by a clan.</summary>
        public class Hunt
        {
            public string TownId { get; set; }
            public string Head { get; set; }
            public string TargetId { get; set; }
            public int Since { get; set; }
            /// <summary>Monthly salary of the deserter (the compensation of the player is counted in it).</summary>
            public int Salary { get; set; }
            /// <summary>Fights the target won.</summary>
            public int Wins { get; set; }
        }

        /// <summary>A war council in which the player (an elder) is asked to vote.</summary>
        public class Council
        {
            public string TownId { get; set; }
            public string Head { get; set; }
            public string Enemy { get; set; }
        }

        public List<Elder> Elders { get; set; } = new List<Elder>();
        public List<Hunt> Hunts { get; set; } = new List<Hunt>();
        /// <summary>Unit id -> the year the player last invited it.</summary>
        public Dictionary<string, int> Asked { get; set; } = new Dictionary<string, int>();
        /// <summary>The year an NPC clan last invited the player.</summary>
        public int InvitedYear { get; set; } = -1;
        /// <summary>A hunt whose attack the player fights (resolved after the battle).</summary>
        public Hunt PendingHunt { get; set; }
        /// <summary>A war council waiting for the player's vote.</summary>
        public Council PendingCouncil { get; set; }

        private static WorldUnitBase Player => g.world.playerUnit;
        private static WorldUnitBase Unit(string id) => FamilyEvent.Unit(id);
        private static bool Alive(WorldUnitBase u) => u != null && !u.isDie;
        private static int Month => GameHelper.GetGameTotalMonth();
        private static MapBuildTown Town(string id) => ModMain.Store.Towns.FirstOrDefault(x => x.buildData.id == id);
        private static Func<double> Roll => () => CommonTool.Random(0f, 1f);

        // ------------------------------------------------------------------ formulas (pure, unit tests)

        /// <summary>Grade an elder needs in a town of the area: the area + GradeOverArea, 1..10.</summary>
        public static int MinGrade(int area) => Math.Max(1, Math.Min(10, area + Conf.GradeOverArea));

        /// <summary>Elders a clan can keep with a house of this level.</summary>
        public static int MaxElders(int houseLevel) => Conf.MaxElders[Math.Max(1, Math.Min(Conf.MaxElders.Length, houseLevel)) - 1];

        /// <summary>Monthly salary of an elder of the grade (before inflation).</summary>
        public static int BaseSalary(int grade) => Conf.Salary[Math.Max(1, Math.Min(Conf.Salary.Length, grade)) - 1];

        /// <summary>Loyalty of a new elder: StartLoyalty + intimacy with the head x LoyaltyPerIntim, 0..100.</summary>
        public static int StartLoyalty(float intim) => (int)Math.Max(0, Math.Min(100, Math.Round(Conf.StartLoyalty + intim * Conf.LoyaltyPerIntim)));

        /// <summary>Chance (0..0.95) that an NPC accepts the player's invitation; none when it dislikes the player.</summary>
        public static float AcceptChance(float intim, int reputation, int houseLevel, int gradeAbove) =>
            intim < 0 ? 0f : Math.Max(0f, Math.Min(0.95f, Conf.AcceptBase + intim / Conf.AcceptIntim + reputation / Conf.AcceptReputation
                + Conf.AcceptHouse * Math.Max(0, houseLevel - 1) - Conf.AcceptGrade * Math.Max(0, gradeAbove)));

        /// <summary>Chance (0.05..0.95) that an elder votes for a war: its loyalty and the clan's share of the power.</summary>
        public static double VoteYesChance(int loyalty, double ourPower, double theirPower)
        {
            var share = ourPower <= 0 && theirPower <= 0 ? 0.5 : ourPower / Math.Max(1.0, ourPower + theirPower);
            return Math.Max(0.05, Math.Min(0.95, loyalty / 200.0 + share * 0.8 - 0.2));
        }

        /// <summary>A war council: the head's HeadVotes and one vote an elder; more than half of all the votes declares the war.</summary>
        public static bool Passes(bool headYes, int elderYes, int elders) => ((headYes ? Conf.HeadVotes : 0) + elderYes) * 2 > Conf.HeadVotes + elders;

        /// <summary>Chance that an elder flees a war: (FleeLoyalty - loyalty) / FleeLoyalty, none at FleeLoyalty or more.</summary>
        public static double FleeChance(int loyalty) => loyalty >= Conf.FleeLoyalty ? 0 : (Conf.FleeLoyalty - Math.Max(0, loyalty)) / (double)Conf.FleeLoyalty;

        // ------------------------------------------------------------------ queries

        public static int Area(MapBuildTown town) => town?.gridData.areaBaseID ?? 1;
        public static int MinGrade(MapBuildTown town) => MinGrade(Area(town));
        public static int HouseLevel(MapBuildTown town, WorldUnitBase head) => EstateEvent.HouseOf(town, head)?.Level ?? 1;
        public static int MaxElders(MapBuildTown town, WorldUnitBase head) => MaxElders(HouseLevel(town, head));

        /// <summary>Monthly salary of an elder (its grade, inflation).</summary>
        public static int Salary(WorldUnitBase u) => u == null ? 0 : InflationaryEvent.CalculateInflationary(BaseSalary(u.GetGradeLvl()));

        /// <summary>The elders of a clan of a town.</summary>
        public static List<Elder> Of(string townId, string head) =>
            Instance?.Elders.Where(x => x.TownId == townId && x.Head == head).ToList() ?? new List<Elder>();
        public static List<Elder> Of(MapBuildTown town, WorldUnitBase head) => town == null || head == null ? new List<Elder>() : Of(town.buildData.id, head.GetUnitId());

        /// <summary>The post of a unit (one at most).</summary>
        public static Elder PostOf(string unitId) => Instance?.Elders.FirstOrDefault(x => x.UnitId == unitId);
        public static Elder PlayerPost => Player == null ? null : PostOf(Player.GetUnitId());

        /// <summary>The player guards this clan.</summary>
        public static bool IsPlayerElderOf(MapBuildTown town, WorldUnitBase head)
        {
            var p = PlayerPost;
            return p != null && town != null && head != null && p.TownId == town.buildData.id && p.Head == head.GetUnitId();
        }

        public static List<Hunt> HuntsOn(string targetId) => Instance?.Hunts.Where(x => x.TargetId == targetId).ToList() ?? new List<Hunt>();

        /// <summary>The unit is kin of the head: a member of its clan, of its blood, a sibling, or the head itself.</summary>
        public static bool IsKin(WorldUnitBase head, WorldUnitBase u)
        {
            if (head == null || u == null) return false;
            var id = u.GetUnitId();
            if (id == head.GetUnitId()) return true;
            if (ClanEvent.Members(head).Any(x => x.GetUnitId() == id)) return true;
            if (FamilyEvent.Blood(head).Contains(id) || FamilyEvent.Blood(u).Contains(head.GetUnitId())) return true;
            return FamilyEvent.Family(head).Siblings.Contains(id);
        }

        /// <summary>Why a unit cannot guard the clan (a LocalText key), null when it can.</summary>
        public static string CannotServe(MapBuildTown town, WorldUnitBase head, WorldUnitBase u, bool countRoom = true)
        {
            if (!Alive(u) || !Alive(head)) return "elder_err_dead";
            if (IsKin(head, u)) return "elder_err_kin";
            if (u.GetGradeLvl() < MinGrade(town)) return "elder_err_grade";
            if (PostOf(u.GetUnitId()) != null) return "elder_err_busy";
            if (ClanEvent.Heads(town).Contains(u.GetUnitId())) return "elder_err_head";
            if (HuntsOn(u.GetUnitId()).Any(x => x.Head == head.GetUnitId())) return "elder_err_hunted";
            if (countRoom && Of(town, head).Count >= MaxElders(town, head)) return "elder_err_full";
            return null;
        }

        /// <summary>The power of the elders of a clan (a war).</summary>
        public static double Power(MapBuildTown town, WorldUnitBase head) =>
            Of(town, head).Select(x => Unit(x.UnitId)).Where(Alive).Sum(x => (double)UnitPowerCalHelper.CalWUnitBattlePower(x));

        private static float Intim(WorldUnitBase from, WorldUnitBase to) => from == null || to == null ? 0 : from.data.unitData.relationData.GetIntim(to.GetUnitId());

        /// <summary>The player is in it: its clan, its post, or it stands in the town.</summary>
        private static bool Related(MapBuildTown town, string head, string unitId = null) =>
            Player != null && (head == Player.GetUnitId() || unitId == Player.GetUnitId() || Player.GetMapBuild<MapBuildTown>()?.buildData.id == town?.buildData.id);

        // ------------------------------------------------------------------ joining and leaving

        /// <summary>The unit becomes an elder of the clan.</summary>
        public static Elder Appoint(MapBuildTown town, WorldUnitBase head, WorldUnitBase u)
        {
            var e = new Elder { TownId = town.buildData.id, Head = head.GetUnitId(), UnitId = u.GetUnitId(), Since = Month, Loyalty = StartLoyalty(Intim(u, head)) };
            Instance.Elders.Add(e);
            TaoistLogEvent.LogIf(Related(town, e.Head, e.UnitId), "log_clan_elder_join", town.name, u.GetName(), ClanEvent.ClanName(head));
            return e;
        }

        /// <summary>An elder leaves (logKey: why, null: not logged).</summary>
        public static void Leave(Elder e, string logKey)
        {
            Instance.Elders.Remove(e);
            var town = Town(e.TownId);
            if (logKey != null && town != null)
                TaoistLogEvent.LogIf(Related(town, e.Head, e.UnitId), logKey, town.name, Unit(e.UnitId)?.GetName() ?? "-", ClanEvent.ClanName(Unit(e.Head)));
        }

        /// <summary>An elder deserts the clan (fled a war): it leaves and the clan hunts it.</summary>
        public static void Desert(Elder e)
        {
            Outlaw(e);
            var u = Unit(e.UnitId); var head = Unit(e.Head);
            var town = Town(e.TownId);
            TaoistLogEvent.Log("log_clan_elder_fled", town?.name ?? "-", u?.GetName() ?? "-", ClanEvent.ClanName(head));
        }

        /// <summary>An elder leaves its clan as a traitor (a deserter, a turncoat): the clan hunts it, its members' intimacy -100.</summary>
        public static void Outlaw(Elder e)
        {
            Instance.Elders.Remove(e);
            var u = Unit(e.UnitId); var head = Unit(e.Head);
            Instance.Hunts.RemoveAll(x => x.TargetId == e.UnitId && x.Head == e.Head);
            Instance.Hunts.Add(new Hunt { TownId = e.TownId, Head = e.Head, TargetId = e.UnitId, Since = Month, Salary = Salary(u) });
            foreach (var m in ClanEvent.Members(head).Where(x => !x.IsPlayer()))
                if (u != null) FamilyEvent.ChangeIntim(m.data.unitData.relationData, e.UnitId, -100);
        }

        /// <summary>The head of the clan died and its heir took over: the elders and the hunts follow; the elders lose SuccessionLoyalty.</summary>
        public static void OnSuccession(string townId, string deadId, string heirId)
        {
            if (Instance == null) return;
            foreach (var e in Instance.Elders.Where(x => x.TownId == townId && x.Head == deadId).ToList())
            {
                // an elder who is the heir (kin now) leaves the post
                if (e.UnitId == heirId) { Instance.Elders.Remove(e); continue; }
                e.Head = heirId;
                e.Loyalty = Math.Max(0, e.Loyalty - Conf.SuccessionLoyalty);
            }
            foreach (var h in Instance.Hunts.Where(x => x.TownId == townId && x.Head == deadId))
                h.Head = heirId;
        }

        /// <summary>The player goes on as its child (cid): the old player's post and the hunts on it end; the child's are the player's.</summary>
        public static void OnPlayerInherit(string pid, string cid)
        {
            if (Instance == null) return;
            Instance.Elders.RemoveAll(x => x.UnitId == pid);
            Instance.Hunts.RemoveAll(x => x.TargetId == pid);
            foreach (var e in Instance.Elders.Where(x => x.UnitId == cid)) e.UnitId = pid;
            foreach (var e in Instance.Elders.Where(x => x.Head == cid)) e.Head = pid;
            foreach (var h in Instance.Hunts.Where(x => x.TargetId == cid)) h.TargetId = pid;
            foreach (var h in Instance.Hunts.Where(x => x.Head == cid)) h.Head = pid;
            Instance.Elders.RemoveAll(x => x.UnitId == x.Head);
        }

        // ------------------------------------------------------------------ war

        public class Ballot
        {
            public string Name { get; set; }
            public int Votes { get; set; }
            public bool Yes { get; set; }
            public bool IsHead { get; set; }
        }

        /// <summary>
        /// The war council of a clan on a war against another: the head (HeadVotes), each elder (1, for with
        /// VoteYesChance; playerYes: the player's vote when it is an elder). Returns the ballots; passed: the war declared.
        /// </summary>
        public static List<Ballot> Vote(MapBuildTown town, WorldUnitBase head, WorldUnitBase enemy, bool headYes, Func<double> roll, out bool passed, bool? playerYes = null)
        {
            var ours = ClanEvent.SidePower(town, head, null, true);
            var theirs = ClanEvent.SidePower(town, enemy, null, true);
            var rs = new List<Ballot> { new Ballot { Name = head.GetName(), Votes = Conf.HeadVotes, Yes = headYes, IsHead = true } };
            foreach (var e in Of(town, head))
            {
                var u = Unit(e.UnitId);
                if (!Alive(u)) continue;
                var yes = u.IsPlayer() && playerYes.HasValue ? playerYes.Value : roll() < VoteYesChance(e.Loyalty, ours, theirs);
                rs.Add(new Ballot { Name = u.GetName(), Votes = 1, Yes = yes });
            }
            passed = Passes(headYes, rs.Count(x => !x.IsHead && x.Yes), rs.Count - 1);
            return rs;
        }

        /// <summary>
        /// A clan of the town is about to start a war (ClanEvent, yearly): its elders vote. True: the war starts now;
        /// false: voted down, or the player (an elder) is asked to vote (the war starts after its answer).
        /// </summary>
        public static bool WarAllowed(MapBuildTown town, WorldUnitBase head, WorldUnitBase enemy, Func<double> roll)
        {
            if (!Enabled || Instance == null || head.IsPlayer() || Of(town, head).Count == 0) return true;
            if (IsPlayerElderOf(town, head) && ClanEvent.CanAsk() && Instance.PendingCouncil == null)
            {
                AskVote(town, head, enemy);
                return false;
            }
            Vote(town, head, enemy, true, roll, out var passed);
            if (!passed)
                TaoistLogEvent.LogIf(Related(town, head.GetUnitId()), "log_clan_elder_voted_down", town.name, ClanEvent.ClanName(head), ClanEvent.ClanName(enemy));
            return passed;
        }

        /// <summary>The player, an elder of the clan, is asked to vote on its war.</summary>
        public static void AskVote(MapBuildTown town, WorldUnitBase head, WorldUnitBase enemy)
        {
            Instance.PendingCouncil = new Council { TownId = town.buildData.id, Head = head.GetUnitId(), Enemy = enemy.GetUnitId() };
            var text = string.Format(GameTool.LS("elder_council_ask"), town.name, ClanEvent.ClanName(head), ClanEvent.ClanName(enemy), Conf.HeadVotes);
            DramaHelper.OpenDrama1(text, new List<string> { GameTool.LS("elder_vote_yes"), GameTool.LS("elder_vote_no") }, x => OnVote(x.id % 10 == 0), head, Player);
        }

        /// <summary>The player voted in the war council of its clan: the votes are counted, the war starts when it passes.</summary>
        public static void OnVote(bool yes)
        {
            var c = Instance.PendingCouncil;
            Instance.PendingCouncil = null;
            var town = Town(c?.TownId);
            var head = Unit(c?.Head); var enemy = Unit(c?.Enemy);
            if (town == null || !Alive(head) || !Alive(enemy)) return;
            var ballots = Vote(town, head, enemy, true, Roll, out var passed, yes);
            ShowBallots(town, head, enemy, ballots, passed, passed ? (Action)(() => ClanEvent.Start(town, head, enemy, true, Roll)) : null);
        }

        /// <summary>Before a war: an elder of low loyalty of either side may flee (a deserter, hunted).</summary>
        public static void BeforeWar(MapBuildTown town, WorldUnitBase a, WorldUnitBase b, Func<double> roll)
        {
            if (!Enabled || Instance == null) return;
            foreach (var e in Of(town, a).Concat(Of(town, b)).ToList())
            {
                var u = Unit(e.UnitId);
                if (!Alive(u)) { Instance.Elders.Remove(e); continue; }
                if (!u.IsPlayer() && roll() < FleeChance(e.Loyalty)) Desert(e);
            }
        }

        /// <summary>After a war: the elders of the losers may fall (WarDeath x ElderDeath; the player never), the others are hurt; loyalty +- WarLoyalty. Returns the fallen.</summary>
        public static int AfterWar(MapBuildTown town, WorldUnitBase winner, WorldUnitBase loser, Func<double> roll)
        {
            if (!Enabled || Instance == null) return 0;
            var fallen = 0;
            foreach (var e in Of(town, loser))
            {
                var u = Unit(e.UnitId);
                e.Loyalty = Math.Max(0, e.Loyalty - Conf.WarLoyalty);
                if (!Alive(u) || u.IsPlayer()) continue;
                if (roll() < ClanEvent.Conf.WarDeath * Conf.ElderDeath) { u.Die(); fallen++; }
                else ClanEvent.Hurt(u);
            }
            foreach (var e in Of(town, winner))
                e.Loyalty = Math.Min(100, e.Loyalty + Conf.WarLoyalty);
            Instance.Elders.RemoveAll(x => !Alive(Unit(x.UnitId)));
            return fallen;
        }

        /// <summary>The player stayed out of a war of the clan it guards: a deserter.</summary>
        public static void OnPlayerStayedOut(MapBuildTown town, WorldUnitBase head)
        {
            if (!IsPlayerElderOf(town, head)) return;
            Desert(PlayerPost);
            InfoDialog.Msg(town.name, string.Format(GameTool.LS("elder_deserted"), ClanEvent.ClanName(head)));
        }

        /// <summary>
        /// The player's clan declares war on another clan of the town: the war council votes (the player: HeadVotes, for);
        /// passed: elders may flee, then the battle against the enemy's champion decides the war.
        /// </summary>
        public static void DeclareWar(MapBuildTown town, WorldUnitBase enemy)
        {
            var ballots = Vote(town, Player, enemy, true, Roll, out var passed);
            ShowBallots(town, Player, enemy, ballots, passed, passed ? (Action)(() => StartPlayerWar(town, enemy)) : null);
        }

        /// <summary>The war declared by the player's clan: elders of low loyalty may flee, the battle against the enemy's champion.</summary>
        public static void StartPlayerWar(MapBuildTown town, WorldUnitBase enemy)
        {
            BeforeWar(town, Player, enemy, Roll);
            var their = ClanEvent.Fighter(enemy, true);
            if (their == null) return;
            ClanEvent.Instance.Pending = new ClanEvent.Fight
            {
                TownId = town.buildData.id, War = true, Ours = Player.GetUnitId(), Theirs = enemy.GetUnitId(),
                OurFighter = (ClanEvent.Fighter(Player, true) ?? Player).GetUnitId(), TheirFighter = their.GetUnitId(),
            };
            ClanEvent.OnAnswer(0);
        }

        // ------------------------------------------------------------------ monthly / yearly

        [EventCondition(IsInGame = HandleEnum.True)]
        public override void OnMonthly()
        {
            base.OnMonthly();
            if (!Enabled) return;
            try { Clean(); } catch (Exception ex) { DebugHelper.WriteLine($"ClanElderEvent.Clean: {ex}"); }
            try { PayMonth(); } catch (Exception ex) { DebugHelper.WriteLine($"ClanElderEvent.PayMonth: {ex}"); }
            try { HuntMonth(Roll); } catch (Exception ex) { DebugHelper.WriteLine($"ClanElderEvent.HuntMonth: {ex}"); }
        }

        [EventCondition(IsInGame = HandleEnum.True)]
        public override void OnYearly()
        {
            base.OnYearly();
            if (!Enabled) return;
            try { Offering(); } catch (Exception ex) { DebugHelper.WriteLine($"ClanElderEvent.Offering: {ex}"); }
            foreach (var town in ModMain.Store.Towns)
            {
                try { Recruit(town, Roll); } catch (Exception ex) { DebugHelper.WriteLine($"ClanElderEvent.Recruit {town?.name}: {ex}"); }
            }
            try { InvitePlayer(Roll); } catch (Exception ex) { DebugHelper.WriteLine($"ClanElderEvent.InvitePlayer: {ex}"); }
        }

        /// <summary>Elders gone: dead, their clan gone (the head dead or no clan any more), kin now (a marriage).</summary>
        public static void Clean()
        {
            foreach (var e in Instance.Elders.ToList())
            {
                var u = Unit(e.UnitId); var head = Unit(e.Head); var town = Town(e.TownId);
                if (!Alive(u) || town == null) { Instance.Elders.Remove(e); continue; }
                if (!Alive(head) || !ClanEvent.Heads(town).Contains(e.Head)) { Leave(e, "log_clan_elder_clan_gone"); continue; }
                if (IsKin(head, u)) Instance.Elders.Remove(e);
            }
            Instance.Hunts.RemoveAll(h => !Alive(Unit(h.TargetId)) || !Alive(Unit(h.Head)) || Month - h.Since >= Conf.HuntMonths);
        }

        /// <summary>A month of pay: the clan pays each elder its salary (an unpaid elder loses loyalty, leaves at 0).</summary>
        public static void PayMonth()
        {
            foreach (var e in Instance.Elders.ToList())
            {
                var u = Unit(e.UnitId); var head = Unit(e.Head);
                if (!Alive(u) || !Alive(head)) continue;
                if (Pay(head, u, Salary(u)))
                {
                    e.Unpaid = 0;
                    e.Loyalty = Math.Min(100, e.Loyalty + Conf.PaidLoyalty);
                }
                else
                {
                    e.Unpaid++;
                    e.Loyalty = Math.Max(0, e.Loyalty - Conf.UnpaidLoyalty);
                    if (e.Loyalty <= 0) Leave(e, "log_clan_elder_left_unpaid");
                }
            }
        }

        /// <summary>The clan (its head) pays an elder: the player's clan only with the money the player has; an NPC clan always pays the player (from the head as far as it can).</summary>
        public static bool Pay(WorldUnitBase head, WorldUnitBase elder, int amount)
        {
            if (amount <= 0) return true;
            if (head.IsPlayer())
            {
                if (head.GetUnitMoney() < amount) return false;
                head.AddUnitMoney(-amount);
                elder.AddUnitMoney(amount);
                return true;
            }
            var fromHead = Math.Min(amount, Math.Max(0, head.GetUnitMoney()));
            if (!elder.IsPlayer() && fromHead < amount) return false;
            head.AddUnitMoney(-fromHead);
            elder.AddUnitMoney(amount);
            return true;
        }

        /// <summary>The yearly offering: OfferingMonths salaries (the player's clan: when it can).</summary>
        public static void Offering()
        {
            foreach (var e in Instance.Elders.ToList())
            {
                var u = Unit(e.UnitId); var head = Unit(e.Head);
                if (!Alive(u) || !Alive(head)) continue;
                var amount = Salary(u) * Conf.OfferingMonths;
                if (Pay(head, u, amount) && (u.IsPlayer() || head.IsPlayer()))
                    TaoistLogEvent.Log("log_clan_elder_offering", Town(e.TownId)?.name ?? "-", u.GetName(), ClanEvent.ClanName(head), amount);
            }
        }

        /// <summary>The candidates of the area for an elder of the clan (NPCs who could serve), the most friendly first.</summary>
        public static List<WorldUnitBase> Candidates(MapBuildTown town, WorldUnitBase head, int max = 8)
        {
            var area = Area(town);
            return (ModMain.Store?.WUnits ?? new WorldUnitBase[0])
                .Where(x => Alive(x) && !x.IsPlayer() && x.GetUnitPosAreaId() == area && !HirePeopleEvent.IsHired(x) && CannotServe(town, head, x, false) == null)
                .OrderByDescending(x => Intim(x, head)).ThenByDescending(x => x.GetGradeLvl())
                .Take(max).ToList();
        }

        /// <summary>A year of an NPC clan of the town: with RecruitChance it takes an NPC elder (one who does not dislike its head).</summary>
        public static int Recruit(MapBuildTown town, Func<double> roll)
        {
            var n = 0;
            foreach (var hid in ClanEvent.Heads(town))
            {
                var head = Unit(hid);
                if (!Alive(head) || head.IsPlayer() || Of(town, head).Count >= MaxElders(town, head) || roll() >= Conf.RecruitChance) continue;
                var u = Candidates(town, head, 3).FirstOrDefault(x => Intim(x, head) >= 0);
                if (u == null) continue;
                Appoint(town, head, u);
                n++;
            }
            return n;
        }

        /// <summary>The NPC clans that would invite the player now (it trusts the player: intimacy, reputation; the player can serve).</summary>
        public static List<(MapBuildTown town, WorldUnitBase head)> Inviters()
        {
            var rs = new List<(MapBuildTown, WorldUnitBase)>();
            if (Player == null || PlayerPost != null || Player.GetDynProperty(UnitDynPropertyEnum.Reputation).value < Conf.InviteReputation) return rs;
            foreach (var town in ModMain.Store.Towns)
                foreach (var hid in ClanEvent.Heads(town))
                {
                    var head = Unit(hid);
                    if (!Alive(head) || head.IsPlayer() || Intim(head, Player) < Conf.InviteIntim) continue;
                    if (EstateEvent.HasHouse(town, Player) && ClanEvent.Heads(town).Contains(Player.GetUnitId()) && ClanEvent.Relation(town, Player, head) <= ClanEvent.Conf.HostileAt) continue;
                    if (CannotServe(town, head, Player) == null) rs.Add((town, head));
                }
            return rs;
        }

        /// <summary>Once a year at most, with InviteChance: the NPC clan that trusts the player most invites it as an elder.</summary>
        public static bool InvitePlayer(Func<double> roll)
        {
            var year = GameHelper.GetGameYear();
            if (Instance.InvitedYear == year || !ClanEvent.CanAsk() || Instance.PendingCouncil != null || Instance.PendingHunt != null) return false;
            var best = Inviters().OrderByDescending(x => Intim(x.head, Player)).FirstOrDefault();
            if (best.head == null || roll() >= Conf.InviteChance) return false;
            Instance.InvitedYear = year;
            AskInvite(best.town, best.head);
            return true;
        }

        /// <summary>An NPC clan invites the player as an elder (a drama): accept or decline.</summary>
        public static void AskInvite(MapBuildTown town, WorldUnitBase head)
        {
            var text = string.Format(GameTool.LS("elder_invited"), ClanEvent.ClanName(head), town.name, Salary(Player), Conf.OfferingMonths, Conf.HeadVotes);
            DramaHelper.OpenDrama1(text, new List<string> { GameTool.LS("elder_accept"), GameTool.LS("elder_decline") }, x =>
            {
                if (x.id % 10 != 0 || CannotServe(town, head, Player) != null) return;
                var e = Appoint(town, head, Player);
                e.Loyalty = 100;
                InfoDialog.Msg(town.name, string.Format(GameTool.LS("elder_joined"), ClanEvent.ClanName(head), Salary(Player)));
            }, head, Player);
        }

        /// <summary>The player invites an NPC to guard its clan: it accepts with AcceptChance (asked once a year). Returns a LocalText key of the result.</summary>
        public static string Invite(MapBuildTown town, WorldUnitBase u, Func<double> roll)
        {
            var err = CannotServe(town, Player, u);
            if (err != null) return err;
            var year = GameHelper.GetGameYear();
            if (Instance.Asked.TryGetValue(u.GetUnitId(), out var y) && y == year) return "elder_err_asked";
            Instance.Asked[u.GetUnitId()] = year;
            if (roll() >= ChanceOf(town, u)) return "elder_refused";
            Appoint(town, Player, u);
            return "elder_accepted";
        }

        /// <summary>Chance that an NPC accepts to guard the player's clan.</summary>
        public static float ChanceOf(MapBuildTown town, WorldUnitBase u) =>
            AcceptChance(Intim(u, Player), Player.GetDynProperty(UnitDynPropertyEnum.Reputation).value, HouseLevel(town, Player), u.GetGradeLvl() - Player.GetGradeLvl());

        // ------------------------------------------------------------------ hunts

        /// <summary>The one who goes after a deserter: the strongest member or elder of the clan (not the player).</summary>
        public static WorldUnitBase Hunter(Hunt h)
        {
            var town = Town(h.TownId); var head = Unit(h.Head);
            if (town == null || !Alive(head)) return null;
            return ClanEvent.Members(head).Concat(Of(town, head).Select(x => Unit(x.UnitId)))
                .Where(x => Alive(x) && !x.IsPlayer() && x.GetUnitId() != h.TargetId)
                .OrderByDescending(x => UnitPowerCalHelper.CalWUnitBattlePower(x)).FirstOrDefault();
        }

        /// <summary>A month of the hunts: each attacks with HuntChance; an NPC deserter fights the hunter, the player is ambushed (one a month).</summary>
        public static void HuntMonth(Func<double> roll)
        {
            var asked = false;
            foreach (var h in Instance.Hunts.ToList())
            {
                if (roll() >= Conf.HuntChance) continue;
                var target = Unit(h.TargetId); var hunter = Hunter(h);
                if (!Alive(target) || hunter == null) continue;
                if (target.IsPlayer())
                {
                    if (asked || !ClanEvent.CanAsk() || Instance.PendingHunt != null || Instance.PendingCouncil != null) continue;
                    asked = true;
                    Ambush(h, hunter);
                    continue;
                }
                ResolveHunt(h, hunter, roll() < ClanEvent.WinChance(UnitPowerCalHelper.CalWUnitBattlePower(hunter), UnitPowerCalHelper.CalWUnitBattlePower(target)), roll);
            }
        }

        /// <summary>The end of an attack (hunterWon): the loser is hurt; an NPC deserter beaten dies with HuntDeath (the hunt ends); the player wins HuntWins fights to end it.</summary>
        public static void ResolveHunt(Hunt h, WorldUnitBase hunter, bool hunterWon, Func<double> roll)
        {
            var target = Unit(h.TargetId);
            var town = Town(h.TownId);
            var clan = ClanEvent.ClanName(Unit(h.Head));
            if (hunterWon)
            {
                ClanEvent.Hurt(target);
                if (Alive(target) && !target.IsPlayer() && roll() < Conf.HuntDeath)
                {
                    target.Die();
                    Instance.Hunts.Remove(h);
                    TaoistLogEvent.Log("log_clan_elder_hunted_down", town?.name ?? "-", target.GetName(), clan, hunter.GetName());
                }
                return;
            }
            ClanEvent.Hurt(hunter);
            h.Wins++;
            if (h.Wins >= Conf.HuntWins)
            {
                Instance.Hunts.Remove(h);
                TaoistLogEvent.LogIf(target.IsPlayer(), "log_clan_elder_hunt_over", town?.name ?? "-", target.GetName(), clan);
            }
        }

        /// <summary>Compensation the player pays to end a hunt.</summary>
        public static int Compensation(Hunt h) => InflationaryEvent.CalculateInflationary(Math.Max(1, h.Salary) * Conf.CompensationMonths);

        /// <summary>Hunters of the clan catch up with the player: fight (a real battle against the hunter) or pay the compensation.</summary>
        public static void Ambush(Hunt h, WorldUnitBase hunter)
        {
            var text = string.Format(GameTool.LS("elder_hunt_ask"), hunter.GetName(), ClanEvent.ClanName(Unit(h.Head)), h.Wins, Conf.HuntWins);
            DramaHelper.OpenDrama1(text, new List<string> { GameTool.LS("elder_hunt_fight"), string.Format(GameTool.LS("elder_hunt_pay"), Compensation(h)) },
                x => OnAmbush(h, hunter, x.id % 10), hunter, Player);
        }

        /// <summary>0: fight the hunter; 1: pay the compensation (the hunt ends; not enough money: the hunter attacks anyway).</summary>
        public static void OnAmbush(Hunt h, WorldUnitBase hunter, int opt)
        {
            // the hunt ended meanwhile (paid, given up)
            if (!Instance.Hunts.Contains(h)) return;
            if (opt == 1 && Player.GetUnitMoney() >= Compensation(h))
            {
                var head = Unit(h.Head);
                Player.AddUnitMoney(-Compensation(h));
                if (Alive(head)) head.AddUnitMoney(Compensation(h));
                Instance.Hunts.Remove(h);
                TaoistLogEvent.Log("log_clan_elder_hunt_paid", Town(h.TownId)?.name ?? "-", ClanEvent.ClanName(head), Compensation(h));
                return;
            }
            if (!Alive(hunter) || GameHelper.IsInBattlle()) return;
            Instance.PendingHunt = h;
            hunter.SetUnitPos(Player.GetUnitPos());
            g.world.battle.IntoBattle(hunter, BattleHelper.NPC_FIGHT_DUNGEON_ID, null, new WorldBattleData
            {
                npcUnit = hunter,
                isRealBattle = true,
                npcWinNotKillPlayerUnit = true,
                npcWinInsultPlayerUnit = false,
            });
        }

        /// <summary>The battle against a hunter ended.</summary>
        public override void OnBattleEndHandler(BattleEndHandler e)
        {
            base.OnBattleEndHandler(e);
            try { EndWarBattle(e.isWin); } catch (Exception ex) { DebugHelper.WriteLine($"ClanElderEvent.EndWarBattle: {ex}"); }
            var h = Instance?.PendingHunt;
            if (h == null) return;
            Instance.PendingHunt = null;
            if (!Instance.Hunts.Contains(h)) return;
            var hunter = Hunter(h);
            if (hunter != null) ResolveHunt(h, hunter, !e.isWin, Roll);
            var town = Town(h.TownId);
            InfoDialog.Msg(town?.name ?? "-", e.isWin
                ? string.Format(GameTool.LS(Instance.Hunts.Contains(h) ? "elder_hunt_won" : "elder_hunt_ended"), ClanEvent.ClanName(Unit(h.Head)), h.Wins, Conf.HuntWins)
                : string.Format(GameTool.LS("elder_hunt_lost"), ClanEvent.ClanName(Unit(h.Head))));
        }
    }
}
