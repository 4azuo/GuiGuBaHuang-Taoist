using MOD_nE7UL2.Mod;
using ModLib.Helper;
using System;

/// <summary>
/// NPC growth turns (S&amp;M GrowUpSpeed = P months): the NPCs are split in P groups by their unit id, and each month
/// only one group grows. An NPC grows once every P months, and the monthly load of the NPC handlers is spread over
/// the P months instead of falling on one month. The player is not split (ModMaster.Ticks, the same month as before).
/// - Turns: growth steps in the running dispatch (0 or 1 in normal play; more over a time skip) -> amounts of growth.
/// - Months: months the turns stand for (Turns x P) -> timers (injury, stays) and economy (income, chances), so these
///   keep their pace while only the growth slows down.
/// </summary>
public static class GrowTurnHelper
{
    /// <summary>GrowUpSpeed of the save (months per growth turn, at least 1).</summary>
    public static int Period => Math.Max(1, SMLocalConfigsEvent.Instance?.Configs?.GrowUpSpeed ?? 1);

    /// <summary>Group (0 .. period - 1) of a unit id: stable between game sessions (FNV-1a, not string.GetHashCode).</summary>
    public static int Group(string unitId, int period)
    {
        if (period <= 1 || string.IsNullOrEmpty(unitId))
            return 0;
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in unitId)
            {
                h ^= c;
                h *= 16777619u;
            }
            return (int)(h % (uint)period);
        }
    }

    /// <summary>Turns of a group in the months (now - monthsPassed, now]: months m with (m + group) multiple of period.</summary>
    public static int Turns(int now, int monthsPassed, int period, int group) =>
        TimeScaleHelper.Ticks(now + group, Math.Max(1, monthsPassed), Math.Max(1, period));

    /// <summary>Autotests: the turns of every NPC while set (a scenario running a handler for one NPC), null in play.</summary>
    public static int? ForcedTurns { get; set; }

    /// <summary>Runs an action with every NPC on its growth turn (ForcedTurns = turns), then restores it.</summary>
    public static void WithTurns(int turns, Action action)
    {
        var prev = ForcedTurns;
        ForcedTurns = turns;
        try { action(); }
        finally { ForcedTurns = prev; }
    }

    /// <summary>Growth turns of a unit in the running month dispatch (player: every period months, all NPCs share the load).</summary>
    public static int Turns(WorldUnitBase wunit)
    {
        var period = Period;
        if (wunit == null)
            return 0;
        if (wunit.IsPlayer())
            return ModLib.Mod.ModMaster.Ticks(period);
        if (ForcedTurns.HasValue)
            return ForcedTurns.Value;
        return Turns(GameHelper.GetGameTotalMonth(), ModLib.Mod.ModMaster.MonthsPassed, period, Group(wunit.GetUnitId(), period));
    }

    /// <summary>Months the turns of an NPC stand for (turns x period): for timers and chances that keep their monthly pace.</summary>
    public static int Months(int turns) => turns * Period;
}
