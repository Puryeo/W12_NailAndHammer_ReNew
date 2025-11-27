using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button button;

    private SkillData currentSkill;
    private SkillSelectionUI uiManager;

    void Start()
    {
        // Button 컴포넌트 자동 찾기
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button == null)
        {
            Debug.LogError($"SkillButton [{gameObject.name}]: Button 컴포넌트를 찾을 수 없습니다!");
        }
    }

    public void SetSkill(SkillData skill, SkillSelectionUI manager)
    {
        if (skill == null)
        {
            Debug.LogError($"SkillButton [{gameObject.name}]: 스킬 데이터가 null입니다!");
            return;
        }

        currentSkill = skill;
        uiManager = manager;

        // 아이콘 설정
        if (iconImage != null && skill.skillIcon != null)
        {
            iconImage.sprite = skill.skillIcon;
            iconImage.enabled = true;
        }
        else if (iconImage != null)
        {
            // 아이콘이 없으면 기본 색상으로
            iconImage.enabled = true;
        }

        // 이름 설정
        if (nameText != null)
        {
            nameText.text = skill.skillName;
        }
        else
        {
            Debug.LogWarning($"SkillButton [{gameObject.name}]: Name Text가 연결되지 않았습니다!");
        }

        // 설명 설정
        if (descriptionText != null)
        {
            descriptionText.text = skill.description;
        }
        else
        {
            Debug.LogWarning($"SkillButton [{gameObject.name}]: Description Text가 연결되지 않았습니다!");
        }

        // 버튼 클릭 이벤트 등록
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        Debug.Log($"SkillButton [{gameObject.name}]: 스킬 설정 완료 - {skill.skillName}");
    }

    void OnClick()
    {
        if (uiManager != null && currentSkill != null)
        {
            Debug.Log($"SkillButton [{gameObject.name}]: 버튼 클릭됨 - {currentSkill.skillName}");
            uiManager.OnSkillSelected(currentSkill);
        }
        else
        {
            Debug.LogError($"SkillButton [{gameObject.name}]: UI Manager 또는 스킬이 없습니다!");
        }
    }
}