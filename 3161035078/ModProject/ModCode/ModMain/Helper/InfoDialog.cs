using ModLib.Enum;
using ModLib.Helper;
using ModLib.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MOD_nE7UL2.Helper
{
    /// <summary>
    /// Message windows of the mod. A short text keeps the game's message box; a long text (or one of several lines) opens
    /// a text window (UITextInfo, UICustom2): the lines wrapped by words, the buttons under them. Same calls as
    /// g.ui.MsgBox, so a MsgBox call becomes InfoDialog.Msg without other change.
    /// </summary>
    public static class InfoDialog
    {
        /// <summary>
        /// Longer than this, or more lines: the text window. The game's message box holds a few short lines ("Cost
        /// 306.\nAre you sure?" stays a message box, with its Yes / No).
        /// </summary>
        public const int SHORT_TEXT = 120, SHORT_LINES = 3;
        public const int WRAP_CHARS = 64;

        public static bool IsLong(string text) => !string.IsNullOrEmpty(text) && (text.Length > SHORT_TEXT || text.Split('\n').Length > SHORT_LINES);

        /// <summary>A message (OK).</summary>
        public static void Msg(string title, string text)
        {
            if (!IsLong(text)) { g.ui.MsgBox(title, text); return; }
            Show(title, text);
        }

        /// <summary>A message with buttons (MsgBoxButtonEnum.YesNo: Yes runs the action).</summary>
        public static void Msg(string title, string text, MsgBoxButtonEnum buttons, Action yes)
        {
            if (!IsLong(text)) { g.ui.MsgBox(title, text, buttons, yes); return; }
            if (buttons == MsgBoxButtonEnum.YesNo)
                Show(title, text, (GameTool.LS("dlg_yes"), yes), (GameTool.LS("dlg_no"), null));
            else
                Show(title, text, (GameTool.LS("dlg_ok"), yes));
        }

        /// <summary>
        /// The text window: the text wrapped by words (a paragraph a line break), the buttons in a row under it (none: the
        /// window's own OK). A button closes the window, then runs its action.
        /// </summary>
        public static UICustomBase Show(string title, string text, params (string label, Action act)[] buttons)
        {
            g.ui.CloseUI(UIType.TextInfo);
            g.ui.CloseUI(UIType.TextInfoLong);
            var paras = (text ?? string.Empty).Split('\n');
            var reserved = buttons.Length > 0 ? 2 : 0;
            // the small window (UITextInfo) while the text fits; a longer one opens the big window (UITextInfoLong):
            // nothing cut (the mortal house of the estate: 12 lines), its title wide enough for "<town> - plots"
            var smallRows = (int)Math.Floor(UIHelper.GetUIScreenHeight() * 0.2f / UIHelper.GetUIDeltaY()) + 1;
            var big = paras.Sum(x => Wrap(x, WRAP_CHARS).Count) > smallRows - reserved;
            UICustomBase ui;
            UnityEngine.UI.Button ok;
            if (big) { var u = new UICustom1(title); ui = u; ok = u.UI.btnOK; LastUIType = UIType.TextInfoLong; }
            else { var u = new UICustom2(title); ui = u; ok = u.UI.btnOK; LastUIType = UIType.TextInfo; }
            var type = LastUIType;
            var row = ui.FirstRow;
            foreach (var para in paras)
                foreach (var line in Wrap(para, big ? WRAP_CHARS_LONG : WRAP_CHARS))
                {
                    if (row >= ui.LastRow - reserved) break;
                    ui.AddText(ui.MidCol, row++, line).Format(null, 14);
                }
            if (buttons.Length > 0)
            {
                // the window's own OK only closes it: with buttons of the dialog (Yes / No...) it is hidden, a player (or a
                // test clicking "OK") would close the choice without an answer
                try { if (ok != null) ok.gameObject.SetActive(false); } catch { }
                var y = Math.Min(ui.LastRow - 1, row + 1);
                var w = Math.Min(260f, (ui.Columns[ui.LastCol] - ui.Columns[ui.FirstCol]) / buttons.Length - 10f);
                for (var i = 0; i < buttons.Length; i++)
                {
                    var b = buttons[i];
                    var x = ui.Columns[ui.FirstCol] + (ui.Columns[ui.LastCol] - ui.Columns[ui.FirstCol]) * (i + 0.5f) / buttons.Length;
                    ui.AddButton(x, ui.Rows[y], () =>
                    {
                        g.ui.CloseUI(type);
                        b.act?.Invoke();
                    }, b.label).Format(Color.black, 14).Size(w, 34f);
                }
            }
            LastText = text;
            return ui;
        }

        /// <summary>Line length of the big window (UITextInfoLong, about twice as wide as UITextInfo).</summary>
        public const int WRAP_CHARS_LONG = 110;

        /// <summary>The text of the last window (tests).</summary>
        public static string LastText { get; private set; }

        /// <summary>The window of the last text: UIType.TextInfo, or UIType.TextInfoLong for a long text (tests).</summary>
        public static UIType.UITypeBase LastUIType { get; private set; } = UIType.TextInfo;

        /// <summary>
        /// Lines of at most max characters, cut between words (a word longer than a line stays whole). A rich text tag
        /// ("&lt;sprite name=zm_lingshi&gt;": the icons of the texts) is never cut and does not count in the length.
        /// </summary>
        public static List<string> Wrap(string text, int max)
        {
            var lines = new List<string>();
            var cur = string.Empty;
            var curLen = 0;
            // languages without spaces (Chinese, Japanese, Thai): cut by characters
            var spaced = Untagged(text).Contains(' ');
            var words = spaced ? SplitWords(text) : SplitChars(text);
            var sep = spaced ? " " : string.Empty;
            foreach (var w in words)
            {
                var len = Untagged(w).Length;
                if (cur.Length > 0 && curLen + sep.Length + len > max)
                {
                    lines.Add(cur);
                    cur = w;
                    curLen = len;
                }
                else if (cur.Length == 0) { cur = w; curLen = len; }
                else { cur = cur + sep + w; curLen += sep.Length + len; }
            }
            if (cur.Length > 0 || lines.Count == 0) lines.Add(cur);
            return lines;
        }

        /// <summary>The text without its rich text tags (what is shown).</summary>
        private static string Untagged(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? string.Empty, "<[^<>]*>", string.Empty);

        /// <summary>Words split at the spaces out of the tags.</summary>
        private static List<string> SplitWords(string text)
        {
            var words = new List<string>();
            var cur = new System.Text.StringBuilder();
            var inTag = false;
            foreach (var ch in text)
            {
                if (ch == '<') inTag = true;
                else if (ch == '>') inTag = false;
                if (ch == ' ' && !inTag) { words.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(ch);
            }
            words.Add(cur.ToString());
            return words;
        }

        /// <summary>Characters, a tag kept whole with the character after it.</summary>
        private static List<string> SplitChars(string text)
        {
            var parts = new List<string>();
            var cur = new System.Text.StringBuilder();
            var inTag = false;
            foreach (var ch in text)
            {
                cur.Append(ch);
                if (ch == '<') inTag = true;
                else if (ch == '>') inTag = false;
                else if (!inTag) { parts.Add(cur.ToString()); cur.Clear(); }
            }
            if (cur.Length > 0) parts.Add(cur.ToString());
            return parts;
        }
    }
}
