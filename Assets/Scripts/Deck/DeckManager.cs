using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 덱 매니저 - 스킬 카드 순환 시스템
/// - Initial Deck을 게임 시작 시 한 번만 섞음
/// - 사용한 카드는 덱 맨 뒤로 이동 (고정된 순서로 순환)
/// - 새 스킬 획득 시 덱[2]에 삽입 (UI에 보이는 덱[0], 덱[1]은 유지)
/// </summary>
public class DeckManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("스킬 교체를 위한 PlayerCombat 참조")]
    [SerializeField] private PlayerCombat playerCombat;

    [Header("Events")]
    [Tooltip("덱 상태가 변경되었을 때 발생하는 이벤트")]
    public UnityEvent OnDeckChanged;

    [Header("Initial Deck Setup")]
    [Tooltip("Inspector에서 설정할 초기 덱 구성")]
    [SerializeField] private List<Card> initialDeck = new List<Card>();

    [Header("Deck Preview UI")]
    [Tooltip("준비된 처형 스킬 카드 이미지 (덱[0] - 지금 사용될 스킬)")]
    [SerializeField] private Image readySkillCardImage;

    [Tooltip("다음 준비될 처형 스킬 카드 이미지 (덱[1] - 바로 다음에 사용될 스킬)")]
    [SerializeField] private Image nextSkillCardImage;

    [Tooltip("카드가 없을 때 표시할 기본 스프라이트")]
    [SerializeField] private Sprite emptyCardSprite;

    [Header("Runtime State (Read Only)")]
    [Tooltip("현재 스킬 카드 풀 (고정된 순서로 순환)")]
    [SerializeField] private List<Card> deck = new List<Card>();

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
        // 덱을 한 번만 섞음 (이후로는 순서 고정)
        if (deck.Count > 0)
        {
            ShuffleDeck();
            Debug.Log("[DeckManager] 덱 섞기 완료 - 이후 순서 고정");
        }

        // 첫 번째 스킬을 PlayerCombat에 자동 장착
        if (deck.Count > 0 && playerCombat != null)
        {
            Card firstCard = deck[0];
            bool success = playerCombat.EquipSkill(firstCard.skillType);
            if (success)
            {
                Debug.Log($"[DeckManager] 게임 시작: 첫 번째 스킬 자동 장착 - {firstCard.cardName} ({firstCard.skillType})");
            }
        }

        // 초기 UI 업데이트
        UpdateDeckUI();
    }

    /// <summary>
    /// 덱 초기화 - Initial Deck을 복사
    /// </summary>
    private void InitializeDeck()
    {
        deck.Clear();

        // Inspector에서 설정한 초기 덱 복사
        foreach (var card in initialDeck)
        {
            if (card != null)
            {
                deck.Add(card);
            }
        }

        Debug.Log($"[DeckManager] 덱 초기화 완료: {deck.Count}장");
    }

    // ==================== 스킬 사용 ====================

    /// <summary>
    /// 현재 준비된 스킬(덱[0]) 사용
    /// - 사용한 카드는 덱 맨 뒤로 이동
    /// - 자동으로 다음 스킬이 준비됨
    /// </summary>
    public bool UseSkill()
    {
        if (deck.Count == 0)
        {
            Debug.LogWarning("[DeckManager] 덱에 카드가 없습니다!");
            return false;
        }

        // 덱[0] 카드를 사용
        Card usedCard = deck[0];
        deck.RemoveAt(0);

        // 덱 맨 뒤로 이동
        deck.Add(usedCard);

        Debug.Log($"[DeckManager] 스킬 사용: {usedCard.cardName} ({usedCard.skillType}) → 덱 맨 뒤로 이동");

        // 다음 스킬 자동 장착
        if (deck.Count > 0 && playerCombat != null)
        {
            Card nextCard = deck[0];
            bool success = playerCombat.EquipSkill(nextCard.skillType);
            if (success)
            {
                Debug.Log($"[DeckManager] 다음 스킬 자동 장착: {nextCard.cardName} ({nextCard.skillType})");
            }
        }

        // UI 업데이트
        UpdateDeckUI();

        // 이벤트 발생
        OnDeckChanged?.Invoke();

        return true;
    }

    // ==================== 새 스킬 획득 ====================

    /// <summary>
    /// 새 스킬 카드를 덱에 추가
    /// - 덱[2]에 삽입 (덱[0], 덱[1]은 UI에 보이는 중이므로 유지)
    /// - 덱이 2장 미만이면 맨 뒤에 추가
    /// </summary>
    public void AddNewSkill(Card newCard)
    {
        if (newCard == null)
        {
            Debug.LogWarning("[DeckManager] null 카드를 추가할 수 없습니다.");
            return;
        }

        // 덱이 2장 이상이면 덱[2]에 삽입 (UI에 보이지 않는 첫 번째 위치)
        if (deck.Count >= 2)
        {
            deck.Insert(2, newCard);
            Debug.Log($"[DeckManager] 새 스킬 획득: {newCard.cardName} ({newCard.skillType}) → 덱[2]에 삽입");
        }
        else
        {
            // 덱이 2장 미만이면 맨 뒤에 추가
            deck.Add(newCard);
            Debug.Log($"[DeckManager] 새 스킬 획득: {newCard.cardName} ({newCard.skillType}) → 덱 맨 뒤에 추가");
        }

        // UI 업데이트
        UpdateDeckUI();

        // 이벤트 발생
        OnDeckChanged?.Invoke();
    }

    /// <summary>
    /// SkillCardData로 새 스킬 추가 (SkillUIChoiceManager용)
    /// </summary>
    public void AddNewSkill(SkillCardData skillData)
    {
        if (skillData == null)
        {
            Debug.LogWarning("[DeckManager] null SkillCardData를 추가할 수 없습니다.");
            return;
        }

        Card newCard = new Card(
            skillData.skillType,
            skillData.skillName,
            skillData.skillIcon
        );

        AddNewSkill(newCard);
    }

    // ==================== 덱 관리 ====================

    /// <summary>
    /// 덱 섞기 (게임 시작 시 한 번만 호출)
    /// </summary>
    private void ShuffleDeck()
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Card temp = deck[i];
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }

        Debug.Log("[DeckManager] 덱 섞기 완료");
    }

    // ==================== UI 업데이트 ====================

    /// <summary>
    /// 덱 미리보기 UI 업데이트
    /// - 준비된 스킬: 덱[0] (지금 사용될 스킬)
    /// - 다음 스킬: 덱[1] (바로 다음에 사용될 스킬)
    /// </summary>
    private void UpdateDeckUI()
    {
        // 준비된 스킬 (덱[0])
        UpdateCardImage(readySkillCardImage, 0);

        // 다음 스킬 (덱[1])
        UpdateCardImage(nextSkillCardImage, 1);
    }

    /// <summary>
    /// 특정 Image에 덱의 카드 아이콘 설정
    /// </summary>
    private void UpdateCardImage(Image targetImage, int deckIndex)
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
                targetImage.color = Color.white;
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
            targetImage.color = new Color(1f, 1f, 1f, 0.5f); // 반투명
        }
        else
        {
            targetImage.enabled = false;
        }
    }

    // ==================== 상태 조회 ====================

    /// <summary>
    /// 현재 준비된 스킬 카드 반환 (덱[0])
    /// </summary>
    public Card GetReadySkillCard()
    {
        if (deck.Count > 0)
        {
            return deck[0];
        }
        return null;
    }

    /// <summary>
    /// 다음 준비될 스킬 카드 반환 (덱[1])
    /// </summary>
    public Card GetNextSkillCard()
    {
        if (deck.Count > 1)
        {
            return deck[1];
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
    /// 덱의 카드 개수 반환
    /// </summary>
    public int GetDeckCount()
    {
        return deck.Count;
    }

    /// <summary>
    /// 현재 덱의 모든 카드 반환 (읽기 전용)
    /// </summary>
    public List<Card> GetDeck()
    {
        return new List<Card>(deck);
    }

    // ==================== 디버그/테스트 ====================

#if UNITY_EDITOR
    [ContextMenu("덱 정보 출력")]
    private void PrintDeckInfo()
    {
        Debug.Log($"=== 덱 매니저 상태 ===");
        Debug.Log($"덱: {deck.Count}장");

        Debug.Log("\n[덱 순서 (순환)]");
        for (int i = 0; i < deck.Count; i++)
        {
            string marker = "";
            if (i == 0) marker = " ← 준비된 스킬 (지금 사용)";
            else if (i == 1) marker = " ← 다음 스킬 (바로 다음)";
            else if (i == 2) marker = " ← 새 스킬 삽입 위치";

            Debug.Log($"  [{i}] {deck[i].cardName} ({deck[i].skillType}){marker}");
        }

        Debug.Log("\n[UI 상태]");
        if (deck.Count > 0)
            Debug.Log($"  준비된 스킬: {deck[0].cardName} ({deck[0].skillType})");
        else
            Debug.Log($"  준비된 스킬: 없음");

        if (deck.Count > 1)
            Debug.Log($"  다음 스킬: {deck[1].cardName} ({deck[1].skillType})");
        else
            Debug.Log($"  다음 스킬: 없음");
    }

    [ContextMenu("테스트: 스킬 사용")]
    private void TestUseSkill()
    {
        UseSkill();
    }

    [ContextMenu("테스트: 새 스킬 추가 (Fire)")]
    private void TestAddFireSkill()
    {
        Card fireCard = new Card(SecondaryChargedAttackType.Windmill, "화염구", null);
        AddNewSkill(fireCard);
    }

    [ContextMenu("테스트: UI 강제 업데이트")]
    private void TestUpdateUI()
    {
        UpdateDeckUI();
        Debug.Log("[DeckManager] UI 업데이트 완료");
    }

    [ContextMenu("테스트: 3번 연속 사용")]
    private void TestUseThreeTimes()
    {
        for (int i = 0; i < 3; i++)
        {
            Debug.Log($"\n--- {i + 1}번째 사용 ---");
            UseSkill();
        }
    }
#endif
}