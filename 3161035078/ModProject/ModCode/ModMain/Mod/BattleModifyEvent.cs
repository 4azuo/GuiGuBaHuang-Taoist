using EBattleTypeData;
using EGameTypeData;
using MOD_nE7UL2.Const;
using MOD_nE7UL2.Enum;
using ModLib.Attributes;
using ModLib.Enum;
using ModLib.Mod;
using ModLib.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ModLib.Helper;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Damage pipeline of Taoist: nullify, evasion, skill/basis/MP/SP/DP damage, critical, block, mitigation, min damage,
    /// time-based damage multiplier, extra MP costs and regeneration.
    /// </summary>
    [Cache(ModConst.BATTLE_MODIFY_EVENT)]
    public class BattleModifyEvent : ModEvent
    {
        public static BattleModifyEvent Instance { get; set; }

        public const int BASIS_ON_DMG = 1000;
        public const int BASIS_ON_DEF = 400;
        public const float BASIS_ON_BLOCK_RATIO = 10000f;
        public const int BASIS_ON_BLOCK_COST = 50;
        public const float LOCAL_DMG_MULTIPLIER = 1.003f;
        public const float GLOBAL_DMG_MULTIPLIER = 1.001f;

        public static bool IsShowCustomMonstCount { get; set; } = false;
        public static Text TextCustomMonstCount1 { get; private set; }
        public static Text TextCustomMonstCount2 { get; private set; }
        public static UIItemText TextDamageMultiplier { get; private set; }

        public static MonstStrongerConfig MonstStrongerConfigs => MonstStrongerConfig.Conf;

        private static readonly IDictionary<string, int> _nullify = new Dictionary<string, int>();

        /// <summary>
        /// BattleInfo: damage multiplier text, optional battle map hiding, monster counters.
        /// </summary>
        public override void OnOpenUIEnd(OpenUIEnd e)
        {
            base.OnOpenUIEnd(e);

            if (e.uiType.uiName == UIType.MapMain.uiName)
            {
                _nullify.Clear();
                _regenLeft.Clear();
            }
            else
            if (e.uiType.uiName == UIType.BattleInfo.uiName)
            {
                DamageInfo.Reset();
                var ui = new UICover<UIBattleInfo>(UIType.BattleInfo);
                {
                    //damage calculation window (also the hotkey of Taoist Settings): under the damage multiplier, top
                    //centre (free: the minimap is at the top right, the skill bar at the bottom)
                    if (!UISettingScreenEvent.Instance.HideDamageCalcButton)
                        ui.AddButton(ui.MidCol, ui.FirstRow + 7, DamageInfo.Toggle, GameTool.LS("dmginfo_button")).Size(150, 30);

                    //damage multiplier
                    if (!UISettingScreenEvent.Instance.HideDamageMultiplier)
                    {
                        TextDamageMultiplier = ui.AddText(ui.MidCol, ui.FirstRow + 5, GameTool.LS("other500020089")).Format(Color.white, 20);
                        TextDamageMultiplier.SetWork(new UIItemWork
                        {
                            Formatter = ModLib.Helper.ActionHelper.WTracedFunc<UIItemBase, object[]>((x) => new object[] { GetDamageMultiplier().ToString("#,##0.00") })
                        });
                    }

                    //monster count
                    ui.UI.uiMap.goGroupRoot.SetActive(!SMLocalConfigsEvent.Instance.Configs.HideBattleMap);
                    if (IsShowCustomMonstCount)
                    {
                        TextCustomMonstCount1 = ui.UI.uiInfo.textMonstCount1.Replace();
                        TextCustomMonstCount2 = ui.UI.uiInfo.textMonstCount2.Replace();
                    }
                    else
                    {
                        ui.UI.uiInfo.goMonstCount1.SetActive(false);
                        ui.UI.uiInfo.goMonstCount2.SetActive(false);
                    }
                }
                ui.IsAutoUpdate = true;
            }
        }

        /// <summary>
        /// Adds defense/speed from basis and caches nullify charges.
        /// </summary>
        public override void OnBattleUnitInto(UnitCtrlBase e)
        {
            base.OnBattleUnitInto(e);

            var humanData = e.data.TryCast<UnitDataHuman>();
            if (humanData?.worldUnitData?.unit != null)
            {
                //humanData.attack.baseValue += (??? / 100.00f * humanData.attack.baseValue).Parse<int>();
                var adjustDef1 = ((((humanData.basisFist.value + humanData.basisPalm.value + humanData.basisFinger.value) / 2.0f) / 1000.00f) * humanData.defense.baseValue).ToIntClamp();
                var adjustDef2 = ((humanData.basisEarth.value / 1000.00f) * humanData.defense.baseValue).ToIntClamp();
                humanData.defense.baseValue = ValueHelper.SafeAddInt(humanData.defense.baseValue, (double)adjustDef1 + adjustDef2);
                var adjustMs = (humanData.basisWind.value / 100.00f).ToIntClamp();
                humanData.moveSpeed.baseValue = ValueHelper.SafeAddInt(humanData.moveSpeed.baseValue, adjustMs);
                _nullify[humanData.worldUnitData.unit.GetUnitId()] = Convert.ToInt32(CustomRefineEvent.GetCustomAdjValue(humanData.worldUnitData.unit, AdjTypeEnum.Nullify));
            }
        }

        /// <summary>A hit between two cultivators (autotest TB-19): the damage at the start, before the MP/SP/DP blocks,
        /// the part they took, the final one and the minimum.</summary>
        public class HitRecord
        {
            public string AtkId, HitId;
            public int Raw, PreGuard, Guard, Final, Min, HitHpMax;
            public bool Crit;
        }

        /// <summary>The hits between cultivators are added here while it is not null (autotest TB-19).</summary>
        public static List<HitRecord> RecordedHits { get; set; }

        /// <summary>Logs the damage after each step of OnBattleUnitHitDynIntHandler (debug, e.g. autotest TB-04).</summary>
        public static bool TraceDamage { get; set; } = false;

        // steps of the pipeline shown by the damage calculation window (DamageInfo): Trace label -> step
        private static readonly Dictionary<string, string> STEP_NAMES = new Dictionary<string, string>
        {
            ["1"] = "base", ["3"] = "time", ["4"] = "skillbasis", ["5"] = "mp", ["6"] = "sp", ["7"] = "dp", ["8"] = "crit",
            ["9"] = "block", ["10"] = "basisdef", ["11"] = "mpblock", ["12"] = "spblock", ["13"] = "dpblock", ["final"] = "min",
            ["nullified"] = "nullified", ["kill"] = "kill", ["evaded"] = "evaded",
        };

        private static void Trace(string step, UnitHitDynIntHandler e)
        {
            var key = step.StartsWith("final") ? "final" : step;
            if (STEP_NAMES.TryGetValue(key, out var name))
                DamageInfo.Step(name, e.dynV.baseValue);
            if (TraceDamage)
                DebugHelper.WriteLine($"BattleModifyEvent dmg {step}: {e.dynV.baseValue} (evade {e.hitData.isEvade}, crit {e.hitData.isCrit}, world unit hit {ModBattleEvent.IsWorldUnitHit}, attacking {ModBattleEvent.IsWorldUnitAttacking})");
        }

        /// <summary>
        /// Rewrites the damage of a hit step by step (see inline comments).
        /// </summary>
        public override void OnBattleUnitHitDynIntHandler(UnitHitDynIntHandler e)
        {
            base.OnBattleUnitHitDynIntHandler(e);
            DamageInfo.BeginHit();

            var attackUnitData = ModBattleEvent.AttackingUnit.data.TryCast<UnitDataHuman>();
            var hitUnitData = ModBattleEvent.HitUnit.data.TryCast<UnitDataHuman>();

            var atkGradeLvl = attackUnitData?.worldUnitData?.unit?.GetGradeLvl() ?? ModBattleEvent.AttackingUnit.data.grade.value;
            var dType = ModBattleEvent.GetDmgBasisType(e.hitData);
            var pEnum = ModBattleEvent.GetDmgPropertyEnum(dType);
            var atk = ModBattleEvent.AttackingUnit.data.attack.baseValue;

            Trace("1", e);
            var rawDmg = e.dynV.baseValue;
            //nullify
            if (ModBattleEvent.IsWorldUnitHit)
            {
                var unitId = ModBattleEvent.HitWorldUnit.GetUnitId();
                if (_nullify.ContainsKey(unitId) && _nullify[unitId] > 0)
                {
                    _nullify[unitId]--;
                    e.dynV.baseValue = 0;
                    Trace("nullified", e);
                    return;
                }
            }

            //instant kill
            if (ModBattleEvent.IsWorldUnitAttacking && !ModBattleEvent.IsWorldUnitHit) // a world unit hits a monster (BUG-01)
            {
                var rate = GetInstantKillBase(ModBattleEvent.AttackingWorldUnit) + GetInstantKillPlus(ModBattleEvent.AttackingWorldUnit);
                if (ValueHelper.IsBetween(CommonTool.Random(0.00f, 100.00f), 0.00f, rate))
                {
                    e.dynV.baseValue = int.MaxValue;
                    Trace("kill", e);
                    return;
                }
            }

            Trace("2", e);
            //evasion
            if (ModBattleEvent.IsWorldUnitHit)
            {
                var evaRate = GetEvadeBase(ModBattleEvent.HitWorldUnit);
                if (ValueHelper.IsBetween(CommonTool.Random(0.00f, 100.00f), 0.00f, Math.Min(GetEvadeMaxBase(ModBattleEvent.HitWorldUnit) + GetEvadeMaxPlus(ModBattleEvent.HitWorldUnit), evaRate) + GetEvadePlus(ModBattleEvent.HitWorldUnit)))
                {
                    e.hitData.isEvade = true;
                    e.dynV.baseValue = 0;
                    Trace("evaded", e);
                    return;
                }
            }
            else
            {
                var evaRate = GetEvadeBase(ModBattleEvent.HitUnit);
                if (ValueHelper.IsBetween(CommonTool.Random(0.00f, 100.00f), 0.00f, Math.Min(GetEvadeMaxBase(ModBattleEvent.HitUnit), evaRate)))
                {
                    e.hitData.isEvade = true;
                    e.dynV.baseValue = 0;
                    Trace("evaded", e);
                    return;
                }
            }

            //add dmg by time: (multiplier - 1) x grade / 3, nothing at the start of the battle (the multiplier starts at x1)
            e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, TimeBonus(GetDamageMultiplier(), atkGradeLvl) * e.dynV.baseValue);

            Trace("3", e);
            //add dmg (skill)
            if (ModBattleEvent.IsWorldUnitAttacking)
            {
                var castingSkill = e.hitData.GetCastingSkill();
                if (castingSkill != null)
                {
                    e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, (double)GetSkillAdjDmgBase(ModBattleEvent.AttackingWorldUnit, castingSkill) * GetSkillAdjDmgPlus(ModBattleEvent.AttackingWorldUnit));
                }
            }

            //add dmg (basis)
            if (pEnum != null)
            {
                var r = ModBattleEvent.GetUnitPropertyValue(ModBattleEvent.AttackingUnit, pEnum);
                e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, (double)atk * r / BASIS_ON_DMG);
            }

            Trace("4", e);
            //add dmg (mp)
            if (ModBattleEvent.IsWorldUnitAttacking && attackUnitData.mp > 0)
            {
                var r = (attackUnitData.mp.Parse<float>() / attackUnitData.maxMP.value.Parse<float>(float.MaxValue));
                e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, (double)atk * r);
                attackUnitData.AddMP(-(atkGradeLvl / 5));
            }

            Trace("5", e);
            //add dmg (sp)
            if (ModBattleEvent.IsWorldUnitAttacking && attackUnitData.sp > 0 && atkGradeLvl >= 4)
            {
                var r = (attackUnitData.sp.Parse<float>() / attackUnitData.maxSP.value.Parse<float>(float.MaxValue));
                e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, (double)atk * r);
            }

            Trace("6", e);
            //add dmg (dp)
            if (ModBattleEvent.IsWorldUnitAttacking && attackUnitData.dp > 0 && atkGradeLvl >= 8)
            {
                var r = (attackUnitData.dp.Parse<float>() / attackUnitData.maxDP.value.Parse<float>(float.MaxValue));
                e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, (double)atk * r);
            }

            Trace("7", e);
            //critical
            if (!e.hitData.isCrit)
            {
                if (ModBattleEvent.IsWorldUnitAttacking)
                {
                    var scritRate = GetSCritChanceBase(ModBattleEvent.AttackingWorldUnit);
                    if (ValueHelper.IsBetween(CommonTool.Random(0.00f, 100.00f), 0.00f, Math.Min(GetSCritChanceMaxBase(ModBattleEvent.AttackingWorldUnit) + GetSCritChanceMaxPlus(ModBattleEvent.AttackingWorldUnit), scritRate) + GetSCritChancePlus(ModBattleEvent.AttackingWorldUnit)))
                    {
                        e.hitData.isCrit = true;
                        e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, (double)e.dynV.baseValue * (GetSCritDamageBase(ModBattleEvent.AttackingWorldUnit) + GetSCritDamagePlus(ModBattleEvent.AttackingWorldUnit)));
                    }
                }
                //monster
                else
                {
                    var criticalRate = CommonTool.Random(0.00f, 100.00f);
                    if (ValueHelper.IsBetween(criticalRate, 0.00f, 0.40f))
                    {
                        e.hitData.isCrit = true;
                        e.dynV.baseValue = ValueHelper.SafeMulInt(e.dynV.baseValue, 4);
                    }
                    else if (ValueHelper.IsBetween(criticalRate, 0.00f, 2.00f))
                    {
                        e.hitData.isCrit = true;
                        e.dynV.baseValue = ValueHelper.SafeMulInt(e.dynV.baseValue, 3);
                    }
                    else if (ValueHelper.IsBetween(criticalRate, 0.00f, 8.00f))
                    {
                        e.hitData.isCrit = true;
                        e.dynV.baseValue = ValueHelper.SafeMulInt(e.dynV.baseValue, 2);
                    }
                }
            }

            Trace("8", e);
            var defGradeLvl = hitUnitData?.worldUnitData?.unit?.GetGradeLvl() ?? ModBattleEvent.HitUnit.data.grade.value;
            var def = ModBattleEvent.HitUnit.data.defense.baseValue;
            var minDmg = (ModBattleEvent.IsWorldUnitAttacking ? (GetMinDmgBase(ModBattleEvent.AttackingWorldUnit) + GetMinDmgPlus(ModBattleEvent.AttackingWorldUnit)) : GetMinDmgBase(ModBattleEvent.AttackingUnit)) * defGradeLvl;
            var blockratio = GetBlockRatio(hitUnitData);

            //block
            var blockRate = Math.Sqrt(ModBattleEvent.GetUnitPropertyValue(ModBattleEvent.HitUnit, pEnum) / 12);
            if (ModBattleEvent.IsWorldUnitHit)
            {
                if (ValueHelper.IsBetween(CommonTool.Random(0.00f, 100.00f), 0.00f, Math.Min(GetBlockMaxBase(ModBattleEvent.HitWorldUnit) + GetBlockMaxPlus(ModBattleEvent.HitWorldUnit), blockRate)))
                {
                    //var r = ModBattleEvent.HitUnit.data.hp.Parse<double>() / ModBattleEvent.HitUnit.data.maxHP.value.Parse<double>();
                    e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, -((double)GetBlockDmgBase(ModBattleEvent.HitWorldUnit) + GetBlockDmgPlus(ModBattleEvent.HitWorldUnit))/* * r*/);
                }
            }
            else
            {
                if (ValueHelper.IsBetween(CommonTool.Random(0.00f, 100.00f), 0.00f, Math.Min(GetBlockMaxBase(ModBattleEvent.HitUnit), blockRate)))
                {
                    //var r = ModBattleEvent.HitUnit.data.hp.Parse<double>() / ModBattleEvent.HitUnit.data.maxHP.value.Parse<double>();
                    e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, -(double)GetBlockDmgBase(ModBattleEvent.HitUnit)/* * r*/);
                }
            }

            Trace("9", e);
            //block dmg (basis)
            if (pEnum != null && e.dynV.baseValue > minDmg)
            {
                var r = ModBattleEvent.GetUnitPropertyValue(ModBattleEvent.HitUnit, pEnum);
                e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, -(double)def * r / BASIS_ON_DEF);
            }

            Trace("10", e);
            // the MP, SP and DP blocks together take at most GUARD_MAX of the damage of the hit (6.3)
            var preGuard = e.dynV.baseValue;
            var guardLeft = GuardCap(preGuard);
            //block dmg (mp)
            if (ModBattleEvent.IsWorldUnitHit && hitUnitData != null && hitUnitData.mp > hitUnitData.maxMP.value / 5 && e.dynV.baseValue > minDmg)
            {
                var blockcost = GetBlockMpCost(hitUnitData);
                var mpRate = hitUnitData.mp.Parse<float>() / hitUnitData.maxMP.value.Parse<float>(float.MaxValue);
                var blockTimes = (mpRate * defGradeLvl).Parse<int>();
                // at most MP_BLOCK_MAX of the damage of the hit (the tries of a high realm blocked everything)
                var blockLeft = Math.Min(MpBlockCap(e.dynV.baseValue), guardLeft);
                for (int i = 0; i < blockTimes && e.dynV.baseValue > minDmg && blockLeft > 0; i++)
                {
                    // the cost is the damage really blocked (not below the minimum damage) / blockcost
                    var blockedDmg = Math.Min(Math.Min(((double)def * mpRate * blockratio).ToIntClamp(), e.dynV.baseValue - minDmg), blockLeft);
                    blockLeft -= Math.Max(0, blockedDmg);
                    var mpcost = BlockMpCostOf(blockedDmg, blockcost);
                    if (blockedDmg <= 0 || hitUnitData.mp < mpcost)
                        break;
                    e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, -(double)blockedDmg);
                    guardLeft -= blockedDmg;
                    hitUnitData.AddMP(-mpcost);
                }
            }
            
            Trace("11", e);
            //block dmg (sp)
            if (ModBattleEvent.IsWorldUnitHit && hitUnitData != null && hitUnitData.sp > 0 && e.dynV.baseValue > minDmg && defGradeLvl >= 4 && guardLeft > 0)
            {
                var r = (hitUnitData.sp.Parse<float>() / hitUnitData.maxSP.value.Parse<float>(float.MaxValue)) * blockratio;
                var blocked = GuardBlock(def * r, guardLeft);
                guardLeft -= blocked;
                e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, -(double)blocked);
                hitUnitData.AddSP(-1);
            }

            Trace("12", e);
            //block dmg (dp)
            if (ModBattleEvent.IsWorldUnitHit && hitUnitData != null && hitUnitData.dp > 0 && e.dynV.baseValue > minDmg && defGradeLvl >= 8 && guardLeft > 0)
            {
                var r = (hitUnitData.dp.Parse<float>() / hitUnitData.maxDP.value.Parse<float>(float.MaxValue)) * blockratio;
                var blocked = GuardBlock(def * r, guardLeft);
                guardLeft -= blocked;
                e.dynV.baseValue = ValueHelper.SafeAddInt(e.dynV.baseValue, -(double)blocked);
            }

            Trace("13", e);
            //min-dmg
            if (e.dynV.baseValue <= minDmg)
                e.dynV.baseValue = minDmg;
            Trace($"final (min {minDmg})", e);
            if (RecordedHits != null && ModBattleEvent.IsWorldUnitAttacking && ModBattleEvent.IsWorldUnitHit)
                RecordedHits.Add(new HitRecord
                {
                    AtkId = ModBattleEvent.AttackingWorldUnit.GetUnitId(), HitId = ModBattleEvent.HitWorldUnit.GetUnitId(),
                    Raw = rawDmg, PreGuard = preGuard, Guard = GuardCap(preGuard) - guardLeft, Final = e.dynV.baseValue, Min = minDmg,
                    HitHpMax = ModBattleEvent.HitUnit.data.maxHP.value, Crit = e.hitData.isCrit,
                });
            RecordStats(e.dynV.baseValue, preGuard, GuardCap(preGuard) - guardLeft, GuardCap(preGuard), minDmg);

            //steal
            if (ModBattleEvent.IsWorldUnitAttacking)
            {
                //steal hp
                ModBattleEvent.AttackingUnit.data.AddHP(new MartialTool.HitData(ModBattleEvent.AttackingUnit, null, 0, 0,
                    ((double)e.dynV.baseValue * (GetStealHpBase(ModBattleEvent.AttackingWorldUnit) + GetStealHpPlus(ModBattleEvent.AttackingWorldUnit))).ToIntClamp()));

                //steal mp
                ModBattleEvent.AttackingUnit.data.AddMP(
                    ((double)e.dynV.baseValue * (GetStealMpBase(ModBattleEvent.AttackingWorldUnit) + GetStealMpPlus(ModBattleEvent.AttackingWorldUnit))).ToIntClamp());

                //steal sp
                ModBattleEvent.AttackingUnit.data.AddSP(
                    ((double)e.dynV.baseValue * (GetStealSpBase(ModBattleEvent.AttackingWorldUnit) + GetStealSpPlus(ModBattleEvent.AttackingWorldUnit))).ToIntClamp());
            }
        }

        /// <summary>Balance numbers (Helper/TaoistStats) of a hit where the player is the attacker or the target.</summary>
        private static void RecordStats(int final, int preGuard, int guard, int cap, int minDmg)
        {
            var byPlayer = ModBattleEvent.IsWorldUnitAttacking && ModBattleEvent.AttackingWorldUnit.IsPlayer();
            var onPlayer = ModBattleEvent.IsWorldUnitHit && ModBattleEvent.HitWorldUnit.IsPlayer();
            if (byPlayer == onPlayer) return; // neither (NPCs between them) or both
            var other = byPlayer ? ModBattleEvent.HitUnit : ModBattleEvent.AttackingUnit;
            var otherWorld = byPlayer ? (ModBattleEvent.IsWorldUnitHit ? ModBattleEvent.HitWorldUnit : null) : (ModBattleEvent.IsWorldUnitAttacking ? ModBattleEvent.AttackingWorldUnit : null);
            var monst = other?.data?.TryCast<UnitDataMonst>();
            var kind = otherWorld != null ? "cultivator" : monst != null && monst.monstType == MonstType.BOSS ? "boss" : "monster";
            var realm = otherWorld?.GetGradeLvl() ?? other?.data?.grade?.value ?? 0;
            Helper.TaoistStats.Hit(byPlayer, kind, realm, final, ModBattleEvent.HitUnit.data.maxHP.value);
            if (ModBattleEvent.IsWorldUnitHit)
                Helper.TaoistStats.Guard(ModBattleEvent.HitWorldUnit.GetGradeLvl(), preGuard, guard, cap, final <= minDmg);
        }

        /// <summary>The end of a battle of the player: its length, the result, the damage multiplier of the time (TaoistStats).</summary>
        public override void OnBattleEndHandler(BattleEndHandler e)
        {
            base.OnBattleEndHandler(e);
            var player = g.world?.playerUnit;
            if (player == null || e == null) return;
            Helper.TaoistStats.BattleEnd(player.GetGradeLvl(), ModBattleEvent.BattleStayTime, e.isWin, GetDamageMultiplier());
        }

        /// <summary>
        /// Extra MP cost of skills.
        /// </summary>
        public override void OnBattleUnitUseSkill(UnitUseSkill e)
        {
            base.OnBattleUnitUseSkill(e);
            var humanData = e.unit.data.TryCast<UnitDataHuman>();
            if (humanData?.worldUnitData?.unit != null)
            {
                humanData?.AddMP(-GetSkillAdjMpCost(humanData.worldUnitData.unit, e?.skill?.TryCast<SkillAttack>()?.skillData.data));
            }
        }

        /// <summary>
        /// Extra MP cost of steps.
        /// </summary>
        public override void OnBattleUnitUseStep(UnitUseStep e)
        {
            base.OnBattleUnitUseStep(e);
            var humanData = e.unit.data.TryCast<UnitDataHuman>();
            if (humanData?.worldUnitData?.unit != null)
            {
                humanData?.AddMP(-GetStepAdjMpCost(humanData.worldUnitData.unit, e?.step?.data?.stepData?.data));
            }
        }

        /// <summary>
        /// Monster and human HP/MP/SP/DP regeneration.
        /// </summary>
        [ErrorIgnore]
        [EventCondition(IsInGame = HandleEnum.Ignore, IsInBattle = HandleEnum.True)]
        public override void OnTimeUpdate200ms()
        {
            base.OnTimeUpdate200ms();
            // the battle scene is gone (leaving the battle, the game closing)
            if (ModBattleEvent.SceneBattle?.unit?.allUnit == null)
                return;

            foreach (var unit in ModBattleEvent.BattleUnits)
            {
                //monster recovery
                var monstData = unit?.data?.TryCast<UnitDataMonst>();
                if (monstData != null && monstData.grade.value >= 3)
                {
                    if (monstData.hp < monstData.maxHP.value)
                        monstData.hp += (Math.Sqrt(Math.Sqrt(monstData.maxHP.value / 3)) + Math.Sqrt(monstData.basisWood.value / 100)).Parse<int>();
                }

                //human recovery: the fractions are kept until they make a whole point (0.44 HP a tick = 2.2 HP a second)
                var humanData = unit?.data?.TryCast<UnitDataHuman>();
                if (humanData?.worldUnitData?.unit != null)
                {
                    var wunit = humanData.worldUnitData.unit;
                    var id = wunit.GetUnitId();
                    if (!_regenLeft.TryGetValue(id, out var left))
                        _regenLeft[id] = left = new double[4];
                    if (humanData.hp < humanData.maxHP.value)
                        humanData.hp = ValueHelper.SafeAddInt(humanData.hp, Regen(ref left[0], GetHpRecoveryBase(wunit) + GetHpRecoveryPlus(wunit)));
                    else left[0] = 0;
                    if (humanData.mp < humanData.maxMP.value)
                        humanData.mp = ValueHelper.SafeAddInt(humanData.mp, Regen(ref left[1], GetMpRecoveryBase(wunit) + GetMpRecoveryPlus(wunit)));
                    else left[1] = 0;
                    if (humanData.sp < humanData.maxSP.value)
                        humanData.sp = ValueHelper.SafeAddInt(humanData.sp, Regen(ref left[2], GetSpRecoveryBase(wunit) + GetSpRecoveryPlus(wunit)));
                    else left[2] = 0;
                    if (humanData.dp < humanData.maxDP.value)
                        humanData.dp = ValueHelper.SafeAddInt(humanData.dp, Regen(ref left[3], GetDpRecoveryBase(wunit) + GetDpRecoveryPlus(wunit)));
                    else left[3] = 0;
                }
            }
        }

        /// <summary>
        /// Damage multiplier growing with battle/room time.
        /// </summary>
        public double GetDamageMultiplier()
        {
            return Math.Pow(GLOBAL_DMG_MULTIPLIER, ModBattleEvent.BattleStayTime) * Math.Pow(LOCAL_DMG_MULTIPLIER, ModBattleEvent.RoomStayTime);
        }

        /// <summary>Share of the damage of a hit the MP block can take at most (6.3).</summary>
        public const double MP_BLOCK_MAX = 0.6;

        /// <summary>Damage the MP block of a hit can take at most.</summary>
        public static int MpBlockCap(int dmg) => Math.Max(0d, dmg * MP_BLOCK_MAX).ToIntClamp();

        /// <summary>Share of the damage of a hit the MP, SP and DP blocks together can take at most (6.3). Not lower:
        /// every block spends its MP / SP / DP and blocks less as they go down.</summary>
        public const double GUARD_MAX = 0.8;

        /// <summary>Damage the MP, SP and DP blocks of a hit can take together at most.</summary>
        public static int GuardCap(int dmg) => Math.Max(0d, dmg * GUARD_MAX).ToIntClamp();

        /// <summary>Damage a SP / DP block takes: what it would block, not over what the guard has left.</summary>
        public static int GuardBlock(double wouldBlock, int guardLeft) => Math.Max(0, Math.Min(Math.Max(0d, wouldBlock).ToIntClamp(), guardLeft));

        /// <summary>Share of the damage added by the time of the battle: (multiplier - 1) x grade / 3 (0 at the start).</summary>
        public static double TimeBonus(double multiplier, int grade) => Math.Max(0d, multiplier - 1d) * (grade / 3d);

        /// <summary>MP cost of one MP block: the damage blocked / blockcost (physical basis sum / 50), at least 1.</summary>
        public static int BlockMpCostOf(int blockedDmg, float blockcost)
        {
            if (blockedDmg <= 0) return 0;
            if (blockcost <= 0f) return int.MaxValue; // no physical basis: no MP block
            return Math.Max(1d, Math.Ceiling(blockedDmg / (double)blockcost)).ToIntClamp();
        }

        /// <summary>Recovery of a tick with the fraction kept from the ticks before (left): the whole points to add.</summary>
        public static int Regen(ref double left, double perTick)
        {
            if (double.IsNaN(perTick) || double.IsInfinity(perTick)) return 0;
            left += perTick;
            var whole = Math.Floor(left);
            left -= whole;
            return whole.ToIntClamp();
        }

        /// <summary>Fractions of the recovery kept for each unit of the battle (HP, MP, SP, DP).</summary>
        private static readonly Dictionary<string, double[]> _regenLeft = new Dictionary<string, double[]>();

        #region Modify
        public static Dictionary<int, float> BlockRatio { get; } = new Dictionary<int, float>()
        {
            [1] = 0.50f,
            [2] = 0.55f,
            [3] = 0.60f,
            [4] = 0.70f,
            [5] = 0.80f,
            [6] = 0.95f,
            [7] = 1.10f,
            [8] = 1.30f,
            [9] = 1.50f,
            [10] = 2.00f,
        };

        /// <summary>
        /// Block ratio by grade plus magic basis.
        /// </summary>
        public static float GetBlockRatio(UnitDataHuman u)
        {
            int gradeLvl;
            if (u == null || !BlockRatio.ContainsKey(gradeLvl = u?.worldUnitData?.unit?.GetGradeLvl() ?? 0))
                return 0f;
            return BlockRatio[gradeLvl] + u.worldUnitData.unit.GetBasisMagicSum() / BASIS_ON_BLOCK_RATIO;
        }

        /// <summary>
        /// MP cost of a block from physical basis.
        /// </summary>
        public static float GetBlockMpCost(UnitDataHuman u)
        {
            if (u == null)
                return 0f;
            return u.worldUnitData.unit.GetBasisPhysicSum().Parse<float>() / BASIS_ON_BLOCK_COST;
        }

        /// <summary>
        /// Base skill adj dmg of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static int GetSkillAdjDmgBase(WorldUnitBase wunit, Tuple<MartialType, DataProps.PropsData> skillData)
        {
            var soleId = skillData.Item2.soleID;
            var grade = skillData.Item2.propsInfoBase.grade;
            var level = skillData.Item2.propsInfoBase.level;
            var mType = skillData.Item1;
            return UnitModifyHelper.GetSkillExpertAtk(wunit.GetDynProperty(UnitDynPropertyEnum.Attack).baseValue, ExpertEvent.GetExpertLvl(soleId, grade, level), grade, level, mType);
        }

        /// <summary>
        /// Bonus skill adj dmg from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetSkillAdjDmgPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.SkillDamage);
        }

        /// <summary>
        /// Extra MP cost of a skill.
        /// </summary>
        public static int GetSkillAdjMpCost(WorldUnitBase wunit, DataProps.PropsData props)
        {
            if (wunit == null || props == null)
                return 0;
            var soleId = props.soleID;
            var grade = props.propsInfoBase.grade;
            var level = props.propsInfoBase.level;
            var expertLvl = ExpertEvent.GetExpertLvl(soleId, grade, level);
            return UnitModifyHelper.GetSkillExpertMpCost(SkillHelper.GetSkillMpCost(props), expertLvl, grade, level, wunit.GetGradeLvl() * expertLvl / 10);
        }

        /// <summary>
        /// Extra MP cost of a step.
        /// </summary>
        public static int GetStepAdjMpCost(WorldUnitBase wunit, DataProps.PropsData props)
        {
            if (wunit == null || props == null)
                return 0;
            var soleId = props.soleID;
            var grade = props.propsInfoBase.grade;
            var level = props.propsInfoBase.level;
            var expertLvl = ExpertEvent.GetExpertLvl(soleId, grade, level);
            return UnitModifyHelper.GetStepExpertMpCost(SkillHelper.GetStepMpCost(props), expertLvl, grade, level, wunit.GetGradeLvl() * expertLvl / 5);
        }

        //Min Dmg
        /// <summary>
        /// Base min dmg of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static int GetMinDmgBase(WorldUnitBase wunit)
        {
            var rs = wunit.GetGradeLvl();
            if (wunit.IsPlayer())
            {
                rs += RebirthEvent.Instance.TotalGradeLvl;
            }
            return rs;
        }

        /// <summary>
        /// Base min dmg of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static int GetMinDmgBase(UnitCtrlBase cunit)
        {
            var gameLvl = GameHelper.GetGameLevel().Parse<int>();
            return cunit.data.grade.value * gameLvl;
        }

        /// <summary>
        /// Bonus min dmg from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static int GetMinDmgPlus(WorldUnitBase wunit)
        {
            return Convert.ToInt32(CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.MinDamage));
        }

        //Block Max
        /// <summary>
        /// Base block max of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetBlockMaxBase(WorldUnitBase wunit)
        {
            return 18.0d;
        }

        /// <summary>
        /// Base block max of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetBlockMaxBase(UnitCtrlBase cunit)
        {
            return 18.0d;
        }

        /// <summary>
        /// Bonus block max from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetBlockMaxPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.BlockChanceMax);
        }

        //Block Dmg
        /// <summary>
        /// Base block dmg of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetBlockDmgBase(WorldUnitBase wunit)
        {
            return wunit.GetDynProperty(UnitDynPropertyEnum.Defense).value;
        }

        /// <summary>
        /// Base block dmg of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetBlockDmgBase(UnitCtrlBase cunit)
        {
            return cunit.data.defense.value;
        }

        /// <summary>
        /// Bonus block dmg from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetBlockDmgPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.BlockDmg);
        }

        //Evade Max
        /// <summary>
        /// Base evade max of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetEvadeMaxBase(WorldUnitBase wunit)
        {
            return 12.0d;
        }

        /// <summary>
        /// Base evade max of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetEvadeMaxBase(UnitCtrlBase cunit)
        {
            return 12.0d;
        }

        /// <summary>
        /// Bonus evade max from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetEvadeMaxPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.EvadeChanceMax);
        }

        //Evade
        /// <summary>
        /// Base evade of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetEvadeBase(WorldUnitBase wunit)
        {
            return Math.Sqrt(wunit.GetDynProperty(UnitDynPropertyEnum.BasisWind).value / 18);
        }

        /// <summary>
        /// Base evade of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetEvadeBase(UnitCtrlBase cunit)
        {
            return Math.Sqrt(cunit.data.basisWind.value / 18);
        }

        /// <summary>
        /// Bonus evade from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetEvadePlus(WorldUnitBase wunit)
        {
            var stepData = wunit.GetMartialStep();
            if (stepData == null)
                return 0;
            var plusValue = 0d;
            //step
            var stepId = stepData.data.soleID;
            var stepGrade = stepData.data.propsInfoBase.grade;
            var stepLevel = stepData.data.propsInfoBase.level;
            plusValue += UnitModifyHelper.GetStepExpertEvade(ExpertEvent.GetExpertLvl(stepId, stepGrade, stepLevel), stepGrade, stepLevel);
            //adj
            plusValue += CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.EvadeChance);
            return plusValue;
        }

        //SCrit Chance Max
        /// <summary>
        /// Base scrit chance max of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetSCritChanceMaxBase(WorldUnitBase wunit)
        {
            return 8.0d;
        }

        /// <summary>
        /// Bonus scrit chance max from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetSCritChanceMaxPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.SCritChanceMax);
        }

        //SCrit Chance
        /// <summary>
        /// Base scrit chance of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetSCritChanceBase(WorldUnitBase wunit)
        {
            return Math.Sqrt(wunit.GetDynProperty(UnitDynPropertyEnum.BasisThunder).value / 50);
        }

        /// <summary>
        /// Bonus scrit chance from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetSCritChancePlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.SCritChance);
        }

        //SCrit Damage
        /// <summary>
        /// Base scrit damage of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetSCritDamageBase(WorldUnitBase wunit)
        {
            return 2.000f + wunit.GetDynProperty(UnitDynPropertyEnum.BasisFire).value / 1000.00f;
        }

        /// <summary>
        /// Bonus scrit damage from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetSCritDamagePlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.SCritDamage);
        }

        //Hp Recovery
        /// <summary>
        /// Base hp recovery of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetHpRecoveryBase(WorldUnitBase wunit)
        {
            return Math.Sqrt(wunit.GetDynProperty(UnitDynPropertyEnum.BasisWood).value / 100f);
        }

        /// <summary>
        /// Bonus hp recovery from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetHpRecoveryPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.RHp);
        }

        //Mp Recovery
        /// <summary>
        /// Base mp recovery of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetMpRecoveryBase(WorldUnitBase wunit)
        {
            return Math.Sqrt(wunit.GetDynProperty(UnitDynPropertyEnum.BasisFroze).value / 200f);
        }

        /// <summary>
        /// Bonus mp recovery from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetMpRecoveryPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.RMp);
        }

        //Sp Recovery
        /// <summary>
        /// Base sp recovery of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetSpRecoveryBase(WorldUnitBase wunit)
        {
            return 0.0d;
        }

        /// <summary>
        /// Bonus sp recovery from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetSpRecoveryPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.RSp);
        }

        //Dp Recovery
        /// <summary>
        /// Base dp recovery of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetDpRecoveryBase(WorldUnitBase wunit)
        {
            return 0.0d;
        }

        /// <summary>
        /// Bonus dp recovery from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetDpRecoveryPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.RDp);
        }

        //Steal Hp
        /// <summary>
        /// Base steal hp of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetStealHpBase(WorldUnitBase wunit)
        {
            return 0.0d;
        }

        /// <summary>
        /// Base steal hp of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetStealHpBase(UnitCtrlBase cunit)
        {
            return 0.0d;
        }

        /// <summary>
        /// Bonus steal hp from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetStealHpPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.StealHp);
        }

        //Steal Mp
        /// <summary>
        /// Base steal mp of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetStealMpBase(WorldUnitBase wunit)
        {
            return 0.0d;
        }

        /// <summary>
        /// Base steal mp of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetStealMpBase(UnitCtrlBase cunit)
        {
            return 0.0d;
        }

        /// <summary>
        /// Bonus steal mp from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetStealMpPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.StealMp);
        }

        //Steal Sp
        /// <summary>
        /// Base steal sp of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetStealSpBase(WorldUnitBase wunit)
        {
            return 0.0d;
        }

        /// <summary>
        /// Base steal sp of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetStealSpBase(UnitCtrlBase cunit)
        {
            return 0.0d;
        }

        /// <summary>
        /// Bonus steal sp from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetStealSpPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.StealSp);
        }

        //Instant Kill
        /// <summary>
        /// Base instant kill of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetInstantKillBase(WorldUnitBase wunit)
        {
            return 0.0d;
        }

        /// <summary>
        /// Base instant kill of the unit, derived from its grade, basis values and properties.
        /// </summary>
        public static double GetInstantKillBase(UnitCtrlBase cunit)
        {
            return 0.0d;
        }

        /// <summary>
        /// Bonus instant kill from equipped custom refine adjustments (CustomRefineEvent.GetCustomAdjValue).
        /// </summary>
        public static double GetInstantKillPlus(WorldUnitBase wunit)
        {
            return CustomRefineEvent.GetCustomAdjValue(wunit, AdjTypeEnum.InstantKill);
        }
        #endregion
    }
}
