using MOD_nE7UL2.Const;
using ModLib.Attributes;
using ModLib.AutoTest;
using ModLib.Helper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// In-game test runner of Taoist: the runner of ModLib (ModLib.AutoTest.AutoTestRunner, driven by
    /// GuiGuBaHuang-ModLib/scripts/game/run-autotest.ps1) with what is specific to Taoist. Loaded only when
    /// &lt;mod cache&gt;\autotest\request.json exists (never for normal players). The scenarios: AutoTest/Scenarios
    /// (IScenarioDefinition classes of this assembly).
    /// </summary>
    [Cache(ModConst.AUTOTEST_EVENT, CacheType = CacheAttribute.CType.Global, WorkOn = CacheAttribute.WType.All)]
    public class AutoTestEvent : AutoTestRunner
    {
        /// <summary>Taoist version in results.json.</summary>
        protected override string ModVersion => ModConst.TAOIST_VERSION;

        /// <summary>The storylines do not open their dramas by themselves during a run (the scenarios open them).</summary>
        protected override void OnRequestLoaded(AutoTestRequest request) => Story.StoryEvent.NoAutoOpen = true;

        /// <summary>The tests never fight a hired NPC (HirePeopleEvent).</summary>
        public override bool CanFightInTests(WorldUnitBase npc) => !HirePeopleEvent.IsHired(npc);

        /// <summary>TP-01 creates the test character when the run asks for it.</summary>
        protected override void OnNewCharacterRequested(bool on) => AutoTest.Scenarios.Title.TP01_NewCharacter.Requested = on;

        /// <summary>
        /// The player's record of every cultivation path (a scenario's merit, karma, body levels... must not leak into the
        /// next one: a massacre's karma made the NPCs of the next battle test hostile) and the S&amp;M settings: read as
        /// JSON when the save is loaded, written back before every game scenario.
        /// </summary>
        private static readonly (string name, Func<string, string> get, Action<string, string> set)[] PATH_DATA =
        {
            ("merit", id => Json(MeritEvent.Instance?.Merits, id), (id, j) => Put(MeritEvent.Instance?.Merits, id, j)),
            ("body", id => Json(BodyCulEvent.Instance?.Bodies, id), (id, j) => Put(BodyCulEvent.Instance?.Bodies, id, j)),
            ("artisan", id => Json(ArtisanEvent.Instance?.Artisans, id), (id, j) => Put(ArtisanEvent.Instance?.Artisans, id, j)),
            ("Dao heart", id => Json(DaoHeartEvent.Instance?.Hearts, id), (id, j) => Put(DaoHeartEvent.Instance?.Hearts, id, j)),
            ("spirit sense", id => Json(SpiritSenseEvent.Instance?.Senses, id), (id, j) => Put(SpiritSenseEvent.Instance?.Senses, id, j)),
        };

        private const string SM_SETTINGS = "S&M settings";

        private static string Json<T>(Dictionary<string, T> d, string id) =>
            d != null && d.TryGetValue(id, out var v) && v != null ? JsonConvert.SerializeObject(v) : null;

        private static void Put<T>(Dictionary<string, T> d, string id, string json)
        {
            if (d == null) return;
            if (json == null) d.Remove(id);
            else d[id] = JsonConvert.DeserializeObject<T>(json);
        }

        private static string SMSettings() =>
            SMLocalConfigsEvent.Instance?.Configs == null ? null : JsonConvert.SerializeObject(SMLocalConfigsEvent.Instance.Configs);

        protected override Dictionary<string, string> CaptureModState(WorldUnitBase player)
        {
            var state = PATH_DATA.ToDictionary(x => x.name, x => x.get(player.GetUnitId()));
            state[SM_SETTINGS] = SMSettings();
            return state;
        }

        protected override List<string> RestoreModState(WorldUnitBase player, Dictionary<string, string> state)
        {
            var changed = new List<string>();
            foreach (var pd in PATH_DATA)
            {
                var was = state.TryGetValue(pd.name, out var j) ? j : null;
                if (pd.get(player.GetUnitId()) == was) continue;
                pd.set(player.GetUnitId(), was);
                changed.Add(pd.name);
            }
            if (changed.Contains("merit") && MeritEvent.Enabled) MeritEvent.Sync(player);
            if (changed.Contains("artisan") && ArtisanEvent.Enabled)
                foreach (ArtisanEvent.Craft cr in System.Enum.GetValues(typeof(ArtisanEvent.Craft))) ArtisanEvent.Sync(player, cr);
            var configs = state.TryGetValue(SM_SETTINGS, out var c) ? c : null;
            if (configs != null && SMLocalConfigsEvent.Instance?.Configs != null && SMSettings() != configs)
            {
                JsonConvert.PopulateObject(configs, SMLocalConfigsEvent.Instance.Configs);
                changed.Add(SM_SETTINGS);
            }
            return changed;
        }
    }
}
