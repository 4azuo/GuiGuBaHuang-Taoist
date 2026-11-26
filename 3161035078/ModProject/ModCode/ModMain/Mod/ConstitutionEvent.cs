using EBattleTypeData;
using MOD_nE7UL2.Const;
using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Helper;
using ModLib.Mod;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Constitutions (thể chất, 6.3): special bodies of the cultivators, as many as a unit can cultivate (no limit). Each one is
    /// gained and grows its own way, tiers 1-10, the realm capping the tier (PillBodyEvent.TierCap) except the innate ones
    /// (born with it: the tier follows the realm):
    /// - Pill Body: the potions taken in battles (PillBodyEvent: their recovery, player).
    /// - Sword Bone / Vajra Body: the hits dealt with sword / fist, palm, finger arts (martial arts).
    /// - Innate Spirit Body / Dragon Blood Body: born with it (a roll at the start, a few NPCs); Dragon Blood also by
    ///   absorbing heaven-and-earth treasures (herbs and ores of level 4+, a button of the window).
    /// - Thunder Body: lightning trials survived, storyline K.
    /// - Venom Body: herbs tasted raw (a button, HP lost), storyline S (the poison tasted).
    /// The stats are added when a cultivator enters a battle (OnBattleUnitInto). NPCs: born ones by their id (no data
    /// kept), the martial ones grow every year with their realm and their best basis. Off with S&amp;M NoConstitution.
    /// </summary>
    [Cache(ModConst.CONSTITUTION_EVENT)]
    public class ConstitutionEvent : ModEvent
    {
        public static ConstitutionEvent Instance { get; set; }

        public enum Kind { Pill, SwordBone, Vajra, InnateSpirit, DragonBlood, Thunder, Venom }

        /// <summary>A constitution: points a tier (x 20 n²), born chance (%), stats a tier (share of the value), a basis a tier.</summary>
        public class Def
        {
            public Kind Kind;
            public int Scale = 1;
            public float BornChance;       // % (player: rolled once; NPC: by its id)
            public bool Innate;            // the tier follows the realm (no points)
            public float Atk, Defense, Hp, Mp, BasisRate;
            public string Basis;           // UnitDynPropertyEnum name (BasisSword...)
            public string Key => "const_" + Kind.ToString().ToLowerInvariant();
        }

        public static readonly Def[] DEFS =
        {
            new Def { Kind = Kind.Pill },                                                                                  // potions (PillBodyEvent)
            new Def { Kind = Kind.SwordBone, Scale = 50, Atk = 0.01f, Basis = "BasisSword", BasisRate = 0.05f },
            new Def { Kind = Kind.Vajra, Scale = 50, Defense = 0.03f, Hp = 0.02f },
            new Def { Kind = Kind.InnateSpirit, Innate = true, BornChance = 5f, Mp = 0.04f, Atk = 0.01f },
            new Def { Kind = Kind.DragonBlood, Scale = 2, BornChance = 1f, Hp = 0.04f, Atk = 0.01f },
            new Def { Kind = Kind.Thunder, Atk = 0.01f, Basis = "BasisThunder", BasisRate = 0.05f },
            new Def { Kind = Kind.Venom, Defense = 0.02f, Basis = "BasisWood", BasisRate = 0.05f },
        };

        public static Def Of(Kind k) => DEFS.First(x => x.Kind == k);

        /// <summary>Destinies (lucks) of the tiers: constluck_roleCreateFeature.json, a name a tier (constluck_localText.json),
        /// group LUCK_BASE + (kind + 1) x 1000, the tier added. Shown in the destinies of the unit; the stats are given by
        /// OnBattleUnitInto (the luck has no effect).</summary>
        public const int LUCK_BASE = 420300000;

        public static int LuckId(Kind k, int tier) => tier <= 0 ? 0 : LUCK_BASE + ((int)k + 1) * 1000 + Math.Min(PillBodyEvent.MAX_TIER, tier);

        /// <summary>The destinies of the unit follow its tiers (the luck of the tier, no other tier of the kind; none when the
        /// constitutions are off).</summary>
        public void SyncLucks(WorldUnitBase w)
        {
            // a luck cannot be added in a battle scene: the monthly / yearly sync gives it on the map
            if (w == null || w.isDie || GameHelper.IsInBattlle()) return;
            foreach (var d in DEFS)
            {
                var want = Enabled ? LuckId(d.Kind, Tier(w, d.Kind)) : 0;
                for (var t = 1; t <= PillBodyEvent.MAX_TIER; t++)
                {
                    var id = LuckId(d.Kind, t);
                    if (id != want && w.GetLuck(id) != null) w.DelLuck(id);
                }
                if (want > 0 && w.GetLuck(want) == null) w.AddLuck(want);
            }
        }

        public const int THUNDER_TRIAL_POINTS = 10;      // x grade, a lightning trial survived
        public const int THUNDER_STORY_POINTS = 200;     // an ending of storyline K
        public const int VENOM_STORY_POINTS = 100;       // storyline S: the poison tasted
        public const float VENOM_HP = 0.10f;             // share of the HP lost tasting a herb
        public const int TREASURE_LEVEL = 4;             // herbs and ores of this level and above are treasures
        public const int NPC_GROWTH = 20;                // x grade, a year, the martial constitution of an NPC
        public const int NPCS_A_YEAR = 500;

        /// <summary>Points by unit id, then by constitution.</summary>
        public Dictionary<string, Dictionary<Kind, int>> Points { get; set; } = new Dictionary<string, Dictionary<Kind, int>>();
        /// <summary>The constitutions awakened (tier 1 reached) by unit id (NPCs born with one: by their id, not kept).</summary>
        public Dictionary<string, HashSet<Kind>> Awakened { get; set; } = new Dictionary<string, HashSet<Kind>>();

        /// <summary>Awakening (tier 0 -> 1): the points of tier 1 (x the scale), then a chance each time points come
        /// (a potion, a battle, a treasure, a herb, a trial, a year of an NPC): 0.3 % at the threshold, growing with the
        /// points kept above it, at most 0.99 %. The tiers above need only their points (PillBodyEvent.PointsOf).</summary>
        public const double AWAKEN_CHANCE = 0.3, AWAKEN_CHANCE_MAX = 0.99;

        /// <summary>Chance (%) to awaken with the points (already divided by the scale).</summary>
        public static double AwakenChance(int points) =>
            points < PillBodyEvent.PointsOf(1) ? 0d : Math.Min(AWAKEN_CHANCE_MAX, AWAKEN_CHANCE * points / PillBodyEvent.PointsOf(1));

        /// <summary>Hits of this battle by unit and constitution (counted when the battle ends: one awakening roll a battle).</summary>
        private readonly Dictionary<string, Dictionary<Kind, int>> battleHits = new Dictionary<string, Dictionary<Kind, int>>();
        private readonly Dictionary<string, WorldUnitBase> battleUnits = new Dictionary<string, WorldUnitBase>();
        /// <summary>The born constitutions of the player were rolled.</summary>
        public bool PlayerRolled { get; set; }
        /// <summary>The tiers already written in the Taoist log (player).</summary>
        public Dictionary<Kind, int> LoggedTier { get; set; } = new Dictionary<Kind, int>();

        public static bool Enabled => Instance != null && !(SMLocalConfigsEvent.Instance?.Configs?.NoConstitution ?? false);
        private static WorldUnitBase Player => g.world.playerUnit;

        // ------------------------------------------------------------------ rules (no game object: unit tests)

        /// <summary>Tier of the points of an awakened constitution at the realm (innate: the realm; awakened: at least 1).</summary>
        public static int TierOf(Def d, int points, int grade, bool awakened)
        {
            if (!awakened) return 0;
            if (d.Innate) return Math.Max(1, Math.Min(PillBodyEvent.MAX_TIER, grade));
            return Math.Max(1, PillBodyEvent.TierOf(points / Math.Max(1, d.Scale), grade));
        }

        /// <summary>Born with it: an NPC by its id (stable, nothing kept), between 0 and 100.</summary>
        public static float Roll(string unitId, Kind k)
        {
            unchecked
            {
                var h = 17;
                foreach (var ch in (unitId ?? string.Empty) + ":" + (int)k) h = h * 31 + ch;
                return (Math.Abs(h) % 10000) / 100f;
            }
        }

        /// <summary>The kind of a martial constitution grown by hits of the basis (null: none).</summary>
        public static Kind? MartialOf(string basis)
        {
            switch (basis)
            {
                case "Sword": return Kind.SwordBone;
                case "Fist": case "Palm": case "Finger": return Kind.Vajra;
                default: return null;
            }
        }

        // ------------------------------------------------------------------ units

        public int Get(WorldUnitBase w, Kind k)
        {
            if (w == null) return 0;
            if (k == Kind.Pill) return w.IsPlayer() ? PillBodyEvent.Instance?.Points ?? 0 : 0;
            return Points.TryGetValue(w.GetUnitId(), out var p) && p.TryGetValue(k, out var v) ? v : 0;
        }

        /// <summary>The unit has the constitution: awakened, or born with it (an NPC by its id).</summary>
        public bool Has(WorldUnitBase w, Kind k)
        {
            if (w == null) return false;
            if (Awakened.TryGetValue(w.GetUnitId(), out var a) && a.Contains(k)) return true;
            var d = Of(k);
            return d.BornChance > 0 && !w.IsPlayer() && Roll(w.GetUnitId(), k) < d.BornChance;
        }

        public int Tier(WorldUnitBase w, Kind k) => w == null ? 0 : TierOf(Of(k), Get(w, k), w.GetGradeLvl(), Has(w, k));

        /// <summary>The constitution awakens (tier 1; born ones, a successful roll, the tests).</summary>
        public void Awaken(WorldUnitBase w, Kind k)
        {
            if (w == null) return;
            var id = w.GetUnitId();
            if (!Awakened.TryGetValue(id, out var a)) Awakened[id] = a = new HashSet<Kind>();
            a.Add(k);
        }

        /// <summary>Adds points to a constitution of a unit (the Pill Body: its points are kept by PillBodyEvent, 0 here), then
        /// rolls the awakening when it is not awakened; the player's awakening and new tiers are written in the log.
        /// roll: 0..100 (below 0: random). Returns true when it awakened now.</summary>
        public bool Add(WorldUnitBase w, Kind k, int points, float roll = -1f)
        {
            if (w == null || points < 0 || !Enabled) return false;
            var id = w.GetUnitId();
            var before = Tier(w, k);
            if (points > 0 && k != Kind.Pill)
            {
                if (!Points.TryGetValue(id, out var p)) Points[id] = p = new Dictionary<Kind, int>();
                p[k] = ValueHelper.SafeAddInt(p.TryGetValue(k, out var v) ? v : 0, points);
            }
            var awakened = false;
            var d = Of(k);
            if (!d.Innate && !Has(w, k))
            {
                var chance = AwakenChance(Get(w, k) / Math.Max(1, d.Scale));
                if (chance > 0 && (roll < 0f ? CommonTool.Random(0f, 100f) : roll) < chance)
                {
                    Awaken(w, k);
                    awakened = true;
                    if (w.IsPlayer())
                    {
                        TaoistLogEvent.Log("log_const_awaken", GameTool.LS(d.Key));
                        Helper.TaoistStats.Constitution(k.ToString(), 1, GameHelper.GetGameTotalMonth());
                    }
                }
            }
            var tier = Tier(w, k);
            if (tier != before) SyncLucks(w);
            if (w.IsPlayer() && tier > before && tier > 1 && tier > (LoggedTier.TryGetValue(k, out var t) ? t : 0))
            {
                LoggedTier[k] = tier;
                TaoistLogEvent.Log("log_const_tier", GameTool.LS(d.Key), tier);
                Helper.TaoistStats.Constitution(k.ToString(), tier, GameHelper.GetGameTotalMonth());
            }
            return awakened;
        }

        /// <summary>The shares added to the stats by every constitution of the unit (atk, def, hp, mp, basis name -> share).</summary>
        public (double atk, double def, double hp, double mp, Dictionary<string, double> basis) Bonus(WorldUnitBase w)
        {
            double atk = 0, def = 0, hp = 0, mp = 0;
            var basis = new Dictionary<string, double>();
            if (!Enabled || w == null) return (0, 0, 0, 0, basis);
            foreach (var d in DEFS)
            {
                var t = Tier(w, d.Kind);
                if (t <= 0) continue;
                atk += d.Atk * t; def += d.Defense * t; hp += d.Hp * t; mp += d.Mp * t;
                if (d.Basis != null) basis[d.Basis] = (basis.TryGetValue(d.Basis, out var b) ? b : 0) + d.BasisRate * t;
            }
            return (atk, def, hp, mp, basis);
        }

        // ------------------------------------------------------------------ events

        /// <summary>The born constitutions of the player (once).</summary>
        public override void OnLoadGame()
        {
            base.OnLoadGame();
            if (Player == null) return;
            if (PlayerRolled) { SyncLucks(Player); return; }
            PlayerRolled = true;
            foreach (var d in DEFS.Where(x => x.BornChance > 0))
                if (CommonTool.Random(0f, 100f) < d.BornChance * 2f)   // the player: twice the chance of an NPC
                {
                    var id = Player.GetUnitId();
                    if (!Points.TryGetValue(id, out var p)) Points[id] = p = new Dictionary<Kind, int>();
                    if (!d.Innate) p[d.Kind] = Math.Max(PillBodyEvent.PointsOf(1) * d.Scale, p.TryGetValue(d.Kind, out var v) ? v : 0);
                    Awaken(Player, d.Kind);
                    TaoistLogEvent.Log("log_const_born", GameTool.LS(d.Key));
                }
            SyncLucks(Player);
        }

        /// <summary>Monthly: the destinies of the player follow the tiers (the realm moves the caps and the innate tiers).</summary>
        [EventCondition(IsInGame = HandleEnum.True)]
        public override void OnMonthly()
        {
            base.OnMonthly();
            SyncLucks(Player);
        }

        /// <summary>A cultivator enters a battle: the stats of his constitutions.</summary>
        public override void OnBattleUnitInto(UnitCtrlBase e)
        {
            base.OnBattleUnitInto(e);
            var h = e?.data?.TryCast<UnitDataHuman>();
            var w = h?.worldUnitData?.unit;
            if (w == null || !Enabled) return;
            var (atk, def, hp, mp, basis) = Bonus(w);
            if (atk > 0) h.attack.baseValue = ValueHelper.SafeAddInt(h.attack.baseValue, h.attack.baseValue * atk);
            if (def > 0) h.defense.baseValue = ValueHelper.SafeAddInt(h.defense.baseValue, h.defense.baseValue * def);
            if (hp > 0) { h.maxHP.baseValue = ValueHelper.SafeAddInt(h.maxHP.baseValue, h.maxHP.baseValue * hp); h.hp = h.maxHP.value; }
            if (mp > 0) { h.maxMP.baseValue = ValueHelper.SafeAddInt(h.maxMP.baseValue, h.maxMP.baseValue * mp); h.mp = h.maxMP.value; }
            foreach (var b in basis)
            {
                var dyn = h.GetValue(UnitDynPropertyEnum.GetEnumByName<UnitDynPropertyEnum>(b.Key)?.PropName ?? string.Empty) as DynInt;
                if (dyn != null) dyn.baseValue = ValueHelper.SafeAddInt(dyn.baseValue, dyn.baseValue * b.Value);
            }
        }

        /// <summary>A hit of a cultivator with a martial art: a point of Sword Bone / Vajra Body (counted at the end of the battle).</summary>
        public override void OnBattleUnitHitDynIntHandler(UnitHitDynIntHandler e)
        {
            base.OnBattleUnitHitDynIntHandler(e);
            if (!Enabled || !ModBattleEvent.IsWorldUnitAttacking || e?.hitData == null) return;
            var k = MartialOf(ModBattleEvent.GetDmgBasisType(e.hitData).ToString());
            if (k == null) return;
            var w = ModBattleEvent.AttackingWorldUnit;
            var id = w.GetUnitId();
            battleUnits[id] = w;
            if (!battleHits.TryGetValue(id, out var h)) battleHits[id] = h = new Dictionary<Kind, int>();
            h[k.Value] = (h.TryGetValue(k.Value, out var v) ? v : 0) + 1;
        }

        /// <summary>The end of a battle: the hits of its martial arts (one awakening roll a battle), a lightning trial survived (Thunder Body).</summary>
        public override void OnBattleEndHandler(BattleEndHandler e)
        {
            base.OnBattleEndHandler(e);
            foreach (var x in battleHits)
                if (battleUnits.TryGetValue(x.Key, out var w) && w != null)
                    foreach (var h in x.Value) Add(w, h.Key, h.Value);
            battleHits.Clear();
            battleUnits.Clear();
            if (RealTrialEvent.IsInTrial && ModBattleEvent.PlayerUnit != null && !ModBattleEvent.PlayerUnit.isDie)
                Add(Player, Kind.Thunder, THUNDER_TRIAL_POINTS * Math.Max(1, Player.GetGradeLvl()));
        }

        /// <summary>Yearly: the NPCs grow the martial constitution of their best basis (sword: Sword Bone; fist, palm, finger: Vajra).</summary>
        [EventCondition(IsInGame = HandleEnum.True)]
        public override void OnYearly()
        {
            base.OnYearly();
            var years = Math.Max(1, ModLib.Mod.ModMaster.YearsPassed);
            var all = (ModMain.Store?.WUnits ?? new WorldUnitBase[0]).Where(x => x != null && !x.isDie && !x.IsPlayer()).ToList();
            if (all.Count == 0) return;
            if (!Enabled)
            {
                // off: the destinies of the constitutions go away, NPCS_A_YEAR NPCs a year
                foreach (var npc in all.Skip((GameHelper.GetGameYear() % Math.Max(1, (all.Count + NPCS_A_YEAR - 1) / NPCS_A_YEAR)) * NPCS_A_YEAR).Take(NPCS_A_YEAR))
                    SyncLucks(npc);
                return;
            }
            // NPCS_A_YEAR a year, the next ones the year after (every NPC in turn), their growth x the years of the turn
            var turns = Math.Max(1, (all.Count + NPCS_A_YEAR - 1) / NPCS_A_YEAR);
            var start = (GameHelper.GetGameYear() % turns) * NPCS_A_YEAR;
            years *= turns;
            foreach (var npc in all.Skip(start).Take(NPCS_A_YEAR))
            {
                var best = new[] { "Sword", "Fist", "Palm", "Finger", "Blade", "Spear" }
                    .OrderByDescending(b => npc.GetDynProperty(UnitDynPropertyEnum.GetEnumByName<UnitDynPropertyEnum>("Basis" + b))?.value ?? 0).First();
                var k = MartialOf(best);
                if (k != null) Add(npc, k.Value, NPC_GROWTH * Math.Max(1, npc.GetGradeLvl()) * years);
                SyncLucks(npc); // born ones, innate tiers (the realm)
            }
        }

        // ------------------------------------------------------------------ treasures and herbs (window buttons)

        /// <summary>Absorbs the best heaven-and-earth treasure of the bag (herb or ore of level 4+): Dragon Blood points = level².
        /// Returns the result key.</summary>
        public static string AbsorbTreasure()
        {
            var item = Player.GetUnitProps().Where(x => (x.propsItem?.IsHerbItem() != null || x.propsItem?.IsMineItem() != null) && x.propsItem.level >= TREASURE_LEVEL)
                .OrderByDescending(x => x.propsItem.level).ThenByDescending(x => x.propsItem.worth).FirstOrDefault();
            if (item == null) return "const_no_treasure";
            var level = item.propsItem.level;
            Player.RemoveUnitProp(item.soleID, 1);
            Instance.Add(Player, Kind.DragonBlood, level * level);
            return "const_absorbed";
        }

        /// <summary>Tastes the cheapest herb of the bag raw: Venom points = its level + 1, 10 % of the HP lost.</summary>
        public static string TasteHerb()
        {
            var herb = Player.GetUnitProps().Where(x => x.propsItem?.IsHerbItem() != null).OrderBy(x => x.propsItem.worth).FirstOrDefault();
            if (herb == null) return "const_no_herb";
            Player.RemoveUnitProp(herb.soleID, 1);
            var hp = Player.GetProperty<int>(UnitPropertyEnum.Hp);
            Player.SetProperty<int>(UnitPropertyEnum.Hp, Math.Max(1, hp - (int)(hp * VENOM_HP)));
            Instance.Add(Player, Kind.Venom, herb.propsItem.level + 1);
            return "const_tasted";
        }

        // ------------------------------------------------------------------ window

        /// <summary>One line a constitution: name (owned / not), tier and cap, points, how it grows, what a tier gives.</summary>
        public static string Summary(WorldUnitBase w)
        {
            var lines = new List<string>();
            var grade = w.GetGradeLvl();
            foreach (var d in DEFS)
            {
                var has = Instance.Has(w, d.Kind);
                var tier = Instance.Tier(w, d.Kind);
                var pts = Instance.Get(w, d.Kind) / Math.Max(1, d.Scale);
                var raw = PillBodyEvent.TierOf(pts, PillBodyEvent.MAX_TIER);
                // not awakened: the points of the awakening and its chance; awakened: the points of the next tier
                var progress = d.Innate ? GameTool.LS(has ? "const_innate" : "const_not_born")
                    : !has ? string.Format(GameTool.LS("const_awakening"), pts, PillBodyEvent.PointsOf(1), AwakenChance(pts).ToString("0.00"), AWAKEN_CHANCE_MAX)
                    : string.Format(GameTool.LS("const_progress"), pts, raw >= PillBodyEvent.MAX_TIER ? "-" : PillBodyEvent.PointsOf(Math.Max(1, raw) + 1).ToString(), PillBodyEvent.TierCap(grade));
                lines.Add((has ? "[icon:daodian] " : "- ") + string.Format(GameTool.LS("const_line"), GameTool.LS(d.Key), tier, progress));
                lines.Add("    " + GameTool.LS(d.Key + "_how") + " " + GameTool.LS(d.Key + "_gain"));
            }
            return TextIconHelper.ToSprites(string.Join("\n", lines));
        }

        /// <summary>Game text icons of the constitutions (DEFS order).</summary>
        public static readonly string[] ICONS = { "liandan", "jianfa", "hudun", "lingli", "jiankang", "leilinggen", "yaocai" };

        /// <summary>The window of the constitutions (player info): the realm cap on top, the awakened constitutions (gold
        /// cards: tier, its destiny, the points to the next tier, how it grows, its effect now), then the ones not awakened
        /// (grey cards: how to awaken, what a tier gives); the buttons absorb a treasure, taste a herb, pill body.</summary>
        public static void Open()
        {
            var w = Player;
            var grade = w.GetGradeLvl();
            var owned = DEFS.Where(d => Instance.Has(w, d.Kind)).ToList();
            var locked = DEFS.Where(d => !Instance.Has(w, d.Kind)).ToList();
            Helper.InfoPanel.Open(GameTool.LS("const_title"), ui =>
            {
                var dy = Helper.InfoPanel.Dy(ui);
                var left = Helper.InfoPanel.Left(ui) + 20f;
                var width = Helper.InfoPanel.Right(ui) - Helper.InfoPanel.Left(ui) - 40f;
                var y = Helper.InfoPanel.Top(ui);
                Helper.InfoPanel.Center(ui, left + width / 2f, y - dy * 0.4f, string.Format(GameTool.LS("const_ui_header"), grade, PillBodyEvent.TierCap(grade)), Helper.InfoPanel.NOTE, 13, FontStyle.Italic);
                y -= dy * 1.0f;
                const float gap = 16f;
                var step = dy * 0.86f;
                var ch = step * 5.3f; // title, progress, 2 lines of how it grows, effect
                float Cards(List<Def> list, float top, bool on)
                {
                    var pos = ModLib.Helper.UIPanelHelper.Grid(list.Count, 2, left, top, width, ch, gap * 0.6f, out var cw);
                    for (var i = 0; i < list.Count; i++)
                    {
                        var d = list[i];
                        var k = Array.IndexOf(DEFS, d);
                        var cx = pos[i].x;
                        var cy = pos[i].y;
                        Helper.InfoPanel.Box(ui, cx, cy, cw, ch, on ? Helper.InfoPanel.CARD_ON : Helper.InfoPanel.CARD_OFF);
                        var tier = Instance.Tier(w, d.Kind);
                        var pts = Instance.Get(w, d.Kind) / Math.Max(1, d.Scale);
                        var main = on ? Helper.InfoPanel.TEXT : Helper.InfoPanel.GREY;
                        var ly = cy - step * 0.6f;
                        Helper.InfoPanel.Text(ui, cx + 10f, ly, Helper.InfoPanel.I(ICONS[k]) + GameTool.LS(d.Key), on ? Helper.InfoPanel.TITLE : Helper.InfoPanel.GREY, 16, FontStyle.Bold);
                        Helper.InfoPanel.RightText(ui, cx + cw - 10f, ly, on ? string.Format(GameTool.LS("const_ui_tier"), tier, GameTool.LS("constluck" + LuckId(d.Kind, tier))) : GameTool.LS("const_ui_locked"),
                            on ? Helper.InfoPanel.GOLD : Helper.InfoPanel.GREY, 14, FontStyle.Bold);
                        ly -= step;
                        string progress;
                        if (d.Innate) progress = GameTool.LS(on ? "const_innate" : "const_not_born");
                        else if (!on) progress = string.Format(GameTool.LS("const_awakening"), pts, PillBodyEvent.PointsOf(1), AwakenChance(pts).ToString("0.00"), AWAKEN_CHANCE_MAX);
                        else
                        {
                            var raw = PillBodyEvent.TierOf(pts, PillBodyEvent.MAX_TIER);
                            progress = string.Format(GameTool.LS("const_progress"), pts, raw >= PillBodyEvent.MAX_TIER ? "-" : PillBodyEvent.PointsOf(Math.Max(1, raw) + 1).ToString(), PillBodyEvent.TierCap(grade));
                        }
                        Helper.InfoPanel.Text(ui, cx + 10f, ly, progress, on ? Helper.InfoPanel.GOLD : Helper.InfoPanel.BAD, 12);
                        ly -= step;
                        foreach (var line in Helper.InfoPanel.Wrap(GameTool.LS(d.Key + "_how"), 82).Take(2))
                        {
                            Helper.InfoPanel.Text(ui, cx + 10f, ly, line, main, 12);
                            ly -= step;
                        }
                        Helper.InfoPanel.Text(ui, cx + 10f, cy - ch + step * 0.55f,
                            on ? GameTool.LS("constluck" + LuckId(d.Kind, tier) + "desc") : GameTool.LS(d.Key + "_gain"), on ? Helper.InfoPanel.GOOD : Helper.InfoPanel.GREY, 12, FontStyle.Bold);
                    }
                    return ModLib.Helper.UIPanelHelper.GridBottom(list.Count, 2, top, ch, gap * 0.6f);
                }
                if (owned.Count > 0)
                {
                    y = Helper.InfoPanel.Section(ui, left, y, width, "daodian", string.Format(GameTool.LS("const_ui_owned"), owned.Count));
                    y = Cards(owned, y - dy * 0.2f, true);
                }
                if (locked.Count > 0)
                {
                    y = Helper.InfoPanel.Section(ui, left, y, width, "kongbai", string.Format(GameTool.LS("const_ui_notyet"), locked.Count));
                    y = Cards(locked, y - dy * 0.2f, false);
                }
                Helper.InfoPanel.Buttons(ui, Math.Max(Helper.InfoPanel.Bottom(ui) + dy * 0.6f, y - dy * 0.9f),
                    (GameTool.LS("const_absorb"), () => ShowResult(AbsorbTreasure())),
                    (GameTool.LS("const_taste"), () => ShowResult(TasteHerb())),
                    (GameTool.LS("pill_title"), () => ModDelayEvent.Instance.DelayEvent(Instance, () => Helper.InfoDialog.ShowLong(GameTool.LS("pill_title"), PillBodyEvent.Summary()), 300)),
                    (GameTool.LS("dlg_ok"), null));
            }, () => Summary(w));
        }

        private static void ShowResult(string key)
        {
            ModDelayEvent.Instance.DelayEvent(Instance, () => Helper.InfoDialog.Msg(GameTool.LS("const_title"), GameTool.LS(key)), 300);
        }
    }
}
