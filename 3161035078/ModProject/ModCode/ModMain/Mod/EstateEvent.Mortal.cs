using MOD_nE7UL2.Helper;
using ModLib.Helper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// A mortal house bought back by the player (only the houses of the mortals: not a shop of the market, not the house
    /// of a cultivator): the land and the whole house (a house bought to make room for a bigger one pays half of it, it
    /// is pulled down); the house keeps its level, the mortals move out. The windows of the plots tell which plot it is.
    /// </summary>
    public partial class EstateEvent
    {
        /// <summary>Price of a mortal house bought back: its land and its whole house.</summary>
        public static long MortalPrice(MapBuildTown town, Plot p) => Payable(LandPrice(town, p) + BuildCost(town, Math.Max(1, p.Level)));

        /// <summary>A house of the mortals that can be bought back (not a shop of the market).</summary>
        public static bool IsMortalHome(Plot p) => p != null && p.OwnerId == null && p.MainIndex == null && p.Level > 0 && DistrictOf(p.District).Code != 'M';

        /// <summary>Buys back a mortal house: the unit's house in this town, of the level of the mortal house.</summary>
        public static BuildResult BuyMortal(MapBuildTown town, Plot p, WorldUnitBase wunit)
        {
            if (!IsMortalHome(p))
                return BuildResult.NotFree;
            if (HasHouse(town, wunit))
                return BuildResult.HasHouse;
            var level = Math.Min(MAX_LEVEL, p.Level);
            if (wunit.GetGradeLvl() < RequiredGrade(level))
                return BuildResult.GradeTooLow;
            var cost = PayHouse(wunit, MortalPrice(town, p));
            if (cost <= 0)
                return BuildResult.NoMoney;
            MapBuildPropertyEvent.AddBuildProperty(town, cost);
            p.OwnerId = wunit.GetUnitId();
            p.Level = level;
            p.Unpaid = 0;
            p.MainIndex = null;
            p.Rooms = new Dictionary<int, int>();
            p.Herbs = new List<int>();
            Touch();
            if (wunit.IsPlayer())
                TaoistLogEvent.Log("log_estate_buy_mortal", town.name, HouseName(level), cost);
            return BuildResult.Ok;
        }

        /// <summary>Which plot: its number and place on the town map.</summary>
        public static string PlotWhere(Plot p) => string.Format(GameTool.LS("estate_plot_where"), p.Index + 1, p.X + 1, p.Y + 1);

        /// <summary>The facts of a plot: who lives there, the district and its prosperity, the land.</summary>
        public static List<InfoSheet.Row> PlotRows(MapBuildTown town, Plot p)
        {
            var rows = new List<InfoSheet.Row>();
            string who;
            if (p.OwnerId != null) who = string.Format(GameTool.LS("estate_val_owner"), g.world.unit.GetUnit(p.OwnerId)?.GetName() ?? "-");
            else if (p.Level > 0) who = GameTool.LS(DistrictOf(p.District).Code == 'M' ? "estate_val_shop" : "estate_val_mortals");
            else who = GameTool.LS("estate_val_free");
            rows.Add(new InfoSheet.Row(GameTool.LS("estate_row_who"), who));
            rows.Add(new InfoSheet.Row(GameTool.LS("estate_row_district"), string.Format(GameTool.LS("estate_val_district"), DistrictName(p.District), p.Prosperity)));
            rows.Add(new InfoSheet.Row(GameTool.LS("estate_row_land"), LandPrice(town, p).ToString("#,##0")));
            return rows;
        }

        /// <summary>The rows of a purchase: the price, the banknotes of the player (green: enough), the house tax.</summary>
        public static void AddPriceRows(List<InfoSheet.Row> rows, MapBuildTown town, Plot p, long price, int level)
        {
            var notes = SpiritNoteEvent.NotesFor(price);
            var have = SpiritNoteEvent.Notes(Player);
            rows.Add(new InfoSheet.Row(GameTool.LS("estate_row_price"), string.Format(GameTool.LS("estate_val_price"), notes, price)));
            rows.Add(new InfoSheet.Row(GameTool.LS("estate_row_have"), string.Format(GameTool.LS("estate_val_notes"), have), have >= notes ? InfoSheet.GOOD : InfoSheet.BAD));
            rows.Add(new InfoSheet.Row(GameTool.LS("estate_row_tax"), string.Format(GameTool.LS("estate_val_tax"), HouseTax(town, p, level))));
        }

        /// <summary>
        /// The window of a plot of the mortals (InfoSheet: the house pictured, the facts in rows): what it is, and the
        /// house bought back (not a shop).
        /// </summary>
        public static void MortalWindow(MapBuildTown town, Plot p, string title)
        {
            var rows = PlotRows(town, p);
            var grown = p.Level > DistrictOf(p.District).MortalLevel ? string.Format(GameTool.LS("estate_info_grown"), HouseName(p.Level)) : null;
            if (!IsMortalHome(p))
            {
                rows.Add(new InfoSheet.Row(GameTool.LS("estate_row_house"), HouseName(p.Level)));
                InfoSheet.Show(title, PlotWhere(p), HouseSprite(p.Level), rows, grown);
                return;
            }
            var price = MortalPrice(town, p);
            var level = Math.Min(MAX_LEVEL, p.Level);
            var gradeOk = Player.GetGradeLvl() >= RequiredGrade(level);
            rows.Add(new InfoSheet.Row(GameTool.LS("estate_row_house"), string.Format(GameTool.LS("estate_val_grade"), HouseName(level), RequiredGrade(level)),
                gradeOk ? (UnityEngine.Color?)null : InfoSheet.BAD));
            AddPriceRows(rows, town, p, price, level);
            var note = string.Join("\n", new[] { grown, GameTool.LS("estate_note_grow") }.Where(x => x != null));
            if (HasHouse(town, Player))
            {
                InfoSheet.Show(title, PlotWhere(p), HouseSprite(p.Level), rows, note + "\n" + GameTool.LS("estate_one_house"));
                return;
            }
            InfoSheet.Show(title, PlotWhere(p), HouseSprite(p.Level), rows, note,
                (string.Format(GameTool.LS("estate_mortal_buy"), SpiritNoteEvent.NotesFor(price)), () =>
                {
                    var rs = BuyMortal(town, p, Player);
                    if (rs == BuildResult.Ok)
                    {
                        OpenHouse(town, p);
                        InfoDialog.Msg(title, string.Format(GameTool.LS("estate_done"), HouseName(p.Level)));
                    }
                    else if (rs == BuildResult.NoMoney) InfoDialog.Msg(title, NoNotesText(price));
                    else if (rs == BuildResult.GradeTooLow) InfoDialog.Msg(title, string.Format(GameTool.LS("estate_grade"), HouseName(level), RequiredGrade(level)));
                    else InfoDialog.Msg(title, GameTool.LS("estate_not_free"));
                }),
                (GameTool.LS("dlg_close"), null));
        }
    }
}
