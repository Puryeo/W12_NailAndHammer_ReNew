using UnityEngine;

/// <summary>
/// HealthSystem 확장 메서드
/// 원본 HealthSystem.cs를 수정하지 않고 기능 추가
/// </summary>
public static class HealthSystemExtensions
{
    /// <summary>
    /// 최대 체력 변경 (스킬 시스템용)
    /// </summary>
    public static void ModifyMaxHealth(this HealthSystem health, float amount)
    {
        float currentMax = health.GetMaxHealth();
        float newMax = currentMax + amount;

        health.SetMaxHealthDirect(newMax);

        Debug.Log($"HealthSystem Extension: 최대 체력 변경! {amount:+0;-0} → 현재 최대 체력: {newMax}");
    }

    /// <summary>
    /// 최대 체력 직접 설정 - Reflection 사용
    /// </summary>
    private static void SetMaxHealthDirect(this HealthSystem health, float newMaxHealth)
    {
        // Reflection으로 private 필드에 접근
        var field = typeof(HealthSystem).GetField("maxHealth",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(health, newMaxHealth);

            // 현재 체력이 새로운 최대치를 넘으면 조정
            float currentHealth = health.GetCurrentHealth();
            if (currentHealth > newMaxHealth)
            {
                // 초과 체력만큼 데미지 (0으로 만들지 않도록)
                float excessHealth = currentHealth - newMaxHealth;
                health.TakeDamage(excessHealth);
            }
        }
        else
        {
            Debug.LogError("HealthSystem Extension: maxHealth 필드를 찾을 수 없습니다!");
        }
    }
}