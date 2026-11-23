using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace UnitTests.Taoist
{
    /// <summary>GrowTurnHelper: NPC growth turns (GrowUpSpeed = P months, NPCs split in P groups, one group a month).</summary>
    [TestClass]
    public class GrowTurnTests
    {
        [TestMethod, Description("GrowTurnHelper.Group(unitId, P): stable (same id, same group), in 0 .. P-1, 0 when P <= 1 or no id")]
        public void Group_StableAndInRange()
        {
            Check.Eq(GrowTurnHelper.Group("aB3xYz", 3), GrowTurnHelper.Group("aB3xYz", 3), "same id, same group");
            Check.Eq(0, GrowTurnHelper.Group("aB3xYz", 1), "P 1: one group");
            Check.Eq(0, GrowTurnHelper.Group(null, 5), "no id");
            for (var i = 0; i < 200; i++)
            {
                var gr = GrowTurnHelper.Group($"unit{i}", 7);
                Check.True(gr >= 0 && gr < 7, $"unit{i}: group {gr} in 0..6");
            }
        }

        [TestMethod, Description("GrowTurnHelper.Group: 3,000 ids spread over P groups (each group within 25 % of an even share)")]
        public void Group_SpreadsUnits()
        {
            foreach (var p in new[] { 2, 3, 6, 12 })
            {
                var counts = Enumerable.Range(0, 3000).GroupBy(i => GrowTurnHelper.Group($"npc{i:x6}", p)).ToDictionary(x => x.Key, x => x.Count());
                Check.Eq(p, counts.Count, $"P {p}: every group used");
                var even = 3000 / p;
                foreach (var c in counts)
                    Check.True(c.Value > even * 0.75 && c.Value < even * 1.25, $"P {p}, group {c.Key}: {c.Value} units (even share {even})");
            }
        }

        [TestMethod, Description("GrowTurnHelper.Turns(now, 1, P, group): over P months each group has exactly one turn, one group a month")]
        public void Turns_OneGroupAMonth()
        {
            const int p = 3;
            for (var now = 30; now < 30 + p; now++)
                Check.Eq(1, Enumerable.Range(0, p).Count(gr => GrowTurnHelper.Turns(now, 1, p, gr) == 1), $"month {now}: one group on its turn");
            for (var gr = 0; gr < p; gr++)
                Check.Eq(1, Enumerable.Range(30, p).Sum(now => GrowTurnHelper.Turns(now, 1, p, gr)), $"group {gr}: one turn in {p} months");
        }

        [TestMethod, Description("GrowTurnHelper.Turns over a time skip of N months: N / P turns per group (P 1: every month)")]
        public void Turns_TimeSkip()
        {
            for (var gr = 0; gr < 3; gr++)
                Check.Eq(4, GrowTurnHelper.Turns(120, 12, 3, gr), $"12 months, P 3, group {gr}");
            Check.Eq(12, GrowTurnHelper.Turns(120, 12, 1, 0), "12 months, P 1");
            Check.Eq(1, GrowTurnHelper.Turns(5, 1, 1, 0), "a month, P 1: every NPC grows (as before)");
        }
    }
}
