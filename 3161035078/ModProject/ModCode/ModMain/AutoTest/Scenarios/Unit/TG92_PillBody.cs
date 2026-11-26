using ModLib.AutoTest;
using ModLib.Enum;
using ModLib.Helper;
using MOD_nE7UL2.Mod;
using MOD_nE7UL2.Story;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Unit
{
    /// <summary>
    /// TG-92 (ConfBalance/PillBodyEvent): the skill values of the configs follow the S&amp;M rates of the save and are worked out
    /// from the values of the game (applying twice gives the same values); the focus of the recovery potions; the pill body
    /// (tiers, the summary, the button of the player info); the Nine-Turn Golden Pill encounter (paid with herbs and
    /// spirit stones); the reward of storyline S. The flags of the save are put back.
    /// </summary>
    public class TG92_PillBody : IScenarioDefinition
    {
        public string Id => "TG-92";

        private static SMGlobalConfigsEvent Cfg => SMLocalConfigsEvent.Instance.Configs;

        public Scenario Create()
        {
            (int points, int logged, bool nine, bool seen, bool heal, bool focus) saved = default;
            return new Scenario { Id = "TG-92", Feature = "ConfBalance/PillBodyEvent", Title = "Skill values by S&M, pill body, Nine-Turn Golden Pill", Overlay = false }
                .Step("skill values", c =>
                {
                    var item = g.conf.battleSkillValue._allConfList.ToArray().FirstOrDefault(x => ConfBalance.KindOfSkillValue(x.key) == ConfBalance.Kind.MpCost && ConfBalance.Original(x.key, 5) > 0);
                    if (!c.Assert(item != null, "an MP cost value of a skill")) return;
                    var orig = ConfBalance.Original(item.key, 5).Value;
                    var ratio = ConfBalance.Ratio(ConfBalance.Kind.MpCost, 5);
                    var rate0 = Cfg.SkillMpCostRate;
                    c.AssertEqual(ConfBalance.Scale(orig, ratio, rate0), item.value5.Parse<int>(), $"{item.key} grade 5 = {orig} x {ratio} x (1 + {rate0})");
                    try
                    {
                        Cfg.SkillMpCostRate = 0.5f;
                        ConfBalance.Apply(Cfg);
                        ConfBalance.Apply(Cfg);
                        c.AssertEqual(ConfBalance.Scale(orig, ratio, 0.5), item.value5.Parse<int>(), "S&M MP cost +50 %, applied twice: x1.5 once");
                        Cfg.SkillMpCostRate = -0.5f;
                        ConfBalance.Apply(Cfg);
                        c.AssertEqual(ConfBalance.Scale(orig, ratio, -0.5), item.value5.Parse<int>(), "S&M MP cost -50 %");
                    }
                    finally { Cfg.SkillMpCostRate = rate0; ConfBalance.Apply(Cfg); }
                    c.AssertEqual(ConfBalance.Scale(orig, ratio, rate0), item.value5.Parse<int>(), "back to the rate of the save");
                    var m = g.conf.battleSkillMastery._allConfList.ToArray().FirstOrDefault();
                    if (m != null)
                        c.AssertEqual(ConfBalance.Scale(ConfBalance.OriginalMastery(m, 3) ?? 0, ConfBalance.Ratio(ConfBalance.Kind.MasteryExp, 3), Cfg.SkillMasteryExpRate), m.grade3, "skill mastery exp of grade 3");
                })
                .Step("potions", c =>
                {
                    for (var grade = 1; grade <= 10; grade++)
                    {
                        var pill = g.conf.itemProps._allConfList.ToArray().Select(p => p.IsPotion()).FirstOrDefault(p => p != null && p.grade == grade);
                        if (pill == null) { c.Info($"no recovery potion of grade {grade}"); continue; }
                        var (hp, mp) = PillBodyEvent.Recovery(pill);
                        c.Info($"potion {pill.id} grade {grade}: {hp:#,0} HP, {mp:#,0} MP, focus {pill.spCost}, cd {pill.applyCD}");
                        c.Assert(hp > 0 || mp > 0, $"potion of grade {grade} recovers something");
                    }
                })
                .Step("pill body", c =>
                {
                    var i0 = PillBodyEvent.Instance;
                    saved = (i0.Points, i0.LoggedTier, i0.NineTurn, i0.NineTurnSeen, i0.KingHeal, i0.KingFocus);
                    var grade = Player.GetGradeLvl();
                    PillBodyEvent.Instance.Points = PillBodyEvent.PointsOf(10);
                    ConstitutionEvent.Instance.Awaken(Player, ConstitutionEvent.Kind.Pill);
                    c.AssertEqual(PillBodyEvent.TierCap(grade), PillBodyEvent.Instance.Tier, $"2,000 points at realm {grade}: the tier of the realm cap");
                    var text = PillBodyEvent.Summary();
                    c.Info(text);
                    c.Assert(!string.IsNullOrEmpty(text) && text.Contains(PillBodyEvent.PointsOf(10).ToString()), "summary shows the points");
                })
                .Step("player info button", c => g.ui.OpenUISafe<UIPlayerInfo>(UIType.PlayerInfo), 1500)
                .Step("pill body window", c =>
                {
                    // the constitutions window holds the pill body (its own button when the constitutions are off)
                    var label = ConstitutionEvent.Enabled ? "const_title" : "pill_title";
                    var btn = FindButtons(UIType.PlayerInfo, GameTool.LS(label)).FirstOrDefault();
                    if (!c.Assert(btn != null, $"'{GameTool.LS(label)}' button in the player info")) return;
                    c.Snap("button");
                    btn.Item.onClick.Invoke();
                }, 1500)
                .Step("pill body details", c =>
                {
                    if (!ConstitutionEvent.Enabled) return;
                    var b = MOD_nE7UL2.Helper.InfoDialog.LastUI?.Items.OfType<ModLib.Object.UIItemButton>().FirstOrDefault(x => x.GetFormat() == GameTool.LS("pill_title"));
                    if (!c.Assert(b != null, "'Pill Body' button in the constitutions window")) return;
                    b.Item.onClick.Invoke();
                }, 1500)
                .Step("pill body shown", c =>
                {
                    c.Assert(g.ui.HasUI(MOD_nE7UL2.Helper.InfoDialog.LastUIType), "the summary is shown");
                    c.Snap("summary");
                }, 1500)
                .Step("close the summary", c => CloseAll(MOD_nE7UL2.Helper.InfoDialog.LastUIType, UIType.PlayerInfo), 1000)
                .Step("nine-turn: no herbs", c =>
                {
                    PillBodyEvent.Instance.NineTurn = false;
                    foreach (var h in Player.GetUnitProps().Where(x => x.propsItem?.IsHerbItem() != null).ToList())
                        c.Bag["herb_" + h.soleID] = h.propsCount;
                    c.Info($"herbs in the bag: {Player.GetUnitProps().Where(x => x.propsItem?.IsHerbItem() != null).Sum(x => x.propsCount)}");
                    if (Player.GetUnitProps().Where(x => x.propsItem?.IsHerbItem() != null).Sum(x => x.propsCount) < PillBodyEvent.NINE_TURN_HERBS)
                    {
                        c.AssertEqual("pill_nineturn_noherb", PillBodyEvent.ResolveNineTurn(0), "not enough herbs");
                        c.Assert(!PillBodyEvent.Instance.NineTurn, "no pill without the herbs");
                    }
                }, 1500)
                .Step("nine-turn: refined", c =>
                {
                    DismissDramas();
                    for (var i = 0; i < PillBodyEvent.NINE_TURN_HERBS; i++) EncounterEvent.GiveItem(x => x.IsHerbItem() != null, 1);
                    var money = Player.GetUnitMoney();
                    Player.AddUnitMoney(PillBodyEvent.NineTurnCost());
                    PillBodyEvent.OpenNineTurn();
                    c.Assert(PillBodyEvent.Instance.NineTurnSeen, "the encounter is seen (once)");
                    c.Assert(!PillBodyEvent.NineTurnPossible(), "it does not come again");
                    c.Snap("encounter");
                    c.AssertEqual("pill_nineturn_done", PillBodyEvent.ResolveNineTurn(0), "refined with the herbs and stones");
                    c.Assert(PillBodyEvent.Instance.NineTurn, "the Nine-Turn Golden Pill is kept");
                    c.AssertEqual(money, Player.GetUnitMoney(), "the stones paid");
                }, 1500)
                .Step("storyline S reward", c =>
                {
                    DismissDramas();
                    LineS_MedicineKing.Reward(true);
                    c.Assert(PillBodyEvent.Instance.KingHeal && !PillBodyEvent.Instance.KingFocus, "ending Heal: +30 % recovery");
                    LineS_MedicineKing.Reward(false);
                    c.Assert(!PillBodyEvent.Instance.KingHeal && PillBodyEvent.Instance.KingFocus, "ending Focus: half of the focus back");
                })
                .Step("restore", c =>
                {
                    DismissDramas();
                    var p = PillBodyEvent.Instance;
                    p.Points = saved.points; p.LoggedTier = saved.logged; p.NineTurn = saved.nine; p.NineTurnSeen = saved.seen; p.KingHeal = saved.heal; p.KingFocus = saved.focus;
                    c.Info("flags of the save put back");
                }, 1000);
        }
    }
}
