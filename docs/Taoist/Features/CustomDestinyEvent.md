# CustomDestinyEvent

`3161035078/ModProject/ModCode/ModMain/Mod/CustomDestinyEvent.cs` - cache key `ModConst.CUSTOM_DESTINY_EVENT` (Global, works everywhere) - group: UI

Character creation: any number of born destinies chosen by the player, on top of the 3 rolled by the game (still required), paid by the S&M score (P) of the new save.

## Saved data

None: `Picked` is in memory only, cleared when the creation screen opens and after the new game's first map. The destinies are saved by the game (`propertyData.bornLuck`), the score in [SMLocalConfigsEvent](SMLocalConfigsEvent.md).

## Penalty

`Penalty(level) = 2,000 + 1,000 x quality` (1 white = 3,000 .. 6 red = 8,000), each. A destiny also rolled by the game is not given twice and not paid.

## Flow

| Hook | What happens |
|---|---|
| `OnOpenUIEnd(CreatePlayer)` | button (`newgame010`, with the count and cost once chosen); the first click tells about the score (Yes / No) |
| window (`Picker`) | `roleCreateFeature` of `type == 1`, best quality first then by name; filters: name, description, quality; the chosen ones in the right column (click: removed); the score before / after in red. OK keeps the choice, Cancel drops it |
| `OnLoadNewGame` | in the first month only: `Apply` - born luck added, effects created (`CreateLuck` + `NewCreateLuck`), `Configs.Score` lowered, Taoist log. Runs before the start bonus of [SMScoreEvent](SMScoreEvent.md) |

## Tests

TT-10, TG-99, unit tests `CustomDestinyTests`.
