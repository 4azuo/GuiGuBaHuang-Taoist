# ConstitutionEvent

`3161035078/ModProject/ModCode/ModMain/Mod/ConstitutionEvent.cs` - cache key `ModConst.CONSTITUTION_EVENT` - group: Battle (order 5046)

Constitutions (体质, "Thể chất", 6.3): special bodies of the cultivators. **No limit**: a unit has every constitution it
cultivates. Each one grows its own way, tiers 1-10; the realm caps the tier like the pill body
(`PillBodyEvent.TierCap`: 3 / 6 / 9 / 10), except the innate ones (born with it: tier = realm).

**Awakening (tier 0 -> 1)**: the points of tier 1 (`100 x scale`), then a **chance each time points come** (a potion, a
battle with hits of the art, a treasure, a herb, a lightning trial, a story reward, a year of an NPC): `0.3 % x points /
points of tier 1`, at most **0.99 %** (`AwakenChance`). Born ones are awakened from the start. **Tiers 2-10**: only their
points, no chance: tier n at `50 x n² x scale` (tier 2: 200, tier 3: 450, tier 10: 5,000, x the scale). Off with S&M
`NoConstitution` (page 4, +1,000 P). Texts: `const_localText.json` (ids `-1000017000`...).

## Saved data

`Points` (unit id -> constitution -> points; the player and the NPCs that grew one), `Awakened` (unit id -> the
constitutions awakened), `PlayerRolled`, `LoggedTier`. The hits of a battle are counted at its end (one awakening roll a
battle).
The NPCs born with one keep nothing: `Roll(unitId, kind)` (0..100, stable) < born chance.

## Constitutions (`DEFS`)

| Constitution | Grows by | Scale (points x) | A tier gives |
|---|---|---|---|
| Pill Body | Herb Flesh, Elixir Skin, Hundred Herbs, Pill Marrow, Herb Spirit, Gold Elixir, Myriad Pills, Immortal Herb, Pill Sage, Medicine King |
| Sword Bone | Sword Embryo, Iron Sword, Sword Intent, Spirit Blade, Sword Heart, Sword Soul, Skysplitter, Sword Sovereign, Supreme Sword, Primal Sword |
| Vajra Body | Bronze Skin, Iron Bone, Rockbreaker, Golden Bell, Vajra, Unbreakable, Arhat Gold, Bodhi Vajra, Buddha Light, Undying |
| Innate Spirit Body | Spirit Touch, Clear Spirit, Spirit Spring, Spirit Vein, Innate Spirit, Purple Qi, Heaven-Born, Star Spirit, Chaos Spirit, Dao Body |
| Dragon Blood Body | Scale Blood, Serpent Blood, Jiao Blood, Qiu Blood, Dragon Blood, True Dragon, Azure Dragon, Dragon Ancestor, Dragon Emperor, Dragon God |
| Thunder Body | Thunderstruck, Thunder Forged, Thunder Vein, Purple Thunder, Thunder Spirit, Five Thunders, Sky Thunder, Tribulation, Thunder Emperor, Divine Thunder |
| Venom Body | Venom Taster, Poisonproof, Hundred Poisons, Venom Vein, Venom Spirit, Poison Eater, Myriad Venoms, Venom King, Venom Saint, Venom Ancestor |

The stats are added to the base values when a cultivator (player or NPC) enters a battle (`OnBattleUnitInto`; HP / MP
filled to the new max). The player's awakening (`log_const_awaken`) and new tiers (`log_const_tier`) are written in the
Taoist log; the born ones at the first load of a save (`log_const_born`). The window shows a constitution not awakened as
"points / points of tier 1, then x % each time".

## Destinies

Every tier of every constitution is a destiny (luck) of its own name, shown in the destinies of the unit:
`constluck_roleCreateFeature.json`, ids `LUCK_BASE (420300000) + (kind + 1) x 1000 + tier` (420301001 Pill Body tier 1
... 420307010 Venom Body tier 10), group `+ (kind + 1) x 1000`, rarity 1,1,2,2,3,3,4,4,5,6; texts
`constluck_localText.json` (name, `desc`: the stats of the tier). The luck has no effect (`effect 0`): the stats are
given when entering a battle. `SyncLucks(unit)` keeps the luck of the tier only (none when the constitutions are off):
after a tier change (`Add`), at the load and every month (player), every year for the NPCs of the yearly turn; never
in a battle scene (a luck cannot be added there: the next sync on the map gives it).

| Constitution | Tiers 1 -> 10 |
|---|---|
| Pill Body | Herb-Steeped Flesh, Elixir-Tempered Body, Hundred-Herb Body, Pill-Marrow Body, Medicine-Spirit Body, Golden Elixir Body, Myriad-Pill Body, Immortal-Elixir Holy Body, Pill-Sage Body, Medicine King's Divine Body |
| Sword Bone | Sword-Embryo Bone, Iron Sword Bone, Sword-Intent Bone, Spirit Sword Bone, Sword-Heart Bone, Sword-Soul Bone, Heaven-Splitting Sword Bone, Sword-Sovereign Bone, Supreme Sword Bone, Primordial Sword Bone |
| Vajra Body | Bronze-Skin Body, Iron-Bone Body, Stone-Shattering Body, Golden-Bell Body, Vajra Body, Indestructible Vajra Body, Arhat Golden Body, Bodhisattva Vajra Body, Buddha-Light Golden Body, Ever-Enduring Undying Body |
| Innate Spirit Body | Spirit-Touched Body, Clear-Spirit Body, Spirit-Spring Body, Spirit-Vein Body, Innate Spirit Body, Purple-Qi Spirit Body, Heaven-Born Spirit Body, Star-Spirit Body, Primordial-Chaos Spirit Body, Innate Dao Body |
| Dragon Blood Body | Scaled-Blood Body, Serpent-Blood Body, Flood-Dragon Blood Body, Horned-Dragon Blood Body, Dragon-Blood Body, True-Dragon Blood Body, Azure-Dragon Blood Body, Ancestral-Dragon Blood Body, Dragon-Emperor Blood Body, Primeval Dragon-God Body |
| Thunder Body | Lightning-Struck Body, Thunder-Tempered Body, Thunder-Vein Body, Purple-Thunder Body, Thunder-Spirit Body, Five-Thunder Body, Heavenly-Thunder Body, Tribulation-Thunder Body, Thunder-Emperor Body, Nine-Heavens Divine Thunder Body |
| Venom Body | Venom-Tasting Body, Poison-Resisting Body, Hundred-Poison Body, Venom-Vein Body, Venom-Spirit Body, Poison-Devouring Body, Myriad-Venom Body, Venom-King Body, Venom-Saint Body, Venom-Ancestor Holy Body |

## NPCs

Born ones by their id. Every year 500 NPCs in turn (all of them over the turns, the growth x the years of a turn) grow
the martial constitution of their best weapon basis: sword -> Sword Bone, fist / palm / finger -> Vajra,
`20 x realm` points a year. Their hits in battles count too.

## Window

Button "Constitutions" (`const_title`) on the top edge of the player info (where the pill body button was; the pill body
button again when the constitutions are off), drawn with `Helper/InfoPanel` (ModLib `UIPanelHelper`): the realm cap on
top, the group "Awakened" (gold cards, 2 columns: icon, name, tier and the destiny of the tier, points / next tier, how it
grows, its effect now in green), the group "Not yet awakened" (grey cards: how to awaken in red, how it grows, what a tier
gives), the buttons Absorb a treasure, Taste a raw herb, Pill Body (its details), OK. Icons `ICONS`; texts `const_ui_*`.
`Summary(unit)`: the same as text (tests, NPCs).

## Tests

Unit tests `BalanceTests.Constitution_Rules`, `DestinyTests.Destinies_UniqueNames` (a luck a tier, every name unique
in every language, no "Lvl"); TG-93 also checks the destinies of the tiers; autotests TG-93 (several at once, treasure, herb, NPCs, window), TB-18
(stats on entering a battle), TG-91 (constitutions of the NPCs of every realm, over the years).
