# PillBodyEvent

`3161035078/ModProject/ModCode/ModMain/Mod/PillBodyEvent.cs` - cache key `ModConst.PILL_BODY_EVENT` - group: Battle (order 5045)

The recovery of the potions (HP / MP) of the **player** in a battle, for the middle and the end of the game (6.3; the
recovery of the game grows slower than the HP and MP). The bonus is added when a potion is used
(`OnBattleUnitUseProp`, a recovery potion: `ItemHelper.IsPotion`), on top of the recovery of the game (the values of
`roleEffect` scaled by `ModMain`). Texts: `pill_localText.json` (ids `-1000016000`...).

## Saved data

`Points` (pill points), `LoggedTier`, `NineTurn`, `NineTurnSeen`, `KingHeal`, `KingFocus`.

## Sources of the bonus

| Source | Rule |
|---|---|
| A. Pill Body | every potion taken in a battle adds its grade to `Points`; a constitution ([ConstitutionEvent](ConstitutionEvent.md)): awakened at 100 points with a chance under 1 % each potion, then tier n at `50 x n²` points (tier 10: 5,000), +10 % recovery a tier; the realm caps the tier: 3 (realms 1-4), 6 (5-6), 9 (7-8), 10 (9+) (`TierCap`). A new tier is written in the Taoist log (`log_pill_tier`, `log_pill_capped` at the cap) |
| C. Alchemy | +5 % a rank of alchemy ([ArtisanEvent](ArtisanEvent.md), ranks 0-5) with a kind of potion the player crafted (`CraftedIds` of the alchemy); off with S&M `NoArtisan` |
| D. Nine-Turn Golden Pill | rare encounter (realm 8+, 2 % a month, once, not during a time skip, off with S&M `NoEncounter`): 9 herbs and `EncounterEvent.Cost(200)` spirit stones; then each potion also gives **10 % of the max HP / MP** (keeps its value when the HP is huge) |
| B. Medicine King's Testament | ending of storyline S ([Storylines/S-Medicine-King](../Storylines/S-Medicine-King.md)): `KingHeal` +30 % recovery, or `KingFocus` half of the focus (SP) of the potion given back |

`Extra = base x (tiers x 10 % + own potion x alchemy rank x 5 % + 30 % KingHeal) + 10 % x max (NineTurn)`, for HP and for MP
(a potion with both effects gets both).

## Player info

The summary (`Summary`, `pill_summary`: tier, cap, points, next tier, every bonus, big text window) from the button
"Pill Body" of the constitutions window ([ConstitutionEvent](ConstitutionEvent.md)); with the constitutions off, a button
"Pill Body" on the top edge of the player info (`UIEvent`, `PILL_BUTTON_DX`). The Pill Body is also listed as a constitution.

## Recovery and focus of the potions

`ModMain.PotionHealRatio(grade, level)`: the HP / MP of a recovery potion is x `(PotionHealBase 1 + PotionHealGradeRatio 1.5 x grade +
PotionHealLevelRatio 1 x level)` of the game (`mod_configs.json`; until 6.3 x `(grade x 4 + level + grade / 2 x 4)`: a level 1 potion of the realm
healed 80-190 % of the max HP of a middle NPC of the realm, measured by TG-91; now about 30-70 %, the pill body and its bonuses
raising it again in the middle and the end of the game).

`ModMain.PotionSpCostRatio(grade)`: the focus of a recovery potion is x `(1 + 0.1 x grade)` of the game (until 6.3 x `1.3 +
0.2 x grade + 0.05 x level`: a grade 3 potion took about 40 focus).

## Tests

Unit tests `BalanceTests.PillBody_Tiers`, `PotionSpCost`; autotests TG-92 (summary, button, encounter, rewards), TB-18
(bonus in a battle), TS-19 (storyline S).
