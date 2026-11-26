using ModLib.AutoTest;
using ModLib.Enum;
using ModLib.Helper;
using MOD_nE7UL2.Mod;
using System.Collections.Generic;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Unit
{
    /// <summary>
    /// TG-93 (ConstitutionEvent): the constitutions of the player (points, tiers capped by the realm, the stats they give,
    /// several at once), a treasure absorbed (Dragon Blood), a raw herb tasted (Venom Body, HP lost), the NPCs born with
    /// one (their tier follows the realm) and the window of the player info. The points of the save are put back.
    /// </summary>
    public class TG93_Constitutions : IScenarioDefinition
    {
        public string Id => "TG-93";

        public Scenario Create()
        {
            Dictionary<ConstitutionEvent.Kind, int> saved = null;
            HashSet<ConstitutionEvent.Kind> savedAwake = null;
            return new Scenario { Id = "TG-93", Feature = "ConstitutionEvent", Title = "Constitutions: many at once, treasures, herbs, NPCs, window", Overlay = false }
                .Step("several constitutions", c =>
                {
                    var ev = ConstitutionEvent.Instance;
                    if (!c.Assert(ConstitutionEvent.Enabled, "constitutions on (S&M)")) return;
                    var id = Player.GetUnitId();
                    saved = ev.Points.TryGetValue(id, out var p) ? new Dictionary<ConstitutionEvent.Kind, int>(p) : new Dictionary<ConstitutionEvent.Kind, int>();
                    savedAwake = ev.Awakened.TryGetValue(id, out var aw) ? new HashSet<ConstitutionEvent.Kind>(aw) : new HashSet<ConstitutionEvent.Kind>();
                    ev.Awakened.Remove(id);
                    c.Info($"the player now: {string.Join(", ", saved.Select(x => $"{x.Key} {x.Value}"))}");
                    ev.Points.Remove(id);
                    var grade = Player.GetGradeLvl();
                    // the awakening: points of tier 1, then a chance under 1 % (a roll of 99 fails, a roll of 0 succeeds)
                    c.Assert(!ev.Add(Player, ConstitutionEvent.Kind.SwordBone, PillBodyEvent.PointsOf(1) * 50 - 1, 0f), "below the points of tier 1: no awakening even with the best roll");
                    c.AssertEqual(0, ev.Tier(Player, ConstitutionEvent.Kind.SwordBone), "Sword Bone not awakened (tier 0)");
                    c.Assert(!ev.Add(Player, ConstitutionEvent.Kind.SwordBone, 1, 50f), $"at the points of tier 1: a roll of 50 fails (chance {ConstitutionEvent.AwakenChance(PillBodyEvent.PointsOf(1)):0.00} %)");
                    c.Assert(ev.Add(Player, ConstitutionEvent.Kind.SwordBone, 0, 0.1f), "a roll of 0.1 awakens it");
                    c.AssertEqual(1, ev.Tier(Player, ConstitutionEvent.Kind.SwordBone), "Sword Bone tier 1");
                    ev.Add(Player, ConstitutionEvent.Kind.SwordBone, PillBodyEvent.PointsOf(2) * 50 - PillBodyEvent.PointsOf(1) * 50, 99f);
                    // the other ones awakened for the test, their tiers by points only (no chance)
                    ev.Awaken(Player, ConstitutionEvent.Kind.Vajra); ev.Awaken(Player, ConstitutionEvent.Kind.Thunder);
                    ev.Add(Player, ConstitutionEvent.Kind.Vajra, PillBodyEvent.PointsOf(1) * 50, 99f);
                    ev.Add(Player, ConstitutionEvent.Kind.Thunder, PillBodyEvent.PointsOf(10), 99f);
                    c.AssertEqual(System.Math.Min(2, PillBodyEvent.TierCap(grade)), ev.Tier(Player, ConstitutionEvent.Kind.SwordBone), "Sword Bone tier 2 by points (no roll)");
                    c.AssertEqual(1, ev.Tier(Player, ConstitutionEvent.Kind.Vajra), "Vajra Body tier 1, at the same time");
                    c.AssertEqual(PillBodyEvent.TierCap(grade), ev.Tier(Player, ConstitutionEvent.Kind.Thunder), $"Thunder Body capped by realm {grade}");
                    var (atk, def, hp, mp, basis) = ev.Bonus(Player);
                    var sword = ev.Tier(Player, ConstitutionEvent.Kind.SwordBone); var thunder = ev.Tier(Player, ConstitutionEvent.Kind.Thunder);
                    c.Assert(System.Math.Abs(atk - 0.01 * (sword + thunder)) < 1e-6, $"attack +{atk:P0}: 1 % a tier of Sword Bone and Thunder");
                    c.Assert(System.Math.Abs(def - 0.03) < 1e-6 && System.Math.Abs(hp - 0.02) < 1e-6, $"Vajra: defense +{def:P0}, HP +{hp:P0}");
                    c.Assert(basis.ContainsKey("BasisSword") && basis.ContainsKey("BasisThunder"), $"bases: {string.Join(", ", basis.Select(x => $"{x.Key} +{x.Value:P0}"))}");
                })
                .Step("destinies", c =>
                {
                    var ev = ConstitutionEvent.Instance;
                    ev.SyncLucks(Player);
                    foreach (var k in new[] { ConstitutionEvent.Kind.SwordBone, ConstitutionEvent.Kind.Vajra, ConstitutionEvent.Kind.Thunder })
                    {
                        var tier = ev.Tier(Player, k);
                        var luck = ConstitutionEvent.LuckId(k, tier);
                        c.Assert(Player.GetLuck(luck) != null, $"{k} tier {tier}: destiny {luck} '{GameTool.LS("constluck" + luck)}'");
                        c.Assert(Enumerable.Range(1, 10).Where(t => t != tier).All(t => Player.GetLuck(ConstitutionEvent.LuckId(k, t)) == null), $"{k}: no destiny of another tier");
                    }
                    c.Assert(Player.GetLuck(ConstitutionEvent.LuckId(ConstitutionEvent.Kind.Venom, 1)) == null || ev.Tier(Player, ConstitutionEvent.Kind.Venom) == 1, "no destiny of a constitution not awakened");
                })
                .Step("treasure", c =>
                {
                    var ev = ConstitutionEvent.Instance;
                    var herbs = g.conf.itemProps._allConfList.ToArray().Where(x => x.IsHerbItem() != null && x.level >= ConstitutionEvent.TREASURE_LEVEL).OrderBy(x => x.level).ToList();
                    if (!c.Assert(herbs.Count > 0, "a herb of level 4+ in the configs")) return;
                    var none = ConstitutionEvent.AbsorbTreasure();
                    c.Info($"without giving one: {none}");
                    Player.AddUnitProp(herbs[0].id, 1);
                    ev.Awaken(Player, ConstitutionEvent.Kind.DragonBlood);
                    var before = ev.Get(Player, ConstitutionEvent.Kind.DragonBlood);
                    c.AssertEqual("const_absorbed", ConstitutionEvent.AbsorbTreasure(), $"treasure {herbs[0].id} (level {herbs[0].level}) absorbed");
                    c.Assert(ev.Get(Player, ConstitutionEvent.Kind.DragonBlood) - before >= herbs[0].level * herbs[0].level, $"Dragon Blood points + level² ({ev.Get(Player, ConstitutionEvent.Kind.DragonBlood)})");
                }, 1000)
                .Step("raw herb", c =>
                {
                    var ev = ConstitutionEvent.Instance;
                    var herb = g.conf.itemProps._allConfList.ToArray().FirstOrDefault(x => x.IsHerbItem() != null && x.level <= 2);
                    if (!c.Assert(herb != null, "a common herb")) return;
                    Player.AddUnitProp(herb.id, 1);
                    Player.SetProperty<int>(UnitPropertyEnum.Hp, Player.GetDynProperty(UnitDynPropertyEnum.HpMax).value);
                    var hp = Player.GetProperty<int>(UnitPropertyEnum.Hp);
                    var before = ev.Get(Player, ConstitutionEvent.Kind.Venom);
                    c.AssertEqual("const_tasted", ConstitutionEvent.TasteHerb(), "a herb tasted");
                    c.Assert(ev.Get(Player, ConstitutionEvent.Kind.Venom) > before, $"Venom points {before} -> {ev.Get(Player, ConstitutionEvent.Kind.Venom)}");
                    c.Assert(Player.GetProperty<int>(UnitPropertyEnum.Hp) < hp, $"HP {hp} -> {Player.GetProperty<int>(UnitPropertyEnum.Hp)}");
                }, 1000)
                .Step("NPCs", c =>
                {
                    var ev = ConstitutionEvent.Instance;
                    var npcs = Npcs.Where(x => x != null && !x.isDie).ToList();
                    var born = npcs.Where(x => ev.Has(x, ConstitutionEvent.Kind.InnateSpirit)).ToList();
                    c.Info($"{npcs.Count} NPCs: {born.Count} born with the Innate Spirit Body, {npcs.Count(x => ev.Has(x, ConstitutionEvent.Kind.DragonBlood))} with Dragon Blood, " +
                        $"{npcs.Count(x => ev.Tier(x, ConstitutionEvent.Kind.SwordBone) > 0)} with Sword Bone, {npcs.Count(x => ev.Tier(x, ConstitutionEvent.Kind.Vajra) > 0)} with Vajra");
                    if (born.Count == 0) { c.Skip("no NPC born with the Innate Spirit Body"); return; }
                    var n = born[0];
                    c.AssertEqual(System.Math.Min(10, System.Math.Max(1, n.GetGradeLvl())), ev.Tier(n, ConstitutionEvent.Kind.InnateSpirit), $"{n.GetName()}: innate tier = realm {n.GetGradeLvl()}");
                    var many = npcs.Select(x => (x, k: ConstitutionEvent.DEFS.Count(d => ev.Tier(x, d.Kind) > 0))).OrderByDescending(x => x.k).First();
                    c.Info($"most constitutions: {many.x.GetName()} ({many.k}): {ConstitutionEvent.Summary(many.x).Replace("\n", " | ").Substring(0, System.Math.Min(300, ConstitutionEvent.Summary(many.x).Length))}");
                })
                .Step("window", c => g.ui.OpenUISafe<UIPlayerInfo>(UIType.PlayerInfo), 1500)
                .Step("constitution button", c =>
                {
                    var btn = FindButtons(UIType.PlayerInfo, GameTool.LS("const_title")).FirstOrDefault();
                    if (!c.Assert(btn != null, "'Constitutions' button in the player info")) return;
                    btn.Item.onClick.Invoke();
                }, 1500)
                .Step("constitution window", c =>
                {
                    c.Assert(g.ui.HasUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType), "the constitutions are shown");
                    c.Snap("window");
                }, 1500)
                .Step("artisan ranks button", c =>
                {
                    g.ui.CloseUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType);
                    var btn = UIEvent.ApprenticeHelpButton as ModLib.Object.UIItemButton;
                    if (!c.Assert(btn != null && btn.Item != null, "the '?' of the artisan ranks in the player info")) return;
                    btn.Item.onClick.Invoke();
                }, 1500)
                .Step("artisan ranks", c =>
                {
                    c.Assert(g.ui.HasUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType), "the artisan ranks are shown");
                    var text = UnitTypeEvent.ApprenticeSummary(Player, 0);
                    c.Assert(text.Contains(GameTool.LS("apprentice420011010")) && text.Contains(GameTool.LS("apprentice420011001desc")), "names and effects of the 10 alchemy ranks");
                    c.Assert(text.Contains(MOD_nE7UL2.Enum.ApprenticeLuckEnum.ApprenticeLevels[9].ToString()), "the value of the last rank");
                    c.Assert(UnitTypeEvent.APPRENTICE_ARTS.Length == 6 && Enumerable.Range(0, 6).All(i => !string.IsNullOrEmpty(UnitTypeEvent.ApprenticeSummary(Player, i))), "a page for each of the 6 arts");
                    c.Snap("artisan-ranks");
                }, 1500)
                .Step("martial ranks button", c =>
                {
                    g.ui.CloseUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType);
                    var btn = UIEvent.MartialHelpButton as ModLib.Object.UIItemButton;
                    if (!c.Assert(btn != null && btn.Item != null, "the '?' of the martial ranks (Martial Arts column)")) return;
                    btn.Item.onClick.Invoke();
                }, 1500)
                .Step("martial ranks", c =>
                {
                    c.Assert(g.ui.HasUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType), "the martial ranks are shown");
                    var text = UnitTypeEvent.TraineeSummary(Player, true);
                    c.Assert(text.Contains(GameTool.LS("trainee420071010")) && text.Contains(GameTool.LS("trainee420071001desc")), "names and effects of the 10 martial ranks");
                    var magic = UnitTypeEvent.TraineeSummary(Player, false);
                    c.Assert(magic.Contains(GameTool.LS("trainee420081010")), "the magic ranks too (Spiritual Root column)");
                    c.Assert(UIEvent.MagicHelpButton != null, "the '?' of the magic ranks");
                    c.Snap("martial-ranks");
                }, 1500)
                .Step("rebirth button", c =>
                {
                    g.ui.CloseUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType);
                    var btn = UIEvent.RebirthHelpButton as ModLib.Object.UIItemButton;
                    if (SMLocalConfigsEvent.Instance.Configs.NoRebirth) { c.Skip("rebirth off (S&M)"); return; }
                    if (!c.Assert(btn != null && btn.Item != null, "the '?' beside the rebirth button")) return;
                    btn.Item.onClick.Invoke();
                }, 1500)
                .Step("rebirth window", c =>
                {
                    if (SMLocalConfigsEvent.Instance.Configs.NoRebirth) return;
                    c.Assert(g.ui.HasUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType), "the rebirth window is shown");
                    var text = RebirthEvent.Help(Player);
                    c.Assert(text.Contains(GameTool.LS("rebirth_help_cond")) && text.Contains(GameTool.LS("rebirth_help_gain")) && text.Contains(GameTool.LS("rebirth_help_cost")), "conditions, gains, costs");
                    c.Assert(text.Contains(GameTool.LS("rebirth420101010")), "the 10 destinies of rebirth");
                    c.Snap("rebirth-help");
                }, 1500)
                .Step("artisan detail", c =>
                {
                    g.ui.CloseUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType);
                    UnitTypeEvent.OpenApprenticeDetail(0);
                }, 1500)
                .Step("artisan detail shown", c =>
                {
                    c.Assert(g.ui.HasUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType), "the 10 ranks of alchemy are shown");
                    var r = UnitTypeEvent.ApprenticeRanks(Player, 0);
                    c.AssertEqual(10, r.List.Count, "10 ranks");
                    c.Snap("artisan-detail");
                }, 1500)
                .Step("merchant button", c =>
                {
                    g.ui.CloseUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType);
                    var btn = UIEvent.MerchantHelpButton as ModLib.Object.UIItemButton;
                    if (!c.Assert(btn != null && btn.Item != null, "the '?' of the merchant ranks beside the bag")) return;
                    btn.Item.onClick.Invoke();
                }, 1500)
                .Step("merchant ranks", c =>
                {
                    c.Assert(g.ui.HasUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType), "the merchant ranks are shown");
                    var r = UnitTypeEvent.MerchantRanks(Player);
                    c.Assert(r.List.Count == 10 && r.List[9].Name == GameTool.LS("merchant420091010"), "10 merchant ranks");
                    c.Snap("merchant-ranks");
                }, 1500)
                .Step("restore", c =>
                {
                    CloseAll(MOD_nE7UL2.Helper.InfoDialog.LastUIType, UIType.PlayerInfo);
                    var id = Player.GetUnitId();
                    ConstitutionEvent.Instance.Points[id] = saved ?? new Dictionary<ConstitutionEvent.Kind, int>();
                    ConstitutionEvent.Instance.Awakened[id] = savedAwake ?? new HashSet<ConstitutionEvent.Kind>();
                    ConstitutionEvent.Instance.SyncLucks(Player);
                    c.Info("points of the save put back (and the destinies)");
                }, 1000);
        }
    }
}
