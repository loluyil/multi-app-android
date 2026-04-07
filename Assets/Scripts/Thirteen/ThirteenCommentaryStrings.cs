using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All user-facing strings for the Thirteen game's turn-phase, played-card, and commentary UI.
/// Tokens you can use inside strings:
///   {player}    -> "you" or "player N"
///   {Player}    -> "You" or "Player N"
///   {cards}     -> human-readable description of the played cards (e.g. "a pair of 7s")
///   {rank}      -> rank name of the primary card (e.g. "7", "J", "A", "2")
///   {count}     -> number of cards in the hand / length of the run
/// Edit freely — keep tokens intact.
/// </summary>
public static class ThirteenCommentaryStrings
{
    public static readonly string[] CommentaryStraight =
    {
        "please dont beat this",
        "i dont know what to put anymore.",
        "1234... a lot of cards.",
        "how many is that",
    };

    public static readonly string[] CommentaryFourOfAKind =
    {
        "chops dick off",
    };

    public static readonly string[] CommentaryChopTwo =
    {
        "HOLY FUCK GET CHOPPED LOLLLLL",
        "bye bye 2",
        "WHAT *** FUCK>>>>???",
        "WHO SHUFFLED THIS ******** DECK",
    };

    public static readonly string[] CommentaryPlayedTwo =
    {
        "thats one 2 out",
        "sit boy.",
        "please dont chop",
        "PLEASE DONT BEAT THIS",
    };

    public static readonly string[] CommentaryWin =
    {
        "run it back now",
        "player 3 cant fking shuffle",
        "+2$",
    };

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    public static string PlayerLabel(int seat, int localSeat, bool capitalized = false)
    {
        if (seat == localSeat)
            return capitalized ? "You" : "you";

        // Map other seats to "player 2", "player 3", "player 4" relative to local = player 1.
        int offset = ((seat - localSeat) % 4 + 4) % 4; // 1..3
        int displayIndex = offset + 1;                  // 2..4
        return (capitalized ? "Player " : "player ") + displayIndex;
    }

    public static string RankName(int rank)
    {
        return rank switch
        {
            1 => "A",
            11 => "J",
            12 => "Q",
            13 => "K",
            _ => rank.ToString(),
        };
    }

    public static string DescribeHand(ThirteenRules.AnalyzedHand hand, IReadOnlyList<Card.CardData> cards)
    {
        if (!hand.IsValid || cards == null || cards.Count == 0)
            return "nothing";

        switch (hand.Type)
        {
            case ThirteenRules.HandType.Single:
                return RankNameFromStrength(hand.PrimaryRankStrength).ToLower()
                    + " of " + SuitName(cards[0].suit);
            case ThirteenRules.HandType.Pair:
                return "pair";
            case ThirteenRules.HandType.Triple:
                return "triple";
            case ThirteenRules.HandType.FourOfAKind:
                return "4 of a kind";
            case ThirteenRules.HandType.Straight:
                return "run of " + JoinUniqueRanks(cards);
            case ThirteenRules.HandType.PairSequence:
                return "pair run of " + JoinUniqueRanks(cards);
            default:
                return "nothing";
        }
    }

    private static string SuitName(Card.Suit suit)
    {
        return suit switch
        {
            Card.Suit.Spades => "spades",
            Card.Suit.Clubs => "clubs",
            Card.Suit.Diamonds => "diamonds",
            Card.Suit.Hearts => "hearts",
            _ => "spades",
        };
    }

    private static string JoinUniqueRanks(IReadOnlyList<Card.CardData> cards)
    {
        List<Card.CardData> ordered = ThirteenRules.SortCards(cards);
        List<string> rankNames = new List<string>();
        int lastRank = int.MinValue;
        for (int i = 0; i < ordered.Count; i++)
        {
            int rank = ordered[i].rank;
            if (rank == lastRank)
                continue;
            lastRank = rank;
            rankNames.Add(RankName(rank).ToLower());
        }
        return string.Join(",", rankNames);
    }

    public static string RankNameFromStrength(int strength)
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
            _ => "?",
        };
    }

    public static string PickRandom(string[] choices)
    {
        if (choices == null || choices.Length == 0)
            return string.Empty;
        return choices[Random.Range(0, choices.Length)];
    }

    public static string Format(string template, string player = null, string cards = null, string rank = null, int count = 0)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        string result = template;
        if (player != null)
        {
            result = result.Replace("{player}", player);
            if (player.Length > 0)
            {
                string capitalized = char.ToUpper(player[0]) + player.Substring(1);
                result = result.Replace("{Player}", capitalized);
            }
        }
        if (cards != null)
            result = result.Replace("{cards}", cards);
        if (rank != null)
            result = result.Replace("{rank}", rank);
        result = result.Replace("{count}", count.ToString());
        return result;
    }
}
