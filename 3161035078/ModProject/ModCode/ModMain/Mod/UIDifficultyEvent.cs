using EGameTypeData;
using MOD_nE7UL2.Const;
using ModLib.Attributes;
using ModLib.Mod;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Difficulty UI: the base game descriptions are replaced by what the difficulty does in Taoist.
    /// </summary>
    [Cache(ModConst.UI_DIFFICULTY_EVENT, CacheType = CacheAttribute.CType.Global, WorkOn = CacheAttribute.WType.Global)]
    public class UIDifficultyEvent : ModEvent
    {
        public const string DESC_NAME = "TaoistDifficultyDesc";
        public const string NOTE_NAME = "TaoistDifficultyNote";
        /// <summary>Recommended highest S&amp;M score by game level (Normal: any).</summary>
        public static readonly int[] RECOMMENDED_P = { int.MaxValue, int.MaxValue, 60000, 30000, 10000, 0 };
        /// <summary>The note shared by every difficulty: one red line at the top right of the cards.</summary>
        public static Color NOTE_COLOR => new Color(0.72f, 0.08f, 0.06f);
        /// <summary>Bottom right corner of the note from the center of the close button (canvas units).</summary>
        public const float NOTE_DX = -40f, NOTE_DY = -80f;

        /// <summary>The text of smcfgs222 split at its first blank line: the note shared by every difficulty, the rest.</summary>
        private static (string note, string body) Split(int gameLvl)
        {
            var all = string.Format(GameTool.LS("smcfgs222"), gameLvl);
            var i = all.IndexOf("\n\n", StringComparison.Ordinal);
            return i < 0 ? (string.Empty, all) : (all.Substring(0, i), all.Substring(i + 2));
        }

        /// <summary>The note shared by every difficulty ("The difficulty also raises the stats...").</summary>
        public static string Note => Split(1).note;

        /// <summary>Description of a game level (1 Normal .. 5 Chaos), without the shared note.</summary>
        public static string Description(int gameLvl)
        {
            var p = RECOMMENDED_P[Math.Max(0, Math.Min(RECOMMENDED_P.Length - 1, gameLvl))];
            return Split(gameLvl).body + "\n\n" + (p == int.MaxValue ? GameTool.LS("smcfgs224") : string.Format(GameTool.LS("smcfgs223"), p));
        }

        /// <summary>The shared note: one red line, right aligned, on the top right corner of the last card (Chaos).</summary>
        private static void AddNote(UIDifficulty ui)
        {
            var old = ui.transform.Find(NOTE_NAME);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var last = ui.itemList.Count == 0 ? null : ui.itemList[ui.itemList.Count - 1];
            if (last == null || last.textDieDesc == null || last.goGroupRoot == null || string.IsNullOrEmpty(Note)) return;
            var go = UnityEngine.Object.Instantiate(last.textDieDesc.gameObject, ui.transform, false);
            go.name = NOTE_NAME;
            go.SetActive(true);
            var text = go.GetComponent<Text>();
            text.alignment = TextAnchor.LowerRight;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.fontSize = 17;
            text.color = NOTE_COLOR;
            text.raycastTarget = false;
            text.text = Note;
            var rt = go.GetComponent<RectTransform>();
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(900f, 26f);
            // right aligned with the cards, between the title and the top of the cards: from the close button of the
            // window (top right corner); drawn over the cards
            Button close = null;
            foreach (var b in ui.GetComponentsInChildren<Button>(true))
                if (b != null && b.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0) { close = b; break; }
            if (close == null) { go.SetActive(false); return; }
            rt.position = close.transform.position + new Vector3(NOTE_DX * rt.lossyScale.x, NOTE_DY * rt.lossyScale.y, 0f);
            go.transform.SetAsLastSibling();
        }

        private static void Place(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void AddDescription(UIDifficultyItem item)
        {
            if (item.goGroupRoot == null) return;
            var old = item.goGroupRoot.transform.Find(DESC_NAME);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            // a light panel: black on the dark pictures (Hell, Chaos) cannot be read
            var panel = new GameObject(DESC_NAME);
            panel.transform.SetParent(item.goGroupRoot.transform, false);
            var image = panel.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.5f);
            image.raycastTarget = false;
            Place(panel.GetComponent<RectTransform>(), 224f, 430f);

            var go = UnityEngine.Object.Instantiate(item.textDieDesc.gameObject, panel.transform, false);
            go.SetActive(true);
            Place(go.GetComponent<RectTransform>(), 208f, 420f);
            var text = go.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.fontSize = 17;
            text.color = Color.black;
            text.raycastTarget = false;
            text.text = Description((int)item.gameLevel);
        }

        /// <summary>
        /// Hides the description roots and adds the Taoist description.
        /// </summary>
        public override void OnOpenUIEnd(OpenUIEnd e)
        {
            base.OnOpenUIEnd(e);
            if (e.uiType.uiName == UIType.Difficulty.uiName)
            {
                var ui = g.ui.GetUI<UIDifficulty>(UIType.Difficulty);
                foreach (var item in ui.itemList)
                {
                    item.goRoot1.active = false;
                    item.goRoot1_En.active = false;
                    item.goRoot2.active = false;
                    item.goRoot2_En.active = false;
                    AddDescription(item);
                }
                AddNote(ui);
            }
        }
    }
}
