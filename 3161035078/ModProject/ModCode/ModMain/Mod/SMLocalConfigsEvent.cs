using MOD_nE7UL2.Const;
using ModLib.Attributes;
using ModLib.Helper;
using ModLib.Mod;
using UnityEngine;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Per-save copy of the S&amp;M configs (taken from the global configs when the save is created). Read by most features.
    /// </summary>
    [Cache(ModConst.SM_LOCAL_CONFIGS_EVENT)]
    public class SMLocalConfigsEvent : ModEvent
    {
        public static SMLocalConfigsEvent Instance { get; set; }
        public SMGlobalConfigsEvent Configs { get; set; }

        /// <summary>
        /// New save: copies the global S&amp;M configs.
        /// </summary>
        public override void OnLoadClass(bool isNew, string modId, CacheAttribute attr)
        {
            base.OnLoadClass(isNew, modId, attr);
            // a new save, or a save without its copy (older save, broken file): the global configs
            if (isNew || Configs == null)
            {
                try { Configs = CacheHelper.ReadGlobalCacheFile<SMGlobalConfigsEvent>(modId, ModConst.SM_GLOBAL_CONFIGS_EVENT); }
                catch (System.Exception ex) { DebugHelper.WriteLine($"SMLocalConfigsEvent: global configs not read: {ex.Message}"); }
                Configs = Configs ?? new SMGlobalConfigsEvent();
            }
        }

        /// <summary>
        /// Works out again the values of the game scaled by the S&amp;M configs of this save (ConfBalance: level-up exp, skill
        /// mastery exp, skill MP cost / cooldown / requirements, power-up pills) from the values of the game: loading another
        /// save in the same session does not multiply them twice.
        /// </summary>
        public override void OnLoadGame()
        {
            base.OnLoadGame();
            ConfBalance.Apply(Configs);
        }

        /// <summary>
        /// bas + bas x addRate (helper used everywhere to apply S&amp;M rates).
        /// </summary>
        public double Calculate(double bas, double addRate)
        {
            return bas + (bas * addRate);
        }
    }
}
