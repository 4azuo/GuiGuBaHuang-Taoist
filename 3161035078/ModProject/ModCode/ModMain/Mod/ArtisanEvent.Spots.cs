using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Mine veins and herb valleys (mỏ khoáng, thung lũng dược liệu): a few rich cells in every area, near its towns (2 of each
    /// an area, fixed for the save). Their cells are rich in ores / herbs for the game's own gathering. When the player
    /// stands on one, a drama offers to stay 1, 3, 5 or 10 years (a seclusion, SeclusionEvent.Kind.MineVein / HerbValley):
    /// at the end the ores / herbs gathered and their Mining / Herbs exp: 1 try a month; each try
    /// picks an item the game gives in the area (SpotPool: WorldBlockHerb weights / the WorldBlockMine row of the area) and
    /// succeeds with GatherChance (the craft value against the item's need, less for an item above the area's level). Every
    /// spot has a marker on the world map. The spirit sense's scan tells where the nearest one is.
    /// </summary>
    public partial class ArtisanEvent
    {
        public enum SpotKind { Mine = 1, Herb = 2 }

        public class Spot
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Area { get; set; }
            public int Kind { get; set; }
            public int LastOfferMonth { get; set; } = -999;
            public Vector2Int Point() => new Vector2Int(X, Y);
        }

        /// <summary>The spots of the save (made once, at load).</summary>
        public List<Spot> Spots { get; set; } = new List<Spot>();

        public const int SPOTS_AN_AREA = 2;           // of each kind
        public const int SPOT_MIN_RANGE = 4, SPOT_MAX_RANGE = 12;   // cells from a town of the area
        public const int SPOT_RICHNESS = 9;           // least max of herbs / ores of a spot cell (the game's cells hold 0..9); kept full every month
        public const int SPOT_COOLDOWN = 12;          // months before a spot offers again
        public static readonly int[] SPOT_YEARS = { 1, 3, 5, 10 };

        public static Craft SpotCraft(SpotKind k) => k == SpotKind.Mine ? Craft.Mining : Craft.Herbal;

        // ------------------------------------------------------------------ spots: gathering tries

        public const int GATHER_TRIES_A_MONTH = 1;    // tries a month of a stay at a spot (the craft rank raises the chance: its craft value)
        public const float GATHER_CHANCE_FULL = 80f;  // chance (%) of a try when the craft value equals the item's need
        public const float GATHER_CHANCE_LEVEL = 15f; // chance (%) lost a level of the item above the area's level
        public const float GATHER_CHANCE_MIN = 5f, GATHER_CHANCE_MAX = 95f;
        public const int HERB_AREAS = 13;             // weight1..weight13 of WorldBlockHerb
        /// <summary>Mining value an ore needs, by its level (WorldBlockMine has no need: about the basHerb of the herbs of the level).</summary>
        public static readonly int[] MINE_NEED = { 0, 0, 12, 45, 120, 240, 390 };

        /// <summary>Gathering tries of a stay at a spot: 1 a month.</summary>
        public static int GatherTries(int months) => GATHER_TRIES_A_MONTH * Math.Max(0, months);

        /// <summary>
        /// Chance (%) of a gathering try: 80 x craft value (Herbs / Mining) / the item's need (basHerb of an herb, MINE_NEED of
        /// an ore), -15 a level of the item above the area's level, 5..95 (an item of no need: 95).
        /// </summary>
        public static float GatherChance(int value, int need, int levelAboveArea)
        {
            if (need <= 0) return GATHER_CHANCE_MAX;
            var c = GATHER_CHANCE_FULL * Math.Max(0, value) / need - GATHER_CHANCE_LEVEL * Math.Max(0, levelAboveArea);
            return Math.Max(GATHER_CHANCE_MIN, Math.Min(GATHER_CHANCE_MAX, c));
        }

        public static int MineNeed(int level) => MINE_NEED[Math.Max(0, Math.Min(MINE_NEED.Length - 1, level))];

        /// <summary>An item a spot gives: its config, the craft value it needs, its weight and level above the area's level.</summary>
        public class GatherOption
        {
            public ConfItemPropsItem Item;
            public int Need;
            public int Weight;
            public int LevelAboveArea;
        }

        /// <summary>Weight of a WorldBlockHerb row in an area (weight1..weight13).</summary>
        private static int HerbWeight(ConfWorldBlockHerbItem x, int area)
        {
            switch (Math.Max(1, Math.Min(HERB_AREAS, area)))
            {
                case 1: return x.weight1;
                case 2: return x.weight2;
                case 3: return x.weight3;
                case 4: return x.weight4;
                case 5: return x.weight5;
                case 6: return x.weight6;
                case 7: return x.weight7;
                case 8: return x.weight8;
                case 9: return x.weight9;
                case 10: return x.weight10;
                case 11: return x.weight11;
                case 12: return x.weight12;
                default: return x.weight13;
            }
        }

        /// <summary>
        /// What a spot gives in an area, as the game's gathering there: herbs of WorldBlockHerb with a weight in the area (need:
        /// basHerb), ores of the WorldBlockMine row of the area (the nearest area with a row; need: MINE_NEED, same weight).
        /// The area's level is the lowest level of its items that need a value (the others are common).
        /// </summary>
        public static List<GatherOption> SpotPool(SpotKind kind, int area)
        {
            var rs = new List<GatherOption>();
            void Add(int id, int need, int weight)
            {
                var item = id > 0 ? g.conf.itemProps.GetItem(id) : null;
                if (item == null || item.sale <= 0 || weight <= 0) return;
                var old = rs.FirstOrDefault(x => x.Item.id == id);
                if (old != null) { old.Weight += weight; return; }
                rs.Add(new GatherOption { Item = item, Need = need, Weight = weight });
            }
            if (kind == SpotKind.Herb)
            {
                foreach (var x in g.conf.worldBlockHerb._allConfList.ToArray())
                    if (x != null) Add(x.item, x.basHerb, HerbWeight(x, area));
            }
            else
            {
                var rows = g.conf.worldBlockMine._allConfList.ToArray().Where(x => x != null && x.item != null && x.item.Length > 0).ToArray();
                if (rows.Length > 0)
                {
                    var best = rows.Min(x => Math.Abs(x.area - area));
                    foreach (var x in rows.Where(x => Math.Abs(x.area - area) == best))
                        for (var i = 0; i < x.item.Length; i++)
                        {
                            var item = g.conf.itemProps.GetItem(x.item[i]);
                            if (item != null) Add(item.id, MineNeed(item.level), 100);
                        }
                }
            }
            var real = rs.Where(x => x.Need > 0).ToList();
            var areaLevel = real.Count > 0 ? real.Min(x => x.Item.level) : 1;
            foreach (var x in rs)
                x.LevelAboveArea = Math.Max(0, x.Item.level - areaLevel);
            return rs;
        }

        /// <summary>Items expected from tries in a pool (the weighted mean chance).</summary>
        public static int ExpectedItems(List<GatherOption> pool, int tries, int value)
        {
            if (pool == null || pool.Count == 0) return tries;
            var total = pool.Sum(x => (double)x.Weight);
            var mean = pool.Sum(x => x.Weight * (double)GatherChance(value, x.Need, x.LevelAboveArea)) / Math.Max(1.0, total) / 100.0;
            return (int)Math.Round(tries * mean);
        }

        private static GatherOption PickOption(List<GatherOption> pool)
        {
            var r = CommonTool.Random(0, Math.Max(1, pool.Sum(x => x.Weight)));
            foreach (var x in pool)
            {
                if (r < x.Weight) return x;
                r -= x.Weight;
            }
            return pool[pool.Count - 1];
        }

        // ------------------------------------------------------------------ markers on the world map

        public const float MARKER_CELL_SIZE = 1.6f;   // width of a marker, in cells (the game's resource pictures)
        public static readonly string[] MARKER_IMAGES = { null, "map_mine.png", "map_herb.png" };   // by SpotKind

        /// <summary>The markers drawn on the world map (a nested class: Unity objects only in game, not in unit tests).</summary>
        private static class Markers
        {
            public static readonly List<GameObject> Objects = new List<GameObject>();
            public static Transform Root;
            public static readonly Dictionary<int, Sprite> Sprites = new Dictionary<int, Sprite>();
        }

        private static bool Alive(UnityEngine.Object o)
        {
            try { return o != null && !o.WasCollected && o.GetInstanceID() != 0 && (o as GameObject)?.transform != null; }
            catch { return false; }
        }

        /// <summary>Markers of the mine veins and herb valleys on the world map (made again when the map scene is new).</summary>
        public static void UpdateSpotMarkers()
        {
            if (!Enabled || Instance.Spots.Count == 0) return;
            MapWorldMgr world = null;
            try { world = SceneType.map?.world; } catch { }
            if (world == null) return;
            Transform root = null;
            try { root = world.mapRoot; } catch { }
            if (root == null) return;
            var same = Markers.Root != null && Markers.Objects.Count == Instance.Spots.Count && Markers.Objects.All(Alive)
                && Markers.Root.GetInstanceID() == root.GetInstanceID();
            if (same) return;
            foreach (var o in Markers.Objects) { try { if (Alive(o)) UnityEngine.Object.Destroy(o); } catch { } }
            Markers.Objects.Clear();
            Markers.Root = root;
            try
            {
                // the layer of the player's model, one order below it: above the ground, under the player
                var layer = 0; var order = 0; var z = 0f;
                try
                {
                    var pr = world.playerRoot;
                    var rd = pr?.GetComponentInChildren<Renderer>();
                    if (rd != null) { layer = rd.sortingLayerID; order = rd.sortingOrder - 1; }
                    if (pr != null) z = pr.position.z;
                }
                catch { }
                var p0 = Instance.Spots[0].Point();
                var cell = (world.PointToWorld(new Vector2Int(p0.x + 1, p0.y)) - world.PointToWorld(p0)).magnitude;
                if (cell <= 0f) cell = 1f;
                foreach (var s in Instance.Spots)
                {
                    if (!Markers.Sprites.TryGetValue(s.Kind, out var sprite) || sprite == null)
                        Markers.Sprites[s.Kind] = sprite = SpriteHelper.GetImage(Instance.ModId, MARKER_IMAGES[s.Kind]);
                    var go = new GameObject($"TaoistSpot_{s.Kind}_{s.X}_{s.Y}");
                    go.transform.SetParent(root, true);
                    var w = world.PointToWorld(s.Point());
                    go.transform.position = new Vector3(w.x, w.y, z);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingLayerID = layer;
                    sr.sortingOrder = order;
                    if (sprite != null)
                    {
                        var k = MARKER_CELL_SIZE * cell / Math.Max(0.01f, sprite.bounds.size.x);
                        go.transform.localScale = new Vector3(k, k, 1f) / Math.Max(0.0001f, root.lossyScale.x);
                    }
                    Markers.Objects.Add(go);
                }
                DebugHelper.WriteLine($"ArtisanEvent: {Markers.Objects.Count} spot markers on the map (cell {cell:0.00}, layer {layer}, order {order})");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"ArtisanEvent: spot markers: {ex.Message}");
            }
        }

        /// <summary>The marker of a spot on the map (tests): null when none.</summary>
        public static GameObject SpotMarker(Spot s)
        {
            var i = Instance.Spots.IndexOf(s);
            return i >= 0 && i < Markers.Objects.Count && Alive(Markers.Objects[i]) ? Markers.Objects[i] : null;
        }

        /// <summary>Items expected of a stay (the drama's options): the tries x the mean chance.</summary>
        public static int SpotExpected(SpotKind kind, int area, int months) =>
            ExpectedItems(SpotPool(kind, area), GatherTries(months), CraftValue(Player, SpotCraft(kind)));
        /// <summary>Direction (0-7: N, NE, E, SE, S, SW, W, NW) from a point to another (y grows to the north).</summary>
        public static int Direction(Vector2Int from, Vector2Int to) => Direction(from.x, from.y, to.x, to.y);
        public static int Direction(int fx, int fy, int tx, int ty)
        {
            var a = Math.Atan2(ty - fy, tx - fx) * 180 / Math.PI;   // 0 = east, 90 = north
            var octant = (int)Math.Round(((90 - a) % 360 + 360) % 360 / 45.0) % 8;
            return octant;
        }

        /// <summary>Makes the spots of every area near its towns (deterministic for a save), the cells made rich.</summary>
        public static void MakeSpots()
        {
            if (!Enabled || Instance.Spots.Count > 0 || ModMain.Store?.Towns == null)
                return;
            var used = new HashSet<string>();
            foreach (var area in ModMain.Store.Towns.Where(x => x != null).GroupBy(x => x.gridData.areaBaseID).OrderBy(x => x.Key))
            {
                var towns = area.OrderBy(x => x.buildData.id, StringComparer.Ordinal).ToList();
                var rnd = new System.Random(StableSeed(towns[0].buildData.id) + area.Key);
                foreach (var kind in new[] { SpotKind.Mine, SpotKind.Herb })
                {
                    var made = 0;
                    for (var tries = 0; tries < 200 && made < SPOTS_AN_AREA; tries++)
                    {
                        var t = towns[rnd.Next(towns.Count)].GetOrigiPoint();
                        var r = rnd.Next(SPOT_MIN_RANGE, SPOT_MAX_RANGE + 1);
                        var p = new Vector2Int(t.x + rnd.Next(-r, r + 1), t.y + rnd.Next(-r, r + 1));
                        if (Math.Abs(p.x - t.x) < SPOT_MIN_RANGE && Math.Abs(p.y - t.y) < SPOT_MIN_RANGE) continue;
                        if (!WUnitHelper.IsValidPoint(p) || used.Contains($"{p.x},{p.y}")) continue;
                        var gd = g.data.grid.GetGridData(p);
                        if (gd == null || gd.IsBuild() || gd.isBlock || gd.areaBaseID != area.Key || g.world.build.GetBuild(p) != null) continue;
                        used.Add($"{p.x},{p.y}");
                        Instance.Spots.Add(new Spot { X = p.x, Y = p.y, Area = area.Key, Kind = (int)kind });
                        made++;
                    }
                }
            }
            Enrich();
        }

        private static int StableSeed(string s) { unchecked { var h = 23; foreach (var ch in s ?? "") h = h * 31 + ch; return h & 0x7fffffff; } }

        /// <summary>The spot cells are rich in their resource (the game's gathering finds more there). Yearly too.</summary>
        public static void Enrich()
        {
            foreach (var s in Instance.Spots)
            {
                DataGrid.GridData gd = null;
                try { gd = g.data.grid.GetGridData(s.Point()); } catch { }
                if (gd == null) continue;
                // the game's scale is small (a cell holds 0..9): a spot is full (its max, at least SPOT_RICHNESS)
                if (s.Kind == (int)SpotKind.Mine) { if (gd.mineralMax < SPOT_RICHNESS) gd.mineralMax = SPOT_RICHNESS; gd.SetMineralValue(gd.mineralMax); }
                else { if (gd.medicinalMax < SPOT_RICHNESS) gd.medicinalMax = SPOT_RICHNESS; gd.SetMedicinalValue(gd.medicinalMax); }
            }
        }

        public static Spot SpotAt(Vector2Int p) => !Enabled ? null : Instance.Spots.FirstOrDefault(s => Math.Abs(s.X - p.x) <= 1 && Math.Abs(s.Y - p.y) <= 1);
        public static Spot NearestSpot(Vector2Int p, SpotKind? kind = null) => !Enabled ? null :
            Instance.Spots.Where(s => kind == null || s.Kind == (int)kind).OrderBy(s => (s.X - p.x) * (s.X - p.x) + (s.Y - p.y) * (s.Y - p.y)).FirstOrDefault();
        public static string SpotName(Spot s) => GameTool.LS(s.Kind == (int)SpotKind.Mine ? "artisan_spot_mine" : "artisan_spot_herb");

        /// <summary>The player on a spot (not offered in the last SPOT_COOLDOWN months): the drama of the stay.</summary>
        private static void CheckSpot()
        {
            if (SeclusionEvent.IsSecluded || SeclusionEvent.Instance == null || (TimeSkipEvent.Instance?.IsProcessing ?? false) || _open.Count > 0 || Blocking(_uis) || DramaHelper.GetOpenDrama() != null)
                return;
            var s = SpotAt(Player.GetUnitPos());
            if (s == null || GameHelper.GetGameTotalMonth() - s.LastOfferMonth < SPOT_COOLDOWN)
                return;
            OfferSpot(s);
        }

        /// <summary>The drama of a spot: stay 1, 3, 5 or 10 years (a seclusion) or leave.</summary>
        public static void OfferSpot(Spot s)
        {
            s.LastOfferMonth = GameHelper.GetGameTotalMonth();
            var life = Player.GetProperty<int>(UnitPropertyEnum.Life) - Player.GetProperty<int>(UnitPropertyEnum.Age);
            var opts = SPOT_YEARS.Select(y => string.Format(GameTool.LS("artisan_spot_o"), y, SpotExpected((SpotKind)s.Kind, s.Area, y * 12))).ToList();
            opts.Add(GameTool.LS("artisan_spot_no"));
            var text = string.Format(GameTool.LS(s.Kind == (int)SpotKind.Mine ? "artisan_spot_mine_text" : "artisan_spot_herb_text"), s.Area, life / 12);
            DramaHelper.OpenDrama2(text, opts, x =>
            {
                var i = x.id % 10;
                if (i < 0 || i >= SPOT_YEARS.Length || SeclusionEvent.Instance == null || SeclusionEvent.IsSecluded) return;
                SeclusionEvent.Instance.Start(s.Kind == (int)SpotKind.Mine ? SeclusionEvent.Kind.MineVein : SeclusionEvent.Kind.HerbValley, SPOT_YEARS[i] * 12, s.Area);
            }, s.Kind == (int)SpotKind.Mine ? "dixiamushi" : "xingxingqiyu");
        }

        /// <summary>The items of the last stay rewarded (tests: they go to the temporary storage when the bag is full).</summary>
        public static Dictionary<int, int> LastSpotGot { get; private set; } = new Dictionary<int, int>();

        /// <summary>The end of a stay at a spot (SeclusionEvent.Reward): the items of the area's grade and the craft exp. Returns the text.</summary>
        public static string SpotReward(SpotKind kind, int area, int months)
        {
            var craft = SpotCraft(kind);
            var got = new Dictionary<int, int>();
            var pool = SpotPool(kind, area);
            var tries = GatherTries(months);
            if (pool.Count > 0)
            {
                // each try picks an item of the area and succeeds with its chance
                var value = CraftValue(Player, craft);
                for (var i = 0; i < tries; i++)
                {
                    var o = PickOption(pool);
                    if (CommonTool.Random(0f, 100f) >= GatherChance(value, o.Need, o.LevelAboveArea)) continue;
                    got[o.Item.id] = (got.TryGetValue(o.Item.id, out var c) ? c : 0) + 1;
                }
            }
            else
            {
                // no config of the area: items of the area's grade, every try
                var grade = Math.Max(1, Math.Min(10, area));
                for (var i = 0; i < tries; i++)
                {
                    var conf = EncounterEvent.PickItem(kind == SpotKind.Mine ? (Func<ConfItemPropsItem, bool>)(x => (x.IsMineItem()?.Length ?? 0) > 0) : x => x.IsHerbItem() != null, grade);
                    if (conf == null) break;
                    got[conf.id] = (got.TryGetValue(conf.id, out var c) ? c : 0) + 1;
                }
            }
            LastSpotGot = got;
            long exp = 0;
            foreach (var x in got)
            {
                Player.AddUnitProp(x.Key, x.Value);
                exp += Crafted(Player, craft, x.Key, x.Value, 100f);
            }
            RefreshBag();
            TaoistLogEvent.Log("log_artisan_spot", GameTool.LS(kind == SpotKind.Mine ? "artisan_spot_mine" : "artisan_spot_herb"), got.Values.Sum(), exp);
            return string.Format(GameTool.LS(kind == SpotKind.Mine ? "secl_res_MineVein" : "secl_res_HerbValley"), got.Values.Sum(),
                string.Join(", ", got.OrderByDescending(x => x.Value).Take(4).Select(x => $"{GameTool.LS(g.conf.itemProps.GetItem(x.Key).name)} x{x.Value}")), exp);
        }
    }
}
