using UnityEngine;

[CreateAssetMenu(fileName = "New Health Skill", menuName = "Skills/Player Health Skill")]
public class PlayerHealthSkillSO : SkillEffectSO
{
    [Header("체력 스킬 설정")]
    [SerializeField] private float healthChange = 50f; // 양수면 증가, 음수면 감소
    [SerializeField] private bool alsoHealCurrent = false; // 현재 체력도 회복할지?

    public override void ApplyEffect()
    {
        Debug.Log($"PlayerHealthSkillSO: {GetSkillName()} 발동! 체력 {healthChange}");

        // Player 찾기
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("PlayerHealthSkillSO: Player를 찾을 수 없어요!");
            return;
        }

        // HealthSystem 찾기
        HealthSystem playerHealth = player.GetComponent<HealthSystem>();
        if (playerHealth == null)
        {
            Debug.LogError("PlayerHealthSkillSO: Player에 HealthSystem이 없어요!");
            return;
        }

        // 체력 변경
        playerHealth.ModifyMaxHealth(healthChange);

        // 체력 증가면서 현재 체력도 회복
        if (alsoHealCurrent && healthChange > 0)
        {
            playerHealth.Heal(healthChange);
        }

        Debug.Log("PlayerHealthSkillSO: 스킬 적용 완료!");
    }
}