using ModLib.Enum;
using ModLib.Helper;
using System.Collections.Generic;
using MOD_nE7UL2.Mod;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Unit
{
    /// <summary>
    /// TG-52 (ExpertEvent/UnitModifyHelper): expertise on a real equipped skill of the player. Using it adds
    /// exp (insight and grade scaled), enough exp raises the expertise level, and an ability with expertise
    /// gives Atk/Def/Hp/Mp bonuses. The exp of the skill is restored afterwards. Replaces the manual TM-33.
    /// </summary>
    public class TG52_ExpertLevel : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TG-52";

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            DataProps.MartialData martial = null;
            DataUnit.ActionMartialData action = null;
            return new Scenario { Id = "TG-52", Feature = "ExpertEvent", Title = "Expertise level of an equipped skill" }
                .Step("equipped skill", c =>
                {
                    var all = new List<DataUnit.ActionMartialData>();
                    foreach (var m in Player.GetActionMartials()) all.Add(m.Value);
                    action = all
                        .Where(x => x.data.To<DataProps.MartialData>() is DataProps.MartialData md && ExpertEvent.Configs.SkillExpRatios.ContainsKey(md.martialType))
                        .OrderBy(x => x.data.To<DataProps.MartialData>().martialType == MartialType.Ability ? 0 : 1)
                        .FirstOrDefault();
                    martial = action?.data.To<DataProps.MartialData>();
                    if (!c.Assert(martial != null, "the player has an equipped skill")) return;
                    c.Bag["exp"] = ExpertEvent.GetExpertExp(martial.data.soleID);
                    c.Info($"{martial.martialType} {martial.martialInfo.name}: grade {martial.martialInfo.grade}, level {martial.martialInfo.level}, expertise exp {c.Bag["exp"]}");
                })
                .Step("use the skill", c =>
                {
                    if (martial == null) return;
                    var id = martial.data.soleID;
                    var before = ExpertEvent.GetExpertExp(id);
                    var ratio = ExpertEvent.Configs.SkillExpRatios[martial.martialType];
                    ExpertEvent.AddExpertExp(Player, id, ratio);
                    var insight = Player.GetDynProperty(UnitDynPropertyEnum.Talent).value / 100f;
                    var min = (int)(ratio * 100f * Player.GetGradeLvl() * 1f * insight) - 1;
                    var max = (int)(ratio * 100f * Player.GetGradeLvl() * 2f * insight) + 1;
                    c.AssertBetween(ExpertEvent.GetExpertExp(id) - before, min, max, "exp gained by one use");
                })
                .Step("level up", c =>
                {
                    if (martial == null) return;
                    var id = martial.data.soleID;
                    int grade = martial.martialInfo.grade, level = martial.martialInfo.level;
                    var lvl = ExpertEvent.GetExpertLvl(id, grade, level);
                    ExpertEvent.GetExpertExps()[id] = ExpertEvent.GetExpertNeedExp(lvl + 1, grade, level);
                    c.AssertEqual(lvl + 1, ExpertEvent.GetExpertLvl(id, grade, level), $"expertise level {lvl} -> {lvl + 1} with {ExpertEvent.GetExpertNeedExp(lvl + 1, grade, level)} exp");
                    var atk = Player.GetDynProperty(UnitDynPropertyEnum.Attack).baseValue;
                    var bonus = UnitModifyHelper.GetAbilityExpertAtk(atk, lvl + 1, grade, level);
                    c.Assert(bonus >= 0, $"ability expertise Atk bonus at level {lvl + 1}: +{bonus}");
                    if (martial.martialType == MartialType.Ability)
                        c.Assert(bonus > UnitModifyHelper.GetAbilityExpertAtk(atk, 0, grade, level), "bonus grows with the expertise level");
                })
                // the expertise panel (UIEvent: an NPCInfoPreview next to the MartialInfo of the player info, 1 s after it opens)
                .Step("open player info", c => { if (martial != null) g.ui.OpenUISafe<UIPlayerInfo>(UIType.PlayerInfo); }, 1500)
                .Step("open the skill info", c =>
                {
                    if (martial == null || !c.Assert(g.ui.HasUI(UIType.PlayerInfo), "player info opened")) return;
                    g.ui.OpenUISafe<UIMartialInfo>(UIType.MartialInfo).InitData(Player, action, UnityEngine.Vector2.zero);
                }, 2500)
                .Step("expertise panel", c =>
                {
                    if (martial == null) return;
                    c.Assert(g.ui.HasUI(UIType.NPCInfoPreview), "expertise panel next to the skill info");
                    // bugsplat #9: hovering skills quickly closes and reopens the skill info, the queued panel opens again
                    // on the window whose texts the previous call hid (index out of range)
                    for (var i = 0; i < 3; i++)
                    {
                        UIClickHelper.CloseAll(UIType.MartialInfo);
                        g.ui.OpenUISafe<UIMartialInfo>(UIType.MartialInfo).InitData(Player, action, UnityEngine.Vector2.zero);
                    }
                }, 3000)
                .Step("expertise panel reopened", c =>
                {
                    if (martial == null) return;
                    c.Assert(g.ui.HasUI(UIType.NPCInfoPreview), "expertise panel shown again after the skill info reopened");
                    int grade = martial.martialInfo.grade, level = martial.martialInfo.level;
                    var expertLvl = ExpertEvent.GetExpertLvl(martial.data.soleID, grade, level);
                    var expected = ModTextHelper.LSF("ui_expert_lvl", expertLvl, ExpertEvent.GetExpertExp(martial.data.soleID), ExpertEvent.GetExpertNeedExp(expertLvl + 1, grade, level));
                    var shown = g.ui.GetUI<UINPCInfoPreview>(UIType.NPCInfoPreview)?.GetComponentsInChildren<UnityEngine.UI.Text>()
                        .Any(x => x.text == expected) ?? false;
                    c.Assert(shown, $"expertise level text visible: {expected}");
                    UIClickHelper.CloseAll(UIType.MartialInfo, UIType.NPCInfoPreview, UIType.PlayerInfo);
                }, 1000)
                .Step("restore", c =>
                {
                    if (martial == null) return;
                    ExpertEvent.GetExpertExps()[martial.data.soleID] = (int)c.Bag["exp"];
                    c.AssertEqual((int)c.Bag["exp"], ExpertEvent.GetExpertExp(martial.data.soleID), "expertise exp restored");
                });
        }
    }
}
