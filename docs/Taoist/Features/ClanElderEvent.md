# ClanElderEvent

`3161035078/ModProject/ModCode/ModMain/Mod/ClanElderEvent.cs` (+ `ClanElderEvent.Window.cs`) - cache key
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
  (clan, salary) with Resign; each clan line lists its elders (`ClanLineSuffix`).
- Elders window (`OpenElders`): rules, the elders (realm, salary, loyalty in red when low; Dismiss), the candidates
  (realm, intimacy, salary, chance; Invite).
- War council (`OpenWarCouncil`): the hostile clans (relation, members, elders, chance to win; Declare war); the ballots
  (`ShowBallots`).

## Settings (`estate_configs.json`, `Elders`)

`Enabled`, `GradeOverArea`, `MaxElders`, `Salary`, `OfferingMonths`, `StartLoyalty`, `LoyaltyPerIntim`, `PaidLoyalty`,
`UnpaidLoyalty`, `WarLoyalty`, `SuccessionLoyalty`, `HeadVotes`, `FleeLoyalty`, `ElderDeath`, `HuntMonths`, `HuntChance`,
`HuntDeath`, `HuntWins`, `CompensationMonths`, `RecruitChance`, `InviteChance`, `InviteIntim`, `InviteReputation`,
`AcceptBase`, `AcceptIntim`, `AcceptReputation`, `AcceptHouse`, `AcceptGrade`.

## Tests

TG-88 (autotest: who can serve, the invitation, the Elders window, pay and loyalty, the war council, desertion and hunt,
an NPC clan inviting the player, resign); `FormulaTests.ClanElder_Formulas`. Promotion pictures: TR-04 (`elders`,
`elder-council`, `elder-invite`, `elder-hunt`).
