using MOD_nE7UL2.Const;
using ModLib.Attributes;
using ModLib.Helper;
using ModLib.Mod;
using System.Linq;

namespace MOD_nE7UL2.Mod
{
    /// <summary>
    /// Spiritlock circle balance: venerable tortoises cannot move or turn, have half defense and twice the max HP.
    /// </summary>
    [Cache(ModConst.SPIRITLOCK_EVENT)]
    public class SpiritlockEvent : ModEvent
    {
        public static SpiritlockEvent Instance { get; set; }

        public static int[] VENERABLE_TORTOISES = new int[]
        {
            12005,
            12006,
            12007,
            12008,
            12009,
            12010,
            12011
        };

        /// <summary>Max HP of the tortoises.</summary>
        public const int TORTOISE_HP_RATIO = 2;

        public const int SPIRITLOCK_CIRCLE = 12012;

        /// <summary>
        /// Applies the changes.
        /// </summary>
        public override void OnBattleUnitInto(UnitCtrlBase e)
        {
            base.OnBattleUnitInto(e);
            var data = e.data.TryCast<UnitDataMonst>();

            if (data != null)
            {
                if (VENERABLE_TORTOISES.Contains(data.unitAttrItem.id))
                {
                    e.data.moveSpeed.baseValue = 0;
                    e.data.defense.baseValue /= 2;
                    e.data.maxHP.baseValue = ValueHelper.SafeMulInt(e.data.maxHP.baseValue, TORTOISE_HP_RATIO);
                    e.data.hp = e.data.maxHP.value;
                    // move speed 0 does not stop it from turning to face its targets
                    e.move.isLockDire = true;
                }
                else if (data.unitAttrItem.id == SPIRITLOCK_CIRCLE)
                {
                    e.data.moveSpeed.baseValue = 0;
                }
            }
        }
    }
}
