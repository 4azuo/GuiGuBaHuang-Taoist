using ModLib.Enum;
using ModLib.Helper;
using MOD_nE7UL2.Mod;
using System.Collections.Generic;

namespace MOD_nE7UL2.Story
{
    /// <summary>
    /// Line S "The Medicine King's Testament" (docs/Taoist/Storylines/S-Medicine-King.md): an old alchemist {O} keeps the torn
    /// testament of the Medicine King; opens at realm 5 (Golden Core) outside towns. Acts S0-S3: the first page in a beast's
    /// lair, the plague village (potions, spirit stones or walk on), the test of the poison (the player, {O} or nobody), the
    /// last page. Endings Heal (+30 % recovery of the potions) / Focus (half of the focus of a potion given back) /
    /// Fragment (the village was left: a pill, no perk). The perks are kept by PillBodyEvent. {O} may die on the way: the
    /// testament goes on (narration).
    /// </summary>
    public class LineS_MedicineKing : StoryLine
    {
        public override string Id => "S";
        public override int Acts => 4;
        public override float MonthlyChance => 0.5f;
        public const int MIN_GRADE = 5;
        public const int VILLAGE_COST = 20;     // x EncounterEvent.Cost()
        public const float POISON_HP = 0.3f;    // share of the HP lost drinking the poison
        public const int POISON_LIFE = 60;      // months of life of {O} testing it

        public override StoryRole[] Roles => new[]
        {
            new StoryRole { Name = "O", MinLived = 0.6f, MaxLived = 0.9f, NoSect = true, MaxGradeOffset = 0 },
        };

        public override bool CanOpen(StoryState s) => Player.GetGradeLvl() >= MIN_GRADE && PlayerTown() == null;

        /// <summary>The testament goes on without him.</summary>
        public override IEnumerable<string> VitalRoles => new string[0];

        public override int TimeLimit(int act) => act == 1 ? 24 : 0;

        /// <summary>{O} speaks when alive, else the step is told.</summary>
        private static void SayOrTell(StoryState s, string key, string[] opts, System.Action<int> pick, params object[] args)
        {
            var o = U(s, "O");
            if (o != null && !o.isDie) Say(s, "O", key, opts, pick, args);
            else Tell(s, key, opts, pick, BG, args);
        }

        public override void Run(StoryState s)
        {
            switch (s.Act * 100 + s.Step)
            {
                // ---- S0 The torn testament
                case 1:
                    U(s, "O")?.SetUnitPos(Player.GetUnitPos());
                    Say(s, "O", "S0_1", new[] { "S0_1_o1", "S0_1_o2" }, i =>
                    {
                        if (i == 0) Next(s); else Close(s, "refused");
                    });
                    break;
                case 2:
                    Tell(s, "S0_2", new[] { "S0_2_o1" }, _ => FightBeast(s, "S0_lair", 3));
                    break;

                // ---- S1 The plague village
                case 101:
                    var price = EncounterEvent.Cost(VILLAGE_COST);
                    Tell(s, "S1_1", new[] { "S1_1_o1", "S1_1_o2", "S1_1_o3" }, i =>
                    {
                        if (i == 0)
                        {
                            if (!Require(s, TakeItem(x => x.IsPotion() != null, 1), "S1_nopill", () => Run(s))) return;
                            s.Set("S1_saved"); AddIntim(s, "O", 100);
                        }
                        else if (i == 1)
                        {
                            if (!Pay(s, price)) return;
                            s.Set("S1_saved");
                        }
                        else s.Set("S1_left");
                        Act(s, 2, 6);
                    }, BG, price);
                    break;

                // ---- S2 The test of the poison
                case 201:
                    SayOrTell(s, "S2_1", new[] { "S2_1_o1", "S2_1_o2", "S2_1_o3" }, i =>
                    {
                        if (i == 0)
                        {
                            var hp = Player.GetProperty<int>(UnitPropertyEnum.Hp);
                            var lost = System.Math.Min((int)(hp * POISON_HP), System.Math.Max(0, hp - 1));
                            Player.SetProperty<int>(UnitPropertyEnum.Hp, hp - lost);
                            s.Set("S2_drank");
                            ConstitutionEvent.Instance?.Add(Player, ConstitutionEvent.Kind.Venom, ConstitutionEvent.VENOM_STORY_POINTS);
                        }
                        else if (i == 1)
                        {
                            U(s, "O")?.AddProperty<int>(UnitPropertyEnum.Life, -POISON_LIFE);
                            s.Set("S2_his");
                        }
                        else s.Set("S2_none");
                        Act(s, 3, 3);
                    });
                    break;

                // ---- S3 The last page
                case 301:
                    if (s.Has("S1_left") || s.Has("S2_none"))
                    {
                        Tell(s, "endS_Fragment", null, _ =>
                        {
                            Give(x => x.IsPotion() != null, Player.GetGradeLvl());
                            End(s, "S_Fragment");
                        });
                        break;
                    }
                    SayOrTell(s, "S3_1", new[] { "S3_1_o1", "S3_1_o2" }, i =>
                    {
                        if (i == 0) Tell(s, "endS_Heal", null, _ => { Reward(true); End(s, "S_Heal"); });
                        else Tell(s, "endS_Focus", null, _ => { Reward(false); End(s, "S_Focus"); });
                    });
                    break;
            }
        }

        /// <summary>The perk of the ending (PillBodyEvent): +30 % recovery, or half of the focus back.</summary>
        public static void Reward(bool heal)
        {
            if (PillBodyEvent.Instance == null) return;
            PillBodyEvent.Instance.KingHeal = heal;
            PillBodyEvent.Instance.KingFocus = !heal;
            TaoistLogEvent.Log(heal ? "log_pill_king_heal" : "log_pill_king_focus");
        }

        public override void OnBattle(StoryState s, string tag, bool won)
        {
            s.Set(tag, won ? 1 : -1);
            if (tag == "S0_lair")
            {
                if (won) Act(s, 1, 6);
                else Wait(s, 6);   // the page is still in the lair: the step comes again
                return;
            }
            Next(s);
        }

        /// <summary>{O} died: the line goes on, told.</summary>
        public override void OnRoleDead(StoryState s, string role) => s.Set("O_dead");
    }
}
