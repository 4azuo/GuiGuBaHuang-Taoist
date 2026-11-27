using MOD_nE7UL2.Const;
using MOD_nE7UL2.Enum;
using MOD_nE7UL2.Object;
using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Mod;
using Newtonsoft.Json;
using System;
using System.Linq;
using ModLib.Helper;

namespace MOD_nE7UL2
{
    /// <summary>
    /// Taoist entry class (ModChild, CacheId nE7UL2). Event order comes from _EventOrderIndex.json.
    /// </summary>
    [ModOrder("_EventOrderIndex.json")]
    [Cache("nE7UL2", CacheType = CacheAttribute.CType.Global, OrderIndex = 0)]
    public class ModMain : ModChild
    {
        public ModStts ModSettings { get; private set; }
        public static ModMain ModObj { get; private set; }

        /// <summary>World lists (units, towns, schools): the store of the mod (nearby units: option AllFunctionsApplyToNearestUnits, PrepareEvent), else the one of ModLib (the usual case). Use it everywhere in Taoist.</summary>
        public static ModLib.Object.ParameterStore Store => ModObj?.ParameterStore ?? ModLib.Mod.ModMaster.ModObj?.ParameterStore;

        /// <summary>
        /// Loads mod_configs.json into ModSettings and rebalances the base game configs once:
        /// level exp, attribute limits, artifact stats, skill mastery exp, skill MP cost/CD/requirements,
        /// item prices by item type (PriceHelper), pill effects, furnace durability, recipe/refine prices, skill prices and pet (potmon) stats.
        /// </summary>
        public override void OnLoadClass(bool isNew, string modId, CacheAttribute attr)
        {
            base.OnLoadClass(isNew, modId, attr);
            ModObj = this;
            //missing file: ModLib shows "files of a mod are missing"; broken JSON: "config file damaged" (KnownErrorHelper)
            var settingsJson = ConfHelper.ReadConfData(modId, "mod_configs.json");
            try
            {
                ModSettings = JsonConvert.DeserializeObject<ModStts>(settingsJson);
            }
            catch (JsonException ex)
            {
                throw KnownErrorHelper.InConfFile(ConfHelper.GetConfFilePath(modId, "mod_configs.json"), ex);
            }
            //settings of the features in their own files (defaults in the code)
            MOD_nE7UL2.Mod.BattleConfig.Load(modId);
            MOD_nE7UL2.Mod.MarketItemConfig.Load(modId);
            MOD_nE7UL2.Mod.MapBuildBattleConfig.Load(modId);
            MOD_nE7UL2.Mod.InflationaryConfig.Load(modId);
            MOD_nE7UL2.Mod.BankAccountConfig.Load(modId);
            MOD_nE7UL2.Mod.CustomRefineConfig.Load(modId);
            MOD_nE7UL2.Mod.ContributionExchangeConfig.Load(modId);
            MOD_nE7UL2.Mod.MissionDeclareConfig.Load(modId);
            MOD_nE7UL2.Mod.BattleManashieldConfig.Load(modId);
            MOD_nE7UL2.Mod.TitleOtherModsConfig.Load(modId);
            MOD_nE7UL2.Mod.UnitTypeConfig.Load(modId);
            MOD_nE7UL2.Mod.RealTrialConfig.Load(modId);
            MOD_nE7UL2.Mod.RealStorageConfig.Load(modId);
            MOD_nE7UL2.Mod.RealMarketConfig.Load(modId);
            MOD_nE7UL2.Mod.MonstStrongerConfig.Load(modId);
            MOD_nE7UL2.Mod.NpcUpgradeSkillConfig.Load(modId);
            MOD_nE7UL2.Mod.ExpertConfig.Load(modId);
            MOD_nE7UL2.Mod.BattleRewardConfig.Load(modId);
            DebugHelper.WriteLine($"Taoist-Version: {ModConst.TAOIST_VERSION}");
            //version of the error reports of Taoist (database: ModConf/crash_reports.json)
            CrashReportHelper.SetModVersion(modId, ModConst.TAOIST_VERSION);
            //exp, skill mastery exp, skill MP cost / cooldown / requirements: the values of the game kept, worked out again for
            //each save (S&M rates, ConfBalance.Apply in SMLocalConfigsEvent.OnLoadGame)
            MOD_nE7UL2.Mod.ConfBalance.Init();
            //unlimit attribute
            foreach (var item in g.conf.roleAttributeLimit._allConfList)
            {
                //DebugHelper.WriteLine($"{item.key}: {item.value}");
                if (item.key.EndsWith("Max"))
                    item.value = ModConst.MAX_ATTRIBUTE;
            }
            //balance artifact attribute
            foreach (var item in g.conf.artifactShape._allConfList)
            {
                item.durable = (item.durable * ModSettings.BaseConfigs.ArtifactDurableRatio).Parse<int>();
                item.spCost = (item.spCost * ModSettings.BaseConfigs.ArtifactSpCostRatio).Parse<int>();
                item.hp = (item.hp * ModSettings.BaseConfigs.ArtifactHpRatio).Parse<int>();
                item.atk = (item.atk * ModSettings.BaseConfigs.ArtifactAtkRatio).Parse<int>();
                item.def = (item.def * ModSettings.BaseConfigs.ArtifactDefRatio).Parse<int>();
            }
            //balance item
            foreach (var props in g.conf.itemProps._allConfList)
            {
                if (props.sale < 0)
                    continue;

                var grade = 1;
                var level = Math.Max(1, Math.Min(6, props.level));
                var ratio = 1.00f;

                ConfItemPillItem pill = null;
                ConfRoleEffectItem roleEfx = null;
                ConfBattleEffectItem battleEfx = null;
                ConfItemRarityMaterialsItem rarity = null;
                ConfRingBaseItem ring = null;
                ConfItemHorseItem mount = null;
                ConfTownMarketItemItem buyable = null;
                ConfRoleGradeItem[] gradeInfo = null;
                ConfMakePillFurnaceItem furnace = null;
                ConfClothItemItem cloth = null;
                ConfItemHobbyItem hobby = null;
                ConfArtifactShapeItem artifact = null;
                ConfSchoolStockItem school = null;
                ConfWorldBlockHerbItem herb = null;
                ConfWorldBlockMineItem[] mines = null;
                ConfGameItemRewardItem[] battles = null;
                ConfRuneFormulaItem[] talismanFormulas = null;
                ConfMakePillFormulaItem pillFormula = null;

                //if ((rarity = props.IsRareItem()) != null)
                //{
                //    if (rarity.hunger == 0)
                //    {
                //        rarity.hunger = rarity.grade;
                //        ratio = 1.10f;
                //    }
                //    else
                //    {
                //        rarity.hunger *= rarity.grade;
                //        ratio = 1.10f + rarity.hunger * 0.0001f;
                //    }
                //}

                if ((gradeInfo = props.IsBreakthroughItemA()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.BreakthroughItemA], level).Parse<float>();
                    grade = Math.Max(1, gradeInfo.Select(x => x.grade).Max());
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                    props.dieDrop = 1;
                    if (level >= 5) props.auction = 1;
                }
                else if ((gradeInfo = props.IsBreakthroughItemB()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.BreakthroughItemB], level).Parse<float>();
                    grade = Math.Max(1, gradeInfo.Select(x => x.grade).Max());
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                    props.dieDrop = 1;
                    if (level >= 5) props.auction = 1;
                }
                else if ((artifact = props.IsArtifact()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.Artifact], level).Parse<float>();
                    grade = artifact.initGrade;
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                    props.dieDrop = 1;
                    if (level >= 5) props.auction = 1;
                }
                else if ((pill = props.IsPotion()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.Potion], level).Parse<float>();
                    grade = Math.Max(1, pill.grade);
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                    pill.spCost = (pill.spCost.Parse<float>() * PotionSpCostRatio(grade)).Parse<int>();
                    pill.applyCD = (pill.applyCD.Parse<float>() * 0.24f).Parse<int>();

                    foreach (var efxId in pill.effectValue.Split('|'))
                    {
                        roleEfx = g.conf.roleEffect.GetItem(efxId.Parse<int>());
                        if (roleEfx != null && (roleEfx.value.StartsWith($"{UnitPropertyEnum.Hp.PropName}_1_") || roleEfx.value.StartsWith($"{UnitPropertyEnum.Mp.PropName}_1_")))
                        {
                            roleEfx.SetEfxValue(2, (roleEfx.GetEfxValue<int>(2) * PotionHealRatio(grade, level, ModSettings.BaseConfigs)).Parse<int>());
                        }
                    }
                }
                else if ((pill = props.IsPowerupPill()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.PowerUpItem], level).Parse<float>();
                    grade = Math.Max(1, pill.grade);
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                    pill.spCost = (pill.spCost.Parse<float>() * (4.00f + (grade * 1.50f + props.level * 0.50f))).Parse<int>();

                    foreach (var efxId in pill.effectValue.Split('|'))
                    {
                        battleEfx = g.conf.battleEffect.GetItem(efxId.Parse<int>());
                        if (battleEfx != null &&
                            (battleEfx.value1 == "3" || battleEfx.value1 == "4") &&
                            (battleEfx.value3.EndsWith("_tsgj") || battleEfx.value3.EndsWith("_tsfy")))
                        {
                            var efxValue = g.conf.battleSkillValue.GetItem(battleEfx.value3);
                            MOD_nE7UL2.Mod.ConfBalance.AddPowerUpValue(efxValue);
                        }
                    }
                }
                else if ((pill = props.IsBottleneckPill()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.BottleneckItem], level).Parse<float>();
                    grade = Math.Max(1, pill.grade);
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                    props.dieDrop = 1;
                }
                else if ((ring = props.IsRing()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.Ring] + ring.capacity * 0.001f, level).Parse<float>();
                    grade = Math.Max(1, ring.grade);
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                    if (level >= 5) props.auction = 1;
                }
                else if ((mount = props.IsMount()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.Mount], level).Parse<float>();
                    grade = Math.Max(1, mount.grade);
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                    if (level >= 5) props.auction = 1;
                }
                else if ((furnace = props.IsFurnace()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.Furnace], level).Parse<float>();
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                    props.dieDrop = 1;
                    if (level >= 5) props.auction = 1;

                    //furnace dur x5
                    var dur = level * 50;
                    foreach (var efxId in furnace.effectValue.Split('|'))
                    {
                        roleEfx = g.conf.roleEffect.GetItem(efxId.Parse<int>());
                        if (roleEfx != null && roleEfx.value.StartsWith($"randomValue_furnaceDurable_"))
                        {
                            roleEfx.SetEfxValue(2, dur);
                            roleEfx.SetEfxValue(3, dur);
                        }
                        else if (roleEfx != null && roleEfx.value.StartsWith($"fixValue_0_"))
                        {
                            roleEfx.SetEfxValue(2, dur);
                        }
                    }
                }
                else if ((herb = props.IsHerbItem()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.Herb], level).Parse<float>();
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                }
                else if ((mines = props.IsMineItem()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.Mine], level).Parse<float>();
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                }
                //else if ((battles = props.IsBattleItem()) != null)
                //{
                //    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.BattleItem], level).Parse<float>();
                //    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                //    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                //}
                else if ((buyable = props.IsMarketBuyableItem()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.MarketItem], level).Parse<float>();
                    grade = Math.Max(1, Math.Min(10, buyable.level.Split('|').Min().Parse<int>()));
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                }
                else if ((school = props.IsSchoolBuyableItem()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.SchoolItem], level).Parse<float>();
                    grade = Math.Max(1, Math.Min(10, school.area));
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                }
                else if ((cloth = props.IsOutfit()) != null)
                {
                    //do nothing
                }
                else if ((hobby = props.IsHobby()) != null)
                {
                    //do nothing
                }
                else
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.Others], level).Parse<float>();
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                }

                ////debug
                //var debugId = 6061251;
                //if (props.id == debugId)
                //{
                //    if (pill != null) DebugHelper.WriteLine($"{debugId}: pill");
                //    if (roleEfx != null) DebugHelper.WriteLine($"{debugId}: roleEfx");
                //    if (battleEfx != null) DebugHelper.WriteLine($"{debugId}: battleEfx");
                //    if (rarity != null) DebugHelper.WriteLine($"{debugId}: rarity");
                //    if (ring != null) DebugHelper.WriteLine($"{debugId}: ring");
                //    if (mount != null) DebugHelper.WriteLine($"{debugId}: mount");
                //    if (buyable != null) DebugHelper.WriteLine($"{debugId}: buyable");
                //    if (gradeInfo != null) DebugHelper.WriteLine($"{debugId}: gradeInfo");
                //    if (furnace != null) DebugHelper.WriteLine($"{debugId}: furnace");
                //    if (cloth != null) DebugHelper.WriteLine($"{debugId}: cloth");
                //    if (hobby != null) DebugHelper.WriteLine($"{debugId}: hobby");
                //    if (artifact != null) DebugHelper.WriteLine($"{debugId}: artifact");
                //    if (school != null) DebugHelper.WriteLine($"{debugId}: school");
                //    if (herb != null) DebugHelper.WriteLine($"{debugId}: herb");
                //    if (mines != null) DebugHelper.WriteLine($"{debugId}: mines");
                //    if (battles != null) DebugHelper.WriteLine($"{debugId}: battles");
                //    if (talismanFormulas != null) DebugHelper.WriteLine($"{debugId}: talismanFormulas");
                //    if (pillFormula != null) DebugHelper.WriteLine($"{debugId}: pillFormula");
                //    DebugHelper.WriteLine($"{debugId}: ratio:{ratio}, sale:{props.sale}, worth:{props.worth}");
                //}
            }
            foreach (var props in g.conf.itemProps._allConfList)
            {
                if (props.sale < 0)
                    continue;

                var grade = 1;
                var level = Math.Max(1, Math.Min(6, props.level));
                var ratio = 1.00f;

                ConfRuneFormulaItem[] talismanFormulas;
                ConfMakePillFormulaItem pillFormula;
                ConfTownFactotySellArtifactItem refiningArtifact;

                if ((talismanFormulas = props.IsTalismanRecipe()) != null)
                {
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.TalismanRecipe], level).Parse<float>();
                    props.sale = PriceHelper.UpPrice(props.sale, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(props.worth, grade, level, ratio);
                }
                else if ((pillFormula = props.IsPillRecipe()) != null)
                {
                    var pills = new string[] { pillFormula.pillA, pillFormula.pillB, pillFormula.pillC, pillFormula.pillD, pillFormula.pillE, pillFormula.pillF };
                    var expensestPrice = pills.Select(x => g.conf.itemProps.GetItem(x.Parse<int>())).Max(x => x.worth);
                    var cheapestPrice = pills.Select(x => g.conf.itemProps.GetItem(x.Parse<int>())).Min(x => x.worth);
                    var avgPrice = (expensestPrice + cheapestPrice) / 2;
                    ratio *= Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.PillRecipe], level).Parse<float>();
                    grade = pillFormula.grade;
                    props.sale = PriceHelper.UpPrice(avgPrice, grade, level, ratio);
                    props.worth = PriceHelper.UpPrice(avgPrice, grade, level, ratio);

                    var factory = g.conf.townFactotySell.GetItem(props.id);
                    if (factory != null)
                        factory.makePrice = (props.worth * ModConst.PILL_RECIPE_RATE).Parse<int>();
                }
                else if ((refiningArtifact = props.IsTownRefiningArtifact()) != null)
                {
                    var artifactId = g.conf.artifactShapeMaterial.GetItem(refiningArtifact.id).shape;
                    //var productShape = g.conf.artifactShape.GetItem(artifactId);
                    var productProp = g.conf.itemProps.GetItem(artifactId);

                    props.sale = productProp.sale / 3;
                    props.worth = productProp.worth / 3;
                }
            }
            foreach (var refine in g.conf.townRefine._allConfList)
            {
                refine.moneyCost = g.conf.itemProps.GetItem(refine.rewardID).worth / 4;
            }
            //balance skill
            foreach (var item in g.conf.itemSkill._allConfList)
            {
                //balance price
                var grade = Math.Max(1, Math.Min(10, item.grade));
                var level = Math.Max(1, Math.Min(6, item.level));
                var ratio = Math.Pow(PriceHelper.ITEM_TYPE_RATIO[ModItemTypeEnum.Skill], level).Parse<float>();
                item.price = PriceHelper.UpPrice(item.price, grade, level, ratio);
                item.cost = PriceHelper.UpPrice(item.cost, grade, level, ratio);
                item.contribution = PriceHelper.UpPrice(item.contribution, grade, level, ratio);
                item.sale = PriceHelper.UpPrice(item.sale, grade, level, ratio);
                item.worth = PriceHelper.UpPrice(item.worth, grade, level, ratio);
            }
            //balance potmon
            foreach (var item in g.conf.potmonBattle._allConfList)
            {
                item.spCost = item.spCost * item.id;
                item.stayTime = 20;
                item.cd = 20;
            }
            //the scaled values before any save (S&M rates at 0)
            MOD_nE7UL2.Mod.ConfBalance.Apply(null);
        }

        /// <summary>HP / MP of a recovery potion: x (base + gradeRatio x grade + levelRatio x level) of the game (mod_configs.json).</summary>
        public static float PotionHealRatio(int grade, int level, ModStts._BaseConfigs s) =>
            Math.Max(1f, (s?.PotionHealBase ?? 1f) + (s?.PotionHealGradeRatio ?? 1.5f) * Math.Max(1, grade) + (s?.PotionHealLevelRatio ?? 1f) * Math.Max(1, level));

        /// <summary>Focus (SP) cost of a recovery potion: x (1 + 0.1 x grade) of the game (was x1.3 + 0.2 x grade + 0.05 x level).</summary>
        public static float PotionSpCostRatio(int grade) => 1.00f + Math.Max(1, grade) * 0.10f;
    }
}
