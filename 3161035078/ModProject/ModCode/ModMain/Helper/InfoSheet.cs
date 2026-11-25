using MOD_nE7UL2.Mod;
using ModLib.Helper;
using ModLib.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MOD_nE7UL2.Helper
{
    /// <summary>
    /// A window of facts (UICustom1): a subtitle, a picture on the left, the facts as "label  value" rows (labels in one
    /// column, values in another, left aligned), a note in small grey lines, the buttons in a row under them. Easier to
    /// read than a paragraph (the plots of the town map). InfoDialog.LastText / LastUIType tell the tests what it shows.
    /// </summary>
    public static class InfoSheet
    {
        public class Row
        {
            public string Label { get; set; }
            public string Value { get; set; }
            public Color? Color { get; set; }
            public Row(string label, string value, Color? color = null) { Label = label; Value = value; Color = color; }
        }

        public static Color LABEL => new Color(0.45f, 0.3f, 0.08f);
        public static Color GOOD => new Color(0.1f, 0.45f, 0.1f);
        public static Color BAD => new Color(0.6f, 0.1f, 0.1f);
        public static Color NOTE => new Color(0.35f, 0.35f, 0.35f);

        /// <summary>A text whose left edge is at x (not its center).</summary>
        public static UIItemText LeftText(UICustomBase ui, float x, float y, string text, Color? color = null, int size = 14, FontStyle style = FontStyle.Normal)
        {
            var t = ui.AddText(x, y, text).Format(color ?? Color.black, size, style).Align(TextAnchor.MiddleLeft);
            t.Pos(x + t.Item.rectTransform.rect.width / 2f, y);
            return t;
        }

        /// <summary>
        /// The portrait of a cultivator in a box centered at (cx, cy) (the face and the bust, as the scan of the spirit
        /// sense); a click opens its information when onClick is null and clickable. Null when it cannot be drawn.
        /// </summary>
        public static UIItemImage Portrait(UICustomBase ui, WorldUnitBase unit, float cx, float cy, float w, float h, bool clickable = true)
        {
            if (unit == null) return null;
            try
            {
                var holder = ui.AddImage(cx, cy, null);
                holder.Item.color = new Color(0f, 0f, 0f, 0.06f);
                holder.Size(w, h);
                var img = PortraitHelper.Draw(holder.Item.transform, unit, new Vector2(w, h), Vector2.zero);
                if (img != null) img.uvRect = DamageInfo.HUMAN_UV;
                if (clickable)
                {
                    holder.Item.raycastTarget = true;
                    var btn = holder.Item.gameObject.AddComponent<UnityEngine.UI.Button>();
                    btn.onClick.AddListener(ActionHelper.TracedUnityAction(() => g.ui.OpenUISafe<UINPCInfo>(UIType.NPCInfo).InitData(unit, false)));
                }
                return holder;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"InfoSheet.Portrait {unit.GetUnitId()}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Height of a row of the window.</summary>
        public static float RowHeight(UICustomBase ui) => Math.Abs(ui.Rows[ui.FirstRow + 1] - ui.Rows[ui.FirstRow]);

        /// <summary>
        /// Opens the window. picture: a game sprite (atlas, name; null: none); buttons: a row under the facts (none: the
        /// window's own OK); a button closes the window, then runs its action. It replaces a window of Taoist and waits for
        /// the window of another mod (TaoistWindows): returns it, or null while it waits.
        /// </summary>
        public static UICustom1 Show(string title, string subtitle, string[] picture, IList<Row> rows, string note, params (string label, Action act)[] buttons)
        {
            TaoistWindows.CloseMine(false);
            UICustom1 shown = null;
            TaoistWindows.Open1(() => shown = Fill(title, subtitle, picture, rows, note, buttons));
            return shown;
        }

        private static UICustom1 Fill(string title, string subtitle, string[] picture, IList<Row> rows, string note, (string label, Action act)[] buttons)
        {
            var ui = new UICustom1(title);
            var dy = RowHeight(ui);
            var left = ui.Columns[ui.FirstCol];
            var width = ui.Columns[ui.LastCol] - left;
            var row = ui.FirstRow;
            if (!string.IsNullOrEmpty(subtitle))
                ui.AddText(ui.MidCol, row++, subtitle).Format(NOTE, 14);
            row++;
            var top = ui.Rows[row] + dy / 2f;
            // the picture on the left, as tall as the rows (at least 5)
            var picRows = Math.Max(5, rows.Count);
            var picW = width * 0.26f;
            var picH = dy * picRows;
            var hasPic = picture != null && EstateEvent.Picture(ui, picture, left + picW / 2f, top - picH / 2f, picW, picH) != null;
            var labelX = hasPic ? left + picW + 40f : left + width * 0.12f;
            var labelW = 160f;
            foreach (var r in rows)
            {
                if (row >= ui.LastRow - 2) break;
                LeftText(ui, labelX, ui.Rows[row], r.Label, LABEL, 14, FontStyle.Bold);
                LeftText(ui, labelX + labelW, ui.Rows[row], r.Value, r.Color ?? Color.black, 14);
                row++;
            }
            if (hasPic) row = Math.Max(row, (int)Math.Ceiling(ui.FirstRow + (string.IsNullOrEmpty(subtitle) ? 1 : 2) + (double)picRows));
            row++;
            foreach (var para in (note ?? string.Empty).Split('\n').Where(x => x.Length > 0))
                foreach (var line in InfoDialog.Wrap(para, InfoDialog.WRAP_CHARS_LONG))
                {
                    if (row >= ui.LastRow - (buttons.Length > 0 ? 2 : 0)) break;
                    ui.AddText(ui.MidCol, row++, line).Format(NOTE, 13);
                }
            if (buttons.Length > 0)
            {
                try { ui.UI.btnOK?.gameObject.SetActive(false); } catch { }
                var y = Math.Min(ui.LastRow - 1, row + 1);
                var w = Math.Min(260f, width / buttons.Length - 10f);
                for (var i = 0; i < buttons.Length; i++)
                {
                    var b = buttons[i];
                    var x = left + width * (i + 0.5f) / buttons.Length;
                    ui.AddButton(x, ui.Rows[y], () =>
                    {
                        TaoistWindows.CloseMine(true);
                        b.act?.Invoke();
                    }, b.label).Format(Color.black, 14).Size(w, 34f);
                }
            }
            var text = string.Join("\n", new[] { subtitle }.Concat(rows.Select(r => $"{r.Label}: {r.Value}")).Concat(new[] { note }).Where(x => !string.IsNullOrEmpty(x)));
            InfoDialog.Remember(text, UIType.TextInfoLong);
            return ui;
        }
    }
}
