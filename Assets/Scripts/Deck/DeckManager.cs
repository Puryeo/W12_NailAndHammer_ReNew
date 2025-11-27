using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 덱 매니저 - 카드 덱과 핸드를 관리
/// - 카드 뽑기, 사용, 섞기 등의 기능 제공
/// - 카드 사용 후 처리 방식을 Inspector에서 설정 가능
/// </summary>
public class DeckManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("스킬 교체를 위한 PlayerCombat 참조")]
    [SerializeField] private PlayerCombat playerCombat;

    [Header("Card Usage Settings")]
    [Tooltip("카드 사용 후 처리 방식")]
    [SerializeField] private CardUsageMode usageMode = CardUsageMode.ReturnToDeckBottom;

    [Header("Initial Deck Setup")]
    [Tooltip("코드에서 랜덤 덱 생성 사용 여부 (true면 initialDeck 무시)")]
    [SerializeField] private bool useRandomGeneration = false;

    [Tooltip("랜덤 생성 시 덱에 포함할 카드 개수")]
    [SerializeField] private int randomDeckSize = 10;

    [Tooltip("Inspector에서 설정할 초기 덱 구성")]
    [SerializeField] private List<Card> initialDeck = new List<Card>();

    [Header("Runtime State (Read Only)")]
    [Tooltip("현재 덱 (카드 뭉치)")]
    [SerializeField] private List<Card> deck = new List<Card>();

    [Tooltip("현재 핸드 패")]
    [SerializeField] private List<Card> hand = new List<Card>();

    [Tooltip("버린 카드 더미 (MoveToDiscardPile 모드용)")]
    [SerializeField] private List<Card> discardPile = new List<Card>();

    private void Awake()
    {
        if (playerCombat == null)
        {
            playerCombat = FindObjectOfType<PlayerCombat>();
            if (playerCombat == null)
            {
                Debug.LogWarning("[DeckManager] PlayerCombat을 찾을 수 없습니다.");
            }
        }

        InitializeDeck();
    }

    /// <summary>
    /// 덱 초기화
    /// </summary>
    private void InitializeDeck()
    {
        deck.Clear();
        hand.Clear();
        discardPile.Clear();

        if (useRandomGeneration)
        {
            GenerateRandomDeck();
        }
        else
        {
            // Inspector에서 설정한 초기 덱 복사
            foreach (var card in initialDeck)
            {
                deck.Add(card);
            }
        }

        Debug.Log($"[DeckManager] 덱 초기화 완료: {deck.Count}장");
    }

    /// <summary>
    /// 랜덤 덱 생성 (테스트용)
    /// </summary>
    private void GenerateRandomDeck()
    {
        // None을 제외한 모든 스킬 타입
        SecondaryChargedAttackType[] skillTypes = new SecondaryChargedAttackType[]
        {
            SecondaryChargedAttackType.Windmill,
            SecondaryChargedAttackType.Thorns,
            SecondaryChargedAttackType.Guardian,
            SecondaryChargedAttackType.Sector
        };

        for (int i = 0; i < randomDeckSize; i++)
        {
            SecondaryChargedAttackType randomSkill = skillTypes[Random.Range(0, skillTypes.Length)];
            Card newCard = new Card(randomSkill, randomSkill.ToString());
            deck.Add(newCard);
        }

        Debug.Log($"[DeckManager] 랜덤 덱 생성: {deck.Count}장");
    }

    // ==================== 카드 뽑기/사용 ====================

    /// <summary>
    /// 덱에서 핸드로 카드 뽑기
    /// </summary>
    /// <param name="count">뽑을 카드 개수</param>
    public void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                Debug.LogWarning("[DeckManager] 덱에 남은 카드가 없습니다.");
                break;
            }

            Card drawnCard = deck[0];
            deck.RemoveAt(0);
            hand.Add(drawnCard);

            Debug.Log($"[DeckManager] 카드 뽑음: {drawnCard}");
        }

        Debug.Log($"[DeckManager] 핸드: {hand.Count}장, 덱: {deck.Count}장");
    }

    /// <summary>
    /// 핸드의 특정 인덱스 카드 사용
    /// </summary>
    /// <param name="handIndex">사용할 카드의 핸드 인덱스</param>
    /// <returns>사용 성공 여부</returns>
    public bool UseCard(int handIndex)
    {
        if (handIndex < 0 || handIndex >= hand.Count)
        {
            Debug.LogWarning($"[DeckManager] 잘못된 핸드 인덱스: {handIndex} (핸드 크기: {hand.Count})");
            return false;
        }

        Card cardToUse = hand[handIndex];
        hand.RemoveAt(handIndex);

        // PlayerCombat에 스킬 교체 요청
        if (playerCombat != null)
        {
            bool success = playerCombat.EquipSkill(cardToUse.skillType);
            if (!success)
            {
                Debug.LogWarning($"[DeckManager] 스킬 장착 실패: {cardToUse}");
                // 실패 시 카드를 핸드로 되돌림
                hand.Insert(handIndex, cardToUse);
                return false;
            }
        }

        // 카드 사용 후 처리
        HandleUsedCard(cardToUse);

        Debug.Log($"[DeckManager] 카드 사용: {cardToUse} → 스킬 교체 완료");
        return true;
    }

    /// <summary>
    /// 핸드의 첫 번째(0번) 카드 사용
    /// </summary>
    public bool UseNextCard()
    {
        return UseCard(0);
    }

    /// <summary>
    /// 핸드에서 랜덤으로 카드 하나 선택하여 사용
    /// </summary>
    public bool UseRandomCardFromHand()
    {
        if (hand.Count == 0)
        {
            Debug.LogWarning("[DeckManager] 핸드에 카드가 없습니다.");
            return false;
        }

        int randomIndex = Random.Range(0, hand.Count);
        Debug.Log($"[DeckManager] 랜덤 카드 선택: 인덱스 {randomIndex}");
        return UseCard(randomIndex);
    }

    /// <summary>
    /// 사용한 카드 처리 (usageMode에 따라)
    /// </summary>
    private void HandleUsedCard(Card usedCard)
    {
        switch (usageMode)
        {
            case CardUsageMode.ReturnToDeckBottom:
                deck.Add(usedCard);
                Debug.Log($"[DeckManager] 카드를 덱 맨 아래로 반환: {usedCard}");
                break;

            case CardUsageMode.RemoveFromGame:
                Debug.Log($"[DeckManager] 카드를 게임에서 제거: {usedCard}");
                // 카드를 어디에도 추가하지 않음 (제거됨)
                break;

            case CardUsageMode.MoveToDiscardPile:
                discardPile.Add(usedCard);
                Debug.Log($"[DeckManager] 카드를 버린 더미로 이동: {usedCard}");
                break;
        }
    }

    // ==================== 셔플 기능 ====================

    /// <summary>
    /// 핸드를 랜덤하게 섞기
    /// </summary>
    public void ShuffleHand()
    {
        ShuffleList(hand);
        Debug.Log("[DeckManager] 핸드 섞기 완료");
    }

    /// <summary>
    /// 덱을 랜덤하게 섞기
    /// </summary>
    public void ShuffleDeck()
    {
        ShuffleList(deck);
        Debug.Log("[DeckManager] 덱 섞기 완료");
    }

    /// <summary>
    /// Fisher-Yates 셔플 알고리즘
    /// </summary>
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    // ==================== 덱 관리 ====================

    /// <summary>
    /// 카드를 덱 맨 아래로 반환
    /// </summary>
    public void ReturnCardToDeck(Card card)
    {
        deck.Add(card);
        Debug.Log($"[DeckManager] 카드를 덱으로 반환: {card}");
    }

    /// <summary>
    /// 모든 카드를 덱으로 되돌리고 섞기
    /// </summary>
    public void ResetDeck()
    {
        // 핸드와 버린 더미의 모든 카드를 덱으로 이동
        deck.AddRange(hand);
        deck.AddRange(discardPile);

        hand.Clear();
        discardPile.Clear();

        ShuffleDeck();

        Debug.Log($"[DeckManager] 덱 리셋 완료: {deck.Count}장");
    }

    /// <summary>
    /// 버린 더미의 카드들을 덱으로 되돌리고 섞기
    /// </summary>
    public void RecycleDiscardPile()
    {
        deck.AddRange(discardPile);
        discardPile.Clear();
        ShuffleDeck();

        Debug.Log($"[DeckManager] 버린 더미 재활용: 덱 {deck.Count}장");
    }

    // ==================== 상태 조회 ====================

    /// <summary>
    /// 핸드의 카드 개수 반환
    /// </summary>
    public int GetHandCount()
    {
        return hand.Count;
    }

    /// <summary>
    /// 덱의 남은 카드 개수 반환
    /// </summary>
    public int GetDeckCount()
    {
        return deck.Count;
    }

    /// <summary>
    /// 버린 더미의 카드 개수 반환
    /// </summary>
    public int GetDiscardPileCount()
    {
        return discardPile.Count;
    }

    /// <summary>
    /// 핸드의 특정 카드 조회
    /// </summary>
    public Card GetCardInHand(int index)
    {
        if (index < 0 || index >= hand.Count)
        {
            Debug.LogWarning($"[DeckManager] 잘못된 핸드 인덱스: {index}");
            return null;
        }

        return hand[index];
    }

    /// <summary>
    /// 현재 핸드의 모든 카드 반환 (읽기 전용)
    /// </summary>
    public List<Card> GetHand()
    {
        return new List<Card>(hand);
    }

    /// <summary>
    /// 현재 덱의 모든 카드 반환 (읽기 전용)
    /// </summary>
    public List<Card> GetDeck()
    {
        return new List<Card>(deck);
    }

    /// <summary>
    /// 현재 버린 더미의 모든 카드 반환 (읽기 전용)
    /// </summary>
    public List<Card> GetDiscardPile()
    {
        return new List<Card>(discardPile);
    }

    // ==================== 디버그/테스트 ====================

#if UNITY_EDITOR
    [ContextMenu("덱 정보 출력")]
    private void PrintDeckInfo()
    {
        Debug.Log($"=== 덱 매니저 상태 ===");
        Debug.Log($"덱: {deck.Count}장");
        Debug.Log($"핸드: {hand.Count}장");
        Debug.Log($"버린 더미: {discardPile.Count}장");
        Debug.Log($"사용 모드: {usageMode}");

        Debug.Log("\n[핸드 카드 목록]");
        for (int i = 0; i < hand.Count; i++)
        {
            Debug.Log($"  [{i}] {hand[i]}");
        }
    }

    [ContextMenu("테스트: 3장 뽑기")]
    private void TestDrawThree()
    {
        DrawCards(3);
    }

    [ContextMenu("테스트: 첫 번째 카드 사용")]
    private void TestUseFirst()
    {
        UseNextCard();
    }

    [ContextMenu("테스트: 랜덤 카드 사용")]
    private void TestUseRandom()
    {
        UseRandomCardFromHand();
    }

    [ContextMenu("테스트: 핸드 섞기")]
    private void TestShuffleHand()
    {
        ShuffleHand();
    }

    [ContextMenu("테스트: 덱 리셋")]
    private void TestResetDeck()
    {
        ResetDeck();
    }
#endif
}
