# SpiritlockEvent

`3161035078/ModProject/ModCode/ModMain/Mod/SpiritlockEvent.cs` - cache key `ModConst.SPIRITLOCK_EVENT` - group: Battle

Balance change for the spiritlock circle fight.

## Saved data

None.

## Flow

| Hook | What happens |
|---|---|
| `OnBattleUnitInto` | Monsters only (`UnitDataMonst`). Venerable tortoises (`unitAttrItem.id` 12005-12011): move speed 0, direction locked (`move.isLockDire`), defense halved, max HP x2 (`TORTOISE_HP_RATIO`). Spiritlock circle (12012): move speed 0. |

## Notes

Move speed 0 alone does not stop a tortoise from turning.

## Tests

TB-05, TM-24.
