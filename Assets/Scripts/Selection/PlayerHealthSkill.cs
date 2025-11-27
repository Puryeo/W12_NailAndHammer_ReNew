using UnityEngine;

public class PlayerHealthSkill : MonoBehaviour, ISkillEffect
{
    [Header("체력 스킬 설정")]
    [SerializeField] private HealthSystem playerHealth;
    [SerializeField] private float healthChange = 50f; // 양수면 증가, 음수면 감소
    [SerializeField] private bool alsoHealCurrent = false; // 현재 체력도 회복할지?

    void Start()
    {
        Debug.Log("PlayerHealthSkill: 준비 완료!");
    }

    // ISkillEffect 인터페이스에서 요구하는 함수
    public void ApplyEffect()
    {
        Debug.Log($"PlayerHealthSkill: 체력 {healthChange} 적용!");

        if (playerHealth != null)
        {
            playerHealth.ModifyMaxHealth(healthChange);

            // 체력 증가면서 현재 체력도 회복
            if (alsoHealCurrent && healthChange > 0)
            {
                playerHealth.Heal(healthChange);
            }
        }
        else
        {
            Debug.LogError("PlayerHealthSkill: Player Health가 연결 안됨!");
        }
    }
}