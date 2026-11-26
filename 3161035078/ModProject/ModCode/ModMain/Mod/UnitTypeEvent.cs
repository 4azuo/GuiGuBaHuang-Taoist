using MOD_nE7UL2.Const;
using MOD_nE7UL2.Enum;
using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Mod;
using ModLib.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Gives every unit a role (UnitTypeEnum) and monthly growth: role luck, artisan/trainee/merchant ranks,
    /// passive income, property growth (player from grade 3, NPCs scaled to catch up with the player).
    /// </summary>
    [Cache(ModConst.UNIT_TYPE_EVENT)]
    public class UnitTypeEvent : ModEvent
    {
        public static UnitTypeEvent Instance { get; set; }

        public IDictionary<string, UnitTypeEnum> UnitTypeDic { get; set; } = new Dictionary<string, UnitTypeEnum>();

        /// <summary>
        /// Re-applies the role luck of every known unit.
        /// </summary>
        public override void OnLoadGame()
        {
            base.OnLoadGame();
            // the role picked in S&M configs is given at once (not at the first monthly tick)
            var player = g.world?.playerUnit;
            if (player != null && !UnitTypeDic.ContainsKey(player.GetUnitId()))
                AddStartupPlayerRole(player);
            foreach (var wunit in ModMain.Store.WUnits)
            {
                var unitId = wunit.GetUnitId();
                if (UnitTypeDic.ContainsKey(unitId) && UnitTypeDic[unitId].CustomLuck != null)
                {
                    RemoveUnitTypeLuck(wunit);
                    wunit.AddLuck(UnitTypeDic[unitId].Value.Parse<int>());
                }
            }
        }

        /// <summary>
        /// Assigns a role on first sight (player role from S&amp;M configs); on the growth turn of the unit (GrowTurnHelper) updates rank lucks, adds income and grows properties.
        /// </summary>
        public override void OnMonthlyForEachWUnit(WorldUnitBase wunit)
        {
            base.OnMonthlyForEachWUnit(wunit);

            var unitId = wunit.GetUnitId();

            //add luck
            if (!UnitTypeDic.ContainsKey(unitId))
            {
                if (!AddStartupPlayerRole(wunit))
                    UnitTypeDic.Add(unitId, AddRandomUnitType(wunit));
            }

            // growth turns (GrowUpSpeed): the player every period months; NPCs split in groups, one group a month, and
            // an NPC updates its lucks, income and properties on its turn only (the income for the months it stands for)
            var ticks = GrowTurnHelper.Turns(wunit);
            if (!wunit.IsPlayer() && ticks <= 0)
                return;

            //add apprentice luck
            AddApprenticeLuck(wunit);

            //add trainee luck
            AddTraineeLuck(wunit);

            //add merchant luck
            AddMerchantLuck(wunit);

            //merchant
            var money = wunit.GetUnitMoney();
            var income = wunit.IsPlayer() ? 0 : CommonTool.Random(Convert.ToInt32(Math.Pow(3, wunit.GetGradeLvl())), Convert.ToInt32(Math.Pow(4, wunit.GetGradeLvl())));

            if (UnitTypeDic[unitId] == UnitTypeEnum.Merchant)
            {
                income += (money * UnitTypeEnum.Merchant.CustomLuck.CustomEffects[ModConst.UTYPE_LUCK_EFX_PASSIVE_INCOME].Value0.Parse<float>()).Parse<int>();
            }

            var merchantLvl = MerchantLuckEnum.Merchant.GetCurLevel(wunit);
            if (merchantLvl > 0)
            {
                income += (money * merchantLvl * MerchantLuckEnum.Merchant.IncPassiveIncomeEachLvl).Parse<int>();
            }

            // one dispatch may stand for several months (time skip); an NPC turn stands for GrowUpSpeed months
            var months = wunit.IsPlayer() ? ModLib.Mod.ModMaster.MonthsPassed : GrowTurnHelper.Months(ticks);
            wunit.AddUnitMoney((int)Math.Min(int.MaxValue, (long)income * months));

            //add property
            for (var t = 0; t < ticks; t++)
            {
                if (wunit.IsPlayer())
                {
                    if (wunit.GetGradeLvl() >= 3)
                        AddPlayerProp(wunit, wunit.GetGradeLvl() / 3.0f);
                }
                else
                {
                    AddNpcProp(wunit, 1.00f + wunit.GetGradeLvl() * 0.03f);

                    foreach (var luck in ApprenticeLuckEnum.GetAllEnums<ApprenticeLuckEnum>())
                    {
                        if (CommonTool.Random(0.00f, 100.00f).IsBetween(0.00f, 8.00f))
                        {
                            wunit.AddProperty<int>(luck.PropertyEnum.GetPropertyEnum(), 1);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// SpeedUnit NPCs gain +1 move speed.
        /// </summary>
        public override void OnYearly()
        {
            base.OnYearly();
            foreach (var wunit in ModMain.Store.WUnits)
            {
                if (!wunit.IsPlayer() && UnitTypeDic.ContainsKey(wunit.GetUnitId()) && UnitTypeDic[wunit.GetUnitId()] == UnitTypeEnum.SpeedUnit)
                {
                    wunit.AddProperty<int>(UnitPropertyEnum.MoveSpeed, Math.Max(1, ModLib.Mod.ModMaster.YearsPassed));
                }
            }
        }

        /// <summary>
        /// Removes all role lucks.
        /// </summary>
        /// <summary>
        /// Player with a StartupPlayerRole in the S&amp;M configs: records the role and adds its luck. False when it does not apply.
        /// </summary>
        private bool AddStartupPlayerRole(WorldUnitBase wunit)
        {
            var roleIdx = SMLocalConfigsEvent.Instance.Configs.StartupPlayerRole;
            var roles = SMGlobalConfigsEvent.Instance.StartupPlayerRoles;
            if (!wunit.IsPlayer() || roleIdx <= 0 || roleIdx >= roles.Length)
                return false;
            var role = roles[roleIdx];
            UnitTypeDic[wunit.GetUnitId()] = role;
            if (role.CustomLuck != null)
            {
                RemoveUnitTypeLuck(wunit);
                wunit.AddLuck(role.Value.Parse<int>());
            }
            return true;
        }

        private void RemoveUnitTypeLuck(WorldUnitBase wunit)
        {
            foreach (var t in UnitTypeEnum.GetAllEnums<UnitTypeEnum>())
            {
                if (t.CustomLuck != null)
                    wunit.DelLuck(t.Value.Parse<int>());
            }
        }

        /// <summary>
        /// Random role from the chances of UnitTypeConfig (r: 0-100; outside them: Default).
        /// </summary>
        public static UnitTypeEnum RandomUnitType(float r)
        {
            var min = 0.00f;
            var max = min;
            foreach (var ut in UnitTypeConfig.Conf.UnitTypes)
            {
                min = max;
                max = min + ut.Value;
                if (ValueHelper.IsBetween(r, min, max))
                {
                    return UnitTypeEnum.GetEnumByName<UnitTypeEnum>(ut.Key);
                }
            }
            return UnitTypeEnum.Default;
        }

        /// <summary>
        /// Random role (+ its luck).
        /// </summary>
        public static UnitTypeEnum AddRandomUnitType(WorldUnitBase wunit)
        {
            var t = RandomUnitType(CommonTool.Random(0.00f, 100.00f));
            if (t.CustomLuck != null)
                wunit.AddLuck(t.Value.Parse<int>());
            return t;
        }

        /// <summary>
        /// Updates the merchant rank luck.
        /// </summary>
        public static void AddMerchantLuck(WorldUnitBase wunit)
        {
            foreach (var luck in MerchantLuckEnum.GetAllEnums<MerchantLuckEnum>())
            {
                var level = luck.GetNxtLevel(wunit);

                var luckId = luck.GetMerchantLuckId(level);
                if (luckId > 0 && wunit.GetLuck(luckId) == null)
                {
                    for (int i = 1; i <= MerchantLuckEnum.MerchantLevels.Length; i++)
                    {
                        wunit.DelLuck(luck.GetMerchantLuckId(i));
                    }
                    wunit.AddLuck(luckId);
                }
            }
        }

        /// <summary>
        /// Updates the artisan rank lucks.
        /// </summary>
        public static void AddApprenticeLuck(WorldUnitBase wunit)
        {
            foreach (var luck in ApprenticeLuckEnum.GetAllEnums<ApprenticeLuckEnum>())
            {
                var level = luck.GetApprenticeLevel(wunit);

                var luckId = luck.GetApprenticeLuckId(level);
                if (luckId > 0 && wunit.GetLuck(luckId) == null)
                {
                    for (int i = 1; i <= ApprenticeLuckEnum.ApprenticeLevels.Length; i++)
                    {
                        wunit.DelLuck(luck.GetApprenticeLuckId(i));
                    }
                    wunit.AddLuck(luckId);
                }
            }
        }

        /// <summary>
        /// Updates the trainee rank lucks.
        /// </summary>
        public static void AddTraineeLuck(WorldUnitBase wunit)
        {
            foreach (var luck in TraineeLuckEnum.GetAllEnums<TraineeLuckEnum>())
            {
                var level = luck.GetTraineeLevel(wunit);

                var luckId = luck.GetTraineeLuckId(level);
                if (luckId > 0 && wunit.GetLuck(luckId) == null)
                {
                    for (int i = 1; i <= TraineeLuckEnum.TraineeLevels.Length; i++)
                    {
                        wunit.DelLuck(luck.GetTraineeLuckId(i));
                    }
                    wunit.AddLuck(luckId);
                }
            }
        }

        /// <summary>
        /// Grows player properties by the Player role ratios.
        /// </summary>
        public void AddPlayerProp(WorldUnitBase wunit, float ratio = 1.00f)
        {
            foreach (var p in UnitTypeEnum.Player.PropIncRatio)
            {
                var pType = p.Value0 as UnitPropertyEnum;
                wunit.AddProperty(pType, (UnitTypeEnum.Player.CalProp(pType, wunit.GetProperty<int>(pType)) * ratio).Parse<int>());
            }
        }

        /// <summary>
        /// Grows NPC properties by their role, faster when far behind the player.
        /// </summary>
        public void AddNpcProp(WorldUnitBase wunit, float ratio = 1.00f)
        {
            var utype = UnitTypeDic[wunit.GetUnitId()];
            foreach (var p in utype.PropIncRatio)
            {
                var prop = p.Value0 as UnitPropertyEnum;
                var r2Player = Math.Max(1.0f, g.world.playerUnit.GetProperty<int>(prop).Parse<float>() / wunit.GetProperty<int>(prop).Parse<float>());
                wunit.SetProperty(prop, utype.CalType(wunit, prop, SMLocalConfigsEvent.Instance.Calculate(ratio * r2Player, SMLocalConfigsEvent.Instance.Configs.AddNpcGrowRate).Parse<float>()));
            }
        }

        /// <summary>
        /// Role of a unit (null when not assigned yet).
        /// </summary>
        public static UnitTypeEnum GetUnitTypeEnum(WorldUnitBase wunit)
        {
            var unitId = wunit.GetUnitId();
            return Instance.UnitTypeDic.ContainsKey(unitId) ? Instance.UnitTypeDic[unitId] : null;
        }

        // ------------------------------------------------------------------ artisan ranks: the "?" of the player info

        /// <summary>The arts of the ranks in the order of the window, with their text keys (ui_appr_&lt;art&gt;).</summary>
        public static readonly (ApprenticeLuckEnum luck, string key)[] APPRENTICE_ARTS =
        {
            (ApprenticeLuckEnum.Alchemy, "ui_appr_alchemy"), (ApprenticeLuckEnum.Forge, "ui_appr_forge"),
            (ApprenticeLuckEnum.FengShui, "ui_appr_fengshui"), (ApprenticeLuckEnum.Talismans, "ui_appr_talismans"),
            (ApprenticeLuckEnum.Herbology, "ui_appr_herbology"), (ApprenticeLuckEnum.Mining, "ui_appr_mining"),
        };

        /// <summary>The ranks of an art: the value of the unit and its rank, then a line a rank (name, the value it needs,
        /// its effect), the rank of the unit marked.</summary>
        public static string ApprenticeSummary(WorldUnitBase w, int art)
        {
            var (luck, key) = APPRENTICE_ARTS[Math.Max(0, Math.Min(APPRENTICE_ARTS.Length - 1, art))];
            var value = w?.GetDynProperty(luck.PropertyEnum).value ?? 0;
            var lvl = w == null ? 0 : luck.GetApprenticeLevel(w);
            var lines = new List<string>
            {
                string.Format(GameTool.LS("ui_appr_header"), GameTool.LS(key), value,
                    lvl <= 0 ? GameTool.LS("ui_appr_none") : string.Format(GameTool.LS("ui_appr_rank"), lvl, GameTool.LS("apprentice" + luck.GetApprenticeLuckId(lvl)))),
                string.Empty,
            };
            for (var i = 1; i <= ApprenticeLuckEnum.ApprenticeLevels.Length; i++)
            {
                var id = luck.GetApprenticeLuckId(i);
                lines.Add((i == lvl ? "[icon:daodian] " : i < lvl ? "+ " : "- ") + string.Format(GameTool.LS("ui_appr_line"), i, GameTool.LS("apprentice" + id),
                    GameTool.LS(key), ApprenticeLuckEnum.ApprenticeLevels[i - 1], GameTool.LS("apprentice" + id + "desc")));
            }
            lines.Add(string.Empty);
            lines.Add(GameTool.LS("ui_appr_note"));
            return TextIconHelper.ToSprites(string.Join("\n", lines));
        }

        /// <summary>The ranks of the martial (physical bases) or magic (spiritual roots) trainee: the sum of the unit and its rank,
        /// a line a rank (name, the sum it needs, its effect), the rank of the unit marked.</summary>
        public static string TraineeSummary(WorldUnitBase w, bool martial)
        {
            var luck = martial ? TraineeLuckEnum.Martial : TraineeLuckEnum.Magic;
            var key = martial ? "ui_trainee_martial" : "ui_trainee_magic";
            var value = w == null ? 0 : luck.CalFunc(w);
            var lvl = w == null ? 0 : luck.GetTraineeLevel(w);
            var lines = new List<string>
            {
                string.Format(GameTool.LS("ui_appr_header"), GameTool.LS(key), value,
                    lvl <= 0 ? GameTool.LS("ui_appr_none") : string.Format(GameTool.LS("ui_appr_rank"), lvl, GameTool.LS("trainee" + luck.GetTraineeLuckId(lvl)))),
                string.Empty,
            };
            for (var i = 1; i <= TraineeLuckEnum.TraineeLevels.Length; i++)
            {
                var id = luck.GetTraineeLuckId(i);
                lines.Add((i == lvl ? "[icon:daodian] " : i < lvl ? "+ " : "- ") + string.Format(GameTool.LS("ui_appr_line"), i, GameTool.LS("trainee" + id),
                    GameTool.LS(key), TraineeLuckEnum.TraineeLevels[i - 1].ToString("#,##0"), GameTool.LS("trainee" + id + "desc")));
            }
            lines.Add(string.Empty);
            lines.Add(GameTool.LS("ui_appr_note"));
            return TextIconHelper.ToSprites(string.Join("\n", lines));
        }

        /// <summary>Game text icons of the arts (APPRENTICE_ARTS order) and of the trainee / merchant ranks.</summary>
        public static readonly string[] APPRENTICE_ICONS = { "liandan", "lianqi", "fengshui", "huafu", "yaocai", "zm_kuangshi" };
        public const string MARTIAL_ICON = "quanfa", MAGIC_ICON = "lingli", MERCHANT_ICON = "zm_lingshi";

        /// <summary>Share of the way from the threshold of the rank now to the next one.</summary>
        public static double Progress(double value, int lvl, IList<long> thresholds)
        {
            if (lvl >= thresholds.Count) return 1d;
            var from = lvl <= 0 ? 0d : thresholds[lvl - 1];
            var to = (double)thresholds[lvl];
            return to <= from ? 1d : Math.Max(0d, Math.Min(1d, (value - from) / (to - from)));
        }

        /// <summary>The ranks of an art (a ranks window of InfoPanel).</summary>
        public static Helper.InfoPanel.Ranks ApprenticeRanks(WorldUnitBase w, int art)
        {
            art = Math.Max(0, Math.Min(APPRENTICE_ARTS.Length - 1, art));
            var (luck, key) = APPRENTICE_ARTS[art];
            var value = w?.GetDynProperty(luck.PropertyEnum).value ?? 0;
            var lvl = w == null ? 0 : luck.GetApprenticeLevel(w);
            var r = new Helper.InfoPanel.Ranks
            {
                Title = GameTool.LS("ui_appr_title") + " - " + GameTool.LS(key), Icon = APPRENTICE_ICONS[art], ValueName = GameTool.LS(key),
                Value = value.ToString("#,##0"), Current = lvl, Note = GameTool.LS("ui_appr_note"),
                Progress = Progress(value, lvl, ApprenticeLuckEnum.ApprenticeLevels.Select(x => (long)x).ToList()),
            };
            for (var i = 1; i <= ApprenticeLuckEnum.ApprenticeLevels.Length; i++)
            {
                var id = luck.GetApprenticeLuckId(i);
                r.List.Add(new Helper.InfoPanel.Rank(GameTool.LS("apprentice" + id), ApprenticeLuckEnum.ApprenticeLevels[i - 1].ToString("#,##0") + "+", GameTool.LS("apprentice" + id + "desc")));
            }
            return r;
        }

        /// <summary>The ranks of the martial (physical bases) or magic (spiritual roots) trainee.</summary>
        public static Helper.InfoPanel.Ranks TraineeRanks(WorldUnitBase w, bool martial)
        {
            var luck = martial ? TraineeLuckEnum.Martial : TraineeLuckEnum.Magic;
            var value = w == null ? 0 : luck.CalFunc(w);
            var lvl = w == null ? 0 : luck.GetTraineeLevel(w);
            var r = new Helper.InfoPanel.Ranks
            {
                Title = GameTool.LS(martial ? "ui_trainee_title_martial" : "ui_trainee_title_magic"), Icon = martial ? MARTIAL_ICON : MAGIC_ICON,
                ValueName = GameTool.LS(martial ? "ui_trainee_martial" : "ui_trainee_magic"), Value = value.ToString("#,##0"), Current = lvl,
                Note = GameTool.LS("ui_appr_note"), Progress = Progress(value, lvl, TraineeLuckEnum.TraineeLevels.Select(x => (long)x).ToList()),
            };
            for (var i = 1; i <= TraineeLuckEnum.TraineeLevels.Length; i++)
            {
                var id = luck.GetTraineeLuckId(i);
                r.List.Add(new Helper.InfoPanel.Rank(GameTool.LS("trainee" + id), TraineeLuckEnum.TraineeLevels[i - 1].ToString("#,##0") + "+", GameTool.LS("trainee" + id + "desc")));
            }
            return r;
        }

        /// <summary>The merchant ranks (spirit stones owned; a rank reached is kept).</summary>
        public static Helper.InfoPanel.Ranks MerchantRanks(WorldUnitBase w)
        {
            var luck = MerchantLuckEnum.Merchant;
            var value = w == null ? 0 : luck.CalFunc(w);
            var lvl = w == null ? 0 : Math.Max(luck.GetCurLevel(w), luck.GetNxtLevel(w));
            var r = new Helper.InfoPanel.Ranks
            {
                Title = GameTool.LS("ui_merchant_title"), Icon = MERCHANT_ICON, ValueName = GameTool.LS("ui_merchant_money"),
                Value = value.ToString("#,##0"), Current = lvl, Note = GameTool.LS("ui_merchant_note"),
                Progress = Progress(value, lvl, MerchantLuckEnum.MerchantLevels),
            };
            for (var i = 1; i <= MerchantLuckEnum.MerchantLevels.Length; i++)
            {
                var id = luck.GetMerchantLuckId(i);
                r.List.Add(new Helper.InfoPanel.Rank(GameTool.LS("merchant" + id), MerchantLuckEnum.MerchantLevels[i - 1].ToString("#,##0") + "+", GameTool.LS("merchant" + id + "desc")));
            }
            return r;
        }

        /// <summary>The window of the martial / magic trainee ranks (the "?" of the Martial Arts / Spiritual Root columns).</summary>
        public static void OpenTraineeHelp(bool martial) => Helper.InfoPanel.OpenRanks(TraineeRanks(g.world.playerUnit, martial));

        /// <summary>The window of the merchant ranks (the "?" beside the bag).</summary>
        public static void OpenMerchantHelp() => Helper.InfoPanel.OpenRanks(MerchantRanks(g.world.playerUnit));

        /// <summary>The 10 ranks of an art ("?" of its card in the overview; Back: the overview).</summary>
        public static void OpenApprenticeDetail(int art)
        {
            var r = ApprenticeRanks(g.world.playerUnit, art);
            r.Back = () => ModDelayEvent.Instance.DelayEvent(Instance, () => OpenApprenticeHelp(), 300);
            Helper.InfoPanel.OpenRanks(r);
        }

        /// <summary>The overview of the 6 arts (the "?" of the Artisanship column): a card an art (value, rank, bar to the
        /// next rank, effect now) with a "?" for its 10 ranks.</summary>
        public static void OpenApprenticeHelp()
        {
            var w = g.world.playerUnit;
            var all = Enumerable.Range(0, APPRENTICE_ARTS.Length).Select(i => ApprenticeRanks(w, i)).ToList();
            Helper.InfoPanel.Open(GameTool.LS("ui_appr_title"), ui =>
            {
                var dy = Helper.InfoPanel.Dy(ui);
                var left = Helper.InfoPanel.Left(ui) + 20f;
                var width = Helper.InfoPanel.Right(ui) - Helper.InfoPanel.Left(ui) - 40f;
                var top = Helper.InfoPanel.Top(ui);
                var y = Helper.InfoPanel.Section(ui, left, top, width, "jineng", GameTool.LS("ui_appr_overview"));
                const int cols = 3;
                var gap = 18f;
                var ch = dy * 6.6f;
                var cards = ModLib.Helper.UIPanelHelper.Grid(all.Count, cols, left, y - dy * 0.4f, width, ch, gap, out var cw);
                for (var i = 0; i < all.Count; i++)
                {
                    var r = all[i];
                    var art = i;
                    var cx = cards[i].x;
                    var cy = cards[i].y;
                    var on = r.Current > 0;
                    Helper.InfoPanel.Box(ui, cx, cy, cw, ch, on ? Helper.InfoPanel.CARD_ON : Helper.InfoPanel.CARD);
                    Helper.InfoPanel.Text(ui, cx + 12f, cy - dy * 0.75f, Helper.InfoPanel.I(r.Icon) + r.ValueName, Helper.InfoPanel.TITLE, 17, FontStyle.Bold);
                    Helper.InfoPanel.Help(ui, cx + cw - 22f, cy - dy * 0.75f, () => OpenApprenticeDetail(art));
                    Helper.InfoPanel.Text(ui, cx + 12f, cy - dy * 1.85f, string.Format(GameTool.LS("ui_appr_value"), r.Value), Helper.InfoPanel.TEXT, 13);
                    var now = on ? r.List[r.Current - 1] : null;
                    Helper.InfoPanel.Text(ui, cx + 12f, cy - dy * 2.8f, now == null ? GameTool.LS("ui_appr_none") : string.Format(GameTool.LS("ui_appr_rank"), r.Current, now.Name),
                        now == null ? Helper.InfoPanel.GREY : Helper.InfoPanel.GOLD, 14, FontStyle.Bold);
                    Helper.InfoPanel.Text(ui, cx + 12f, cy - dy * 3.75f, now == null ? "-" : now.Effect, now == null ? Helper.InfoPanel.GREY : Helper.InfoPanel.GOOD, 13);
                    var next = r.Current < r.List.Count ? r.List[r.Current] : null;
                    Helper.InfoPanel.Bar(ui, cx + 12f, cy - dy * 4.5f, cw - 24f, dy * 0.85f, next == null ? 1d : r.Progress,
                        next == null ? GameTool.LS("ui_panel_top") : string.Format(GameTool.LS("ui_appr_to_next"), next.Needs));
                    Helper.InfoPanel.Text(ui, cx + 12f, cy - dy * 5.95f, next == null ? string.Empty : next.Name + ": " + next.Effect, Helper.InfoPanel.GREY, 12, FontStyle.Italic);
                }
                var by = ModLib.Helper.UIPanelHelper.GridBottom(all.Count, cols, y - dy * 0.4f, ch, gap);
                Helper.InfoPanel.Center(ui, left + width / 2f, by - dy * 0.2f, GameTool.LS("ui_appr_note"), Helper.InfoPanel.NOTE, 12, FontStyle.Italic);
                Helper.InfoPanel.Center(ui, left + width / 2f, by - dy * 1.1f, GameTool.LS("ui_appr_overview_hint"), Helper.InfoPanel.NOTE, 12, FontStyle.Italic);
                Helper.InfoPanel.Buttons(ui, by - dy * 2.6f, (GameTool.LS("dlg_ok"), null));
            }, () => string.Join("\n", all.Select(r => $"{r.ValueName}: {r.Value}, " + (r.Current > 0 ? r.List[r.Current - 1].Name : "-"))));
        }
    }
}
