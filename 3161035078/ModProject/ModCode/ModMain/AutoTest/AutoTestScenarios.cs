using ModLib.AutoTest;
using System.Collections.Generic;

namespace MOD_nE7UL2.AutoTest
{
    /// <summary>
    /// The automatic scenarios of Taoist: every IScenarioDefinition of this assembly (one class per scenario in
    /// AutoTest/Scenarios/&lt;Group&gt;/&lt;Id&gt;_&lt;Feature&gt;.cs, ids of 3161035078/tests/scenarios.json), found by
    /// ModLib.AutoTest.ScenarioRegistry, with the tools of ModLib (effect galleries TE-xx).
    /// </summary>
    public static class AutoTestScenarios
    {
        /// <summary>Creates all scenarios ordered by id; fails on duplicated or mismatching ids.</summary>
        public static List<Scenario> Create() => ScenarioRegistry.Create(typeof(AutoTestScenarios).Assembly, typeof(ScenarioRegistry).Assembly);
    }
}
