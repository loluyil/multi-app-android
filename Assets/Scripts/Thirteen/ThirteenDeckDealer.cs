using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private RectTransform player2Container;
    [SerializeField] private RectTransform player3Container;
    [SerializeField] private RectTransform player4Container;
    [SerializeField] private GameObject cardBackPrefab;

    [Header("Opponent Card Back Layout")]
    [SerializeField] private float sideCardBackRotation = 90f;

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
        AutoAssignSceneReferences();
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

        PopulateOpponentHand(player2Container, opponentHandA.Count, true);
        PopulateOpponentHand(player3Container, opponentHandB.Count, false);
        PopulateOpponentHand(player4Container, opponentHandC.Count, true);
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
            .OrderBy(card => ThirteenRules.GetRankStrength(card.rank))
            .ThenBy(card => (int)card.suit)
            .ToList();
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

    private void AutoAssignSceneReferences()
    {
        if (playerHandHolder == null)
            playerHandHolder = FindFirstObjectByType<HorizontalCardHolder>();

        if (player2Container == null)
            player2Container = FindContainer("Player2");

        if (player3Container == null)
            player3Container = FindContainer("Player3");

        if (player4Container == null)
            player4Container = FindContainer("Player4");
    }

    private static RectTransform FindContainer(string name)
    {
        GameObject found = GameObject.Find(name);
        return found != null ? found.GetComponent<RectTransform>() : null;
    }

    private void PopulateOpponentHand(RectTransform container, int count, bool isSidePlayer)
    {
        if (container == null || cardBackPrefab == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        for (int i = 0; i < count; i++)
        {
            GameObject cardBack = Instantiate(cardBackPrefab, container);
            cardBack.name = $"CardBack_{i}";

            Card card = cardBack.GetComponent<Card>();
            if (card != null)
            {
                card.SetInteractionEnabled(false, false);
                card.enabled = false;
            }

            Button button = cardBack.GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
                button.enabled = false;
            }

            CanvasGroup canvasGroup = cardBack.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 1f;
            }

            Image image = cardBack.GetComponent<Image>();
            if (image != null)
                image.color = Color.white;

            ConfigureCardBackTransform(cardBack.GetComponent<RectTransform>(), isSidePlayer);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(container);
    }

    private void ConfigureCardBackTransform(RectTransform rectTransform, bool isSidePlayer)
    {
        if (rectTransform == null)
            return;

        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, isSidePlayer ? sideCardBackRotation : 0f);
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
