# ClanEvent

`3161035078/ModProject/ModCode/ModMain/Mod/ClanEvent.cs` - cache key `ModConst.CLAN_EVENT` - group: Economy (order 3022)

Cultivator clans (世家) of the towns. Builds on [EstateEvent](EstateEvent.md) (NPC cultivators buy houses) and
[FamilyEvent](FamilyEvent.md) (marriages, children).

## Clans

- A clan: a cultivator with a house in a town (an NPC or the player) and its living family: spouse, children,
  grandchildren (`Members`), at least `MinMembers` (2). Name: "the <head> clan".
- Relation of two clans of a town, -100..100 (`Relation`):
  `base + MemberWeight (0.5) x mean intimacy between their members (both ways, 8 x 8 at most) + MarriageBonus (25) x marriages between them`.
  The base starts at random in `StartRelation` (-40..40) and falls with their fights (`BrawlRelation` -15, a death in a
  brawl `DeathRelation` -40, a war `WarRelation` -50). States: blood feud (`WarAt` -70 or less), hostile (`HostileAt`
  -30 or less), allied (`AllyAt` 50 or more), neutral.
- Elders: outsiders who guard a clan, vote on its wars and fight in them: [ClanElderEvent](ClanElderEvent.md).
- The town map has a "Clans (n)" button (top left): a card a clan (6.1.01: the picture of its mansion by the level of
  its house, its name - gold for the player's -, its members and house, its elders), 5 a row, at most 10, the player's
  first; then "Relations between the clans": the worst 9 pairs in three columns (red hostile, green allied).

## Fights (yearly, very rare)

- Hostile clans brawl with `BrawlChance` (1 %) a year: the youngest member of each (not the player) fights; win chance =
  power / both powers (`UnitPowerCalHelper`). The loser is hurt (HP at most 1/5), dies with `BrawlDeath` (5 %); the two
  fighters' intimacy -30 both ways.
- Clans in a blood feud go to war with `WarChance` (0.2 %): all members' power; each loser dies with `WarDeath` (25 %),
  the others are hurt; each winner dies with a quarter of it; the losers' house goes to the mortals. A clan left without
  members is wiped out.
- The player never dies in a simulated fight.
- A war that wipes a clan out (no member left) leaves its mansion in ruins (see below); losers who survive lose their
  house to the mortals.
- The player's side: its own clan (the player has a house in the town and a family) and its allies there. When one side
  is the player's (and not in battle, not skipping time), a drama asks: step in (a real battle against the enemy's
  fighter, the player not killed; the result decides the fight) or stay out (decided at once). `Pending` keeps the fight
  until the battle ends (`OnBattleEndHandler`).
- Taoist log (icon zhandouxiaoguo): brawls when the player's side is in it or the player is in the town, deaths, wars,
  wiped-out clans (always).

## Succession and ruins

- The head dies (EstateEvent, the month of its town: the owner of a house is dead): `Successor` takes over the house (all
  its plots) and heads the clan: a son or daughter - a grown one (`HeirAge`), the strongest first, else the eldest
  child -, else a grown grandchild, else a brother or sister (of blood first, then sworn), else the spouse, else a young
  grandchild; never one with a house of its own in the town. `Transfer`: the plots, the relations with the other clans
  (`Bases` keys renamed), the elders and hunts (ClanElderEvent.OnSuccession); the family of the old head stays in the
  clan (`Kin`: head id -> former heads' family, counted in `Members` with their children). Taoist log
  `log_clan_succession`; the player as the heir gets a message.
- The player at the end of its life goes on as one of its descendants (FamilyEvent.Inherit, S&M "Continue as
  descendant"; the player's unit keeps its id) - `OnPlayerInherit`, in each town where the player heads a clan: the
  chosen one is the next head (`Successor`) -> the player goes on heading the clan, the old family stays (`Kin`);
  another one -> the house and the clan pass to the next head, the player is a member of its clan. The chosen one's own
  houses become the player's. The message is added to the inheritance drama.
- No one left: the clan is gone, its mansion stands in ruins (`EstateEvent.Ruin`: owner `EstateEvent.RUIN`, `RuinOf`
  the clan's name, `RuinSince`; drawn greyed with "Ruins", ground color `Ruin`); mortals neither live nor build there.
  Yearly (`YearRuins`): a ruin past `RuinMonths` is split into plots for the mortals (`Release`); else with
  `RuinTakeChance` the richest NPC clan of the town with a smaller house that can pay `RuinPrice` x the land price takes
  it as its seat (the town budget gets the price, its old house goes to the mortals). A click on a ruin tells whose it
  was and when it will be split; the player without a house in the town may restore it as its seat (banknotes).

## Settings (`estate_configs.json`, `Clans`)

`Enabled`, `MinMembers`, `StartRelation`, `MemberWeight`, `MarriageBonus`, `HostileAt`, `WarAt`, `AllyAt`,
`BrawlChance`, `WarChance`, `BrawlDeath`, `WarDeath`, `BrawlRelation`, `DeathRelation`, `WarRelation`, `HeirAge`,
`RuinMonths`, `RuinTakeChance`, `RuinPrice`.

## Tests

TG-72 (autotest: two clans, the window, a brawl, the question, a war that wipes a clan out, its mansion in ruins);
TG-89 (succession: a grown child, a sibling, the elder and the feud follow, a ruin taken by a clan or split); TG-74 (in depth: the members'
intimacy and a marriage move the relation, a year of the town by pair, the player's clan and an ally, "step in" and the
real battle won, the player never dies in a lost war); `FormulaTests.Clan_Formulas`. Promotion pictures: TR-04 (`clan`, `clan-war-call`, `clan-battle`, `clan-victory`, `clan-extinct`: the 6.0 set), TR-03 (`clans`,
`clan-war`).
