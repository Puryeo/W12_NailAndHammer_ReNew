using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 덱 매니저 - 카드 덱과 핸드를 관리 + UI 업데이트 통합
/// - 카드 뽑기, 사용, 섞기 등의 기능 제공
/// - 카드 사용 후 처리 방식을 Inspector에서 설정 가능
/// - 다음/그다음 카드 UI 자동 업데이트
/// </summary>
public class DeckManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("스킬 교체를 위한 PlayerCombat 참조")]
    [SerializeField] private PlayerCombat playerCombat;

    [Header("Events")]
    [Tooltip("덱 상태가 변경되었을 때 발생하는 이벤트 (카드 사용, 뽑기 등)")]
    public UnityEvent OnDeckChanged;

    [Header("Card Usage Settings")]
    [Tooltip("카드 사용 후 처리 방식")]
    [SerializeField] private CardUsageMode usageMode = CardUsageMode.MoveToDiscardPile;

    [Header("Initial Deck Setup")]
    [Tooltip("코드에서 랜덤 덱 생성 사용 여부 (true면 initialDeck 무시)")]
    [SerializeField] private bool useRandomGeneration = false;

    [Tooltip("랜덤 생성 시 덱에 포함할 카드 개수")]
    [SerializeField] private int randomDeckSize = 10;

    [Tooltip("Inspector에서 설정할 초기 덱 구성")]
    [SerializeField] private List<Card> initialDeck = new List<Card>();

    [Header("Deck Preview UI")]
    [Tooltip("준비된 처형 스킬 카드 이미지 (덱의 0번 - 지금 당장 사용될 스킬)")]
    [SerializeField] private Image readySkillCardImage;

    [Tooltip("다음 준비될 처형 스킬 카드 이미지 (덱의 1번 - 그 다음에 사용될 스킬)")]
    [SerializeField] private Image nextSkillCardImage;

    [Tooltip("카드가 없을 때 표시할 기본 스프라이트")]
    [SerializeField] private Sprite emptyCardSprite;

    [Header("Auto Recycle Settings")]
    [Tooltip("덱이 비었을 때 자동으로 버린 더미를 섞어서 덱으로 만들기")]
    [SerializeField] private bool autoRecycleWhenEmpty = true;

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

    private void Start()
    {
        // 덱 섞기
        if (deck.Count > 0)
        {
            ShuffleDeck();
        }

        // 초기 핸드 드로우 (1장 - 준비된 스킬용)
        if (deck.Count > 0)
        {
            DrawCards(1);
            Debug.Log("[DeckManager] 초기 핸드 드로우: 1장");
        }

        // 핸드의 첫 번째 카드를 자동으로 처형 스킬로 장착
        if (hand.Count > 0 && playerCombat != null)
        {
            Card firstCard = hand[0];
            bool success = playerCombat.EquipSkill(firstCard.skillType);
            if (success)
            {
                Debug.Log($"[DeckManager] 게임 시작: 첫 번째 스킬 자동 장착 - {firstCard}");
            }
        }

        // 초기 UI 업데이트
        UpdateDeckUI();
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
            // 덱이 비었는지 확인
            if (deck.Count == 0)
            {
                // 자동 재활용이 켜져있고 버린 더미에 카드가 있으면
                if (autoRecycleWhenEmpty && discardPile.Count > 0)
                {
                    Debug.Log("[DeckManager] 덱이 비어 자동으로 버린 더미를 재활용합니다.");
                    RecycleDiscardPile();
                }
                else
                {
                    Debug.LogWarning("[DeckManager] 덱에 남은 카드가 없습니다.");
                    break;
                }
            }

            // 여전히 덱이 비어있으면 중단
            if (deck.Count == 0)
            {
                Debug.LogWarning("[DeckManager] 재활용 후에도 덱이 비어있습니다.");
                break;
            }

            Card drawnCard = deck[0];
            deck.RemoveAt(0);
            hand.Add(drawnCard);

            Debug.Log($"[DeckManager] 카드 뽑음: {drawnCard}");
        }

        Debug.Log($"[DeckManager] 핸드: {hand.Count}장, 덱: {deck.Count}장");

        // UI 업데이트
        UpdateDeckUI();
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

        // 카드 사용 후 처리 (버린 더미로)
        HandleUsedCard(cardToUse);

        Debug.Log($"[DeckManager] 카드 사용: {cardToUse} → 스킬 교체 완료");

        // 핸드가 비었으면 자동으로 다음 카드 뽑기
        if (hand.Count == 0 && deck.Count > 0)
        {
            DrawCards(1);
            Debug.Log("[DeckManager] 핸드가 비어 자동으로 카드를 뽑았습니다.");

            // 뽑은 카드를 PlayerCombat에 자동 장착
            if (hand.Count > 0 && playerCombat != null)
            {
                Card newCard = hand[0];
                playerCombat.EquipSkill(newCard.skillType);
                Debug.Log($"[DeckManager] 새 스킬 자동 장착: {newCard.skillType}");
            }
        }
        else if (hand.Count == 0 && deck.Count == 0)
        {
            Debug.LogWarning("[DeckManager] 핸드와 덱이 모두 비어있습니다!");
        }

        OnDeckChanged?.Invoke();

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

        // UI 업데이트
        UpdateDeckUI();
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

        // UI 업데이트
        UpdateDeckUI();
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

        // UI 업데이트
        UpdateDeckUI();
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

        // UI 업데이트
        UpdateDeckUI();
    }

    // ==================== UI 업데이트 ====================

    /// <summary>
    /// 덱 미리보기 UI 업데이트
    /// - 준비된 스킬: 핸드의 0번 (현재 사용될 스킬)
    /// - 다음 스킬: 덱의 0번 (다음에 뽑아서 사용될 스킬)
    /// - 덱이 비어있으면 자동으로 버린 더미를 재활용하여 표시
    /// </summary>
    private void UpdateDeckUI()
    {
        // 준비된 스킬 카드 (핸드의 0번 - 지금 당장 처형 스킬로 사용될 카드)
        UpdateCardImageFromHand(readySkillCardImage, 0);

        // 다음 준비될 스킬 카드 (덱의 0번 - 다음에 뽑아서 사용될 카드)
        // 덱이 비어있으면 자동 재활용
        if (deck.Count == 0 && autoRecycleWhenEmpty && discardPile.Count > 0)
        {
            Debug.Log("[DeckManager] UI 업데이트: 덱이 비어 버린 더미를 재활용합니다.");
            RecycleDiscardPileQuietly();
        }

        UpdateCardImageFromDeck(nextSkillCardImage, 0);
    }

    /// <summary>
    /// 핸드에서 카드 이미지 업데이트
    /// </summary>
    private void UpdateCardImageFromHand(Image targetImage, int handIndex)
    {
        if (targetImage == null) return;

        // 핸드에 해당 인덱스의 카드가 있는지 확인
        if (handIndex < hand.Count && hand[handIndex] != null)
        {
            Card card = hand[handIndex];

            // 카드에 아이콘이 있으면 설정
            if (card.cardIcon != null)
            {
                targetImage.sprite = card.cardIcon;
                targetImage.enabled = true;
                targetImage.color = Color.white; // 정상 색상
            }
            else
            {
                // 아이콘이 없으면 빈 카드 표시
                SetEmptyCard(targetImage);
            }
        }
        else
        {
            // 핸드에 카드가 없으면 빈 카드 표시
            SetEmptyCard(targetImage);
        }
    }

    /// <summary>
    /// 덱에서 카드 이미지 업데이트
    /// </summary>
    private void UpdateCardImageFromDeck(Image targetImage, int deckIndex)
    {
        if (targetImage == null) return;

        // 덱에 해당 인덱스의 카드가 있는지 확인
        if (deckIndex < deck.Count && deck[deckIndex] != null)
        {
            Card card = deck[deckIndex];

            // 카드에 아이콘이 있으면 설정
            if (card.cardIcon != null)
            {
                targetImage.sprite = card.cardIcon;
                targetImage.enabled = true;
                targetImage.color = Color.white; // 정상 색상
            }
            else
            {
                // 아이콘이 없으면 빈 카드 표시
                SetEmptyCard(targetImage);
            }
        }
        else
        {
            // 덱에 카드가 없으면 빈 카드 표시
            SetEmptyCard(targetImage);
        }
    }

    /// <summary>
    /// 빈 카드 표시
    /// </summary>
    private void SetEmptyCard(Image targetImage)
    {
        if (targetImage == null) return;

        if (emptyCardSprite != null)
        {
            targetImage.sprite = emptyCardSprite;
            targetImage.enabled = true;
            targetImage.color = new Color(1f, 1f, 1f, 0.5f); // 반투명하게
        }
        else
        {
            targetImage.enabled = false;
        }
    }

    /// <summary>
    /// 버린 더미를 조용히 재활용 (로그 없이)
    /// </summary>
    private void RecycleDiscardPileQuietly()
    {
        deck.AddRange(discardPile);
        discardPile.Clear();
        ShuffleList(deck);
        // UpdateDeckUI()는 호출하지 않음 (무한 루프 방지)
    }

    // ==================== 상태 조회 ====================

    /// <summary>
    /// 현재 준비된 스킬 카드 반환 (핸드의 0번 - 지금 당장 사용될 스킬)
    /// </summary>
    public Card GetReadySkillCard()
    {
        if (hand.Count > 0)
        {
            return hand[0];
        }
        return null;
    }

    /// <summary>
    /// 다음 준비될 스킬 카드 반환 (덱의 0번 - 다음에 뽑을 카드)
    /// </summary>
    public Card GetNextSkillCard()
    {
        // 덱이 비었으면 자동 재활용
        if (deck.Count == 0 && autoRecycleWhenEmpty && discardPile.Count > 0)
        {
            RecycleDiscardPileQuietly();
        }

        if (deck.Count > 0)
        {
            return deck[0];
        }
        return null;
    }

    /// <summary>
    /// 현재 준비된 스킬 타입 반환 (PlayerCombat이 사용)
    /// </summary>
    public SecondaryChargedAttackType GetReadySkillType()
    {
        Card readyCard = GetReadySkillCard();
        if (readyCard != null)
        {
            return readyCard.skillType;
        }
        return SecondaryChargedAttackType.None;
    }

    /// <summary>
    /// Initial Deck에 있는 모든 스킬 타입 반환 (스킬 선택에서 제외하기 위해)
    /// </summary>
    public List<SecondaryChargedAttackType> GetInitialSkillTypes()
    {
        List<SecondaryChargedAttackType> types = new List<SecondaryChargedAttackType>();
        foreach (var card in initialDeck)
        {
            if (card != null)
            {
                types.Add(card.skillType);
            }
        }
        return types;
    }

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
        Debug.Log($"자동 재활용: {(autoRecycleWhenEmpty ? "ON" : "OFF")}");

        Debug.Log("\n[핸드 카드 목록]");
        for (int i = 0; i < hand.Count; i++)
        {
            Debug.Log($"  [{i}] {hand[i]}");
        }

        Debug.Log("\n[덱 미리보기 - 준비된 스킬]");
        if (hand.Count > 0)
            Debug.Log($"  준비된 스킬 (핸드[0] - 지금 사용될): {hand[0]}");
        else
            Debug.Log($"  준비된 스킬: 핸드 비어있음");

        if (deck.Count > 0)
            Debug.Log($"  다음 스킬 (덱[0] - 다음에 뽑을): {deck[0]}");
        else if (discardPile.Count > 0)
            Debug.Log($"  다음 스킬: 덱 비어있음 (버린 더미: {discardPile.Count}장 - 재활용 예정)");
        else
            Debug.Log($"  다음 스킬: 덱과 버린 더미 모두 비어있음");
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

    [ContextMenu("테스트: UI 강제 업데이트")]
    private void TestUpdateUI()
    {
        UpdateDeckUI();
        Debug.Log("[DeckManager] UI 업데이트 완료");
    }

    [ContextMenu("테스트: 버린 더미 재활용")]
    private void TestRecycleDiscardPile()
    {
        RecycleDiscardPile();
    }
#endif
}