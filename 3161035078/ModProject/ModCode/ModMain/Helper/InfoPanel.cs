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
    /// Drawing kit of the information windows of Taoist (UICustom1 = UITextInfoLong): colored boxes (cards, highlighted
    /// rows), section titles with a game icon, left aligned texts, progress bars, a row of buttons; and a ready window of
    /// ranks (a header card with the value, the rank and the bar to the next one, then a table of the ranks: the reached
    /// ones in black, the current one highlighted, the next ones in grey). Used by the constitutions, the artisan /
    /// trainee / merchant ranks and the rebirth. The text of a window is remembered (InfoDialog.LastText) for the tests.
    /// </summary>
    public static class InfoPanel
    {
        public static Color TITLE => new Color(0.45f, 0.12f, 0.08f);
        public static Color GOLD => new Color(0.62f, 0.40f, 0.02f);
        public static Color TEXT => new Color(0.12f, 0.10f, 0.08f);
        public static Color GOOD => new Color(0.10f, 0.45f, 0.15f);
        public static Color BAD => new Color(0.68f, 0.10f, 0.08f);
        public static Color GREY => new Color(0.52f, 0.50f, 0.48f);
        public static Color NOTE => new Color(0.38f, 0.36f, 0.33f);
        /// <summary>Background of a card / an owned item / the current row / a locked item / a bar.</summary>
        public static Color CARD => new Color(0.55f, 0.42f, 0.25f, 0.10f);
        public static Color CARD_ON => new Color(0.90f, 0.66f, 0.18f, 0.24f);
        public static Color CARD_OFF => new Color(0.30f, 0.30f, 0.30f, 0.08f);
        public static Color LINE => new Color(0.45f, 0.12f, 0.08f, 0.55f);
        public static Color BAR_BACK => new Color(0f, 0f, 0f, 0.14f);
        public static Color BAR_FILL => new Color(0.85f, 0.55f, 0.10f, 0.85f);

        /// <summary>A game text icon ("[icon:NAME]" as a sprite) and a space; empty for no icon.</summary>
        public static string I(string icon) => UIPanelHelper.Icon(icon);

        // the drawing itself: ModLib UIPanelHelper (shared with the other mods)
        public static float Dy(UICustomBase ui) => UIPanelHelper.RowHeight(ui);
        public static float Left(UICustomBase ui) => UIPanelHelper.Left(ui);
        public static float Right(UICustomBase ui) => UIPanelHelper.Right(ui);
        public static float Top(UICustomBase ui) => UIPanelHelper.Top(ui);
        public static float Bottom(UICustomBase ui) => UIPanelHelper.Bottom(ui);

        public static UIItemImage Box(UICustomBase ui, float left, float top, float w, float h, Color c) => UIPanelHelper.Box(ui, left, top, w, h, c);

        public static UIItemText Text(UICustomBase ui, float x, float y, string text, Color? color = null, int size = 13, FontStyle style = FontStyle.Normal)
            => UIPanelHelper.Text(ui, x, y, text, color ?? TEXT, size, style);

        public static UIItemText Center(UICustomBase ui, float x, float y, string text, Color? color = null, int size = 13, FontStyle style = FontStyle.Normal)
            => UIPanelHelper.Center(ui, x, y, text, color ?? TEXT, size, style);

        public static UIItemText RightText(UICustomBase ui, float x, float y, string text, Color? color = null, int size = 13, FontStyle style = FontStyle.Normal)
            => UIPanelHelper.RightText(ui, x, y, text, color ?? TEXT, size, style);

        /// <summary>A section title (icon, bold, dark red) with a thin line under it; returns the y under the line.</summary>
        public static float Section(UICustomBase ui, float left, float top, float w, string icon, string title) => UIPanelHelper.Group(ui, left, top, w, icon, title, TITLE);

        /// <summary>A progress bar (0..1) by its top left corner, its text centered on it.</summary>
        public static void Bar(UICustomBase ui, float left, float top, float w, float h, double share, string text) => UIPanelHelper.Bar(ui, left, top, w, h, share, text, BAR_FILL);

        /// <summary>The lines of a text wrapped to about chars characters.</summary>
        public static List<string> Wrap(string text, int chars) => InfoDialog.Wrap(text ?? string.Empty, chars);

        /// <summary>A row of buttons centered at y (the window's own OK hidden): a click closes the window, then acts
        /// (a null action: only closes).</summary>
        public static void Buttons(UICustom1 ui, float y, params (string label, Action act)[] buttons)
        {
            try { ui.UI.btnOK?.gameObject.SetActive(false); } catch { }
            var left = Left(ui);
            var width = Right(ui) - left;
            var w = Math.Min(220f, width / Math.Max(1, buttons.Length) - 20f);
            for (var i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                var x = left + width * (i + 0.5f) / buttons.Length;
                ui.AddButton(x, y, () =>
                {
                    TaoistWindows.CloseMine(true);
                    b.act?.Invoke();
                }, b.label).Format(Color.black, 14).Size(w, 34f);
            }
        }

        /// <summary>A small "?" button (an action, the window stays: the action opens another one).</summary>
        public static UIItemButton Help(UICustomBase ui, float x, float y, Action act)
            => (UIItemButton)ui.AddButton(x, y, act, "?").Format(Color.black, 14).Size(28f, 26f);

        /// <summary>Opens a window of Taoist (it replaces another window of Taoist, waits for the window of another mod);
        /// text: what it shows, for the tests (InfoDialog.LastText).</summary>
        public static void Open(string title, Action<UICustom1> fill, Func<string> text = null)
        {
            TaoistWindows.CloseMine(false);
            TaoistWindows.Open1(() =>
            {
                var ui = new UICustom1(title);
                fill(ui);
                InfoDialog.Remember(text?.Invoke() ?? title, UIType.TextInfoLong);
            });
        }

        // ------------------------------------------------------------------ the window of ranks

        /// <summary>A rank of a ranks window: its title, what it needs, what it gives.</summary>
        public class Rank
        {
            public string Name, Needs, Effect;
            public Rank(string name, string needs, string effect) { Name = name; Needs = needs; Effect = effect; }
        }

        /// <summary>A ranks window: the value (name, icon, value now, value of the next rank as a share), the ranks, the
        /// current one (0: none), a note, a back button (null: none) and more buttons.</summary>
        public class Ranks
        {
            public string Title, Icon, ValueName, Value, Note;
            public int Current;
            public double Progress;
            public List<Rank> List = new List<Rank>();
            public Action Back;
            public List<(string label, Action act)> More = new List<(string, Action)>();
        }

        /// <summary>Text of a ranks window (tests).</summary>
        public static string TextOf(Ranks r) => string.Join("\n", new[] { r.Title, $"{r.ValueName}: {r.Value}" }
            .Concat(r.List.Select((x, i) => $"{i + 1}. {x.Name}: {x.Needs}, {x.Effect}")).Concat(new[] { r.Note }).Where(x => !string.IsNullOrEmpty(x)));

        public static void OpenRanks(Ranks r) => Open(r.Title, ui => FillRanks(ui, r), () => TextOf(r));

        public static void FillRanks(UICustom1 ui, Ranks r)
        {
            var dy = Dy(ui);
            var left = Left(ui) + 20f;
            var width = Right(ui) - Left(ui) - 40f;
            var top = Top(ui);

            // the header card: the value, the rank now, the bar to the next rank
            var cardH = dy * 3.1f;
            Box(ui, left, top, width, cardH, CARD);
            Text(ui, left + 16f, top - dy * 0.75f, I(r.Icon) + $"{r.ValueName}: {r.Value}", TITLE, 17, FontStyle.Bold);
            var now = r.Current > 0 && r.Current <= r.List.Count ? r.List[r.Current - 1] : null;
            RightText(ui, left + width - 16f, top - dy * 0.75f,
                now == null ? GameTool.LS("ui_appr_none") : string.Format(GameTool.LS("ui_appr_rank"), r.Current, now.Name), now == null ? GREY : GOLD, 16, FontStyle.Bold);
            var next = r.Current < r.List.Count ? r.List[r.Current] : null;
            Bar(ui, left + 16f, top - dy * 1.6f, width - 32f, dy * 0.95f, next == null ? 1d : r.Progress,
                next == null ? GameTool.LS("ui_panel_top") : string.Format(GameTool.LS("ui_panel_next"), next.Name, next.Needs, (Math.Max(0d, Math.Min(1d, r.Progress)) * 100).ToString("0")));
            var y = top - cardH - dy * 0.4f;

            // the table of the ranks
            y = Section(ui, left, y, width, "xingyun", GameTool.LS("ui_panel_ranks"));
            var rows = r.List.Select((x, i) => new UIPanelHelper.Cell[]
            {
                (i + 1 <= r.Current ? I("daodian") : "   ") + (i + 1), x.Name, x.Needs, new UIPanelHelper.Cell(x.Effect, i + 1 <= r.Current ? GOOD : GREY),
            }).ToList();
            y = UIPanelHelper.Table(ui, left, y, width, new[] { 0f, 0.08f, 0.36f, 0.62f },
                new[] { GameTool.LS("ui_panel_rank"), GameTool.LS("ui_panel_title"), GameTool.LS("ui_panel_needs"), GameTool.LS("ui_panel_effect") },
                rows, r.Current - 1, i => i + 1 < r.Current ? TEXT : GREY);
            if (!string.IsNullOrEmpty(r.Note))
            {
                y -= dy * 0.3f;
                foreach (var line in Wrap(r.Note, 120)) { Center(ui, left + width / 2f, y - dy * 0.5f, line, NOTE, 12, FontStyle.Italic); y -= dy * 0.9f; }
            }
            var buttons = new List<(string, Action)>();
            if (r.Back != null) buttons.Add((GameTool.LS("ui_panel_back"), r.Back));
            buttons.AddRange(r.More);
            buttons.Add((GameTool.LS("dlg_ok"), null));
            Buttons(ui, Math.Max(Bottom(ui) + dy, y - dy * 1.2f), buttons.ToArray());
        }
    }
}
