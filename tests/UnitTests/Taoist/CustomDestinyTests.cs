using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOD_nE7UL2.Mod;

namespace UnitTests.Taoist
{
    [TestClass]
    public class CustomDestinyTests
    {
        [TestMethod]
        public void Penalty_ByQuality()
        {
            Assert.AreEqual(3000, CustomDestinyEvent.Penalty(1));
            Assert.AreEqual(8000, CustomDestinyEvent.Penalty(6));
            Assert.AreEqual(3000, CustomDestinyEvent.Penalty(-5), "outside 1..6: brought into it");
            Assert.AreEqual(11000, CustomDestinyEvent.TotalPenalty(new[] { 1, 6 }));
        }

        [TestMethod]
        public void Filters()
        {
            Assert.IsTrue(CustomDestinyEvent.Matches(3, "Deadly Fingers", "Spear +15", 0, "", null));
            Assert.IsTrue(CustomDestinyEvent.Matches(3, "Deadly Fingers", "Spear +15", 3, "deadly", "SPEAR"));
            Assert.IsFalse(CustomDestinyEvent.Matches(3, "Deadly Fingers", "Spear +15", 4, "", ""));
            Assert.IsFalse(CustomDestinyEvent.Matches(3, "Deadly Fingers", "Spear +15", 0, "spear", ""));
        }
    }
}
