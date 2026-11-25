using ModLib.Helper;
using MOD_nE7UL2.Helper;
using MOD_nE7UL2.Mod;

namespace MOD_nE7UL2.Story
{
    /// <summary>
    /// Line Q "The Nameless Art" (docs/Taoist/Storylines/Q-Nameless-Art.md; Dao of Creation only): {D} was cast out of a
    /// sect for changing its founder's art. Acts Q0-Q3: the outcast, the first art of {D} (created with Dao of Creation),
    /// the sect claims it (2 years), the manual; Q4: the revenge of a betrayed {D}. Endings Founder / Lost / Traitor.
    /// Without Dao of Creation the line does not open, and an open one waits.
    /// </summary>
    public class LineQ_NamelessArt : StoryLine
    {
        public override string Id => "Q";
        public override int Acts => 4;
        public override float MonthlyChance => 0.6f;

        public override StoryRole[] Roles => new[]
        {
            new StoryRole { Name = "D", NoSect = true, MinLived = 0.1f, MaxLived = 0.6f, MinGradeOffset = -1, MaxGradeOffset = 1, OutTraits = new[] { 8, 15 } },
        };

        public override bool CanOpen(StoryState s) => DocHelper.Ready && Player.GetGradeLvl() >= 3;

        public override void OnCast(StoryState s) => U(s, "D")?.SetUnitPos(Player.GetUnitPos());

        public override int TimeLimit(int act) => act == 2 ? 24 : 0;

        /// <summary>Q2 late: the sect took the manual by force.</summary>
        public override void OnLate(StoryState s)
        {
            if (s.Act != 2) return;
            s.Set("seized");
            Act(s, 3);
        }

        /// <summary>{D} died: what is left of the art goes to the player (betrayed and killed: the Traitor ending).</summary>
        public override void OnRoleDead(StoryState s, string role)
        {
            if (s.Act == 4) { Tell(s, "endQ_Traitor", null, _ => End(s, "Q_Traitor")); return; }
            if (!s.Roles.ContainsKey(ART)) { Close(s, "dead"); return; }
            s.Act = 9;
            s.Step = 1;
            Wait(s, 0);
        }

        public override void Run(StoryState s)
        {
            // the mod was removed: the line waits for it (the tests of the dialogue go on without)
            if (!DocHelper.Ready && !StoryEvent.NoGates) { Wait(s, 12); return; }
            var price = EncounterEvent.Cost(10);
            switch (s.Act * 100 + s.Step)
            {
                // ---- Q0 The outcast
                case 1:
                    Say(s, "D", "Q0_1", new[] { "Q0_1_o1", "Q0_1_o2", "Q0_1_o3" }, i =>
                    {
                        if (i == 0) { AddIntim(s, "D", 100); Next(s); }
                        else if (i == 1) Spar(s, U(s, "D"), "Q0_spar");
                        else Close(s, "ignored");
                    });
                    break;
                case 2:
                    Say(s, "D", "Q0_2", null, _ => Act(s, 1, 6));
                    break;

                // ---- Q1 The first art
                case 101:
                    Say(s, "D", "Q1_1", new[] { "Q1_1_o1", "Q1_1_o2" }, i =>
                    {
                        if (i == 0) { if (!Offer(s, x => x.IsPill() != null, 1, "pill")) return; AddIntim(s, "D", 200); Next(s); }
                        else FightBeast(s, "Q1_guard", 1);
                    });
                    break;
                case 102:
                    if (!Forge(s, "D")) return;
                    Say(s, "D", s.Roles.ContainsKey(ART) ? "Q1_2" : "Q1_2_none", null, _ => Act(s, 2, 12));
                    break;

                // ---- Q2 The sect claims it (2 years)
                case 201:
                    Say(s, "D", "Q2_1", new[] { "Q2_1_o1", "Q2_1_o2", "Q2_1_o3" }, i =>
                    {
                        if (i == 0) Fight(s, Foe(s), "Q2_sect");
                        else if (i == 1) { if (!Pay(s, price)) return; AddIntim(s, "D", 100); Act(s, 3, 12); }
                        else { AddIntim(s, "D", -800); Act(s, 4, 24); }
                    }, price);
                    break;

                // ---- Q3 The manual
                case 301:
                    if (s.Has("seized"))
                    {
                        Say(s, "D", "endQ_Lost", null, _ => { Teach(s, Player, false); End(s, "Q_Lost"); });
                        return;
                    }
                    Say(s, "D", "Q3_1", new[] { "Q3_1_o1", "Q3_1_o2" }, i =>
                    {
                        s.Set(i == 0 ? "learnt" : "kept");
                        Next(s);
                    });
                    break;
                case 302:
                    Say(s, "D", "endQ_Founder", null, _ =>
                    {
                        Teach(s, Player, s.Has("learnt"));
                        if (s.Has("kept")) DocHelper.AddEssence(Player.GetUnitId(), 20f);
                        AddIntim(s, "D", 400);
                        End(s, "Q_Founder");
                    });
                    break;

                // ---- Q4 The betrayed
                case 401:
                    Say(s, "D", "Q4_1", new[] { "Q4_1_o1" }, _ => Fight(s, U(s, "D"), "Q4_revenge"));
                    break;

                // ---- {D} died
                case 901:
                    Tell(s, "Q_dead", null, _ => { Teach(s, Player, false); End(s, "Q_Lost"); });
                    break;
            }
        }

        public override void OnBattle(StoryState s, string tag, bool won)
        {
            s.Set(tag, won ? 1 : -1);
            switch (tag)
            {
                case "Q0_spar": AddIntim(s, "D", 50); Next(s); break;
                case "Q1_guard": if (won) { AddIntim(s, "D", 250); Next(s); } else Wait(s, 3); break;
                case "Q2_sect":
                    if (won) AddIntim(s, "D", 300); else s.Set("seized");
                    Act(s, 3, 12);
                    break;
                case "Q4_revenge": Tell(s, "endQ_Traitor", null, _ => End(s, "Q_Traitor")); break;
                default: Next(s); break;
            }
        }

        /// <summary>Founder: the school of {D} sends its insights every year.</summary>
        public override void Yearly(StoryState s)
        {
            if (s.Ending == "Q_Founder") DocHelper.AddEssence(Player.GetUnitId(), 5f);
        }
    }
}
