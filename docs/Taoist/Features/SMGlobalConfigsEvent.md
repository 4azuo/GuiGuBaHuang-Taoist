# SMGlobalConfigsEvent

`3161035078/ModProject/ModCode/ModMain/Mod/SMGlobalConfigsEvent.cs` - cache key `ModConst.SM_GLOBAL_CONFIGS_EVENT` (Global cache, works on the title screen) - group: Core

The S&M ("sadism & masochism") difficulty configs, edited in the "S&M Configs" window of the title screen and saved globally. Every option has a score; the total **P** is the difficulty (and drives [SMScoreEvent](SMScoreEvent.md)). A new save copies all of it into [SMLocalConfigsEvent](SMLocalConfigsEvent.md), which is what the features read.

## Options and scores

"Rate" sliders are multipliers added to the base (`Calculate(base, rate) = base x (1 + rate)`); the score of a slider is `value x factor`. Toggles score only when on (and when their condition holds). The UI shows each option's points.

### Page 1

| Option | Range / default | Score | Used by |
|---|---|---|---|
| `AddAtkRate`, `AddDefRate`, `AddHpRate`, `AddBasisRate` (monsters) | -0.5 .. 8 / 0 | x 1,000 (6.1.01; was x 100) | [MonstStrongerEvent](MonstStrongerEvent.md) |
| `AddMonstShieldRate` / `AddMonstExplodeRate` / `AddMonstMultiplyRate` | -1 .. 1 / 0 (-1: off) | x 1,000 each | [SpecialMonsterEvent](SpecialMonsterEvent.md); the former `AddSpecialMonsterRate` of older configs is read into the three |
| `NoMonsterShield` | off | -1,000 | SpecialMonsterEvent (no shield at all; the shield rate and `BossHasShield` then count nothing) |
| `MonsterPropertiesDependOnPlayerProperties` | off | +5,000 | MonstStrongerEvent |
| `BossHasShield` | off | +10,000 | SpecialMonsterEvent |
| `AddNpcGrowRate` | 0 .. 8 / 0 | x 1,000 | [NpcAutoEvent](NpcAutoEvent.md), [UnitTypeEvent](UnitTypeEvent.md) |
| `NPCAmount` | 1,000 .. 10,000 / 2,000 | value - 2,000 (6.1.01; was = value) | the game's initial NPC count of a new world: OK writes it into `g.conf.npcInitParameters` item 1 (only in that game session: after a restart it applies once the window is confirmed again) |
| `AddLevelupExpRate` | 0 .. 1 / 0 | x 5,000 (6.1.01; was x 2,000) | SMLocalConfigsEvent |
| `GrowUpSpeed` (months per growth tick) | 1 .. 24 / 3 | `4,000 x (3 / value - 1)` rounded to 100: 1 = +8,000, 2 = +2,000, 3 = 0, 4 = -1,000, 6 = -2,000, 12 = -3,000, 24 = -3,500 (6.1.01; was `(12 - value) x 1,000`) | the NPC growth turns (below), monsters, training |
| `StartupPlayerRole` | none / 8 roles | -10,000 if set | UnitTypeEvent |
| `PriorityDestinyLevel` | 0 .. 6 / 0 | `(3 - level) x 1,000` if set | [UICreatePlayerEvent](UICreatePlayerEvent.md) |
| `HideSaveButton` | off | +1,000 (enables HideReload) | [UIEvent](UIEvent.md) |
| `HideReloadButton` | off | +5,000 (enables Onelife) | UIEvent |
| `LostItemWhenDie` | off | +10,000 | [BattleRewardEventV2](BattleRewardEventV2.md) |
| `LostMoneyWhenDie` | **on** | +1,000 | BattleRewardEventV2 |
| `LostLifespanWhenDie` | off | +2,000 | BattleRewardEventV2 |
| `Onelife` | off | +20,000 with AutoSaveAfterLostInBattle (which it forces on), else 0 | BattleRewardEventV2 |
| `AutoSaveAfterLostInBattle` | off | +20,000 without Onelife (with it: counted by Onelife; 6.1.01) | BattleRewardEventV2 |
| `EnableTrainer` | off | -1,000,000 | [TrainerEvent](TrainerEvent.md) |
| `AllFunctionsApplyToNearestUnits` | off | -3,000 (6.1.01; was -1,000: the far NPCs really stop growing since ModLib 2.2.36) | [PrepareEvent](PrepareEvent.md) |

Buttons: Export (save dialog, copies the global cache file), Import (open dialog, replaces and reloads; unreadable file -> `ui_file_errors`), open the save data folder, open the ModConf folder (Explorer: `mod_configs.json` and the `*_configs.json` settings files of the features).

### Growth turns (GrowUpSpeed)

`GrowTurnHelper` (`ModMain/Helper/GrowTurnHelper.cs`). With `GrowUpSpeed` = P, the NPCs are split in P groups by
their unit id (FNV-1a hash, stable between sessions) and each month **one group** grows: an NPC grows once every P
months, and the load of the NPC handlers is spread evenly over the P months (no month with every NPC at once).

- `Turns(wunit)`: growth turns in the running month dispatch. NPC: 1 on its turn, 0 otherwise (`N / P` over a time
  skip of N months). Player: `ModMaster.Ticks(P)` (every P months, as before), not split.
- `Months(turns)` = turns x P: the months a turn stands for, for timers and chances that keep their monthly pace.
- Growth amounts are given per turn (one month's worth), so a larger P slows NPC growth; P = 1: every NPC every month,
  as before the turns.
- `ForcedTurns` / `WithTurns(n, action)`: autotests run a handler for one NPC on its turn.

| Feature (NPC side) | By turns (growth) | By `Months(turns)` (pace kept) |
|---|---|---|
| [NpcAutoEvent](NpcAutoEvent.md) | exp, breakthrough rate | |
| [NpcUpgradeSkillEvent](NpcUpgradeSkillEvent.md) | one martial upgrade try a turn | |
| [NpcAutoCraftArtifact](NpcAutoCraftArtifact.md) | craft chance `ChanceOver(.., turns)` | |
| [BodyCulEvent](BodyCulEvent.md) | training exp, place exp | taking up the path, injury, stays, going to a place |
| [ArtisanEvent](ArtisanEvent.md) | craft exp; the yearly exam on the first turn of the year | |
| [MeritEvent](MeritEvent.md), [DaoHeartEvent](DaoHeartEvent.md), [SpiritSenseEvent](SpiritSenseEvent.md) | monthly gain | |
| [UnitTypeEvent](UnitTypeEvent.md) | rank lucks, property growth | passive income (the role is still given on first sight, every month) |
| [QiCulEvent](QiCulEvent.md), [ExpertEvent](ExpertEvent.md), [CustomRefineEvent](CustomRefineEvent.md) | as before (every P months), now spread over the groups | |
| [EstateEvent](EstateEvent.md) | | buying / upgrading a house, going home |
| [HirePeopleEvent](HirePeopleEvent.md) | | teaming up with friends (the payment of masters stays monthly) |

Not split: the player side of every feature, `UnitModifyEvent` (hooks new units), `MapBuildPropertyEvent` (town tax).

### Page 2 (economy, market, systems)

| Option | Range / default | Score | Used by |
|---|---|---|---|
| `AddTaxRate` | -0.5 .. 5 | x 400 (6.1.01; was x 100) | [MapBuildPropertyEvent](MapBuildPropertyEvent.md) |
| `AddInflationRate` | -0.5 .. 2 | x 1,000 | [InflationaryEvent](InflationaryEvent.md) |
| `AddBuildingCostRate` | 0 .. 8 | x 100 | [BuildingArrangeEvent](BuildingArrangeEvent.md) |
| `AddBankAccountCostRate` | 0 .. 8 | x 100 | [BankAccountEvent](BankAccountEvent.md) |
| `AddBankFee` | 0 .. 10 (was 0 .. 100) | x 200 (top still +2,000 P) | [RealStorageEvent](RealStorageEvent.md) |
| `AddRefineCost` | -0.5 .. 1 | x 2,000 | [CustomRefineEvent](CustomRefineEvent.md) |
| `AddSectExchangeRate` | -0.5 .. 1 | x 2,000 (not with SectNoExchange) | [ContributionExchangeEvent](ContributionExchangeEvent.md) |
| `AddItemValueRate` | -0.5 .. 1 | x 1,000 | InflationaryEvent (start index) |
| `MarketItemNpcJoinRate`, `MarketItemGetAttackedRate` | -0.5 .. 1 | x 100 / x 1,000 (market on) | [RealMarketEvent3](RealMarketEvent3.md) |
| `MaxSellersEachTown`, `MaxItemsOnSeller`, `MaxBuyersOnSellingItem` | 1..30 / 8, 1..10 / 4, 1..20 / 4 | 0 | RealMarketEvent3 |
| `NoMarketItem` | off | +10,000 | RealMarketEvent3 |
| `NoStalker` | off | -3,000 | [BattleAfterEvent](BattleAfterEvent.md) |
| `NoTaxing` | off | -2,000 | MapBuildPropertyEvent |
| `NoQiCultivation` | off | +2,000 | [QiCulEvent](QiCulEvent.md) |
| `HideBattleMap` | off | +2,000 | [BattleModifyEvent](BattleModifyEvent.md) |
| `NoRebirth` | off | +10,000 | [RebirthEvent](RebirthEvent.md) |
| `OnlyPortalAtCityAndSect` | off | +1,000 | BuildingArrangeEvent |
| `NoExpFromBattles` | off | +1,000 | BattleRewardEventV2 |
| `SectNoExchange` | off | +10,000 | ContributionExchangeEvent |
| `NoGrowupFromBattles` | off | +10,000 | BattleRewardEventV2 |
| `AllowUpgradeNaturally` | off | -2,000 | [UpgradeNaturallyEvent](UpgradeNaturallyEvent.md) |
| `AllowTownBuildupOverTime` (+ `_IncludeFirstTown`) | off | +1,000 (+1,000) | BuildingArrangeEvent |
| `OnlyActiveOnCurOrNearArea` | **on** | 0 (was -1,000) | RealMarketEvent3 |
| `DestroyBuildingAfterTownWar` | **on** | +2,000 | [MapBuildBattleEvent](MapBuildBattleEvent.md) |
| `AllowRepairGodWeapon` | off | -5,000 | [RepairShinkiEvent](RepairShinkiEvent.md) |

### Page 3 (additional features)

Turning off a feature that helps the player costs points; turning off one that hurts gives points back.

| Option | Score | Used by |
|---|---|---|
| `NoEncounter` | +1,000 | [EncounterEvent](EncounterEvent.md) |
| `EncounterRate` (0 .. 10 %, default 3) | `(3 - rate) x 300` (encounters on) | EncounterEvent |
| `NoBankLoan` | +1,000 | [BankLoanEvent](BankLoanEvent.md) |
| `NoBuyOrder` | +500 | RealMarketEvent3 |
| `NoBounty` | -2,000 | [WantedEvent](WantedEvent.md) |
| `NoSeclusion` | +1,000 | [SeclusionEvent](SeclusionEvent.md) |
| `SeclusionRate` (0 .. 3 %, default 0.5) | `(0.5 - rate) x 2,000` (seclusion on) | SeclusionEvent |
| `NoForcedSeclusion` | -2,000 (seclusion on) | SeclusionEvent |
| `NoFamily` | +2,000 | [FamilyEvent](FamilyEvent.md), [FamilyFundEvent](FamilyFundEvent.md) |
| `AllowSameSexCouple`, `ChildSurname` (father / mother / random) | 0 | FamilyEvent |
| `ContinueAsDescendant` | -10,000 (family on) | FamilyEvent |

**Cultivation paths** (section `smcfgs197`, 6.0): on / off on the left, how fast each path grows on the right. A gain
rate is added to x1 (-0.5 .. +2: half .. three times) and applied where the path gains (`SMGlobalConfigsEvent.Rate`):
`BodyCulEvent.AddExp`, `MeritEvent.Add` (merit only, not karma), `ArtisanEvent.AddExp`, `DaoHeartEvent.Add` (gains
only), `SpiritSenseEvent.AddExp`.

| Option | Score | Used by |
|---|---|---|
| `NoBodyCultivation` | +1,000 | [BodyCulEvent](BodyCulEvent.md) |
| `NoMerit` | +500 | [MeritEvent](MeritEvent.md) |
| `NoArtisan` | +500 | [ArtisanEvent](ArtisanEvent.md) |
| `NoDaoHeart` | +500 | [DaoHeartEvent](DaoHeartEvent.md) |
| `NoSpiritSense` | +500 | [SpiritSenseEvent](SpiritSenseEvent.md) |
| `BodyExpRate`, `MeritRate`, `ArtisanExpRate`, `DaoHeartRate`, `SpiritSenseRate` (-0.5 .. 2, default 0) | `-rate x 300` each (its path on) | the path |

**Estates** (section `smcfgs198`, 6.0):

| Option | Score | Used by |
|---|---|---|
| `NoEstate` | +500 | [EstateEvent](EstateEvent.md): no "Plots" button (town window, town management), NPCs buy no house (`EstateEvent.Enabled`); houses already owned stay |
| `NoNpcHouses` | 0 | EstateEvent: NPCs buy and upgrade no house (`EstateEvent.NpcHouses`) |
| `EstatePriceRate` (-0.5 .. 3, default 0) | `rate x 500` (estates on) | EstateEvent: land and building prices x (1 + rate) (`LandPrice`, `BuildCost`: also the upgrades and the mortal houses bought back) |
| `EstateTaxRate` (-1 .. 3, default 0) | `rate x 500` (estates on) | EstateEvent: house tax x (1 + rate) |

Page 3 shows a hint of the features under the sliders, wrapped by `WrapText` (about 80 Latin characters; a CJK
character counts as 2 and may be split between two CJK characters).

### Page 4 (skills and pills, 6.3)

Eight sliders added to x1 of the ratios of `mod_configs.json` ([ConfBalance](ConfBalance.md), `SKILL_SLIDERS`): skill
mastery exp (-50..+200 %, +1,000 P a +100 %), MP cost (-75..+100 %, +3,000), cooldown (-50..+100 %, +3,000), basis
required base / a level (-75..+100 %, +1,000 / +500), Dao points required base / a level (-50..+100 %, +1,000 / +500),
power-up pill effect (-75..+100 %, -2,000). The presets set them to 0 (their scores unchanged). Hint `smcfgs234`. Under them `NoConstitution` (+1,000 P, [ConstitutionEvent](ConstitutionEvent.md), hint `smcfgs238`).

The pages of another mod (after page 5): a banner "Options of the mod <title> (not counted in the S&M score)" (`smcfgs236`, blue, `MOD_OPTIONS_COLOR`) and the note of the mod on the top rows, the options of `ModOptionsPage` on the rows under them (the grid rows of the window are narrowed while they are added: they stood over the note and over the red line of the last row). Two columns of options at the grid columns `MOD_OPTION_COLUMNS` (3 and 16; the first one was at 4: its values ran into the labels of the second one).

### Page 5 (NPC storylines)

| Option | Score | Used by |
|---|---|---|
| `NoStory` | +1,000 | StoryEvent (`ModMain/Story/`) |

One line per storyline A-S under "No NPC storylines" (`smcfgs169-174`, `smcfgs186-191`, `smcfgs208-211`), wrapped
by `WrapText` (about 140 Latin characters).

## Presets (right column)

- Total score (green >= 0, red < 0, `smcfgs043`) and the title of its tier.
- **Base** (`SetLevelBase`): every option to its default (battle map hidden).
- **Level 0 .. 10** (`SetLevel(n)`): sliders to a share of their range by level (e.g. monster Atk `n x 4 %`, HP `n x 5 %`, inflation `n x 5 %`, refine `n x 10 %`; GrowUpSpeed from 12 down to 2; NPC amount 800 .. 3,200), and toggles switched on by level: battle map hidden (>0), lifespan / money lost (>0), town build-up (>0), save hidden (>1), no battle exp (>2), portals only in cities (>3), monsters follow the player (>3), boss shield (>4), items lost (>4), no growth from battles (>5), reload hidden and auto-save after a loss (>6), no sect exchange (>7), no rebirth (>8), one life (>9). The additional features go back to their defaults.

## Flow

| Hook | What happens |
|---|---|
| `OnLoadClass` | builds `ScoreCalculator` (`Register(component, score, condition, formatter, onChange)`, `Object/SMItemWork.cs`) |
| `OnOpenUIEnd(Login)` | button "S&M Configs" in the title buttons -> `OpenSMConfigs()` (3 pages, navigation, OK = `SetSMConfigs`) |
| OK (`SetSMConfigs`) | `Score = CalSMTotalScore()`; every UI value copied into its property; saved globally; `NPCAmount` written into the game config `npcInitParameters` (item 1) |

`CalSMTotalScore()` sums the score of every enabled component whose condition holds; `CalCompScore(comp)`, `IsEnableComp(comp)`. `ShowTrainer(on)` (tests) sets the trainer toggle.

## Tests

TT-02.

## Options of the other mods (6.2.0)

Page 4 also holds the toggle "Keep balance numbers" (`KeepBalanceStats`, `smcfgs239`/`240`, default on, not in the score):
the balance numbers of the play (`Helper/TaoistStats` on ModLib `BalanceStatsHelper`: `balance-stats.json` in the global
cache folder of Taoist, only on this computer; what a player sends with a balance report). Under the Level buttons (every
page): the button "Performance: ON/OFF" (`PerformanceMode`, `smcfgs241`-`243`, not in the score): on, the balance numbers
are off and the options that save processing time are on (`OnlyActiveOnCurOrNearArea`, `AllFunctionsApplyToNearestUnits`:
its score as usual); the presets keep the mode (`ApplyPerformanceOptions` after them); off changes nothing back. Checked by
TT-02.

After page 5, a page (or more) per loaded mod shipping `ModConf/*_options.json` (ModLib `ModOptionsHelper.LoadedOptions`, `ModOptionsPage`), e.g. Dao of Creation (`doc_options.json`). OK writes their values file (the mod applies it at the next world load). They do not change the score and the presets leave them alone.
