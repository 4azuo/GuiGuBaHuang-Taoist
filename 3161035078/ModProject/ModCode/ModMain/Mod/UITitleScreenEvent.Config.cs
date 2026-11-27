using MOD_nE7UL2.Object;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// The other mods of the author shown on the title screen (UITitleScreenEvent, panel "More mods by Fouru"), read from
    /// ModConf/title_othermods_configs.json; the defaults below are the same as the file.
    /// </summary>
    public class TitleOtherModsConfig : JsonConfig<TitleOtherModsConfig>
    {
        public const string FILE = "title_othermods_configs.json";
        public override string FileName => FILE;

        /// <summary>The panel is shown (false: never).</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>Seconds a mod stays in the panel before the next one (several mods).</summary>
        public int RotateSec { get; set; } = 8;
        /// <summary>The mods, in this order; a mod loaded in this game is not shown (the player has it).</summary>
        public List<OtherMod> Mods { get; set; } = new List<OtherMod>
        {
            new OtherMod
            {
                ModId = "rLvJjS",
                Name = "Dao of Creation",
                Text = "ui_othermods_doc",
                Image = "othermods_doc.jpg",
                Url = "https://discord.gg/XKaP3EYfw",
                Button = "ui_othermods_discord",
            },
        };

        protected override void Fix(TitleOtherModsConfig d)
        {
            RotateSec = Math.Max(3, RotateSec);
            Mods = (Mods ?? d.Mods).Where(x => x != null && !string.IsNullOrEmpty(x.Name)).ToList();
        }

        /// <summary>A mod of the panel.</summary>
        public class OtherMod
        {
            /// <summary>Its mod id (soleID): not shown when it is loaded; empty: always shown (software...).</summary>
            public string ModId { get; set; }
            /// <summary>Its name (not translated).</summary>
            public string Name { get; set; }
            /// <summary>Its text: a localText key of Taoist (or the text itself).</summary>
            public string Text { get; set; }
            /// <summary>Its picture in ModImg of Taoist (16:9; empty: none).</summary>
            public string Image { get; set; }
            /// <summary>Where the button goes (its page, a Discord server...).</summary>
            public string Url { get; set; }
            /// <summary>Label of the button: a localText key of Taoist (or the text itself).</summary>
            public string Button { get; set; }
        }
    }
}
