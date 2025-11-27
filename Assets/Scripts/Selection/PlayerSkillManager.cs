using UnityEngine;

/// <summary>
/// 테스트용 플레이어 스킬 매니저
/// 팀원이 실제 스킬 시스템 만들 때까지 임시로 사용
/// </summary>
public class PlayerSkillManager : MonoBehaviour, ISkillReceiver
{
    [Header("References")]
    [SerializeField] private HealthSystem healthSystem;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    void Start()
    {
        // HealthSystem 자동 찾기
        if (healthSystem == null)
        {
            healthSystem = GetComponent<HealthSystem>();

            if (healthSystem == null)
            {
                Debug.LogError("PlayerSkillManager: HealthSystem을 찾을 수 없습니다!");
            }
            else
            {
                if (showDebugLogs)
                {
                    Debug.Log("PlayerSkillManager: HealthSystem 자동 연결 완료!");
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log("PlayerSkillManager: 초기화 완료!");
        }
    }

    /// <summary>
    /// ISkillReceiver 인터페이스 구현
    /// UI에서 선택한 스킬을 받아서 적용
    /// </summary>
    public void ApplySkill(SkillData skill)
    {
        if (skill == null)
        {
            Debug.LogError("PlayerSkillManager: 스킬 데이터가 null입니다!");
            return;
        }

        if (healthSystem == null)
        {
            Debug.LogError("PlayerSkillManager: HealthSystem이 없어서 스킬을 적용할 수 없습니다!");
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log($"PlayerSkillManager: 스킬 적용 시작 - {skill.skillName} (ID: {skill.skillID})");
        }

        // 스킬 ID에 따라 다른 효과 적용
        switch (skill.skillID)
        {
            case 1: // 테스트 스킬 A: 최대 체력 -50
                ApplySkill_HealthDown();
                break;

            case 2: // 테스트 스킬 B: 최대 체력 +50
                ApplySkill_HealthUp();
                break;

            case 3: // 추가 테스트용 (나중에 사용 가능)
                Debug.Log("PlayerSkillManager: 스킬 ID 3 - 아직 미구현");
                break;

            default:
                Debug.LogWarning($"PlayerSkillManager: 알 수 없는 스킬 ID {skill.skillID}");
                break;
        }
    }

    /// <summary>
    /// 스킬 A: 최대 체력 -50 (총 50)
    /// </summary>
    private void ApplySkill_HealthDown()
    {
        if (showDebugLogs)
        {
            Debug.Log("PlayerSkillManager: 스킬 A 적용 - 최대 체력 -50");
        }

        // 확장 메서드 사용!
        healthSystem.ModifyMaxHealth(-50f);
    }

    /// <summary>
    /// 스킬 B: 최대 체력 +50 (총 150)
    /// </summary>
    private void ApplySkill_HealthUp()
    {
        if (showDebugLogs)
        {
            Debug.Log("PlayerSkillManager: 스킬 B 적용 - 최대 체력 +50");
        }

        // 확장 메서드 사용!
        healthSystem.ModifyMaxHealth(50f);

        // 선택: 현재 체력도 회복
        healthSystem.Heal(50f);
    }
}