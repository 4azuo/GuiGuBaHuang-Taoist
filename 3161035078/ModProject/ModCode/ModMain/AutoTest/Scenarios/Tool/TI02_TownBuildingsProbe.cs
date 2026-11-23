using ModLib.Helper;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Tool
{
    /// <summary>
    /// TI-02 (investigation, a tool: run only with -Scenario TI-02): a town with a storage (Tree Vault) is entered and the
    /// hierarchy of its town window is written to the log (active / inactive objects, sprites), to find why a
    /// sub-building of the game's data is not drawn in the town window.
    /// </summary>
    public class TI02_TownBuildingsProbe : IScenarioDefinition
    {
        public string Id => "TI-02";

        public Scenario Create()
        {
            return new Scenario { Id = Id, Feature = "Autotest", Title = "Investigation: sub-buildings of the town window", Kind = ScenarioKind.Tool, Overlay = false }
                .Step("travel", c => TravelTo(c, FindTown(t => Has(t, MapBuildSubType.TownStorage) && !t.IsSmallTown()) ?? FindTown(t => Has(t, MapBuildSubType.TownStorage))))
                .Step("enter town", c =>
                {
                    var town = BagTown(c);
                    var subs = System.Enum.GetValues(typeof(MapBuildSubType)).Cast<MapBuildSubType>()
                        .Where(t => (int)t >= 2000 && (int)t < 3000 && town.GetBuildSub(t) != null).Select(t => $"{t}({(int)t})");
                    c.Info($"{town.name} data: {string.Join(" ", subs)}");
                    town.OpenBuild();
                })
                .Wait("town opened", c => g.ui.HasUI(UIType.Town), 10000)
                .Step("dump", c =>
                {
                    var ui = g.ui.GetUI<UITown>(UIType.Town);
                    // the building slots (goPosiRoot / goPosi*: "Fix/<sub-building id>/<item>") in depth
                    var posi = ui.GetComponentsInChildren<UnityEngine.Transform>(true).FirstOrDefault(t => t.name == "Posi");
                    var dump = BattleSceneHelper.DumpHierarchy(posi ?? ui.transform, 6);
                    DebugHelper.WriteLine("TI-02 town window:\n" + dump);
                    c.Info($"town window dumped: {dump.Split('\n').Length} lines (mod log)");
                    c.Snap("town window");
                }, 2000)
                .Step("leave town", c => CloseAll(UIType.Town));
        }
    }
}
