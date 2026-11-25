using ModLib.Helper;
using ModLib.Object;
using System;

namespace MOD_nE7UL2.Helper
{
    /// <summary>
    /// Opens the windows of Taoist (UICustom1 = the game's UITextInfoLong, UICustom2 = UITextInfo; the game keeps one of
    /// each). A window of Taoist open in that UI is replaced at once, as before (refresh, next page, next step); a window of
    /// another mod or of the game in it (the error reports consent of ModLib, a notice...) is never written over: the
    /// window of Taoist waits until it is closed (ModLib UICustom1.WhenFree / UICustom2.WhenFree).
    /// </summary>
    public static class TaoistWindows
    {
        private static UICustomBase mine1, mine2;

        /// <summary>Opens a UICustom1 (open makes and fills it). True when opened now, false when it waits.</summary>
        public static bool Open1(Action open) => Open(true, open);

        /// <summary>Opens a UICustom2 (open makes and fills it). True when opened now, false when it waits.</summary>
        public static bool Open2(Action open) => Open(false, open);

        private static UIType.UITypeBase TypeOf(bool big) => big ? UIType.TextInfoLong : UIType.TextInfo;

        /// <summary>The UI (UITextInfoLong when big, else UITextInfo) shows a window of Taoist.</summary>
        public static bool MineOpen(bool big)
        {
            var last = big ? (UICustomBase)UICustom1.LastUICustom : UICustom2.LastUICustom;
            return last != null && last == (big ? mine1 : mine2) && g.ui.HasUI(TypeOf(big));
        }

        /// <summary>Closes the UI only when it shows a window of Taoist (never the window of another mod).</summary>
        public static void CloseMine(bool big)
        {
            if (MineOpen(big)) g.ui.CloseUI(TypeOf(big));
        }

        private static bool Open(bool big, Action open)
        {
            if (open == null) return false;
            // our own window: replaced now (the queue would wait until the player closes it)
            if (MineOpen(big)) { Run(big, open); return true; }
            return big ? UICustom1.WhenFree(() => Run(true, open)) : UICustom2.WhenFree(() => Run(false, open));
        }

        private static void Run(bool big, Action open)
        {
            open();
            if (big) mine1 = UICustom1.LastUICustom;
            else mine2 = UICustom2.LastUICustom;
        }
    }
}
