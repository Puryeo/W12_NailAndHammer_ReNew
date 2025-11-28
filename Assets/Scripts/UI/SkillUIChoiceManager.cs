using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 스킬 선택 UI를 관리하는 매니저
/// - 랜덤으로 스킬 옵션 제공
/// - 선택한 스킬을 DeckManager에 추가
/// - 중복 방지 처리
/// - 패널이 켜질 때마다 자동으로 선택되지 않은 스킬로 버튼 세팅
/// </summary>
public class SkillUIChoiceManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private GameObject skillChoicePanel; // 스킬 선택 UI 패널

    [Header("Skill Pool Settings")]
    [SerializeField] private List<SkillCardData> allAvailableSkills = new List<SkillCardData>(); // 전체 스킬 풀

    [Header("Choice Settings")]
    [SerializeField] private int maxChoices = 3; // 총 선택 횟수
    [SerializeField] private int optionsPerChoice = 3; // 한 번에 보여줄 옵션 개수 (2-3개)

    private HashSet<SecondaryChargedAttackType> selectedSkillTypes = new HashSet<SecondaryChargedAttackType>(); // 이미 선택한 스킬들
    private int currentChoiceCount = 0; // 현재까지 선택한 횟수
    private SkillChoiceButton[] skillChoiceButtons; // 자동으로 찾아서 세팅
    private bool isPanelActive = false; // 패널 활성화 상태 추적

    private void Awake()
    {
        if (deckManager == null)
        {
            deckManager = FindObjectOfType<DeckManager>();
        }

        // 패널에서 버튼들 자동으로 찾기
        if (skillChoicePanel != null)
        {
            skillChoiceButtons = skillChoicePanel.GetComponentsInChildren<SkillChoiceButton>(true);
            Debug.Log($"[SkillUIChoiceManager] 버튼 자동 감지: {skillChoiceButtons.Length}개");

            // 패널 초기에는 비활성화
            skillChoicePanel.SetActive(false);
        }

        // DeckManager의 Initial Deck에 있는 스킬들을 이미 선택한 것으로 처리
        if (deckManager != null)
        {
            List<SecondaryChargedAttackType> initialSkills = deckManager.GetInitialSkillTypes();
            foreach (var skillType in initialSkills)
            {
                selectedSkillTypes.Add(skillType);
                Debug.Log($"[SkillUIChoiceManager] Initial Deck 스킬 제외: {skillType}");
            }
        }
    }

    private void Update()
    {
        // 패널 활성화 상태 감지
        if (skillChoicePanel != null)
        {
            bool currentActive = skillChoicePanel.activeSelf;

            // 비활성 → 활성 전환 감지
            if (currentActive && !isPanelActive)
            {
                OnPanelActivated();
            }

            isPanelActive = currentActive;
        }
    }

    /// <summary>
    /// 패널이 활성화될 때 자동으로 호출됨
    /// </summary>
    private void OnPanelActivated()
    {
        Debug.Log("[SkillUIChoiceManager] 패널 활성화 감지 - 스킬 리스트 업데이트");
        UpdateSkillButtons();
    }

    /// <summary>
    /// 스킬 선택 시스템 초기화 (게임 시작 시 또는 리셋 시)
    /// </summary>
    public void ResetSkillPool()
    {
        selectedSkillTypes.Clear();
        currentChoiceCount = 0;
        Debug.Log("[SkillUIChoiceManager] 스킬 풀 초기화 완료");
    }

    /// <summary>
    /// 스킬 선택 UI 표시
    /// </summary>
    public void ShowSkillChoice()
    {
        if (currentChoiceCount >= maxChoices)
        {
            Debug.Log("[SkillUIChoiceManager] 이미 모든 스킬을 선택했습니다.");
            return;
        }

        if (skillChoicePanel != null)
        {
            skillChoicePanel.SetActive(true);
        }

        // 게임 일시정지 (선택 중)
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 스킬 버튼 업데이트 (패널 활성화 시 자동 호출)
    /// - 패널이 켜질 때마다 자동으로 선택되지 않은 스킬로 버튼 세팅
    /// </summary>
    private void UpdateSkillButtons()
    {
        if (skillChoiceButtons == null || skillChoiceButtons.Length == 0)
        {
            Debug.LogWarning("[SkillUIChoiceManager] 버튼이 설정되지 않았습니다!");
            return;
        }

        // 선택 가능한 스킬들만 필터링 (아직 선택하지 않은 스킬)
        List<SkillCardData> availableSkills = allAvailableSkills
            .Where(skill => !selectedSkillTypes.Contains(skill.skillType))
            .ToList();

        if (availableSkills.Count == 0)
        {
            Debug.LogWarning("[SkillUIChoiceManager] 선택 가능한 스킬이 없습니다!");
            CloseSkillChoice();
            return;
        }

        // 랜덤으로 옵션 선택
        List<SkillCardData> randomOptions = GetRandomSkills(availableSkills, optionsPerChoice);

        // 버튼에 스킬 데이터 할당 (자동으로 아이콘, 이름, 설명 세팅됨)
        for (int i = 0; i < skillChoiceButtons.Length; i++)
        {
            if (i < randomOptions.Count)
            {
                skillChoiceButtons[i].gameObject.SetActive(true);
                skillChoiceButtons[i].SetSkillData(randomOptions[i], this);
            }
            else
            {
                skillChoiceButtons[i].gameObject.SetActive(false);
            }
        }

        Debug.Log($"[SkillUIChoiceManager] 스킬 버튼 업데이트 완료 ({randomOptions.Count}개 옵션)");
    }

    /// <summary>
    /// 선택 가능한 스킬 풀에서 랜덤으로 스킬 선택
    /// </summary>
    private List<SkillCardData> GetRandomSkills(List<SkillCardData> availableSkills, int count)
    {
        List<SkillCardData> selectedSkills = new List<SkillCardData>();

        if (availableSkills.Count == 0)
        {
            Debug.LogWarning("[SkillUIChoiceManager] 선택 가능한 스킬이 없습니다!");
            return selectedSkills;
        }

        // 실제로 선택할 개수 (남은 스킬이 부족할 수 있음)
        int actualCount = Mathf.Min(count, availableSkills.Count);

        // Fisher-Yates 셔플을 이용한 랜덤 선택
        List<SkillCardData> shuffled = new List<SkillCardData>(availableSkills);

        for (int i = 0; i < actualCount; i++)
        {
            int randomIndex = Random.Range(i, shuffled.Count);
            selectedSkills.Add(shuffled[randomIndex]);

            // 스왑
            var temp = shuffled[i];
            shuffled[i] = shuffled[randomIndex];
            shuffled[randomIndex] = temp;
        }

        return selectedSkills;
    }

    /// <summary>
    /// 스킬 선택 완료 (버튼에서 호출)
    /// </summary>
    public void OnSkillSelected(SkillCardData selectedSkill)
    {
        if (deckManager == null)
        {
            Debug.LogError("[SkillUIChoiceManager] DeckManager가 없습니다!");
            return;
        }

        // 카드 생성
        Card newCard = new Card(
            selectedSkill.skillType,
            selectedSkill.skillName,
            selectedSkill.skillIcon
        );

        // 덱에 추가 - DeckManager의 initialDeck에 직접 추가하는 대신
        // DeckManager가 제공하는 메서드를 사용하거나, 직접 deck에 추가
        // 여기서는 ReturnCardToDeck 메서드를 활용
        deckManager.ReturnCardToDeck(newCard);

        Debug.Log($"[SkillUIChoiceManager] 스킬 선택 완료: {selectedSkill.skillName} → 덱에 추가됨");

        // 선택한 스킬 타입을 기록 (중복 방지)
        selectedSkillTypes.Add(selectedSkill.skillType);

        // 선택 횟수 증가
        currentChoiceCount++;

        // UI 닫기
        CloseSkillChoice();
    }

    /// <summary>
    /// 스킬 선택 UI 닫기
    /// </summary>
    private void CloseSkillChoice()
    {
        if (skillChoicePanel != null)
        {
            skillChoicePanel.SetActive(false);
        }

        // 게임 재개
        Time.timeScale = 1f;

        Debug.Log($"[SkillUIChoiceManager] 스킬 선택 완료 ({currentChoiceCount}/{maxChoices})");

        // 모든 선택 완료 시
        if (currentChoiceCount >= maxChoices)
        {
            OnAllChoicesComplete();
        }
    }

    /// <summary>
    /// 모든 스킬 선택 완료 시 호출
    /// </summary>
    private void OnAllChoicesComplete()
    {
        Debug.Log("[SkillUIChoiceManager] 모든 스킬 선택 완료!");

        // 덱 섞기 (선택한 카드들이 랜덤하게 분포하도록)
        if (deckManager != null)
        {
            deckManager.ShuffleDeck();
        }

        // 여기서 게임 시작 또는 다음 단계로 진행
        // 예: 초기 핸드 드로우
        // deckManager.DrawCards(3);
    }

    // ==================== Public API ====================

    /// <summary>
    /// 현재까지 선택한 횟수 반환
    /// </summary>
    public int GetCurrentChoiceCount()
    {
        return currentChoiceCount;
    }

    /// <summary>
    /// 최대 선택 횟수 반환
    /// </summary>
    public int GetMaxChoices()
    {
        return maxChoices;
    }

    /// <summary>
    /// 모든 선택을 완료했는지 여부
    /// </summary>
    public bool IsAllChoicesComplete()
    {
        return currentChoiceCount >= maxChoices;
    }

    /// <summary>
    /// 선택 가능한 스킬 개수 반환
    /// </summary>
    public int GetAvailableSkillCount()
    {
        return allAvailableSkills.Count - selectedSkillTypes.Count;
    }

    // ==================== 디버그/테스트 ====================

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 테스트용 - 스킬 선택 트리거
    /// </summary>
    [ContextMenu("Test Show Skill Choice")]
    public void TestShowSkillChoice()
    {
        ShowSkillChoice();
    }

    [ContextMenu("Test Reset Skill Pool")]
    public void TestResetSkillPool()
    {
        ResetSkillPool();
        Debug.Log($"[SkillUIChoiceManager] 리셋 완료. 선택 가능 스킬: {GetAvailableSkillCount()}개");
    }

    [ContextMenu("Print Skill Pool Status")]
    public void PrintSkillPoolStatus()
    {
        Debug.Log($"=== 스킬 풀 상태 ===");
        Debug.Log($"전체 스킬: {allAvailableSkills.Count}개");
        Debug.Log($"선택한 스킬: {selectedSkillTypes.Count}개");
        Debug.Log($"선택 가능 스킬: {GetAvailableSkillCount()}개");
        Debug.Log($"진행 상황: {currentChoiceCount}/{maxChoices}");

        if (selectedSkillTypes.Count > 0)
        {
            Debug.Log("\n[이미 선택한 스킬들]");
            foreach (var skillType in selectedSkillTypes)
            {
                Debug.Log($"  - {skillType}");
            }
        }
    }
#endif
}

/// <summary>
/// 스킬 카드의 기본 데이터 (ScriptableObject로 만들거나, 여기서는 직렬화 클래스로)
/// </summary>
[System.Serializable]
public class SkillCardData
{
    public SecondaryChargedAttackType skillType;
    public string skillName;
    public string skillDescription;
    public Sprite skillIcon;

    public SkillCardData(SecondaryChargedAttackType type, string name, string desc, Sprite icon = null)
    {
        skillType = type;
        skillName = name;
        skillDescription = desc;
        skillIcon = icon;
    }
}