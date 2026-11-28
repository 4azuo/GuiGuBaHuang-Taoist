using ModLib.AutoTest;
using ModLib.Enum;
using ModLib.Helper;
using MOD_nE7UL2.Mod;
using System.Linq;
using UnityEngine;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;
using static MOD_nE7UL2.AutoTest.TownTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Unit
{
    /// <summary>
    /// TG-86 (ArtisanEvent spots): every area with towns has 2 mine veins and 2 herb valleys near its towns, on free cells of
    /// that area, made rich in ores / herbs; the spirit sense's scan points to the nearest one; the player standing on a mine
    /// vein gets the drama of the stay; the end of a stay (SeclusionEvent.Reward of a year) gives ores of the area's grade
    /// and Mining exp, a herb valley herbs and Herbs exp. Data, place, cells restored.
    /// </summary>
    public class TG86_Spots : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TG-86";

        /// <summary>Option buttons of the open drama really shown: active, with a label (uGUI Text or TextMeshPro).</summary>
        private static int ShownOptions()
        {
            var ui = DramaHelper.GetOpenDrama();
            if (ui == null) return 0;
            return ui.GetComponentsInChildren<UnityEngine.UI.Button>(false)
                .Count(b => b.gameObject.activeInHierarchy && !string.IsNullOrEmpty(ModLib.Helper.UIClickHelper.GetLabel(b)));
        }

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            return new Scenario { Id = "TG-86", Feature = "ArtisanEvent", Title = "Mine veins and herb valleys of every area", Overlay = false }
                .Step("the spots of the areas", c =>
                {
                    if (!c.Assert(ArtisanEvent.Enabled, "ArtisanEvent loaded")) return;
                    var spots = ArtisanEvent.Instance.Spots;
                    var areas = ModMain.Store.Towns.Select(x => x.gridData.areaBaseID).Distinct().ToList();
                    c.Info($"{spots.Count} spots in {areas.Count} areas: {string.Join(", ", areas.OrderBy(x => x).Select(a => $"{a}:{spots.Count(s => s.Area == a && s.Kind == 1)}m/{spots.Count(s => s.Area == a && s.Kind == 2)}h"))}");
                    c.Assert(areas.All(a => spots.Count(s => s.Area == a && s.Kind == (int)ArtisanEvent.SpotKind.Mine) == ArtisanEvent.SPOTS_AN_AREA
                        && spots.Count(s => s.Area == a && s.Kind == (int)ArtisanEvent.SpotKind.Herb) == ArtisanEvent.SPOTS_AN_AREA), "2 mine veins and 2 herb valleys an area");
                    c.Assert(spots.All(s => { var gd = g.data.grid.GetGridData(s.Point()); return gd != null && gd.areaBaseID == s.Area && !gd.IsBuild() && g.world.build.GetBuild(s.Point()) == null; }),
                        "every spot on a free cell of its area");
                    c.Assert(spots.All(s => { var gd = g.data.grid.GetGridData(s.Point()); return s.Kind == 1 ? gd.mineral >= System.Math.Max(ArtisanEvent.SPOT_RICHNESS, gd.mineralMax) : gd.medicinal >= System.Math.Max(ArtisanEvent.SPOT_RICHNESS, gd.medicinalMax); }),
                        "the spot cells are rich");
                    var s0 = spots.First(); var g0 = g.data.grid.GetGridData(s0.Point());
                    c.Info($"first spot ({s0.X},{s0.Y}) kind {s0.Kind}: mineral {g0.mineral}/{g0.mineralMax}, medicinal {g0.medicinal}/{g0.medicinalMax}");
                    Mod.AutoTestEvent.DramaTimeoutSec = 40; // the drama of the stay is read to its options
                    c.Bag["pos"] = Player.GetUnitPos();
                    c.Bag["data"] = ArtisanEvent.Instance.Artisans.TryGetValue(Player.GetUnitId(), out var old) ? Newtonsoft.Json.JsonConvert.SerializeObject(old) : null;
                })
                .Step("the scan points to the nearest", c =>
                {
                    if (!SpiritSenseEvent.Enabled) { c.Info("spirit sense off"); return; }
                    var r = SpiritSenseEvent.Scan(Player);
                    var near = ArtisanEvent.NearestSpot(Player.GetUnitPos());
                    if (!c.Assert(r.SpotName != null && near != null, $"the nearest spot: {r.SpotName}, {r.SpotDistance} cells, direction {r.SpotDirection}")) return;
                    c.AssertEqual(ArtisanEvent.Direction(Player.GetUnitPos(), near.Point()), r.SpotDirection, "its direction");
                })
                .Step("on a mine vein", c =>
                {
                    CloseAll(UIType.TextInfoLong, UIType.TextInfo);
                    // no other drama open (the wait below must see the drama of this vein only)
                    DramaHelper.DismissDramas();
                    var near = ArtisanEvent.NearestSpot(Player.GetUnitPos(), ArtisanEvent.SpotKind.Mine);
                    if (!c.Assert(near != null, "a mine vein")) return;
                    // the spot the mod offers on that cell
                    var s = ArtisanEvent.SpotAt(near.Point()) ?? near;
                    c.Bag["spot"] = s;
                    s.LastOfferMonth = -999;
                    Player.SetUnitPos(s.Point());
                    c.AssertEqual(s, ArtisanEvent.SpotAt(Player.GetUnitPos()), "the player stands on it");
                }, 2500)
                // the options show when the text is written out
                .Wait("the options shown", c => ((ArtisanEvent.Spot)c.Bag["spot"]).LastOfferMonth == GameHelper.GetGameTotalMonth()
                    && DramaHelper.GetOpenDrama() != null && ShownOptions() >= ArtisanEvent.SPOT_YEARS.Length + 1, 20000, 500)
                .Step("the drama of the stay", c =>
                {
                    var s = (ArtisanEvent.Spot)c.Bag["spot"];
                    var text = DramaHelper.GetDramaText();
                    c.Info(text ?? "-");
                    c.Assert(DramaHelper.GetOpenDrama() != null && text != null && text.Contains(s.Area.ToString()), "the drama of the mine vein");
                    c.AssertEqual(ArtisanEvent.SPOT_YEARS.Length + 1, DramaHelper.GetDramaOptions().Count, "1 / 3 / 5 / 10 years or walk on");
                    // the options are really shown (a drama of more than 4 options hid them all: ModLib DramaHelper)
                    var shown = ShownOptions();
                    c.Assert(shown >= ArtisanEvent.SPOT_YEARS.Length + 1, $"{shown} option buttons shown");
                    c.AssertEqual(GameHelper.GetGameTotalMonth(), s.LastOfferMonth, "offered once (cooldown)");
                    DramaHelper.DismissDramas();
                    Mod.AutoTestEvent.DramaTimeoutSec = 5;
                }, 1500)
                .Step("a year in the vein", c =>
                {
                    var s = (ArtisanEvent.Spot)c.Bag["spot"];
                    // a year at each kind: 12 tries, each with its chance, items the game gives in the area
                    foreach (var kind in new[] { ArtisanEvent.SpotKind.Mine, ArtisanEvent.SpotKind.Herb })
                    {
                        var craft = ArtisanEvent.SpotCraft(kind);
                        var pool = ArtisanEvent.SpotPool(kind, s.Area);
                        var value = ArtisanEvent.CraftValue(Player, craft);
                        var tries = ArtisanEvent.GatherTries(12);
                        c.Info($"{kind} area {s.Area}: {pool.Count} items, {craft} value {value}, {tries} tries, expected {ArtisanEvent.ExpectedItems(pool, tries, value)}: "
                            + string.Join(", ", pool.Take(6).Select(x => $"{GameTool.LS(x.Item.name)} L{x.Item.level} need {x.Need} +{x.LevelAboveArea} {ArtisanEvent.GatherChance(value, x.Need, x.LevelAboveArea):0}%")));
                        c.Assert(pool.Count > 0, $"the area gives {kind} items (WorldBlock config)");
                        var exp0 = ArtisanEvent.Get(Player, craft)?.Exp ?? 0;
                        var text = SeclusionEvent.Reward(new SeclusionEvent.Running { Kind = kind == ArtisanEvent.SpotKind.Mine ? SeclusionEvent.Kind.MineVein : SeclusionEvent.Kind.HerbValley, Months = 12, Area = s.Area });
                        c.Info(text);
                        // counted from the reward (a full bag sends the items to the temporary storage)
                        var got = ArtisanEvent.LastSpotGot;
                        c.Assert(got.Values.Sum() <= tries, $"{got.Values.Sum()} items, at most {tries} tries");
                        c.Assert(got.Keys.All(k => pool.Any(x => x.Item.id == k)), "only the items of the area");
                        c.Assert(got.Keys.All(k => ArtisanEvent.GatherCraft(g.conf.itemProps.GetItem(k)) == craft), $"all are of {craft}");
                        var d = ArtisanEvent.Get(Player, craft);
                        c.Assert(got.Count == 0 || (d != null && (d.Rank > 0 || d.Exp > exp0)), $"{craft} exp ({d?.Rank}, {d?.Exp})");
                    }
                })
                .Step("the markers on the map", c =>
                {
                    ArtisanEvent.UpdateSpotMarkers();
                    var spots = ArtisanEvent.Instance.Spots;
                    var shown = spots.Count(x => ArtisanEvent.SpotMarker(x) != null);
                    c.AssertEqual(spots.Count, shown, "a marker for every spot");
                    var s = spots.First();
                    var m = ArtisanEvent.SpotMarker(s);
                    var w = SceneType.map.world.PointToWorld(s.Point());
                    c.Assert(m != null && Vector2.Distance(new Vector2(m.transform.position.x, m.transform.position.y), w) < 0.01f, $"at its cell ({w.x:0.00},{w.y:0.00})");
                    var sr = m?.GetComponent<SpriteRenderer>();
                    c.Assert(sr != null && sr.sprite != null, $"a picture ({sr?.sortingLayerName} {sr?.sortingOrder}, size {sr?.bounds.size.x:0.00})");
                    // the player next to the nearest herb valley: the marker is on screen (the screenshot of the step)
                    var near = ArtisanEvent.NearestSpot(Player.GetUnitPos(), ArtisanEvent.SpotKind.Herb);
                    if (near != null) { near.LastOfferMonth = GameHelper.GetGameTotalMonth(); Player.SetUnitPos(new Vector2Int(near.X + 2, near.Y)); }
                }, 2500)
                .Step("the marker seen", c =>
                {
                    var near = ArtisanEvent.NearestSpot(Player.GetUnitPos(), ArtisanEvent.SpotKind.Herb);
                    c.Assert(near != null && ArtisanEvent.SpotMarker(near) != null, "the herb valley next to the player has its marker (screenshot)");
                })
                .Step("restore", c =>
                {
                    DramaHelper.DismissDramas();
                    var id = Player.GetUnitId();
                    foreach (var cr in ArtisanEvent.CRAFTS) { var d = ArtisanEvent.Get(Player, cr); if (d != null) { d.Rank = 0; d.Essence = 0; } ArtisanEvent.Sync(Player, cr); }
                    ArtisanEvent.Instance.Artisans.Remove(id);
                    if (c.Bag["data"] != null)
                    {
                        var old = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<int, ArtisanEvent.CraftData>>((string)c.Bag["data"]);
                        foreach (var x in old.Values) x.Given = 0;
                        ArtisanEvent.Instance.Artisans[id] = old;
                        foreach (var cr in ArtisanEvent.CRAFTS) ArtisanEvent.Sync(Player, cr);
                    }
                    if (c.Bag.ContainsKey("spot")) ((ArtisanEvent.Spot)c.Bag["spot"]).LastOfferMonth = GameHelper.GetGameTotalMonth();
                    Player.SetUnitPos((Vector2Int)c.Bag["pos"]);
                });
        }
    }
}
