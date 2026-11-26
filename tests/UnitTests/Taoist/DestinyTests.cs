using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOD_nE7UL2.Mod;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;

namespace UnitTests.Taoist
{
    /// <summary>Destinies (lucks) of the mod: the constitution tiers, the names of the rank destinies (constitutions,
    /// artisan, rebirth, trainee, merchant).</summary>
    [TestClass]
    public class DestinyTests
    {
        private static string ModConf => TestSetup.Repo("3161035078", "ModProject", "ModConf");

        [TestMethod, Description("Destinies: a luck a tier of every constitution (ConstitutionEvent.LuckId), every destiny name (constitutions, artisan, rebirth, trainee, merchant ranks) unique, no 'Lvl' names, in every language")]
        public void Destinies_UniqueNames()
        {
            var feats = JArray.Parse(File.ReadAllText(Path.Combine(ModConf, "constluck_roleCreateFeature.json")));
            var ids = feats.Select(x => (int)x["id"]).ToList();
            foreach (ConstitutionEvent.Kind k in System.Enum.GetValues(typeof(ConstitutionEvent.Kind)))
                for (var t = 1; t <= 10; t++)
                    Check.True(ids.Contains(ConstitutionEvent.LuckId(k, t)), $"luck of {k} tier {t}", ConstitutionEvent.LuckId(k, t));
            Check.Eq(70, ids.Distinct().Count(), "70 lucks");
            Check.Eq(0, ConstitutionEvent.LuckId(ConstitutionEvent.Kind.Venom, 0), "tier 0: no luck");
            var langs = new[] { "de", "es", "fr", "ja", "la", "ru", "th", "vi" };
            var arts = new[] { "alchemy", "forge", "fengshui", "herbology", "mining", "talismans" };
            var files = new[] { "constluck_localText.json", "rebirth_localText.json", "trainee_martial_localText.json", "trainee_magic_localText.json", "merchant_localText.json" }.Concat(arts.Select(a => $"apprentice_{a}_localText.json")).ToList();
            var rank = new System.Text.RegularExpressions.Regex(@"^(apprentice|constluck|trainee|merchant)\d+$|^rebirth4201010\d\d$");
            foreach (var field in new[] { "en", "ch", "tc", "kr" }.Concat(langs))
            {
                var names = files.SelectMany(f => JArray.Parse(File.ReadAllText(langs.Contains(field) ? Path.Combine(ModConf, field, f) : Path.Combine(ModConf, f))))
                    .Where(x => rank.IsMatch((string)x["key"]))
                    .Select(x => (string)x[langs.Contains(field) ? "en|ch|tc|kr" : field]).ToList();
                Check.Eq(170, names.Count, $"{field}: 70 constitution + 60 artisan + 10 rebirth + 20 trainee + 10 merchant names");
                Check.True(names.Count == names.Distinct().Count(), $"{field}: every name unique", string.Join(", ", names.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)));
                Check.True(names.All(x => !string.IsNullOrWhiteSpace(x) && !x.Contains("Lvl")), $"{field}: no empty / 'Lvl' name");
            }
        }
    }
}
