// RelicItem.cs
using UnityEngine;

public class RelicItem : MonoBehaviour
{
    [SerializeField] private SkillData[] availableSkills; // Inspector에서 할당

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PickupRelic();
        }
    }

    void PickupRelic()
    {
        // 랜덤으로 2개 스킬 선택
        SkillData skill1 = GetRandomSkill();
        SkillData skill2 = GetRandomSkill();

        // 같은 스킬이 안 나오도록 (선택적)
        while (skill2 == skill1 && availableSkills.Length > 1)
        {
            skill2 = GetRandomSkill();
        }

        // UI 띄우기
        SkillSelectionUI.Instance.ShowSkillSelection(skill1, skill2);

        Destroy(gameObject);
    }

    SkillData GetRandomSkill()
    {
        return availableSkills[Random.Range(0, availableSkills.Length)];
    }
}