using UnityEngine;

// ScriptableObject 베이스 클래스
public abstract class SkillEffectSO : ScriptableObject, ISkillEffect
{
    [Header("스킬 정보")]
    [SerializeField] private string skillName = "새 스킬";
    [TextArea(2, 4)]
    [SerializeField] private string description = "스킬 설명을 입력하세요";

    // 자식 클래스가 이걸 구현해야 해!
    public abstract void ApplyEffect();

    // 스킬 이름 가져오기
    public string GetSkillName()
    {
        return skillName;
    }

    // 설명 가져오기
    public string GetDescription()
    {
        return description;
    }
}