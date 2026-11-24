using ModLib.AutoTest;
using ModLib.Helper;
using ModLib.Mod;
using MOD_nE7UL2.Mod;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MOD_nE7UL2.AutoTest.Scenarios.Title
{
    /// <summary>
    /// TT-09 (WallpaperHelper of ModLib): the wallpapers declared by the files "*_wallpaper.json" of the ModConf folders
    /// of the mods (Taoist: taoist_1_wallpaper.json, taoist_2_wallpaper.json) are listed in the Change Theme window and
    /// shown on the title screen when chosen (saved); a wallpaper of the game removes it. The settings of each file: the
    /// game starts on each Taoist wallpaper a quarter of the time (startupRate 0.25: half of the time on one of them,
    /// session only), the game's title hidden (hideGameTitle, the toggle of the window overrides it). The player's choices
    /// are restored.
    /// </summary>
    public class TT09_Wallpapers : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TT-09";

        private const string TaoistId = "nE7UL2";
        private static readonly string[] Files = { "taoist_1_wallpaper.png", "taoist_2_wallpaper.png" };
        private static string Key(int i) => WallpaperHelper.MakeKey(TaoistId, Files[i]);
        private static List<WallpaperHelper.Wallpaper> TaoistWallpapers => WallpaperHelper.GetWallpapers().Where(x => x.ModId == TaoistId).ToList();
        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        private static UIWallPaper Window => g.ui.GetUI<UIWallPaper>(UIType.WallPaper);

        private static Toggle ToggleOf(GameObject item) => item?.GetComponentInChildren<Toggle>(true);

        //chooses an item of the Change Theme window as the player does (its toggle, else its button)
        private static void Pick(GameObject item)
        {
            var t = ToggleOf(item);
            if (t != null) t.isOn = true;
            else item?.GetComponentInChildren<Button>(true)?.onClick.Invoke();
        }

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            return new Scenario { Id = "TT-09", Feature = "WallpaperHelper (ModLib) / UITitleScreenEvent", Title = "Mod wallpapers: Change Theme window, title screen, Taoist startup wallpaper", Scope = ScenarioScope.Title, Overlay = false }
                .Step("wallpaper files", c =>
                {
                    c.Bag["wallpaper"] = ModTitleEvent.Instance.Wallpaper;
                    c.Bag["startup"] = WallpaperHelper.StartupWallpaper;
                    c.Bag["hideTitle"] = ModTitleEvent.Instance.HideGameTitle;
                    var all = WallpaperHelper.GetWallpapers();
                    c.Info($"mod wallpapers: {string.Join(", ", all.Select(x => $"{x.Key} ({x.ConfFile}: order {x.Conf.Order}, hideGameTitle {x.HideGameTitle}, startupRate {x.StartupRate}, startupWhenChosen {x.Conf.StartupWhenChosen})"))}");
                    var taoist = TaoistWallpapers;
                    if (!c.Assert(taoist.Count == 2 && Same(taoist[0].Key, Key(0)) && Same(taoist[1].Key, Key(1)), "the 2 Taoist wallpapers (ModConf/taoist_1_wallpaper.json, taoist_2_wallpaper.json), in their order")) return;
                    c.Assert(taoist.All(x => x.HideGameTitle && x.StartupRate == 0.25 && !x.Conf.StartupWhenChosen), "settings of each file read (hideGameTitle true, startupRate 0.25)");
                    c.Assert(all.All(x => WallpaperHelper.GetSprite(x) != null), "every wallpaper loaded");
                    //files read from JSON: settings, missing / wrong images and broken JSON skipped
                    var a = WallpaperHelper.ReadWallpaper("m", "a_wallpaper.json", "{ \"Image\": \"a.png\", \"name\": \"A\", \"order\": 3, \"hideGameTitle\": false, \"startupRate\": 0.1, \"startupWhenChosen\": true }", f => true);
                    c.Assert(a != null && a.Key == "m/a.png" && a.Name == "A" && a.Conf.Order == 3 && !a.HideGameTitle && a.StartupRate == 0.1 && a.Conf.StartupWhenChosen, "JSON file: every setting read");
                    var d = WallpaperHelper.ReadWallpaper("m", "d_wallpaper.json", "{ \"image\": \"d.jpg\" }", f => true);
                    c.Assert(d != null && d.HideGameTitle && d.StartupRate == 0 && d.Conf.Order == 0, "JSON file: defaults (hideGameTitle true, no startup)");
                    c.Assert(WallpaperHelper.ReadWallpaper("m", "b_wallpaper.json", "{ \"image\": \"missing.png\" }", f => f != "missing.png") == null, "missing image: skipped");
                    c.Assert(WallpaperHelper.ReadWallpaper("m", "c_wallpaper.json", "{ \"image\": \"c.txt\" }", f => true) == null, "not an image: skipped");
                    c.Assert(WallpaperHelper.ReadWallpaper("m", "e_wallpaper.json", "{ not json", f => true) == null, "broken JSON: skipped (logged)");
                    var cur = WallpaperHelper.GetCurrentKey();
                    c.Info($"at the start: startup wallpaper {WallpaperHelper.StartupWallpaper ?? "none"}, chosen {ModTitleEvent.Instance.Wallpaper ?? "game"}");
                    c.Assert(cur == null || WallpaperHelper.IsShownOnTitle(), $"title screen shows the current wallpaper ({cur ?? "game"})");
                    c.Assert(WallpaperHelper.StartupDrawn, "startup draw made on the first title screen");
                })
                .Step("startup draw", c =>
                {
                    var taoist = TaoistWallpapers;
                    //the player uses a Taoist wallpaper already: no draw
                    var skipped = 0;
                    for (var i = 0; i < 50; i++)
                    {
                        if (WallpaperHelper.DrawStartupWallpaper(taoist, Key(0), new System.Random(i * 7919 + 1)) == null) skipped++;
                    }
                    c.Assert(skipped == 50, $"Taoist wallpaper chosen by the player: no startup draw ({skipped}/50)");
                    //the draw of the first title screen, 200 times (the player on a wallpaper of the game): about half on a
                    //Taoist wallpaper (startupRate 0.5), both of them
                    var counts = new Dictionary<string, int>();
                    for (var i = 0; i < 200; i++)
                    {
                        var k = WallpaperHelper.DrawStartupWallpaper(taoist, null, new System.Random(i * 7919 + 1)) ?? "none";
                        counts[k] = counts.TryGetValue(k, out var n) ? n + 1 : 1;
                    }
                    c.Info($"200 draws: {string.Join(", ", counts.Select(x => $"{x.Key} {x.Value}"))}");
                    var none = counts.TryGetValue("none", out var a) ? a : 0;
                    c.Assert(none >= 70 && none <= 130, $"no Taoist wallpaper about half of the time ({none}/200)");
                    c.Assert(counts.ContainsKey(Key(0)) && counts.ContainsKey(Key(1)) && counts.Keys.All(x => x == "none" || x == Key(0) || x == Key(1)), "both Taoist wallpapers drawn, nothing else");
                    //rates over 1 in total: scaled down, always a wallpaper; a wallpaper with startupWhenChosen stays in
                    var big = new List<WallpaperHelper.Wallpaper>
                    {
                        WallpaperHelper.ReadWallpaper("m", "x_wallpaper.json", "{ \"image\": \"x.png\", \"startupRate\": 0.8 }", f => true),
                        WallpaperHelper.ReadWallpaper("m", "y_wallpaper.json", "{ \"image\": \"y.png\", \"startupRate\": 0.8, \"startupWhenChosen\": true }", f => true),
                    };
                    var always = Enumerable.Range(0, 50).All(i => WallpaperHelper.DrawStartupWallpaper(big, null, new System.Random(i)) != null);
                    c.Assert(always, "rates over 1 in total: a wallpaper every time");
                    var onlyY = Enumerable.Range(0, 50).Select(i => WallpaperHelper.DrawStartupWallpaper(big, "m/x.png", new System.Random(i))).ToList();
                    c.Assert(onlyY.All(x => x == null || x == "m/y.png") && onlyY.Contains("m/y.png"), "player on a wallpaper of the mod: only the startupWhenChosen one drawn");
                    //drawn once a session
                    var before = WallpaperHelper.StartupWallpaper;
                    WallpaperHelper.ShowStartupWallpaper(new System.Random(0));
                    c.Assert(WallpaperHelper.StartupWallpaper == before, "drawn once (the next title screens keep it)");

                    WallpaperHelper.StartupWallpaper = Key(1);
                    c.Assert(WallpaperHelper.ShowOnTitle(Key(1)) && WallpaperHelper.IsShownOnTitle(), "Taoist wallpaper 2 on the title screen");
                    c.Assert(Same(ModTitleEvent.Instance.Wallpaper, (string)c.Bag["wallpaper"]), "startup wallpaper not saved");
                    //the player did not choose: the setting of the wallpaper file (hideGameTitle true)
                    ModTitleEvent.Instance.HideGameTitle = null;
                    WallpaperHelper.UpdateGameTitle();
                    c.Assert(WallpaperHelper.IsGameTitleHidden(), "game's title hidden (hideGameTitle of the wallpaper file)");
                }, 500)
                .Step("open Change Theme", c =>
                {
                    var login = g.ui.GetUI<UILogin>(UIType.Login);
                    var btn = new[] { login?.btnPaperChange, login?.btnPaperChange_En }.FirstOrDefault(x => x != null && x.gameObject.activeInHierarchy);
                    if (!c.Assert(btn != null, "Change Theme button")) return;
                    btn.onClick.Invoke();
                }, 1500)
                .Step("mod wallpapers listed", c =>
                {
                    if (!c.Assert(Window != null, "Change Theme window open")) return;
                    //the mod wallpapers are after the game's: the list scrolled to the end for the picture
                    var scroll = Window.goItemRoot.GetComponentInParent<ScrollRect>();
                    if (scroll != null) scroll.verticalNormalizedPosition = 0f;
                    var items = WallpaperHelper.GetThemeWindowItems();
                    var all = WallpaperHelper.GetWallpapers();
                    c.Assert(items.Count == all.Count, $"{items.Count} mod wallpaper(s) in the window ({all.Count} found)");
                    var item2 = items.FirstOrDefault(x => x.name.EndsWith(Key(1), StringComparison.OrdinalIgnoreCase));
                    c.Assert(item2 != null && (ToggleOf(item2)?.isOn ?? true), "the wallpaper shown (Taoist 2) is selected");
                    c.Assert(item2 != null && WallpaperHelper.GetCurrentMarkItem() == item2, $"'Current' mark on Taoist 2 ({WallpaperHelper.GetCurrentMarkItem()?.name ?? "none"})");
                    //the items are pictures (Tg > Paper): the picture of the wallpaper
                    foreach (var w in all)
                    {
                        var item = items.FirstOrDefault(x => x.name.EndsWith(w.Key, StringComparison.OrdinalIgnoreCase));
                        var paper = item?.GetComponentsInChildren<Image>(true).FirstOrDefault(x => x.name == "Paper");
                        c.Assert(paper != null && paper.sprite == WallpaperHelper.GetSprite(w), $"picture of {w.Key} in its item");
                    }
                }, 1500)
                .Step("choose Taoist 1", c =>
                {
                    var item1 = WallpaperHelper.GetThemeWindowItems().FirstOrDefault(x => x.name.EndsWith(Key(0), StringComparison.OrdinalIgnoreCase));
                    if (!c.Assert(item1 != null, "item of Taoist wallpaper 1")) return;
                    Pick(item1);
                }, 500)
                .Step("chosen and saved", c =>
                {
                    c.Assert(Same(ModTitleEvent.Instance.Wallpaper, Key(0)), $"choice saved: {ModTitleEvent.Instance.Wallpaper}");
                    c.Assert(WallpaperHelper.StartupWallpaper == null, "startup wallpaper dropped");
                    c.Assert(WallpaperHelper.IsShownOnTitle(), "shown on the title screen");
                    var mark = WallpaperHelper.GetCurrentMarkItem();
                    c.Assert(mark != null && mark.name.EndsWith(Key(0), StringComparison.OrdinalIgnoreCase), $"'Current' mark on Taoist 1 ({mark?.name ?? "none"})");
                    //back to the wallpaper of the game shown before (the game's own setting stays the same)
                    var ui = Window;
                    var index = ui?.paperNames == null ? -1 : Array.IndexOf(ui.paperNames.ToArray(), ui.curPaper);
                    var root = ui?.goItemRoot?.transform;
                    var gameItems = new List<GameObject>();
                    for (var i = 0; root != null && i < root.childCount; i++)
                    {
                        var go = root.GetChild(i).gameObject;
                        if (go.activeSelf && !go.name.StartsWith(WallpaperHelper.ITEM_PREFIX)) gameItems.Add(go);
                    }
                    c.Info($"game wallpaper {ui?.curPaper} (item {index} of {gameItems.Count})");
                    if (!c.Assert(gameItems.Count > 0, "wallpapers of the game in the window")) return;
                    var picked = gameItems[Math.Max(0, Math.Min(index, gameItems.Count - 1))];
                    c.Bag["gameItem"] = picked.GetInstanceID();
                    Pick(picked);
                }, 1000)
                .Step("game wallpaper", c =>
                {
                    c.Assert(ModTitleEvent.Instance.Wallpaper == null, "choice saved: the game's wallpaper");
                    c.Assert(!WallpaperHelper.IsShownOnTitle(), "mod wallpaper removed from the title screen");
                    c.Assert(WallpaperHelper.GetCurrentMarkItem()?.GetInstanceID() == (int)c.Bag["gameItem"], "'Current' mark on the game's wallpaper");
                    c.Assert(!WallpaperHelper.IsGameTitleHidden(), "game's title shown on the game's wallpaper");
                    //the option "hide the game's title on mod wallpapers" in the window: on
                    var tg = ModTitleEvent.HideTitleToggle;
                    if (c.Assert(tg != null && tg.Item != null, "'hide the game's title' toggle in the window"))
                    {
                        c.Assert(tg.Item.isOn, "toggle on (setting of the wallpaper file)");
                        tg.Item.isOn = false;
                        tg.Item.isOn = true;
                        c.Assert(ModTitleEvent.Instance.HideGameTitle == true, "toggle on: player's choice saved");
                    }
                    //Taoist 1 again for the picture of the title screen
                    Pick(WallpaperHelper.GetThemeWindowItems().FirstOrDefault(x => x.name.EndsWith(Key(0), StringComparison.OrdinalIgnoreCase)));
                    Window?.btnClose?.onClick.Invoke();
                }, 1000)
                .Step("title screen", c =>
                {
                    c.Assert(!g.ui.HasUI(UIType.WallPaper), "Change Theme window closed");
                    c.Assert(WallpaperHelper.IsShownOnTitle(), "Taoist wallpaper 1 on the title screen");
                    var title = WallpaperHelper.GetGameTitleObjects(g.ui.GetUI<UILogin>(UIType.Login));
                    c.Info($"game title: {string.Join(", ", title.Select(x => x.name))}");
                    c.Assert(title.Count > 0 && WallpaperHelper.IsGameTitleHidden() && title.All(x => !x.activeSelf), "game's title hidden on the mod wallpaper");
                }, 1500)
                .Step("game title shown", c =>
                {
                    //the option off: the game's title over the mod wallpaper
                    ModTitleEvent.Instance.HideGameTitle = false;
                    WallpaperHelper.UpdateGameTitle();
                    c.Assert(!WallpaperHelper.IsGameTitleHidden() && WallpaperHelper.GetGameTitleObjects(g.ui.GetUI<UILogin>(UIType.Login)).All(x => x.activeSelf), "option off: game's title shown");
                }, 1000)
                .Step("restore", c =>
                {
                    ModTitleEvent.Instance.Wallpaper = (string)c.Bag["wallpaper"];
                    ModTitleEvent.Instance.HideGameTitle = (bool?)c.Bag["hideTitle"];
                    CacheHelper.SaveGlobalCache(ModTitleEvent.Instance);
                    WallpaperHelper.StartupWallpaper = (string)c.Bag["startup"];
                    WallpaperHelper.ShowOnTitle(WallpaperHelper.GetCurrentKey());
                    c.Assert(ModTitleEvent.Instance.Wallpaper == (string)c.Bag["wallpaper"], $"player's choice restored ({ModTitleEvent.Instance.Wallpaper ?? "game"})");
                }, 1000);
        }
    }
}
