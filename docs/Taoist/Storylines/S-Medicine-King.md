# Line S — "The Medicine King's Testament" (`{O}`; opens at Golden Core, grade ≥ 5, outside towns)

Dialogue of the line (6.3, coded: `ModMain/Story/Lines/LineS_MedicineKing.cs`, texts in `story_localText.json`
`story_*S*`, quest `story_taskBase.json` 444447029, 4 acts). Conventions: `00-Conventions.md`. Purpose: a way to raise
the recovery of the potions in the middle of the game ([PillBodyEvent](../Features/PillBodyEvent.md)).

`{O}`: an old alchemist, no sect, near the end of his life; quiet, precise, ashamed of his shaking hands. He may die
on the way: the line goes on, told (no vital role).

## S0. The torn testament
1. *An old alchemist by a dead fire, burnt paper on his knees.* **{O}:** "These are the last words of the Medicine King. Three pages are missing... Will you?"
   ▸ I will find them ▸ Not my business (the line closes)
2. *The first page smells of musk: a cave of gnawed bones.* → Battle with a beast (+3 levels). Lost: the step comes again after 6 months.

## S1. The plague village (6 months later, time limit 2 years)
1. *Coughing behind every door.* **{O}** reads: "A healer who walks past is no healer."
   ▸ Give a potion (a recovery potion of the bag; +100 intimacy with {O}) ▸ Pay a doctor (`Cost(20)` spirit stones) ▸ Walk on (flag `S1_left`)

## S2. The test of the poison (6 months later)
1. *The third page is a dried black paste.* **{O}:** "The cure is written in the poison. Someone has to taste it."
   ▸ Taste it yourself (-30 % HP) ▸ Let {O} taste it (-60 months of his life) ▸ Nobody tastes it (flag `S2_none`)

## S3. The last page (3 months later)
- Village left or nobody tasted: **Fragment** — the page stays blank; {O} gives a potion of the player's realm. No perk.
- Otherwise: *"A healer chooses what to keep - the strength of the medicine, or the calm of the one who takes it."*
  ▸ The strength of the medicine → **Heal**: potions recover +30 % ▸ The calm → **Focus**: half of the focus of a potion back

## Endings S
- **S_Heal** (`end_S_Heal` "The strength of the medicine"): `PillBodyEvent.KingHeal`.
- **S_Focus** (`end_S_Focus` "The calm mind"): `PillBodyEvent.KingFocus`.
- **S_Fragment** (`end_S_Fragment` "A blank page"): a potion.

Test: TS-19.
