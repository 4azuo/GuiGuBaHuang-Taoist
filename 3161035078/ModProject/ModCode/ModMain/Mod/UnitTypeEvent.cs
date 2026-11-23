using MOD_nE7UL2.Const;
using MOD_nE7UL2.Enum;
using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Mod;
using ModLib.Helper;
using System;
using System.Collections.Generic;

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
    }
}
