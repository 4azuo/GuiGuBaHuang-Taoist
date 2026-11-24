using ModLib.AutoTest;
using ModLib.Helper;
using ModLib.Mod;
using ModLib.Object;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static MOD_nE7UL2.AutoTest.AutoTestHelper;

namespace MOD_nE7UL2.AutoTest.Scenarios.Title
{
    /// <summary>
    /// TT-08 (ModTitleEvent, ModLib): the ModLib buttons of the title screen ("Powered by ModLib", the language / ModCreator
    /// panel) use the frame of the game's DLC buttons, without the DLC icon and the "Claimed" mark, and keep their text.
    /// </summary>
    public class TT08_ModLibTitleFrame : IScenarioDefinition
    {
        /// <summary>Scenario id (see 3161035078/tests/scenarios.json).</summary>
        public string Id => "TT-08";

        private static string CreditText => string.Format(GameTool.LS("libtxt999990014"), ModMaster.ModObj.Version);

        /// <summary>Builds the scenario and its steps.</summary>
        public Scenario Create()
        {
            return new Scenario { Id = "TT-08", Feature = "ModTitleEvent (ModLib)", Title = "ModLib title buttons in the frame of the DLC buttons", Scope = ScenarioScope.Title, Overlay = false }
                .Step("ModLib buttons", c =>
                {
                    var login = g.ui.GetUI<UILogin>(UIType.Login);
                    if (!c.Assert(login != null, "title screen")) return;
                    var credit = FindButtons(UIType.Login, CreditText).FirstOrDefault();
                    if (!c.Assert(credit != null, "'Powered by ModLib' button")) return;
                    c.Assert(credit.ButtonLabel != null && credit.ButtonLabel.text == CreditText, $"label: {credit.ButtonLabel?.text}");

                    var dlc = new[] { login.btnDLC, login.btnDLC_En, login.btnFiveFlower, login.btnFiveFlower_En, login.btnArtBook, login.btnArtBook_En }
                        .FirstOrDefault(x => x != null && x.gameObject.activeInHierarchy);
                    if (!c.Assert(dlc != null, "a DLC button of the game on the title screen")) return;
                    //the frame of the DLC button is a sprite (Normal {SpriteRenderer}: LoginButton_normal_DLC)
                    var frameSprites = dlc.GetComponentsInChildren<SpriteRenderer>(false).Where(x => x.sprite != null).Select(x => x.sprite.name).Distinct().ToList();
                    var dlcSprites = dlc.GetComponentsInChildren<Image>(true).Where(x => x.sprite != null).Select(x => x.sprite.name).Concat(frameSprites).Distinct().ToList();
                    var shown = credit.Item.GetComponentsInChildren<Image>(false).Where(x => x.sprite != null).ToList();
                    c.Info($"DLC button {dlc.name}: frame {string.Join(", ", frameSprites)}, images {string.Join(", ", dlcSprites.Except(frameSprites))}; ModLib button: {string.Join(", ", shown.Select(x => x.sprite.name))}");
                    c.Assert(frameSprites.Count > 0 && shown.Any(x => frameSprites.Contains(x.sprite.name)), "frame picture of the DLC button");

                    //no small picture (DLC icon, "Claimed" mark): every picture shown is (nearly) as big as the button
                    var size = credit.Item.GetComponent<RectTransform>().rect.size;
                    var small = shown.Where(x => x.rectTransform.rect.width < size.x * 0.8f || x.rectTransform.rect.height < size.y * 0.6f).ToList();
                    c.Assert(small.Count == 0, small.Count == 0 ? "no DLC icon / Claimed mark" : $"small pictures shown: {string.Join(", ", small.Select(x => $"{x.name} {x.sprite.name}"))}");

                    //the panel of the language selector and the ModCreator button: same frame
                    var panel = UIHelper.GetUICustomBase(UIType.Login).SelectMany(x => x.Items).OfType<UIItemButton>()
                        .FirstOrDefault(x => string.IsNullOrEmpty(x.GetFormat()) && x.Item.GetComponentsInChildren<Image>(false).Any(i => i.sprite != null && frameSprites.Contains(i.sprite.name)));
                    c.Assert(panel != null, "language panel in the DLC frame");
                    c.Assert(FindButtons(UIType.Login, GameTool.LS("libtxt999990009")).Any(), "ModCreator button");
                })
                .Step("text kept", c =>
                {
                    //the game's scripts of the copied button would set its text again
                    var credit = FindButtons(UIType.Login, CreditText).FirstOrDefault();
                    c.Assert(credit?.ButtonLabel != null && credit.ButtonLabel.text == CreditText, $"label after 2 s: {credit?.ButtonLabel?.text}");
                }, 2000);
        }
    }
}
