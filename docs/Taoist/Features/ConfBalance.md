# ConfBalance

`3161035078/ModProject/ModCode/ModMain/Mod/ConfBalance.cs` - static class (no event) - used by `ModMain` and [SMLocalConfigsEvent](SMLocalConfigsEvent.md)

The values of the game configs that Taoist scales by `mod_configs.json` **and** by the S&M configs of the save (6.3):

| Kind | Config of the game | Ratio of `mod_configs.json` (grades 1-10) | S&M rate (page 4) | Points a +100 % |
|---|---|---|---|---|
| Level-up exp | `roleGrade.exp` | `LevelExpRatio` | `AddLevelupExpRate` (page 1) | (page 1) |
| Skill mastery exp | `battleSkillMastery.grade1..10` | `SkillMasteryExpRatio` | `SkillMasteryExpRate` (-50..+200 %) | +1,000 |
| Skill MP cost | `battleSkillValue *_mpCost` | `SkillMpCostRatio` | `SkillMpCostRate` (-75..+100 %) | +3,000 |
| Skill cooldown | `*_cd` | `SkillCdRatio` | `SkillCdRate` (-50..+100 %) | +3,000 |
| Basis required | `&zizhiBase_*` / `&zizhiAdd_*` | `SkillZizhiBaseRatio` / `SkillZizhiAddRatio` | -75..+100 % | +1,000 / +500 |
| Dao points required | `&daodianBase_*` / `&daodianAdd_*` | `SkillDaodianBaseRatio` / `SkillDaodianAddRatio` | -50..+100 % | +1,000 / +500 |
| Power-up pills | `*_tsgj` / `*_tsfy` of the power-up pills | `PowerUpPillEfxRatio` | `PowerUpPillEfxRate` (-75..+100 %) | -2,000 (easier) |

`value = value of the game x ratio(grade) x (1 + S&M rate)` (`Scale`, not below 0).

The game configs are changed in place and the S&M configs belong to a save: `Init` keeps the values of the game once
(at the start, before any change), `Apply(configs)` works out every value again from them - at the start with the
rates at 0 (`ModMain.OnLoadClass`) and each time a save is loaded (`SMLocalConfigsEvent.OnLoadGame`). Loading other
saves in the same session never multiplies twice (until 6.3 the level-up exp rate was multiplied again at each load).
A power-up value used by several pills is scaled once (`AddPowerUpValue`).

New defaults of 6.3 in `mod_configs.json`: `SkillMpCostRatio` 1.3 / 1.5 / 1.8 / 2.2 / 2.8 / 3.6 / 5 / 7 / 10 / 14 (was up to 25),
`SkillZizhiBaseRatio` 1.1 / 1.3 / 1.7 / 2.2 / 3 / 4 / 5.5 / 7.5 / 10 / 14 (was up to 25), `PowerUpPillEfxRatio` 6 / 8 / 11 / 14 / 18 / 23 / 29 /
36 / 45 / 55 (was 10..100).

Tests: unit tests `BalanceTests.ConfBalance_ScaleAndKinds`, `ModConfigs_Ratios`, `SkillSliders_Score`; autotests TG-92, TG-91, TT-02.
