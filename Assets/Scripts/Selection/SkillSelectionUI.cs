// SkillSelectionUI.cs
using UnityEngine;

public class SkillSelectionUI : MonoBehaviour
{
    public static SkillSelectionUI Instance;

    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private SkillButton button1;
    [SerializeField] private SkillButton button2;

    void Awake()
    {
        if (Instance == null) Instance = this;
        selectionPanel.SetActive(false);
    }

    public void ShowSkillSelection(SkillData skill1, SkillData skill2)
    {
        selectionPanel.SetActive(true);
        Time.timeScale = 0; // 게임 일시정지

        button1.SetSkill(skill1, this);
        button2.SetSkill(skill2, this);
    }

    public void OnSkillSelected(SkillData selectedSkill)
    {
        Debug.Log($"선택된 스킬: {selectedSkill.skillName}");

        // 팀원의 스킬 매니저로 전달
        SkillSystemConnector.Instance.SendSkillToPlayer(selectedSkill);

        CloseSelection();
    }

    private void CloseSelection()
    {
        selectionPanel.SetActive(false);
        Time.timeScale = 1;
    }
}