using ModLib.Helper;
using ModLib.Object;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Title screen: the panel "More mods by Fouru" (TitleOtherModsConfig, ModConf/title_othermods_configs.json): one mod
    /// at a time - its picture, name, text and a button to its page - in the free space on the left (between the buttons
    /// of the game and the middle of the wallpaper); the next one every RotateSec when there are several. A mod loaded in
    /// this game is not shown (the player has it); no mod left: no panel.
    /// </summary>
    public partial class UITitleScreenEvent
    {
        /// <summary>The panel: its centre and its width as shares of the screen (centre from the bottom-left corner).</summary>
        public const float PANEL_X = 0.262f, PANEL_Y = 0.315f, PANEL_W = 0.19f;
        private const float PAD = 12f, TITLE_H = 26f, NAME_H = 30f, TEXT_H = 62f, BUTTON_H = 38f, GAP = 6f;

        private static readonly List<UIItemBase> _otherItems = new List<UIItemBase>();
        private static UIItemImage _otherBack, _otherPicture;
        private static UIItemText _otherTitle, _otherName, _otherText;
        private static UIItemButton _otherButton;
        private static List<TitleOtherModsConfig.OtherMod> _otherMods = new List<TitleOtherModsConfig.OtherMod>();
        private static int _otherIndex;
        private static DateTime _otherNext;

        /// <summary>The mods the panel shows: those of the config not loaded in this game (none when it is disabled).</summary>
        public static List<TitleOtherModsConfig.OtherMod> OtherModsToShow()
        {
            var c = TitleOtherModsConfig.Conf;
            if (c == null || !c.Enabled) return new List<TitleOtherModsConfig.OtherMod>();
            return c.Mods.Where(m => string.IsNullOrEmpty(m.ModId) || !IsModLoaded(m.ModId)).ToList();
        }

        /// <summary>A mod is loaded in this game (by its id).</summary>
        public static bool IsModLoaded(string modId)
        {
            try { return g.mod.IsLoadMod(modId); }
            catch { return false; }
        }

        /// <summary>The panel is on the title screen (tests); the name of the mod it shows.</summary>
        public static bool OtherModsShown => _otherBack?.Component != null && _otherBack.IsActive();
        public static string OtherModShown => OtherModsShown && _otherMods.Count > 0 ? _otherMods[_otherIndex].Name : null;
        /// <summary>The panel items (tests: positions, sizes).</summary>
        public static UIItemImage OtherModsPanel => _otherBack;
        public static UIItemButton OtherModsButton => _otherButton;

        /// <summary>The panel built on the title screen (again: the one of an older title screen removed). False: nothing to show.</summary>
        public static bool ShowOtherMods()
        {
            HideOtherMods();
            _otherMods = OtherModsToShow();
            if (_otherMods.Count == 0 || !g.ui.HasUI(UIType.Login)) return false;
            try
            {
                var ui = new UICover<UILogin>(UIType.Login);
                var parent = ui.UI.btnSet.transform.parent;
                float W = UIHelper.GetUIScreenWidth(), H = UIHelper.GetUIScreenHeight();
                var w = W * PANEL_W;
                var inner = w - 2 * PAD;
                var picH = inner * 9f / 16f;
                var h = PAD + TITLE_H + GAP + picH + GAP + NAME_H + TEXT_H + GAP + BUTTON_H + PAD;
                // UI units from the bottom-left corner of the screen: the centre of the panel, its top
                var cx = W * PANEL_X;
                var top = H * PANEL_Y + h / 2f;

                _otherBack = Keep(ui.AddImage(0f, 0f, null));
                _otherBack.Item.sprite = null;
                _otherBack.Item.color = new Color(0.04f, 0.05f, 0.08f, 0.68f);
                _otherBack.Size(w, h);
                Place(_otherBack, parent, cx, top - h / 2f);

                var y = top - PAD - TITLE_H / 2f;
                _otherTitle = Keep(ui.AddText(0f, 0f, GameTool.LS("ui_othermods_title")));
                _otherTitle.Align(TextAnchor.MiddleCenter).Format(new Color(0.91f, 0.79f, 0.49f), 17, FontStyle.Bold);
                SizeText(_otherTitle, inner, TITLE_H);
                Place(_otherTitle, parent, cx, y);

                y = top - PAD - TITLE_H - GAP - picH / 2f;
                _otherPicture = Keep(ui.AddImage(0f, 0f, null));
                _otherPicture.Size(inner, picH);
                Place(_otherPicture, parent, cx, y);

                y = top - PAD - TITLE_H - GAP - picH - GAP - NAME_H / 2f;
                _otherName = Keep(ui.AddText(0f, 0f, ""));
                _otherName.Align(TextAnchor.MiddleLeft).Format(Color.white, 20, FontStyle.Bold);
                SizeText(_otherName, inner, NAME_H);
                Place(_otherName, parent, cx, y);

                y -= NAME_H / 2f + TEXT_H / 2f;
                _otherText = Keep(ui.AddText(0f, 0f, ""));
                _otherText.Align(TextAnchor.UpperLeft, VerticalWrapMode.Truncate, HorizontalWrapMode.Wrap).Format(new Color(0.88f, 0.86f, 0.8f), 14);
                SizeText(_otherText, inner, TEXT_H);
                Place(_otherText, parent, cx, y);

                y = top - h + PAD + BUTTON_H / 2f;
                _otherButton = Keep((UIItemButton)ui.AddButton(0f, 0f, OpenOtherMod, "", ui.UI.btnPaperChange));
                _otherButton.Align(TextAnchor.MiddleCenter).Format(Color.white, 16);
                StripGameText(_otherButton);
                _otherButton.Size(inner * 0.7f, BUTTON_H);
                Place(_otherButton, parent, cx, y);

                _otherIndex = 0;
                ShowOtherMod(0);
                DebugHelper.WriteLine($"Title: other mods panel ({string.Join(", ", _otherMods.Select(m => m.Name))})");
                return true;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Title: other mods panel not shown: {ex.Message}");
                HideOtherMods();
                return false;
            }
        }

        /// <summary>The mod <paramref name="index"/> in the panel.</summary>
        public static void ShowOtherMod(int index)
        {
            if (!OtherModsShown || _otherMods.Count == 0) return;
            _otherIndex = ((index % _otherMods.Count) + _otherMods.Count) % _otherMods.Count;
            var m = _otherMods[_otherIndex];
            _otherName.Set(m.Name);
            _otherText.Set(Text(m.Text));
            _otherButton.Set(Text(string.IsNullOrEmpty(m.Button) ? "ui_othermods_open" : m.Button));
            _otherButton.Active(!string.IsNullOrEmpty(m.Url));
            var sprite = string.IsNullOrEmpty(m.Image) ? null : SpriteHelper.GetImage(ModMain.ModObj?.ModId ?? "nE7UL2", m.Image);
            _otherPicture.Active(sprite != null);
            if (sprite != null) _otherPicture.SetSprite(sprite);
            _otherNext = DateTime.Now.AddSeconds(TitleOtherModsConfig.Conf.RotateSec);
        }

        /// <summary>The next mod when its time came (title screen, every second).</summary>
        private static void RotateOtherMods()
        {
            if (_otherMods.Count > 1 && OtherModsShown && DateTime.Now >= _otherNext) ShowOtherMod(_otherIndex + 1);
        }

        /// <summary>The page of the mod shown (its button).</summary>
        public static void OpenOtherMod()
        {
            if (_otherMods.Count == 0) return;
            var url = _otherMods[_otherIndex].Url;
            if (string.IsNullOrEmpty(url) || !(url.StartsWith("https://") || url.StartsWith("http://"))) return;
            try { Process.Start(url); }
            catch (Exception ex) { DebugHelper.WriteLine($"Title: {url} not opened: {ex.Message}"); }
        }

        public static void HideOtherMods()
        {
            foreach (var x in _otherItems)
            {
                try { if (x?.Component != null) UnityEngine.Object.Destroy(((Component)x.Component).gameObject); }
                catch { }
            }
            _otherItems.Clear();
            _otherBack = _otherPicture = null;
            _otherTitle = _otherName = _otherText = null;
            _otherButton = null;
        }

        private static T Keep<T>(T item) where T : UIItemBase
        {
            _otherItems.Add(item);
            return item;
        }

        /// <summary>A localText key of Taoist, or the text itself.</summary>
        private static string Text(string keyOrText)
        {
            if (string.IsNullOrEmpty(keyOrText)) return string.Empty;
            var t = GameTool.LS(keyOrText);
            return string.IsNullOrEmpty(t) || t == keyOrText ? keyOrText : t;
        }

        private static void SizeText(UIItemText t, float w, float h) => t.Item.rectTransform.sizeDelta = new Vector2(w, h);

        /// <summary>
        /// An item put under <paramref name="parent"/> (the layer of the buttons of the title screen) at a point given in UI
        /// units from the bottom-left corner of the screen (screen point -> world point of the parent: whatever the canvas,
        /// as TitleModHelper).
        /// </summary>
        private static void Place(UIItemBase item, Transform parent, float ux, float uy)
        {
            item.SetParentTransform(parent);
            var rt = ((Component)item.Component).GetComponent<RectTransform>();
            var prt = parent.GetComponent<RectTransform>();
            var canvas = rt.GetComponentInParent<Canvas>();
            var cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var sp = new Vector2(ux / UIHelper.GetUIScreenWidth() * Screen.width, uy / UIHelper.GetUIScreenHeight() * Screen.height);
            if (prt != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(prt, sp, cam, out var wp))
                rt.position = new Vector3(wp.x, wp.y, rt.position.z);
        }
    }
}
