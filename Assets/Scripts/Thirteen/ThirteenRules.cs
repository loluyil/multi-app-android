using System;
using System.Collections.Generic;
using System.Linq;

public static class ThirteenRules
{
    public enum HandType
    {
        Invalid = 0,
        Single = 1,
        Pair = 2,
        Triple = 3,
        FourOfAKind = 4,
        Straight = 5,
        PairSequence = 6
    }

    public readonly struct AnalyzedHand
    {
        public readonly HandType Type;
        public readonly int CardCount;
        public readonly int PrimaryRankStrength;
        public readonly int HighestSuitStrength;
        public readonly int SequenceLength;
        public readonly bool IsChop;

        public bool IsValid => Type != HandType.Invalid;

        public AnalyzedHand(
            HandType type,
            int cardCount,
            int primaryRankStrength,
            int highestSuitStrength,
            int sequenceLength,
            bool isChop)
        {
            Type = type;
            CardCount = cardCount;
            PrimaryRankStrength = primaryRankStrength;
            HighestSuitStrength = highestSuitStrength;
            SequenceLength = sequenceLength;
            IsChop = isChop;
        }
    }

    public static AnalyzedHand Analyze(IReadOnlyList<Card.CardData> cards)
    {
        if (cards == null || cards.Count == 0)
            return InvalidHand(cards?.Count ?? 0);

        List<Card.CardData> orderedCards = SortCards(cards);
        int cardCount = orderedCards.Count;

        if (cardCount == 1)
        {
            Card.CardData single = orderedCards[0];
            return new AnalyzedHand(
                HandType.Single,
                1,
                GetRankStrength(single.rank),
                GetSuitStrength(single.suit),
                1,
                IsSingleTwo(single));
        }

        if (AllSameRank(orderedCards))
        {
            int strength = GetRankStrength(orderedCards[0].rank);
            return cardCount switch
            {
                2 => new AnalyzedHand(HandType.Pair, 2, strength, GetSuitStrength(orderedCards[^1].suit), 1, false),
                3 => new AnalyzedHand(HandType.Triple, 3, strength, GetSuitStrength(orderedCards[^1].suit), 1, false),
                4 => new AnalyzedHand(HandType.FourOfAKind, 4, strength, GetSuitStrength(orderedCards[^1].suit), 1, true),
                _ => InvalidHand(cardCount)
            };
        }

        if (IsStraight(orderedCards, out int highestStraightStrength))
        {
            return new AnalyzedHand(
                HandType.Straight,
                cardCount,
                highestStraightStrength,
                GetSuitStrength(orderedCards[^1].suit),
                cardCount,
                false);
        }

        if (IsPairSequence(orderedCards, out int highestPairStrength, out int pairSequenceLength))
        {
            return new AnalyzedHand(
                HandType.PairSequence,
                cardCount,
                highestPairStrength,
                GetSuitStrength(orderedCards[^1].suit),
                pairSequenceLength,
                true);
        }

        return InvalidHand(cardCount);
    }

    public static bool CanPlayOn(AnalyzedHand challenger, AnalyzedHand current, out string reason)
    {
        if (!challenger.IsValid)
        {
            reason = "Hand is invalid.";
            return false;
        }

        if (!current.IsValid)
        {
            reason = "Table is empty.";
            return true;
        }

        if (challenger.Type == current.Type)
        {
            return CompareSameType(challenger, current, out reason);
        }

        if (CanChopSingleTwo(challenger, current, out reason))
            return true;

        reason = $"Cannot play {challenger.Type} on {current.Type}.";
        return false;
    }

    public static string Describe(AnalyzedHand hand)
    {
        if (!hand.IsValid)
            return "Invalid";

        return hand.Type switch
        {
            HandType.Single => $"Single ({RankNameFromStrength(hand.PrimaryRankStrength)})",
            HandType.Pair => $"Pair ({RankNameFromStrength(hand.PrimaryRankStrength)})",
            HandType.Triple => $"Triple ({RankNameFromStrength(hand.PrimaryRankStrength)})",
            HandType.FourOfAKind => $"Four Of A Kind ({RankNameFromStrength(hand.PrimaryRankStrength)})",
            HandType.Straight => $"Straight ({hand.CardCount} cards, high {RankNameFromStrength(hand.PrimaryRankStrength)})",
            HandType.PairSequence => $"Pair Sequence ({hand.SequenceLength} pairs, high {RankNameFromStrength(hand.PrimaryRankStrength)})",
            _ => "Invalid"
        };
    }

    public static List<Card.CardData> SortCards(IEnumerable<Card.CardData> cards)
    {
        return cards
            .OrderBy(card => GetRankStrength(card.rank))
            .ThenBy(card => GetSuitStrength(card.suit))
            .ToList();
    }

    public static int GetRankStrength(int rank)
    {
        return rank switch
        {
            3 => 0,
            4 => 1,
            5 => 2,
            6 => 3,
            7 => 4,
            8 => 5,
            9 => 6,
            10 => 7,
            11 => 8,
            12 => 9,
            13 => 10,
            1 => 11,
            2 => 12,
            _ => -1
        };
    }

    public static int GetSuitStrength(Card.Suit suit)
    {
        return suit switch
        {
            Card.Suit.Spades => 0,
            Card.Suit.Clubs => 1,
            Card.Suit.Diamonds => 2,
            Card.Suit.Hearts => 3,
            _ => -1
        };
    }

    private static bool CompareSameType(AnalyzedHand challenger, AnalyzedHand current, out string reason)
    {
        if (challenger.Type == HandType.Straight || challenger.Type == HandType.PairSequence)
        {
            if (challenger.CardCount != current.CardCount)
            {
                reason = $"{challenger.Type} length does not match.";
                return false;
            }
        }

        if (challenger.PrimaryRankStrength < current.PrimaryRankStrength)
        {
            reason = $"{challenger.Type} is too low.";
            return false;
        }

        if (challenger.PrimaryRankStrength > current.PrimaryRankStrength)
        {
            reason = $"{challenger.Type} is higher.";
            return true;
        }

        if (challenger.HighestSuitStrength > current.HighestSuitStrength)
        {
            reason = $"{challenger.Type} wins on suit.";
            return true;
        }

        reason = $"{challenger.Type} matches current value and does not beat current suit.";
        return false;
    }

    private static bool CanChopSingleTwo(AnalyzedHand challenger, AnalyzedHand current, out string reason)
    {
        bool currentIsSingleTwo = current.Type == HandType.Single && current.PrimaryRankStrength == GetRankStrength(2);
        if (!currentIsSingleTwo)
        {
            reason = "Not a chop scenario.";
            return false;
        }

        if (challenger.Type == HandType.FourOfAKind)
        {
            reason = "Four of a kind chops a single 2.";
            return true;
        }

        if (challenger.Type == HandType.PairSequence && challenger.SequenceLength >= 3)
        {
            reason = "Pair sequence chops a single 2.";
            return true;
        }

        reason = "Only four of a kind or a sequence of at least three pairs can chop a single 2.";
        return false;
    }

    private static bool AllSameRank(IReadOnlyList<Card.CardData> cards)
    {
        if (cards.Count == 0)
            return false;

        int rank = cards[0].rank;
        for (int i = 1; i < cards.Count; i++)
        {
            if (cards[i].rank != rank)
                return false;
        }

        return true;
    }

    private static bool IsStraight(IReadOnlyList<Card.CardData> cards, out int highestStrength)
    {
        highestStrength = -1;
        if (cards.Count < 3)
            return false;

        int previousStrength = -1;
        for (int i = 0; i < cards.Count; i++)
        {
            int currentStrength = GetRankStrength(cards[i].rank);
            if (currentStrength < 0 || cards[i].rank == 2)
                return false;

            if (i > 0 && currentStrength != previousStrength + 1)
                return false;

            if (i > 0 && currentStrength == previousStrength)
                return false;

            previousStrength = currentStrength;
        }

        highestStrength = previousStrength;
        return true;
    }

    private static bool IsPairSequence(IReadOnlyList<Card.CardData> cards, out int highestPairStrength, out int pairSequenceLength)
    {
        highestPairStrength = -1;
        pairSequenceLength = 0;

        if (cards.Count < 6 || cards.Count % 2 != 0)
            return false;

        int previousPairStrength = -1;
        for (int i = 0; i < cards.Count; i += 2)
        {
            Card.CardData first = cards[i];
            Card.CardData second = cards[i + 1];
            if (first.rank != second.rank || first.rank == 2)
                return false;

            int pairStrength = GetRankStrength(first.rank);
            if (pairStrength < 0)
                return false;

            if (i > 0 && pairStrength != previousPairStrength + 1)
                return false;

            previousPairStrength = pairStrength;
            pairSequenceLength++;
        }

        highestPairStrength = previousPairStrength;
        return pairSequenceLength >= 3;
    }

    private static bool IsSingleTwo(Card.CardData card)
    {
        return card.rank == 2;
    }

    private static AnalyzedHand InvalidHand(int cardCount)
    {
        return new AnalyzedHand(HandType.Invalid, cardCount, -1, -1, 0, false);
    }

    private static string RankNameFromStrength(int strength)
    {
        return strength switch
        {
            0 => "3",
            1 => "4",
            2 => "5",
            3 => "6",
            4 => "7",
            5 => "8",
            6 => "9",
            7 => "10",
            8 => "J",
            9 => "Q",
            10 => "K",
            11 => "A",
            12 => "2",
            _ => "?"
        };
    }
}
