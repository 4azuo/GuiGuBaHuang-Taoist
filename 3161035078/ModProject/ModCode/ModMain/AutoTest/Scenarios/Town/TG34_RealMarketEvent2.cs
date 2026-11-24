using ModLib.AutoTest;
using ModLib.Helper;
using MOD_nE7UL2.Mod;
using ModLib.Object;
using System.Linq;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Town
{
    /// <summary>TG-34 (RealMarketEvent2): the shop shows the town budget and the buy price rate.</summary>
    public class TG34_RealMarketEvent2 : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TG-34";

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            return new Scenario { Id = "TG-34", Feature = "RealMarketEvent2", Title = "Shop: town budget and buy price rate", Overlay = false }
                .Step("travel", c => TravelTo(c, FindTown(t => Has(t, MapBuildSubType.TownMarketPill))))
                .Step("open shop", c =>
                {
                    var town = CurrentTown;
                    if (town == null) { c.Skip("player is not in a town"); return; }
                    if (!town.allBuildSub.ContainsKey(MapBuildSubType.TownMarketPill)) { c.Skip($"{town.name} has no shop"); return; }
                    OpenTownUI<UITownMarketBuy>(UIType.TownMarketBuy, ui => ui.InitData(town));
                })
                .Step("texts", c =>
                {
                    if (!g.ui.HasUI(UIType.TownMarketBuy)) { c.Skip("shop not opened"); return; }
                    var texts = UIHelper.GetUICustomBase(UIType.TownMarketBuy).SelectMany(x => x.Items).OfType<UIItemText>().Select(x => TextIconHelper.GetShownText(x.InnerText)).ToList();
                    c.Assert(texts.Any(x => x.StartsWith("Market:")), $"budget text: {texts.FirstOrDefault(x => x.StartsWith("Market:"))}");
                    c.Assert(texts.Any(x => x.StartsWith("Price rate:")), $"rate text: {texts.FirstOrDefault(x => x.StartsWith("Price rate:"))}");
                    c.AssertBetween(RealMarketEvent2.GetBuyRate(CurrentTown, Player), 1, 1000, "buy rate (%)");
                })
                // bugsplat #11: a town without a pill shop (only a book/clothes shop, also one built later) had no rate:
                // opening its shop threw KeyNotFoundException in GetBuyRate (GetSellRate the same)
                .Step("town without a rate", c =>
                {
                    CloseAll(UIType.TownMarketBuy);
                    var town = CurrentTown;
                    if (town == null || !town.allBuildSub.ContainsKey(MapBuildSubType.TownMarketPill)) { c.Skip("no shop"); return; }
                    c.Bag["buy"] = RealMarketEvent2.Instance.MarketPriceRate.TryGetValue(town.buildData.id, out var b) ? (object)b : null;
                    c.Bag["sell"] = RealMarketEvent.Instance.MarketPriceRate.TryGetValue(town.buildData.id, out var s) ? (object)s : null;
                    RealMarketEvent2.Instance.MarketPriceRate.Remove(town.buildData.id);
                    RealMarketEvent.Instance.MarketPriceRate.Remove(town.buildData.id);
                    OpenTownUI<UITownMarketBuy>(UIType.TownMarketBuy, ui => ui.InitData(town));
                }, 1500)
                .Step("default rate", c =>
                {
                    if (!c.Bag.ContainsKey("buy")) return;
                    var town = CurrentTown;
                    c.Assert(g.ui.HasUI(UIType.TownMarketBuy), "shop opened without a rate");
                    c.AssertEqual(100f, RealMarketEvent2.Instance.MarketPriceRate.TryGetValue(town.buildData.id, out var b) ? b : -1f, "buy rate 100 % added");
                    c.Assert(RealMarketEvent.GetSellRate(town, Player) > 0, "sell rate without an entry");
                    c.AssertEqual(100f, RealMarketEvent.Instance.MarketPriceRate.TryGetValue(town.buildData.id, out var s) ? s : -1f, "sell rate 100 % added");
                    if (c.Bag["buy"] is float ob) RealMarketEvent2.Instance.MarketPriceRate[town.buildData.id] = ob;
                    if (c.Bag["sell"] is float os) RealMarketEvent.Instance.MarketPriceRate[town.buildData.id] = os;
                })
                .Step("close", c =>
                {
                    CloseAll(UIType.TownMarketBuy);
                    c.Assert(!g.ui.HasUI(UIType.TownMarketBuy), "shop closed");
                });
        }
    }
}
