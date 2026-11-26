# UIDifficultyEvent

`3161035078/ModProject/ModCode/ModMain/Mod/UIDifficultyEvent.cs` - cache key `ModConst.UI_DIFFICULTY_EVENT` (Global) - group: UI

Replaces the descriptions of the game's own difficulty levels by what the difficulty does in Taoist (S&M not included) and the highest S&M score recommended with it (`RECOMMENDED_P`) ([SMGlobalConfigsEvent](SMGlobalConfigsEvent.md)).

## Flow

| Hook | What happens |
|---|---|
| `OnOpenUIEnd(Difficulty)` | For each difficulty item: `goRoot1`, `goRoot1_En`, `goRoot2`, `goRoot2_En` hidden; a text `TaoistDifficultyDesc` added (`Description(gameLvl)`, keys `smcfgs222`-`smcfgs224`, without the first paragraph of `smcfgs222`). That paragraph (`Note`: "The difficulty also raises the stats... (S&M Configs not included)") is shown once, a red line `TaoistDifficultyNote` right aligned at the top right (from the close button: `NOTE_DX`, `NOTE_DY`), over the cards; checked by TT-03. |

No saved data, no switch.

## Tests

TT-03, TM-01.
