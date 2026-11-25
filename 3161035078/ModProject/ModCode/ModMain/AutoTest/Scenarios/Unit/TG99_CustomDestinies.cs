using ModLib.AutoTest;
using ModLib.Helper;
using MOD_nE7UL2.Mod;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Unit
{
    /// <summary>
    /// TG-99 (CustomDestinyEvent.Apply): the destinies chosen at the character creation are given as born ones, their
    /// effect works, the score of the save pays for them, and the player info shows them.
    /// </summary>
    public class TG99_CustomDestinies : IScenarioDefinition
    {
        public string Id => "TG-99";

        /// <summary>Deadly Fingers: Spear and Finger +15.</summary>
        private const int STAT_DESTINY = 1414;

        public Scenario Create()
        {
            return new Scenario { Id = "TG-99", Feature = "CustomDestinyEvent", Title = "Chosen destinies given to the player", Overlay = false }
                .Step("give destinies", c =>
                {
                    var data = Player.data.unitData.propertyData;
                    var before = data.bornLuck.Length;
                    var score = SMLocalConfigsEvent.Instance.Configs.Score;
                    var ids = CustomDestinyEvent.All().Where(x => x.Level >= 1 && x.Id != STAT_DESTINY && data.bornLuck.All(l => l.id != x.Id)).OrderBy(x => x.Level).Take(3).Select(x => x.Id).ToList();
                    var hadStat = data.bornLuck.Any(l => l.id == STAT_DESTINY);
                    if (!hadStat) ids.Add(STAT_DESTINY);
                    var spear = Player.data.dynUnitData.basisSpear.value;
                    CustomDestinyEvent.Apply(Player, ids);
                    c.AssertEqual(before + ids.Count, data.bornLuck.Length, "born destinies");
                    c.Assert(ids.All(id => Player.GetLuck(id) != null), $"every destiny active: {string.Join(", ", ids)}");
                    c.Assert(hadStat || Player.data.dynUnitData.basisSpear.value == spear + 15, $"effect of the destiny: spear {spear} -> {Player.data.dynUnitData.basisSpear.value}");
                    var cost = CustomDestinyEvent.TotalPenalty(ids.Select(id => g.conf.roleCreateFeature.GetItem(id).level));
                    c.AssertEqual(score - cost, SMLocalConfigsEvent.Instance.Configs.Score, "score of the save");
                    CustomDestinyEvent.Apply(Player, ids);
                    c.AssertEqual(score - cost, SMLocalConfigsEvent.Instance.Configs.Score, "given twice: not paid twice");
                    c.AssertEqual(before + ids.Count, data.bornLuck.Length, "given twice: not doubled");
                })
                .Step("player info", c => g.ui.OpenUISafe<UIPlayerInfo>(UIType.PlayerInfo), 3000)
                .Step("close", c =>
                {
                    c.Assert(g.ui.HasUI(UIType.PlayerInfo), "player info opens with more than 3 born destinies (screenshot)");
                    g.ui.CloseUI(UIType.PlayerInfo);
                }, 1000);
        }
    }
}
