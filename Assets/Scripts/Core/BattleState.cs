using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreeColorCard
{
    [Flags]
    public enum CardColors { Red = 1, Blue = 2, Yellow = 4 }

    public sealed class BattleCard
    {
        public int Id { get; private set; }
        public CardColors Colors { get; private set; }
        public int Points { get; set; }
        public BattleCard(int id, CardColors colors, int points)
        { Id = id; Colors = colors; Points = points; }
    }

    public sealed class ScoreResult
    {
        public int BasePoints;
        public int Multiplier;
        public decimal Score;
        public decimal Coins;
        public decimal Energy;
    }

    // Pure C# rules: no scene references or changes to ScriptableObject assets.
    public sealed class BattleState
    {
        private readonly Random random;
        private readonly List<BattleCard> draw = new List<BattleCard>();
        private readonly List<BattleCard> discard = new List<BattleCard>();
        private readonly List<BattleCard> hand = new List<BattleCard>();
        private readonly List<BattleCard> scoring = new List<BattleCard>();
        public IReadOnlyList<BattleCard> Hand { get { return hand.AsReadOnly(); } }
        public IReadOnlyList<BattleCard> Scoring { get { return scoring.AsReadOnly(); } }
        public int DrawCount { get { return draw.Count; } }
        public int DiscardCount { get { return discard.Count; } }
        public int ShuffleCount { get; private set; }
        // 0 = primary; 1..3 = scoring phases; 4 = finished.
        public int Phase { get; private set; }
        public bool Finished { get { return Phase == 4; } }
        public bool Won { get { return Finished && Score >= Target; } }
        public decimal Target { get; private set; }
        public decimal Score { get; private set; }
        public decimal Coins { get; private set; }
        public decimal Energy { get; private set; }
        public ScoreResult LastResult { get; private set; }

        public BattleState(int seed, decimal target = 60)
        {
            random = new Random(seed);
            Target = target;
            int id = 0;
            foreach (CardColors color in new[] { CardColors.Red, CardColors.Blue, CardColors.Yellow })
                for (int points = 1; points <= 5; points++)
                    draw.Add(new BattleCard(id++, color, points));
            Shuffle();
            Draw(7);
        }

        private void Shuffle()
        {
            for (int i = draw.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                BattleCard temp = draw[i]; draw[i] = draw[j]; draw[j] = temp;
            }
        }

        private void Draw(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (draw.Count == 0)
                {
                    if (discard.Count == 0) break;
                    draw.AddRange(discard); discard.Clear(); Shuffle(); ShuffleCount++;
                }
                hand.Add(draw[draw.Count - 1]); draw.RemoveAt(draw.Count - 1);
            }
        }

        public bool MoveScoringCard(int index, int direction)
        {
            int destination = index + direction;
            if (Finished || Math.Abs(direction) != 1 || index < 0 || index >= scoring.Count ||
                destination < 0 || destination >= scoring.Count) return false;
            BattleCard temp = scoring[index]; scoring[index] = scoring[destination]; scoring[destination] = temp;
            return true;
        }

        // Selection order is significant: incoming cards are appended in that order.
        // Prepare and settle are separate so all five cards can be reordered before scoring.
        public bool Prepared { get; private set; }
        private BattleCard[] previousHand;
        private BattleCard[] previousScoring;
        private BattleCard[] previousDiscard;

        public bool CancelPreparation()
        {
            if (Finished || !Prepared) return false;
            hand.Clear(); hand.AddRange(previousHand);
            scoring.Clear(); scoring.AddRange(previousScoring);
            discard.Clear(); discard.AddRange(previousDiscard);
            ClearPreparation();
            return true;
        }

        private void ClearPreparation()
        {
            Prepared = false;
            previousHand = previousScoring = previousDiscard = null;
        }

        public bool Prepare(IList<int> incomingIds, IList<int> outgoingIds)
        {
            if (Finished || Prepared || incomingIds == null || outgoingIds == null) return false;
            int required = Phase == 0 ? 3 : 2;
            if (incomingIds.Count != required || incomingIds.Distinct().Count() != required ||
                incomingIds.Any(id => !hand.Any(c => c.Id == id))) return false;
            int replacements = Phase >= 2 ? 2 : 0;
            if (outgoingIds.Count != replacements || outgoingIds.Distinct().Count() != replacements ||
                outgoingIds.Any(id => !scoring.Any(c => c.Id == id))) return false;

            previousHand = hand.ToArray();
            previousScoring = scoring.ToArray();
            previousDiscard = discard.ToArray();
            foreach (int id in outgoingIds)
            {
                BattleCard card = scoring.First(c => c.Id == id); scoring.Remove(card); discard.Add(card);
            }
            foreach (int id in incomingIds)
            {
                BattleCard card = hand.First(c => c.Id == id); hand.Remove(card); scoring.Add(card);
            }
            discard.AddRange(hand); hand.Clear();
            Prepared = true;
            return true;
        }

        public bool Confirm()
        {
            if (Finished || !Prepared) return false;
            if (Phase > 0)
            {
                LastResult = Calculate(scoring);
                Score += LastResult.Score; Coins += LastResult.Coins; Energy += LastResult.Energy;
                // Exact color sets define distinctness in this prototype.
                if (scoring.Select(c => c.Colors).Distinct().Count() == scoring.Count)
                    foreach (BattleCard card in scoring) card.Points++;
            }
            Phase++; ClearPreparation();
            if (!Finished) Draw(5);
            return true;
        }

        public static ScoreResult Calculate(IReadOnlyList<BattleCard> cards)
        {
            if (cards == null || cards.Count != 5) throw new ArgumentException("Five scoring cards required.");
            int sum = cards.Sum(c => c.Points);
            int multiplier = cards.All(c => c.Colors == cards[0].Colors) ? 2 : 1;
            decimal amount = sum * multiplier;
            CardColors main = cards[cards.Count - 1].Colors;
            return new ScoreResult
            {
                BasePoints = sum, Multiplier = multiplier,
                Score = amount * ((main & CardColors.Red) != 0 ? 1.5m : 1m),
                Coins = (main & CardColors.Yellow) != 0 ? amount * 0.5m : 0m,
                Energy = (main & CardColors.Blue) != 0 ? amount : 0m
            };
        }
    }
}
