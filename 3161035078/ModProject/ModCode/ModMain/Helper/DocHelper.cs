using ModLib.Helper;
using System;
using System.Linq;
using System.Reflection;

namespace MOD_nE7UL2.Helper
{
    /// <summary>
    /// Dao of Creation (optional mod), by reflection on its public API MOD_rLvJjS.Helper.DocApi (no reference: Taoist
    /// builds and runs without it). Every call is safe: false / null / nothing when the mod is not loaded.
    /// </summary>
    public static class DocHelper
    {
        public const string API_TYPE = "MOD_rLvJjS.Helper.DocApi";

        private static Type _api;

        private static Type Api
        {
            get
            {
                // looked for again until found: the mod may be loaded after Taoist
                if (_api == null)
                    _api = AppDomain.CurrentDomain.GetAssemblies().Select(a => { try { return a.GetType(API_TYPE, false); } catch { return null; } }).FirstOrDefault(t => t != null);
                return _api;
            }
        }

        private static object Call(string method, params object[] args)
        {
            if (!Ready) return null;
            try
            {
                return Api.GetMethod(method, BindingFlags.Public | BindingFlags.Static)?.Invoke(null, args);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"DocHelper.{method}: {(ex.InnerException ?? ex).Message}");
                return null;
            }
        }

        /// <summary>Dao of Creation is loaded in this game.</summary>
        public static bool Ready
        {
            get
            {
                try { return Api?.GetProperty("IsReady", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is bool b && b; }
                catch { return false; }
            }
        }

        /// <summary>An NPC creates a martial art (soleID; null: it could not).</summary>
        public static string NpcForge(string unitId) => Call("NpcForge", unitId) as string;

        /// <summary>A copy of a created martial art given to a unit, learnt at once or as a manual (soleID of the copy).</summary>
        public static string GiveCopy(string soleId, string toUnitId, bool learn) => Call("GiveCopy", soleId, toUnitId, learn) as string;

        /// <summary>The name of a created martial art (null: unknown).</summary>
        public static string BookName(string soleId) => string.IsNullOrEmpty(soleId) ? null : Call("BookName", soleId) as string;

        /// <summary>The created martial arts a unit has learnt.</summary>
        public static string[] ArtsOf(string unitId) => Call("ArtsOf", unitId) as string[] ?? new string[0];

        /// <summary>Dao Essence given to a unit.</summary>
        public static void AddEssence(string unitId, float amount) => Call("AddEssence", unitId, amount);
    }
}
