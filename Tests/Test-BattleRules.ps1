$ErrorActionPreference = 'Stop'
$core = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../Assets/Scripts/Core/BattleState.cs')
$tests = @"
public static class BattleRuleChecks
{
    private static void Check(bool value, string message) { if (!value) throw new System.Exception(message); }
    private static void Conservation(ThreeColorCard.BattleState battle)
    {
        Check(battle.Hand.Count + battle.Scoring.Count + battle.DrawCount + battle.DiscardCount == 15, "Cards lost or duplicated");
        Check(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(System.Linq.Enumerable.Concat(battle.Hand, battle.Scoring), c => c.Id)).Count() == battle.Hand.Count + battle.Scoring.Count, "Duplicate visible card");
    }
    public static string Run()
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            var battle = new ThreeColorCard.BattleState(seed);
            Check(battle.Hand.Count == 7 && battle.Score == 0, "Initial deal");
            Check(!battle.Confirm(), "Must prepare first");
            Check(!battle.Prepare(new int[] { battle.Hand[0].Id, battle.Hand[0].Id, battle.Hand[1].Id }, new int[0]), "Duplicate selection accepted");
            decimal total = 0;
            for (int phase = 0; phase < 4; phase++)
            {
                Conservation(battle);
                var ids = System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(battle.Hand, phase == 0 ? 3 : 2), c => c.Id).ToArray();
                var removed = phase >= 2 ? System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(battle.Scoring, 2), c => c.Id).ToArray() : new int[0];
                var originalHand = battle.Hand.Select(c => c.Id).ToArray();
                var originalScoring = battle.Scoring.Select(c => c.Id).ToArray();
                int originalDiscard = battle.DiscardCount;
                decimal originalScore = battle.Score;
                Check(battle.Prepare(ids, removed), "Valid selection rejected");
                if (phase >= 2)
                {
                    Check(removed.All(id => battle.Scoring.All(c => c.Id != id)), "Removed cards still scoring");
                    Check(ids.All(id => battle.Scoring.Any(c => c.Id == id)), "Incoming cards missing");
                    Check(battle.Scoring.Count == 5, "Replacement changed scoring count");
                    Check(battle.MoveScoringCard(0, 1), "Reorder before undo");
                    Check(battle.CancelPreparation(), "Cannot return to selection");
                    Check(battle.Hand.Select(c => c.Id).SequenceEqual(originalHand), "Undo changed original hand");
                    Check(battle.Scoring.Select(c => c.Id).SequenceEqual(originalScoring), "Undo changed original scoring order");
                    Check(battle.DiscardCount == originalDiscard && battle.Score == originalScore && battle.Phase == phase, "Undo changed resources or phase");
                    Check(!battle.CancelPreparation(), "Undo accepted twice");
                    Conservation(battle);
                    // Choose different outgoing cards after returning, not just the same pair.
                    removed = battle.Scoring.Skip(2).Take(2).Select(c => c.Id).ToArray();
                    Check(battle.Prepare(ids, removed), "Cannot replace again after returning");
                    Check(removed.All(id => battle.Scoring.All(c => c.Id != id)), "Reselected cards still scoring");
                }
                Check(!battle.Prepare(ids, removed), "Prepared twice");
                Conservation(battle);
                Check(battle.MoveScoringCard(0, 1), "Reordering failed");
                Check(!battle.MoveScoringCard(0, -1), "Invalid reorder accepted");
                if (phase > 0) total += ThreeColorCard.BattleState.Calculate(battle.Scoring).Score;
                Check(battle.Confirm(), "Confirmation failed");
                Check(!battle.CancelPreparation(), "Undo allowed after settlement");
                Check(battle.Score == total, "Incorrect cumulative score");
                if (phase == 0) Check(battle.Score == 0 && battle.LastResult == null, "Primary phase scored");
                if (phase < 3) Check(battle.Hand.Count == 5, "Secondary draw count");
                Conservation(battle);
            }
            Check(battle.Finished && battle.ShuffleCount > 0, "End or reshuffle failed");
            Check(!battle.Confirm(), "Repeated final scoring accepted");
            Check(battle.Won == (battle.Score >= battle.Target), "Win condition");
        }
        var cards = new System.Collections.Generic.List<ThreeColorCard.BattleCard>();
        for (int i = 0; i < 5; i++) cards.Add(new ThreeColorCard.BattleCard(i, ThreeColorCard.CardColors.Red, 3));
        var red = ThreeColorCard.BattleState.Calculate(cards);
        Check(red.Score == 45 && red.Coins == 0 && red.Energy == 0, "Same color red multiplier");
        cards[4] = new ThreeColorCard.BattleCard(4, ThreeColorCard.CardColors.Yellow, 3);
        var yellow = ThreeColorCard.BattleState.Calculate(cards);
        Check(yellow.Score == 15 && yellow.Coins == 7.5m, "Yellow fractional income");
        cards[4] = new ThreeColorCard.BattleCard(4, ThreeColorCard.CardColors.Red | ThreeColorCard.CardColors.Blue | ThreeColorCard.CardColors.Yellow, 3);
        var fused = ThreeColorCard.BattleState.Calculate(cards);
        Check(fused.Score == 22.5m && fused.Energy == 15 && fused.Coins == 7.5m, "Fused color effects");
        Check(new ThreeColorCard.BattleState(42).Hand.Select(c => c.Id).SequenceEqual(new ThreeColorCard.BattleState(42).Hand.Select(c => c.Id)), "Seed reproducibility");
        return "PASS: 1000 full battles; conservation, selection validation, ordering, reshuffling, three settlements, color effects, fractional rewards, deterministic seeds.";
    }
}
"@
Add-Type -TypeDefinition ($core + [Environment]::NewLine + $tests)
[BattleRuleChecks]::Run()
