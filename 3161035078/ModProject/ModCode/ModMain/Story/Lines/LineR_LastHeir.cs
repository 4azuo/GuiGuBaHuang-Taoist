using ModLib.Helper;
using MOD_nE7UL2.Helper;
using MOD_nE7UL2.Mod;
using System.Collections.Generic;
using System.Linq;

namespace MOD_nE7UL2.Story
{
    /// <summary>
    /// Line R "For One Heir Only" (docs/Taoist/Storylines/R-Last-Heir.md; Dao of Creation only): the old master {O} looks
    /// for the one heir of an art not written yet; {W} is the other candidate. Acts R0-R4: the offer, three trials
    /// (patience, strength, heart), the theft {W} proposes, the art (created with Dao of Creation) and its heir, the art
    /// handed on years later. Endings Lineage / Buried / Stolen. Without Dao of Creation the line does not open.
    /// </summary>
    public class LineR_LastHeir : StoryLine
    {
        public override string Id => "R";
        public override int Acts => 5;
        public override float MonthlyChance => 0.5f;

        public override StoryRole[] Roles => new[]
        {
            new StoryRole { Name = "O", MinLived = 0.6f, MinGradeOffset = 0, MaxGradeOffset = 2, OutTraits = new[] { 13, 14 } },
            new StoryRole { Name = "W", MaxLived = 0.5f, MinGradeOffset = -1, MaxGradeOffset = 0 },
        };

        public override IEnumerable<string> VitalRoles => new[] { "O" };

        public override bool CanOpen(StoryState s) => DocHelper.Ready && Player.GetGradeLvl() >= 5;

        public override void OnCast(StoryState s) => U(s, "O")?.SetUnitPos(Player.GetUnitPos());

        /// <summary>{O} died: the line ends, unless the art is already handed over (R4).</summary>
        public override void OnRoleDead(StoryState s, string role)
        {
            if (s.Act >= 4) return;
            s.Act = 9;
            s.Step = 1;
            Wait(s, 0);
        }

        /// <summary>Who the player teaches: the first living member of the family, else {W}.</summary>
        private static WorldUnitBase Pupil(StoryState s) =>
            FamilyFundEvent.Members().FirstOrDefault(x => x != null && !x.isDie && !x.IsPlayer()) ?? U(s, "W");

        public override void Run(StoryState s)
        {
            if (!DocHelper.Ready && !StoryEvent.NoGates) { Wait(s, 12); return; }
            switch (s.Act * 100 + s.Step)
            {
                // ---- R0 The offer
                case 1:
                    Say(s, "O", "R0_1", new[] { "R0_1_o1", "R0_1_o2" }, i => { if (i == 0) Act(s, 1, 3); else Close(s, "refused"); });
                    break;

                // ---- R1 Three trials: patience, strength, heart
                case 101:
                    Say(s, "O", "R1_1", new[] { "R1_1_o1", "R1_1_o2" }, i =>
                    {
                        if (i == 0) { Count(s, "worth"); Next(s, 12); }
                        else Next(s, 1);
                    });
                    break;
                case 102:
                    Say(s, "W", "R1_2", new[] { "R1_2_o1" }, _ => Spar(s, U(s, "W"), "R1_duel"));
                    break;
                case 103:
                    Say(s, "W", "R1_3", new[] { "R1_3_o1", "R1_3_o2" }, i =>
                    {
                        if (i == 0) { if (!Offer(s, x => x.IsPill() != null, 1, "pill")) return; Count(s, "worth"); AddIntim(s, "W", 300); }
                        else AddIntim(s, "W", -200);
                        Act(s, 2, 6);
                    });
                    break;

                // ---- R2 The theft
                case 201:
                    Say(s, "W", "R2_1", new[] { "R2_1_o1", "R2_1_o2" }, i =>
                    {
                        if (i == 0) { Count(s, "worth"); AddIntim(s, "W", -100); }
                        else s.Set("theft");
                        Act(s, 3, 6);
                    });
                    break;

                // ---- R3 The art and its heir
                case 301:
                    if (!Forge(s, "O")) return;
                    Next(s);
                    break;
                case 302:
                    if (s.Has("theft"))
                        Tell(s, "endR_Stolen", null, _ =>
                        {
                            Teach(s, Player, false);
                            Teach(s, U(s, "W"), true);
                            AddIntim(s, "O", -1000);
                            if (DaoHeartEvent.Enabled) DaoHeartEvent.Add(Player, -200, "heart_story");
                            if (MeritEvent.Enabled) MeritEvent.Add(Player, -200, "merit_story");
                            End(s, "R_Stolen");
                        });
                    else if (s.Flag("worth") >= 2)
                        Say(s, "O", "R3_chosen", new[] { "R3_chosen_o1", "R3_chosen_o2" }, i =>
                        {
                            Teach(s, Player, true);
                            if (i == 1) { Teach(s, U(s, "W"), true); AddIntim(s, "W", 500); }
                            AddIntim(s, "O", 300);
                            Act(s, 4, 60);
                        });
                    else
                        Say(s, "O", "R3_passed", new[] { "R3_passed_o1", "R3_passed_o2" }, i =>
                        {
                            if (i == 0) { Teach(s, U(s, "W"), true); AddIntim(s, "W", 200); Teach(s, Player, false); Act(s, 4, 60); }
                            else Spar(s, U(s, "W"), "R3_duel");
                        });
                    break;

                // ---- R4 The art handed on
                case 401:
                    var pupil = Pupil(s);
                    var name = pupil?.GetName() ?? "?";
                    Tell(s, "R4_1", new[] { "R4_1_o1", "R4_1_o2" }, i =>
                    {
                        if (i == 0)
                        {
                            Teach(s, pupil, true);
                            Tell(s, "endR_Lineage", null, _ => { DocHelper.AddEssence(Player.GetUnitId(), 20f); End(s, "R_Lineage"); }, BG, name);
                        }
                        else Tell(s, "endR_Buried", null, _ => { DocHelper.AddEssence(Player.GetUnitId(), 40f); End(s, "R_Buried"); });
                    }, BG, name);
                    break;

                // ---- {O} died before the art was handed over
                case 901:
                    Tell(s, "R_dead", null, _ =>
                    {
                        if (!s.Roles.ContainsKey(ART)) { Close(s, "dead"); return; }
                        Teach(s, Player, false);
                        End(s, "R_Buried");
                    });
                    break;
            }
        }

        public override void OnBattle(StoryState s, string tag, bool won)
        {
            s.Set(tag, won ? 1 : -1);
            switch (tag)
            {
                case "R1_duel": if (won) Count(s, "worth"); Next(s, 3); break;
                case "R3_duel":
                    Teach(s, won ? Player : U(s, "W"), true);
                    if (!won) Teach(s, Player, false);
                    Act(s, 4, 60);
                    break;
                default: Next(s); break;
            }
        }
    }
}
