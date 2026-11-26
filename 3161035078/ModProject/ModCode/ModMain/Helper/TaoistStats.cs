using ModLib.Helper;
using System;
using System.Runtime.CompilerServices;

namespace MOD_nE7UL2.Helper
{
    /// <summary>
    /// Balance numbers of Taoist (ModLib BalanceStatsHelper: balance-stats.json in the global cache folder of the mod, only
    /// on the player's computer, never sent; S&amp;M option KeepBalanceStats). What a player sends with a "too strong / too
    /// weak" report (series: n, sum, min, max, mean, p50, p90):
    /// - hit/&lt;player|on-player&gt;/&lt;cultivator|monster|boss&gt;/r&lt;realm&gt;: % of the max HP a hit took (the realm of the target of
    ///   the player's hits, of the attacker of the hits on the player);
    /// - guard/r&lt;realm&gt;: % of a hit taken by the MP / SP / DP blocks of a cultivator (realm of the blocker, player's battles);
    ///   guard-capped/r&lt;realm&gt;: hits whose blocks reached the cap (GUARD_MAX); min-dmg/r&lt;realm&gt;: hits at the minimum damage;
    /// - potion/r&lt;realm&gt;: % of the max HP a potion of the player restored (game recovery + Taoist bonus);
    /// - battle/length/r&lt;realm&gt;: seconds of a battle of the player; battle/win, battle/lose: counts; battle/time-mult: the
    ///   damage multiplier of the time at the end;
    /// - constitution/awaken/&lt;kind&gt;, constitution/tier&lt;n&gt;/&lt;kind&gt;: game months when the player got it;
    /// - rebirth/realm: the realm of a rebirth.
    /// Every call is safe with an older ModLib (no BalanceStatsHelper: nothing kept).
    /// </summary>
    public static class TaoistStats
    {
        private static bool _missing;
        private static string ModId => ModMain.ModObj?.ModId ?? "nE7UL2";

        /// <summary>At the load of the mod and when the S&amp;M option changes.</summary>
        public static void Init(string modId, string version, bool enabled) => Safe(() => InitNow(modId, version, enabled));

        public static void Add(string group, string key, double value) => Safe(() => AddNow(group, key, value));

        public static void Count(string group, string key) => Add(group, key, 1d);

        private static void Safe(Action a)
        {
            if (_missing) return;
            try { a(); }
            catch (Exception ex) when (ex is MissingMethodException || ex is TypeLoadException || ex is MissingFieldException)
            {
                _missing = true;
                DebugHelper.WriteLine($"Taoist-Stats: off (ModLib without BalanceStatsHelper: {ex.Message})");
            }
            catch (Exception ex) { DebugHelper.WriteLine($"Taoist-Stats: {ex.Message}"); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitNow(string modId, string version, bool enabled)
        {
            BalanceStatsHelper.SetModVersion(modId, version);
            BalanceStatsHelper.SetEnabled(modId, enabled);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AddNow(string group, string key, double value) => BalanceStatsHelper.Add(ModId, group, key, value);

        // ------------------------------------------------------------------ keys (pure: unit tests)

        public static string Realm(int grade) => "r" + Math.Max(0, grade);

        /// <summary>Key of a hit: who hit (the player or not), the kind of the other side, its realm.</summary>
        public static string HitKey(bool byPlayer, string target, int realm) => $"{(byPlayer ? "player" : "on-player")}/{target}/{Realm(realm)}";

        /// <summary>Share (%) of a part in a whole, 0 when the whole is not positive.</summary>
        public static double Percent(double part, double whole) => whole <= 0 ? 0d : part * 100d / whole;

        // ------------------------------------------------------------------ records

        /// <summary>A hit where the player is the attacker or the target.</summary>
        public static void Hit(bool byPlayer, string otherKind, int otherRealm, int dmg, int hpMax)
        {
            if (hpMax <= 0 || dmg < 0) return;
            Add("hit", HitKey(byPlayer, otherKind, otherRealm), Percent(dmg, hpMax));
        }

        /// <summary>The MP / SP / DP blocks of a cultivator on a hit of a player's battle.</summary>
        public static void Guard(int realm, int preGuard, int guard, int cap, bool atMin)
        {
            if (preGuard <= 0) return;
            Add("guard", Realm(realm), Percent(guard, preGuard));
            if (guard > 0 && guard >= cap - 1) Count("guard-capped", Realm(realm));
            if (atMin) Count("min-dmg", Realm(realm));
        }

        public static void Potion(int realm, int heal, int hpMax)
        {
            if (hpMax <= 0 || heal <= 0) return;
            Add("potion", Realm(realm), Percent(heal, hpMax));
        }

        public static void BattleEnd(int realm, float seconds, bool win, double timeMult)
        {
            if (seconds <= 0f) return;
            Add("battle", "length/" + Realm(realm), seconds);
            Count("battle", win ? "win" : "lose");
            Add("battle", "time-mult", timeMult);
        }

        public static void Constitution(string kind, int tier, int months) =>
            Add("constitution", (tier <= 1 ? "awaken" : "tier" + tier) + "/" + kind, months);

        public static void Rebirth(int realm) => Add("rebirth", "realm", realm);
    }
}
