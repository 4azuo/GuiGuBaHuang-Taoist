using EBattleTypeData;
using MOD_nE7UL2.Const;
using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Helper;
using ModLib.Mod;
using System;
using System.Linq;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Recovery of the potions (HP/MP) of the player in a battle, for the middle and the end of the game:
    /// - Pill Body (A): every potion taken adds its grade to the pill points; tier n at 20 x n² points (tier 10: 2,000),
    ///   +10 % recovery a tier; the realm caps the tier (3 below realm 5, 6 below realm 7, 9 below realm 9, then 10).
    /// - Alchemy (C, ArtisanEvent): +5 % a rank of alchemy with a potion of a kind the player crafted.
    /// - Nine-Turn Golden Pill (D, a rare encounter of realm 8 and above): each potion also gives 10 % of the max HP / MP.
    /// - Medicine King's Testament (B, storyline S): +30 % recovery, or half of the focus (SP) of a potion given back.
    /// The bonus is given when the potion is used (OnBattleUnitUseProp), on top of the recovery of the game.
    /// </summary>
    [Cache(ModConst.PILL_BODY_EVENT)]
    public class PillBodyEvent : ModEvent
    {
        public static PillBodyEvent Instance { get; set; }

        public const int MAX_TIER = 10;
        public const int AWAKEN_POINTS = 100;          // tier 1 (the awakening: then a chance, ConstitutionEvent)
        public const int TIER_POINTS = 50;             // tier n >= 2: TIER_POINTS x n²
        public const float TIER_BONUS = 0.10f;         // recovery a tier
        public const float ALCHEMY_RANK_BONUS = 0.05f; // recovery a rank of alchemy (own kind of potion)
        public const float NINE_TURN_MAX = 0.10f;      // share of the max HP / MP a potion (Nine-Turn Golden Pill)
        public const int NINE_TURN_GRADE = 8;          // realm of the encounter
        public const float NINE_TURN_CHANCE = 2f;      // % a month (realm 8+, encounters on)
        public const float KING_HEAL = 0.30f;          // storyline S: recovery
        public const float KING_FOCUS = 0.50f;         // storyline S: share of the focus given back

        /// <summary>Pill points (sum of the grades of the potions taken in battles).</summary>
        public int Points { get; set; }
        /// <summary>Highest tier reached (the log is written once a tier).</summary>
        public int LoggedTier { get; set; }
        /// <summary>The Nine-Turn Golden Pill was refined (encounter D).</summary>
        public bool NineTurn { get; set; }
        /// <summary>The encounter D was seen (it comes once).</summary>
        public bool NineTurnSeen { get; set; }
        /// <summary>Reward of storyline S: +30 % recovery (true) or half of the focus back (KingFocus).</summary>
        public bool KingHeal { get; set; }
        public bool KingFocus { get; set; }

        private static WorldUnitBase Player => g.world.playerUnit;

        // ------------------------------------------------------------------ rules (no game object: unit tests)

        /// <summary>Pill points of a tier (20 x tier²).</summary>
        public static int PointsOf(int tier) => tier <= 0 ? 0 : tier == 1 ? AWAKEN_POINTS : TIER_POINTS * tier * tier;

        /// <summary>Highest tier the realm allows: 3 (realms 1-4), 6 (5-6), 9 (7-8), 10 (9+).</summary>
        public static int TierCap(int grade) => grade >= 9 ? 10 : grade >= 7 ? 9 : grade >= 5 ? 6 : 3;

        /// <summary>Tier of the points at the realm.</summary>
        public static int TierOf(int points, int grade)
        {
            var t = 0;
            while (t < MAX_TIER && points >= PointsOf(t + 1)) t++;
            return Math.Min(t, TierCap(grade));
        }

        /// <summary>Share of the recovery added: tiers, alchemy ranks (own kind of potion), the storyline reward.</summary>
        public static double Bonus(int tier, int alchemyRank, bool ownPotion, bool kingHeal) =>
            tier * TIER_BONUS + (ownPotion ? alchemyRank * ALCHEMY_RANK_BONUS : 0f) + (kingHeal ? KING_HEAL : 0f);

        /// <summary>Recovery added to a potion of base value v (max: the max HP / MP): base x bonus, + 10 % of the max with the Nine-Turn pill.</summary>
        public static int Extra(int baseValue, int max, double bonus, bool nineTurn) =>
            Math.Max(0d, baseValue * bonus + (nineTurn ? max * (double)NINE_TURN_MAX : 0d)).ToIntClamp();

        // ------------------------------------------------------------------ the player

        /// <summary>Tier of the Pill Body of the player: awakened as a constitution (ConstitutionEvent), else 0.</summary>
        public int Tier => Player == null || ConstitutionEvent.Instance == null || !ConstitutionEvent.Enabled ? 0 : ConstitutionEvent.Instance.Tier(Player, ConstitutionEvent.Kind.Pill);

        /// <summary>Alchemy rank of the player and whether the potion kind was crafted by the player.</summary>
        public static (int rank, bool own) Alchemy(int propsId)
        {
            if (!ArtisanEvent.Enabled || Player == null) return (0, false);
            var d = ArtisanEvent.Get(Player, ArtisanEvent.Craft.Alchemy);
            return (d?.Rank ?? 0, d?.CraftedIds?.Contains(propsId) ?? false);
        }

        /// <summary>HP and MP recovery of a potion (the values of the game changed by Taoist).</summary>
        public static (int hp, int mp) Recovery(ConfItemPillItem pill)
        {
            int hp = 0, mp = 0;
            foreach (var efxId in (pill?.effectValue ?? string.Empty).Split('|'))
            {
                var roleEfx = g.conf.roleEffect.GetItem(efxId.Parse<int>());
                if (roleEfx == null) continue;
                if (roleEfx.value.StartsWith($"{UnitPropertyEnum.Hp.PropName}_1_")) hp += roleEfx.GetEfxValue<int>(2);
                else if (roleEfx.value.StartsWith($"{UnitPropertyEnum.Mp.PropName}_1_")) mp += roleEfx.GetEfxValue<int>(2);
            }
            return (hp, mp);
        }

        /// <summary>A potion used by the player in a battle: the bonus recovery, the pill points, the focus given back.</summary>
        public override void OnBattleUnitUseProp(UnitUseProp e)
        {
            base.OnBattleUnitUseProp(e);
            var unit = e?.unit;
            var props = e?.prop?.data?.propsItem;
            if (unit == null || props == null || !unit.IsPlayer()) return;
            var pill = props.IsPotion();
            if (pill == null) return;
            Use(unit, props, pill);
        }

        /// <summary>The bonus of one potion (also called by the tests). Returns the HP and MP added.</summary>
        public (int hp, int mp) Use(UnitCtrlBase unit, ConfItemPropsItem props, ConfItemPillItem pill)
        {
            var grade = Math.Max(1, pill.grade);
            var gradeLvl = Player.GetGradeLvl();
            var before = Tier;
            Points = ValueHelper.SafeAddInt(Points, grade);
            ConstitutionEvent.Instance?.Add(Player, ConstitutionEvent.Kind.Pill, 0);   // the awakening roll, the log of the tiers
            var tier = Tier;
            var (rank, own) = Alchemy(props.id);
            var bonus = Bonus(tier, rank, own, KingHeal);
            var (hp, mp) = Recovery(pill);
            var data = unit.data;
            var addHp = hp > 0 ? Extra(hp, data.maxHP.value, bonus, NineTurn) : 0;
            var addMp = mp > 0 ? Extra(mp, data.maxMP.value, bonus, NineTurn) : 0;
            if (addHp > 0) data.AddHP(new MartialTool.HitData(unit, null, 0, 0, addHp));
            if (addMp > 0) data.AddMP(addMp);
            if (KingFocus) data.AddSP((pill.spCost * KING_FOCUS).ToIntClamp());
            if (hp > 0) Helper.TaoistStats.Potion(gradeLvl, hp + addHp, data.maxHP.value);
            DebugHelper.WriteLine($"PillBodyEvent: potion {props.id} grade {grade}: +{addHp} HP, +{addMp} MP (base {hp}/{mp}, bonus {bonus:0.00}, nine-turn {NineTurn}), points {Points}, tier {tier}");
            if (tier > before && tier > LoggedTier)
            {
                LoggedTier = tier;
                TaoistLogEvent.Log("log_pill_tier", tier, (tier * TIER_BONUS * 100).ToString("0"));
                if (tier == TierCap(gradeLvl) && tier < MAX_TIER)
                    TaoistLogEvent.Log("log_pill_capped", tier);
            }
            if (before == 0 && tier > 0)
                DebugHelper.WriteLine($"PillBodyEvent: the Pill Body awakened ({Points} points)");
            return (addHp, addMp);
        }

        // ------------------------------------------------------------------ D: Nine-Turn Golden Pill

        /// <summary>The encounter D may come: realm 8+, not seen, encounters on.</summary>
        public static bool NineTurnPossible() =>
            Instance != null && !Instance.NineTurnSeen && Player != null && Player.GetGradeLvl() >= NINE_TURN_GRADE;

        /// <summary>Monthly: the rare encounter of the Nine-Turn Golden Pill (realm 8 and above, once).</summary>
        [EventCondition(IsInGame = HandleEnum.True)]
        public override void OnMonthly()
        {
            base.OnMonthly();
            if (!NineTurnPossible() || (SMLocalConfigsEvent.Instance?.Configs?.NoEncounter ?? false) || GameHelper.IsInBattlle() || (TimeSkipEvent.Instance?.IsProcessing ?? false))
                return;
            if (CommonTool.Random(0f, 100f) >= NINE_TURN_CHANCE)
                return;
            OpenNineTurn();
        }

        /// <summary>Price of the encounter D: spirit stones (x inflation) and herbs of realm 8.</summary>
        public static int NineTurnCost() => EncounterEvent.Cost(200);
        public const int NINE_TURN_HERBS = 9;

        /// <summary>Opens the drama of the encounter D (also called by the tests).</summary>
        public static void OpenNineTurn()
        {
            Instance.NineTurnSeen = true;
            DramaHelper.OpenDrama2(string.Format(GameTool.LS("pill_nineturn"), NineTurnCost(), NINE_TURN_HERBS), new System.Collections.Generic.List<string>
            {
                GameTool.LS("pill_nineturn_o1"), GameTool.LS("pill_nineturn_o2"),
            }, x => ResolveNineTurn(x.id % 10), "lianhua2");
        }

        /// <summary>The choice of the encounter D (0: refine the pill, else leave). Returns the result key.</summary>
        public static string ResolveNineTurn(int opt)
        {
            string key;
            if (opt != 0) key = "pill_nineturn_leave";
            else
            {
                var herbs = Player.GetUnitProps().Where(x => x.propsItem?.IsHerbItem() != null && x.propsItem.level >= 1).OrderBy(x => x.propsItem.worth).ToList();
                var cost = NineTurnCost();
                if (herbs.Sum(x => x.propsCount) < NINE_TURN_HERBS) key = "pill_nineturn_noherb";
                else if (Player.GetUnitMoney() < cost) key = "enc_nomoney";
                else
                {
                    var left = NINE_TURN_HERBS;
                    foreach (var h in herbs)
                    {
                        if (left <= 0) break;
                        var k = Math.Min(left, h.propsCount);
                        Player.RemoveUnitProp(h.soleID, k);
                        left -= k;
                    }
                    Player.AddUnitMoney(-cost);
                    Instance.NineTurn = true;
                    key = "pill_nineturn_done";
                    TaoistLogEvent.Log("log_pill_nineturn");
                }
            }
            var text = string.Format(GameTool.LS(key), NineTurnCost(), NINE_TURN_HERBS);
            DebugHelper.WriteLine($"PillBodyEvent: Nine-Turn Golden Pill option {opt} -> {key}");
            ModDelayEvent.Instance.DelayEvent(Instance, () =>
                DramaHelper.OpenDrama2(text, new System.Collections.Generic.List<string> { GameTool.LS("enc_leave") }, null, "lianhua2"), 300);
            return key;
        }

        /// <summary>The text of the pill body (player info, Taoist log): tier, points, next tier, bonuses.</summary>
        public static string Summary()
        {
            if (Instance == null || Player == null) return string.Empty;
            var grade = Player.GetGradeLvl();
            var tier = Instance.Tier;
            var raw = TierOf(Instance.Points, MAX_TIER);   // the tier of the points without the cap of the realm
            var next = tier == 0 ? PointsOf(1) : raw >= MAX_TIER ? 0 : PointsOf(Math.Max(1, raw) + 1);
            var (rank, _) = Alchemy(0);
            return string.Format(GameTool.LS("pill_summary"), tier, TierCap(grade), Instance.Points, next, (tier * TIER_BONUS * 100).ToString("0"),
                (rank * ALCHEMY_RANK_BONUS * 100).ToString("0"), GameTool.LS(Instance.NineTurn ? "pill_yes" : "pill_no"),
                GameTool.LS(Instance.KingHeal ? "pill_king_heal" : Instance.KingFocus ? "pill_king_focus" : "pill_no"));
        }
    }
}
