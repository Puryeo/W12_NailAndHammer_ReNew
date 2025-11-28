using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스킬 선택 버튼 UI 컴포넌트
/// - 스킬 정보 표시 (아이콘, 이름, 설명)
/// - 클릭 시 스킬 선택
/// </summary>
public class SkillChoiceButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image skillIconImage;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI skillDescriptionText;
    [SerializeField] private Button button;

    private SkillCardData currentSkillData;
    private SkillUIChoiceManager choiceManager;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        // 버튼 클릭 이벤트 등록
        button.onClick.AddListener(OnButtonClicked);
    }

    /// <summary>
    /// 스킬 데이터를 버튼에 설정
    /// </summary>
    public void SetSkillData(SkillCardData skillData, SkillUIChoiceManager manager)
    {
        currentSkillData = skillData;
        choiceManager = manager;

        // UI 업데이트
        UpdateUI();
    }

    /// <summary>
    /// UI 요소 업데이트
    /// </summary>
    private void UpdateUI()
    {
        if (currentSkillData == null) return;

        // 아이콘 설정
        if (skillIconImage != null && currentSkillData.skillIcon != null)
        {
            skillIconImage.sprite = currentSkillData.skillIcon;
            skillIconImage.enabled = true;
        }
        else if (skillIconImage != null)
        {
            skillIconImage.enabled = false;
        }

        // 스킬 이름 설정
        if (skillNameText != null)
        {
            skillNameText.text = currentSkillData.skillName;
        }

        // 스킬 설명 설정
        if (skillDescriptionText != null)
        {
            skillDescriptionText.text = currentSkillData.skillDescription;
        }
    }

    /// <summary>
    /// 버튼 클릭 시 호출
    /// </summary>
    private void OnButtonClicked()
    {
        if (currentSkillData == null || choiceManager == null)
        {
            Debug.LogWarning("[SkillChoiceButton] 스킬 데이터 또는 매니저가 설정되지 않았습니다!");
            return;
        }

        // 스킬 선택을 매니저에 전달
        choiceManager.OnSkillSelected(currentSkillData);

        Debug.Log($"[SkillChoiceButton] 버튼 클릭: {currentSkillData.skillName}");
    }

    private void OnDestroy()
    {
        // 버튼 이벤트 해제
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
    }
}