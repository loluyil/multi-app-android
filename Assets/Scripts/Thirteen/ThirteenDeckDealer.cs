using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ThirteenDeckDealer : MonoBehaviour
{
    [System.Serializable]
    private struct CardSpriteEntry
    {
        public string key;
        public Sprite sprite;
    }

    [Header("References")]
    [SerializeField] private HorizontalCardHolder playerHandHolder;

    [Header("Art")]
    [SerializeField] private string spriteFolder = "Assets/Images/thirteen/numbers";
    [SerializeField] private List<CardSpriteEntry> cardSprites = new List<CardSpriteEntry>();

    [Header("Debug")]
    [SerializeField] private bool dealOnStart = true;

    [SerializeField] private List<Card.CardData> playerHand = new List<Card.CardData>();
    [SerializeField] private List<Card.CardData> opponentHandA = new List<Card.CardData>();
    [SerializeField] private List<Card.CardData> opponentHandB = new List<Card.CardData>();
    [SerializeField] private List<Card.CardData> opponentHandC = new List<Card.CardData>();

    private readonly Dictionary<string, Sprite> spriteLookup = new Dictionary<string, Sprite>();

    private void Awake()
    {
        RebuildSpriteLookup();
    }

    private void Start()
    {
        if (dealOnStart)
            ShuffleAndDeal();
    }

    public void ShuffleAndDeal()
    {
        RebuildSpriteLookup();

        List<Card.CardData> deck = CreateDeck();
        Shuffle(deck);

        playerHand = SortHand(deck.Take(13).ToList());
        opponentHandA = SortHand(deck.Skip(13).Take(13).ToList());
        opponentHandB = SortHand(deck.Skip(26).Take(13).ToList());
        opponentHandC = SortHand(deck.Skip(39).Take(13).ToList());

        if (playerHandHolder != null)
            playerHandHolder.SetHand(playerHand, spriteLookup);
    }

    public IReadOnlyList<Card.CardData> GetPlayerHand() => playerHand;
    public IReadOnlyList<Card.CardData> GetOpponentHandA() => opponentHandA;
    public IReadOnlyList<Card.CardData> GetOpponentHandB() => opponentHandB;
    public IReadOnlyList<Card.CardData> GetOpponentHandC() => opponentHandC;

    public static string GetSuitSpriteName(Card.Suit suit)
    {
        return suit switch
        {
            Card.Suit.Spades => "spades",
            Card.Suit.Clubs => "clubs",
            Card.Suit.Diamonds => "diamonds",
            Card.Suit.Hearts => "hearts",
            _ => "spades"
        };
    }

    private static List<Card.CardData> CreateDeck()
    {
        List<Card.CardData> deck = new List<Card.CardData>(52);

        foreach (Card.Suit suit in System.Enum.GetValues(typeof(Card.Suit)))
        {
            for (int rank = 1; rank <= 13; rank++)
                deck.Add(new Card.CardData(suit, rank));
        }

        return deck;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static List<Card.CardData> SortHand(List<Card.CardData> hand)
    {
        return hand
            .OrderBy(card => GetRankSortValue(card.rank))
            .ThenBy(card => (int)card.suit)
            .ToList();
    }

    private static int GetRankSortValue(int rank)
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
            _ => 99
        };
    }

    private void RebuildSpriteLookup()
    {
        spriteLookup.Clear();

        foreach (CardSpriteEntry entry in cardSprites)
        {
            if (entry.sprite == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            spriteLookup[entry.key] = entry.sprite;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(spriteFolder))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { spriteFolder });
        List<CardSpriteEntry> loadedSprites = new List<CardSpriteEntry>(guids.Length);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                continue;

            loadedSprites.Add(new CardSpriteEntry
            {
                key = System.IO.Path.GetFileNameWithoutExtension(path),
                sprite = sprite
            });
        }

        cardSprites = loadedSprites.OrderBy(entry => entry.key).ToList();
        RebuildSpriteLookup();
    }
#endif
}
