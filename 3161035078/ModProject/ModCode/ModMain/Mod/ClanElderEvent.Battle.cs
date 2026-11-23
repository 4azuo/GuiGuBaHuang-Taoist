using MOD_nE7UL2.Helper;
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
    /// Clan elders in the real battle of a clan war (the player steps in, or its clan declared the war): the elders of both
    /// clans join on their side; an elder may betray its clan in the middle of the battle and fight for the other side,
    /// only when many conditions hold (BetrayChance). A turncoat is hunted by the clan it betrayed; when the side it joined
    /// wins and it can serve there, it becomes an elder of that clan.
    /// </summary>
    public partial class ClanElderEvent
    {
        /// <summary>An elder fighting in the battle.</summary>
        private sealed class BattleElder
        {
            public Elder Post;
            public UnitCtrlBase Unit;
            /// <summary>Joined on the player's side.</summary>
            public bool PlayerSide;
        }

        /// <summary>An elder who changed sides in the battle.</summary>
        private sealed class Turncoat
        {
            public string UnitId;
            public string TownId;
            /// <summary>The head of the clan it went over to.</summary>
            public string ToHead;
            /// <summary>Went over to the player's side.</summary>
            public bool ToPlayerSide;
        }

        // the clan war of the battle (a copy: ClanEvent clears its Pending first after the battle); runtime only
        private static ClanEvent.Fight warFight;
        /// <summary>Seconds of the clan war battle so far (runtime; set by the tests).</summary>
        public static int WarSeconds { get; set; }
        /// <summary>Power of the player's side and of the other at the start of the war (runtime; set by the tests).</summary>
        public static double PowerOurs { get; set; }
        public static double PowerTheirs { get; set; }
        /// <summary>A clan war battle is being fought.</summary>
        public static bool InWarBattle => warFight != null;
        /// <summary>The battle unit of an elder who joined the battle (null: not in it).</summary>
        public static UnitCtrlBase BattleUnitOf(string unitId) => battleElders.FirstOrDefault(x => x.Post.UnitId == unitId)?.Unit;
        private static readonly List<BattleElder> battleElders = new List<BattleElder>();
        private static readonly List<Turncoat> turncoats = new List<Turncoat>();

        // ------------------------------------------------------------------ formulas (pure, unit tests)

        /// <summary>A motive to betray: a selfish, evil or power-hungry nature, unpaid months, or closer to the other head (BetrayIntimGap) than to its own.</summary>
        public static bool BetrayMotive(bool badNature, int unpaid, float intimOwnHead, float intimOtherHead) =>
            badNature || unpaid > 0 || intimOtherHead - intimOwnHead >= Conf.BetrayIntimGap;

        /// <summary>
        /// Chance a second that an elder betrays its clan in the battle; 0 unless all hold: a motive, BetrayAfterSeconds
        /// passed, loyalty under BetrayLoyalty, served less than BetrayLoyalMonths, its side's leader at BetrayLeaderHp of
        /// its HP or less, the other side at least BetrayPowerRatio x its side's power. Then BetrayRate x
        /// (BetrayLoyalty - loyalty) / BetrayLoyalty.
        /// </summary>
        public static double BetrayChance(bool motive, int seconds, int loyalty, int servedMonths, double leaderHp, double ownPower, double otherPower)
        {
            if (!motive || Conf.BetrayLoyalty <= 0 || seconds < Conf.BetrayAfterSeconds || loyalty >= Conf.BetrayLoyalty ||
                servedMonths >= Conf.BetrayLoyalMonths || leaderHp > Conf.BetrayLeaderHp || otherPower < ownPower * Math.Round((double)Conf.BetrayPowerRatio, 4))
                return 0;
            return Conf.BetrayRate * (Conf.BetrayLoyalty - Math.Max(0, loyalty)) / (double)Conf.BetrayLoyalty;
        }

        /// <summary>A nature that betrays (as the team members of BattleEvent): inner trait selfish or evil, an outer trait power-hungry.</summary>
        public static bool BadNature(WorldUnitBase u)
        {
            var p = u?.data?.unitData?.propertyData;
            return p != null && (BattleEvent.betrayInTraits.Contains(p.inTrait) || BattleEvent.betrayOutTraits.Contains(p.outTrait1) || BattleEvent.betrayOutTraits.Contains(p.outTrait2));
        }

        // ------------------------------------------------------------------ battle

        /// <summary>The battle of a clan war starts (ClanEvent.Pending, a war): the elders join after JoinSeconds.</summary>
        public override void OnBattleStart(ETypeData e)
        {
            base.OnBattleStart(e);
            ResetWarBattle();
            var f = ClanEvent.Instance?.Pending;
            if (!Enabled || !Conf.BattleJoin || f == null || !f.War) return;
            var town = Town(f.TownId);
            var ours = Unit(f.Ours); var theirs = Unit(f.Theirs);
            if (town == null || !Alive(ours) || !Alive(theirs)) return;
            warFight = f;
            PowerOurs = ClanEvent.SidePower(town, ours, null, true);
            PowerTheirs = ClanEvent.SidePower(town, theirs, null, true);
        }

        /// <summary>Every second of a clan war battle: the elders join, then may betray.</summary>
        [ErrorIgnore]
        [EventCondition(IsInGame = HandleEnum.Ignore, IsInBattle = HandleEnum.True)]
        public override void OnTimeUpdate1000ms()
        {
            base.OnTimeUpdate1000ms();
            if (warFight == null || !BattleHelper.BattleStarted || ModBattleEvent.PlayerUnit == null || ModBattleEvent.PlayerUnit.isDie) return;
            WarSeconds++;
            if (WarSeconds == Math.Max(1, Conf.JoinSeconds))
                JoinBattle();
            else if (WarSeconds > Conf.JoinSeconds)
                TryBetray(Roll);
        }

        /// <summary>The elders of both clans (not the player) join the battle on their side. Returns how many joined.</summary>
        public static int JoinBattle()
        {
            var f = warFight;
            var town = Town(f?.TownId);
            if (town == null) return 0;
            foreach (var (head, playerSide) in new[] { (Unit(f.Ours), true), (Unit(f.Theirs), false) })
            {
                foreach (var e in Of(town, head))
                {
                    var u = Unit(e.UnitId);
                    if (!Alive(u) || u.IsPlayer()) continue;
                    foreach (var c in BattleEvent.NPCJoin(playerSide ? UnitType.PlayerNPC : UnitType.Monst, u))
                        battleElders.Add(new BattleElder { Post = e, Unit = c, PlayerSide = playerSide });
                }
            }
            if (battleElders.Count > 0)
                DebugHelper.WriteLine($"Clan elders join the war battle: {string.Join(", ", battleElders.Select(x => $"{Unit(x.Post.UnitId)?.GetName()} ({(x.PlayerSide ? "ours" : "theirs")})"))}");
            return battleElders.Count;
        }

        /// <summary>HP share of a battle unit (0 dead, 1 unknown).</summary>
        private static double HpShare(UnitCtrlBase c)
        {
            if (c == null) return 1;
            if (c.isDie) return 0;
            var max = c.data.maxHP.value;
            return max <= 0 ? 1 : Math.Max(0, c.data.hp) / (double)max;
        }

        /// <summary>A second of the battle: each elder who could betray rolls its chance (one betrayal a second, BetrayMax a battle).</summary>
        public static bool TryBetray(Func<double> roll)
        {
            if (warFight == null || turncoats.Count >= Conf.BetrayMax) return false;
            var ours = Unit(warFight.Ours); var theirs = Unit(warFight.Theirs);
            var playerHp = HpShare(ModBattleEvent.PlayerUnit);
            var theirHp = HpShare(Unit(warFight.TheirFighter).GetBattleUnit());
            foreach (var b in battleElders.ToList())
            {
                if (b.Unit == null || b.Unit.isDie || !Instance.Elders.Contains(b.Post)) continue;
                var u = Unit(b.Post.UnitId);
                if (!Alive(u)) continue;
                var own = b.PlayerSide ? ours : theirs;
                var other = b.PlayerSide ? theirs : ours;
                var motive = BetrayMotive(BadNature(u), b.Post.Unpaid, Intim(u, own), Intim(u, other));
                var chance = BetrayChance(motive, WarSeconds, b.Post.Loyalty, Month - b.Post.Since, b.PlayerSide ? playerHp : theirHp,
                    b.PlayerSide ? PowerOurs : PowerTheirs, b.PlayerSide ? PowerTheirs : PowerOurs);
                if (chance <= 0 || roll() >= chance) continue;
                Betray(b, own, other);
                return true;
            }
            return false;
        }

        /// <summary>
        /// The elder turns on its clan: its battle unit changes sides, it leaves the clan as a traitor (hunted); against
        /// the player: hate both ways; for the player: closer to it. A message in the battle, a log line.
        /// </summary>
        private static void Betray(BattleElder b, WorldUnitBase own, WorldUnitBase other)
        {
            var u = Unit(b.Post.UnitId);
            var town = Town(b.Post.TownId);
            b.Unit.data.unitType = b.PlayerSide ? UnitType.Monst : UnitType.PlayerNPC; // change sides
            Outlaw(b.Post);
            var pid = Player.GetUnitId();
            if (b.PlayerSide)
            {
                u.data.unitData.relationData.AddHate(pid, 200);
                Player.data.unitData.relationData.AddHate(u.GetUnitId(), 200);
            }
            else
            {
                FamilyEvent.ChangeIntim(u.data.unitData.relationData, pid, 30);
                FamilyEvent.ChangeIntim(Player.data.unitData.relationData, u.GetUnitId(), 30);
            }
            turncoats.Add(new Turncoat { UnitId = u.GetUnitId(), TownId = b.Post.TownId, ToHead = other.GetUnitId(), ToPlayerSide = !b.PlayerSide });
            DebugHelper.WriteLine($"Clan elder betrays: {u.GetName()} ({u.GetUnitId()}) {ClanEvent.ClanName(own)} -> {ClanEvent.ClanName(other)}, loyalty {b.Post.Loyalty}, second {WarSeconds}");
            TaoistLogEvent.Log("log_clan_elder_betrayed", town?.name ?? "-", u.GetName(), ClanEvent.ClanName(own), ClanEvent.ClanName(other));
            var text = string.Format(GameTool.LS(b.PlayerSide ? "elder_betray_ours" : "elder_betray_theirs"), u.GetName(), ClanEvent.ClanName(own), ClanEvent.ClanName(other));
            DramaHelper.OpenDrama1(text, new List<string> { GameTool.LS("elder_betray_fight") }, null, u, Player);
        }

        /// <summary>
        /// The battle ended (playerWon): a turncoat alive whose new side won becomes an elder of that clan when it can
        /// serve there (a message when it is the player's clan).
        /// </summary>
        public static void EndWarBattle(bool playerWon)
        {
            if (warFight == null) return;
            foreach (var t in turncoats)
            {
                var u = Unit(t.UnitId); var head = Unit(t.ToHead); var town = Town(t.TownId);
                if (!Alive(u) || !Alive(head) || town == null || t.ToPlayerSide != playerWon) continue;
                if (CannotServe(town, head, u) != null) continue;
                Appoint(town, head, u);
                TaoistLogEvent.Log("log_clan_elder_turncoat", town.name, u.GetName(), ClanEvent.ClanName(head));
                if (head.IsPlayer())
                    InfoDialog.Msg(town.name, string.Format(GameTool.LS("elder_turncoat_joined"), u.GetName()));
            }
            ResetWarBattle();
        }

        private static void ResetWarBattle()
        {
            warFight = null;
            WarSeconds = 0;
            PowerOurs = 0;
            PowerTheirs = 0;
            battleElders.Clear();
            turncoats.Clear();
        }
    }
}
