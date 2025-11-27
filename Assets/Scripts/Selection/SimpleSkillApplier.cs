using UnityEngine;

public class SimpleSkillApplier : MonoBehaviour
{
    [Header("패널 설정")]
    [SerializeField] private GameObject panelToClose;

    [Header("스킬 목록 (여기에 스킬 ScriptableObject들을 추가하세요!)")]
    [SerializeField] private SkillEffectSO[] skillEffects; // S.O. 배열!

    void Start()
    {
        Debug.Log("SimpleSkillApplier: 시작!");
    }

    // 버튼 A - 첫 번째 스킬 실행
    public void OnButtonA_Click()
    {
        Debug.Log("SimpleSkillApplier: 버튼 A 클릭됨!");
        ApplySkill(0); // 배열의 0번 스킬 실행
        ClosePanel();
    }

    // 버튼 B - 두 번째 스킬 실행
    public void OnButtonB_Click()
    {
        Debug.Log("SimpleSkillApplier: 버튼 B 클릭됨!");
        ApplySkill(1); // 배열의 1번 스킬 실행
        ClosePanel();
    }

    // 스킬 실행 함수
    void ApplySkill(int index)
    {
        // 배열이 비어있는지 확인
        if (skillEffects == null || skillEffects.Length == 0)
        {
            Debug.LogError("SimpleSkillApplier: 스킬이 하나도 없어요!");
            return;
        }

        // 인덱스가 범위 안에 있는지 확인
        if (index < 0 || index >= skillEffects.Length)
        {
            Debug.LogError($"SimpleSkillApplier: {index}번 스킬이 없어요!");
            return;
        }

        // 해당 스킬이 null인지 확인
        if (skillEffects[index] == null)
        {
            Debug.LogError($"SimpleSkillApplier: {index}번 슬롯이 비어있어요!");
            return;
        }

        // 스킬 실행!
        Debug.Log($"SimpleSkillApplier: {skillEffects[index].GetSkillName()} 실행!");
        skillEffects[index].ApplyEffect();
    }

    void ClosePanel()
    {
        Debug.Log("SimpleSkillApplier: 패널 닫기!");
        if (panelToClose != null)
        {
            panelToClose.SetActive(false);
        }
        Time.timeScale = 1; // 게임 재개
    }
}