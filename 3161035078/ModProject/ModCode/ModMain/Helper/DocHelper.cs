using ModLib.Helper;

namespace MOD_nE7UL2.Helper
{
    /// <summary>
    /// Dao of Creation (optional mod), through its public API MOD_rLvJjS.Helper.DocApi (ModLib ModApiHelper: by reflection,
    /// no reference: Taoist builds and runs without it). Every call is safe: false / null / nothing when the mod is not
    /// loaded or its API is older than API_VERSION.
    /// </summary>
    public static class DocHelper
    {
        public const string API_TYPE = "MOD_rLvJjS.Helper.DocApi";
        /// <summary>The DocApi version Taoist is written for (1: IsReady, NpcForge, GiveCopy, BookName, ArtsOf, AddEssence).</summary>
        public const int API_VERSION = 1;

        /// <summary>Dao of Creation is loaded in this game (its save data ready).</summary>
        public static bool Ready => ModApiHelper.Has(API_TYPE, API_VERSION) && ModApiHelper.Get(API_TYPE, "IsReady", false);

        /// <summary>An NPC creates a martial art (soleID; null: it could not).</summary>
        public static string NpcForge(string unitId) => Ready ? ModApiHelper.Call<string>(API_TYPE, "NpcForge", null, unitId) : null;

        /// <summary>A copy of a created martial art given to a unit, learnt at once or as a manual (soleID of the copy).</summary>
        public static string GiveCopy(string soleId, string toUnitId, bool learn) =>
            Ready ? ModApiHelper.Call<string>(API_TYPE, "GiveCopy", null, soleId, toUnitId, learn) : null;

        /// <summary>The name of a created martial art (null: unknown).</summary>
        public static string BookName(string soleId) =>
            string.IsNullOrEmpty(soleId) || !Ready ? null : ModApiHelper.Call<string>(API_TYPE, "BookName", null, soleId);

        /// <summary>The created martial arts a unit has learnt.</summary>
        public static string[] ArtsOf(string unitId) => (Ready ? ModApiHelper.Call<string[]>(API_TYPE, "ArtsOf", null, unitId) : null) ?? new string[0];

        /// <summary>Dao Essence given to a unit.</summary>
        public static void AddEssence(string unitId, float amount)
        {
            if (Ready) ModApiHelper.Invoke(API_TYPE, "AddEssence", unitId, amount);
        }
    }
}
