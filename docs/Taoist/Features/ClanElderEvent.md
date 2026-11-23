# ClanElderEvent

`3161035078/ModProject/ModCode/ModMain/Mod/ClanElderEvent.cs` (+ `ClanElderEvent.Window.cs`, `ClanElderEvent.Battle.cs`) - cache key
`ModConst.CLAN_ELDER_EVENT` - group: Economy (order 3023)

Clan elders (6.1.0): cultivators of no kin who guard a clan of [ClanEvent](ClanEvent.md), paid by it. They have no say
in its affairs but vote on its wars and fight in them; one who flees a war is hunted as a deserter.

## Saved data

| Property | Meaning |
|---|---|
| `Elders` | `List<Elder>`: TownId, Head (the clan), UnitId, Since (game month), Loyalty 0..100, Unpaid (months in a row) |
| `Hunts` | `List<Hunt>`: TownId, Head, TargetId (the deserter), Since, Salary (its last salary), Wins (attacks it beat off) |
| `Asked` | unit id -> the year the player last invited it |
| `InvitedYear` | the year an NPC clan last invited the player |
| `PendingHunt` | the hunter's attack the player is fighting (resolved after the battle) |
| `PendingCouncil` | a war council waiting for the player's vote (TownId, Head, Enemy) |

## Rules

- **Who can serve** (`CannotServe`, a LocalText key or null): alive; not kin of the head (`IsKin`: a member of its clan,
  of its blood, a sibling); realm >= `MinGrade(town)` = area of the town + `GradeOverArea` (1..10); no other post (one
  clan at most); not the head of a clan of the same town; not a deserter of this clan; room: `MaxElders` by the level of
  the clan's house.
- **Pay**: monthly `Salary(unit)` = `Salary[grade]` = 5 x 3^grade (inflation applied), a yearly offering of
  `OfferingMonths` salaries. Balance: the income yardstick of the estates is 3^grade x 1,000 a year; a player of grade g
  lives in area g, its elders are of grade g+1 or more: one costs about 21 % of its income (with the offering), two
  42 %, four (an immortal palace) 84 %, the same share at every grade; the house tax is 1-5 % of it.
  The player's clan pays only with the money the player has; an NPC clan always pays the player (from its head as far as
  it can). Loyalty: `StartLoyalty` + intimacy with the head x `LoyaltyPerIntim`; +`PaidLoyalty` a paid month,
  -`UnpaidLoyalty` an unpaid one (at 0 the elder leaves); +-`WarLoyalty` a war won / lost; -`SuccessionLoyalty` when a
  new head takes over.
- **War council** (`Vote`): the head has `HeadVotes` votes, each elder 1 (for with `VoteYesChance(loyalty, our power,
  their power)`); a war needs more than half of all the votes (`Passes`). The player's clan declares war from the
  Clans window (War council button: the hostile clans, Declare war -> the ballots window -> To war: the battle against
  the enemy's champion). An NPC clan's yearly war (ClanEvent.YearTown) is voted the same way (`WarAllowed`); when the
  player is one of its elders a drama asks for its vote.
- **In a war**: the elders' power counts for their clan (`ClanEvent.SidePower`); before it, an elder whose loyalty is
  under `FleeLoyalty` flees with `FleeChance` (a deserter); after it, the losers' elders fall with WarDeath x
  `ElderDeath` (never the player), the others are hurt.
- **In the battle of a clan war** (6.1.01, `ClanElderEvent.Battle.cs`): the real battle the player fights in a war
  (`ClanEvent.Pending` with `War`: the player steps in, or its clan declared the war). `OnBattleStart` keeps the war and
  the power of both sides (`ClanEvent.SidePower`); `JoinSeconds` after the start the elders of both clans (not the
  player) join (`BattleEvent.NPCJoin`): those of the player's side as allies, the others as enemies.
- **Betrayal in the battle** (`TryBetray`, each second): an elder turns on its clan only when **all** hold
  (`BetrayChance`):
  - a motive (`BetrayMotive`): a nature that betrays (`BadNature`: inner trait selfish or evil, an outer trait
    power-hungry, as the team members of BattleEvent), unpaid months, or an intimacy with the other head `BetrayIntimGap`
    above the one with its own head;
  - `BetrayAfterSeconds` of battle passed; loyalty under `BetrayLoyalty`; served less than `BetrayLoyalMonths`;
  - its side's leader hurt: the player (for the elders of the player's side) or the enemy's champion at `BetrayLeaderHp`
    of its HP or less;
  - the other side at least `BetrayPowerRatio` x the power of its own (at the start of the war).

  Then the chance a second is `BetrayRate x (BetrayLoyalty - loyalty) / BetrayLoyalty`, `BetrayMax` betrayals a battle.
  An elder of the player's side who betrays becomes an enemy (its battle unit changes sides), +200 hate both ways; an
  enemy elder who betrays fights beside the player, +30 intimacy both ways. Either way it leaves its clan as a traitor
  (`Outlaw`: hunted like a deserter, its old clan's intimacy -100), a drama in the battle (`elder_betray_ours` /
  `elder_betray_theirs`), Taoist log `log_clan_elder_betrayed`. After the battle (`EndWarBattle`): a turncoat alive
  whose new side won becomes an elder of that clan when it can serve there (`log_clan_elder_turncoat`; the player's
  clan: message `elder_turncoat_joined`). An elder who fled before the war (`FleeLoyalty`) is not in the battle.
- **Desertion** (`Desert`): the elder leaves, the clan members' intimacy with it -100, a hunt for `HuntMonths`. The
  player deserts by staying out of a war of the clan it guards (ClanEvent.OnAnswer). Each month a hunt attacks with
  `HuntChance`: the hunter is the strongest member or elder of the clan; an NPC deserter beaten dies with `HuntDeath`;
  the player is ambushed (drama): fight (a real battle, not killed; `HuntWins` fights won end the hunt) or pay
  `CompensationMonths` salaries to the clan.
- **Invitations**: the player invites from the Elders window of its clan (the candidates of the area, with their chance
  `AcceptChance(intimacy, reputation, house level, realms above the player)`, once a year each). NPC clans take an NPC
  elder with `RecruitChance` a year, and invite the player (`InviteChance` a year, at most one a year) when the head's
  intimacy with it is >= `InviteIntim`, its reputation >= `InviteReputation` and it could serve. The player may resign
  in peace (Clans window).
- **Succession** (`OnSuccession`): the elders and the hunts follow the new head of the clan.
- **The player goes on as its child** (`OnPlayerInherit`): the old player's post and the hunts on it end; the child's
  are the player's.

## Windows

- Clans window (town map, `AddClansButtons`): "Elders (n/max)" and "War council" for the player's clan; the player's post
  (clan, salary) with Resign; each clan card lists its elders (`ElderNames`).
- Elders window (`OpenElders`, 6.1.01 cards): rules, then a card per elder (portrait - `InfoSheet.Portrait`, a click
  opens its information -, name, realm and salary, loyalty in red when low / green; Dismiss), then a card per candidate
  (portrait, name, realm and intimacy, salary and the chance it accepts - red at 0 %, green from 50 %; Invite).
- War council (`OpenWarCouncil`): the hostile clans (relation, members, elders, chance to win; Declare war); the ballots
  (`ShowBallots`).

## Settings (`estate_configs.json`, `Elders`)

`Enabled`, `GradeOverArea`, `MaxElders`, `Salary`, `OfferingMonths`, `StartLoyalty`, `LoyaltyPerIntim`, `PaidLoyalty`,
`UnpaidLoyalty`, `WarLoyalty`, `SuccessionLoyalty`, `HeadVotes`, `FleeLoyalty`, `ElderDeath`, `HuntMonths`, `HuntChance`,
`HuntDeath`, `HuntWins`, `CompensationMonths`, `RecruitChance`, `InviteChance`, `InviteIntim`, `InviteReputation`,
`AcceptBase`, `AcceptIntim`, `AcceptReputation`, `AcceptHouse`, `AcceptGrade`; in the battle: `BattleJoin` (true),
`JoinSeconds` (3), `BetrayAfterSeconds` (10), `BetrayLoyalty` (30), `BetrayLoyalMonths` (120), `BetrayIntimGap` (100),
`BetrayLeaderHp` (0.5), `BetrayPowerRatio` (0.8), `BetrayRate` (0.05 a second), `BetrayMax` (1; 0: never).

## Tests

TG-90 (autotest: the elder joins the war battle, no betrayal at full HP, betrays under half of the HP, hunted, one a battle);
TG-88 (autotest: who can serve, the invitation, the Elders window, pay and loyalty, the war council, desertion and hunt,
an NPC clan inviting the player, resign); `FormulaTests.ClanElder_Formulas`, `FormulaTests.ClanElder_Betrayal`. Promotion pictures: TR-04 (`elders`,
`elder-council`, `elder-invite`, `elder-hunt`).
