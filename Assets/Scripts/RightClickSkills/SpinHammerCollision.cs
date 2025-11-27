using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 팽이 처형 스킬 - 망치 충돌 처리
/// - HammerSwingController와 동일한 로직 사용
/// </summary>
public class SpinHammerCollision : MonoBehaviour
{
    private SpinExecutionController controller;
    private float normalDamage;
    private float executeHealAmount;
    private int executeAmmoReward;
    private PlayerCombat playerCombat;

    [Header("Execution")]
    [Tooltip("처형 시 적용할 넉백 강도")]
    [SerializeField] private float executeKnockbackForce = 12f;

    [Header("Behaviour")]
    [Tooltip("빠른 스턴 지속시간")]
    [SerializeField] private float quickStun = 0.12f;

    // 중복 타격 방지용
    private Dictionary<Collider2D, float> lastHitTime = new Dictionary<Collider2D, float>();
    private float hitCooldown = 0.1f; // 같은 적을 0.1초마다 한 번씩만 타격
    private HashSet<int> alreadyHitIds = new HashSet<int>();

    public void Initialize(
        SpinExecutionController controller,
        float normalDamage,
        float executeHealAmount,
        int executeAmmoReward,
        PlayerCombat playerCombat)
    {
        this.controller = controller;
        this.normalDamage = normalDamage;
        this.executeHealAmount = executeHealAmount;
        this.executeAmmoReward = executeAmmoReward;
        this.playerCombat = playerCombat;

        alreadyHitIds.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ProcessHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 쿨타임 체크
        if (lastHitTime.ContainsKey(other))
        {
            if (Time.time - lastHitTime[other] < hitCooldown)
            {
                return; // 아직 쿨타임
            }
        }

        ProcessHit(other);
    }

    private void ProcessHit(Collider2D other)
    {
        // 적 태그 체크
        if (!other.CompareTag("Enemy"))
        {
            return;
        }

        // 마지막 타격 시간 갱신
        lastHitTime[other] = Time.time;

        // EnemyController 찾기 (HammerSwingController와 동일한 방식)
        var enemyCtrl = other.GetComponent<EnemyController>() ?? other.GetComponentInParent<EnemyController>();
        var enemyHealth = other.GetComponent<HealthSystem>() ?? other.GetComponentInParent<HealthSystem>();

        if (enemyCtrl == null)
        {
            Debug.LogWarning($"[SpinHammer] {other.name}에서 EnemyController를 찾을 수 없습니다!");
            return;
        }

        // 중복 타격 방지
        int id = enemyCtrl.GetInstanceID();
        if (alreadyHitIds.Contains(id))
        {
            return;
        }
        alreadyHitIds.Add(id);

        // ===== HammerSwingController와 동일한 처형 로직 =====

        // 1. 그로기 상태면 처형!
        if (enemyCtrl.IsGroggy())
        {
            Debug.Log($"[SpinHammer] 처형! {enemyCtrl.name}");

            // 스택 소모 (보상 획득)
            int stackReward = enemyCtrl.ConsumeStacks(true, true, playerCombat);

            // 처형 마크
            enemyCtrl.MarkExecuted();

            // 플레이어 회복 + 탄약 보상
            if (playerCombat != null)
            {
                playerCombat.OnExecutionSuccess(executeHealAmount, executeAmmoReward);
            }

            // 넉백 적용
            Rigidbody2D targetRb = other.GetComponent<Rigidbody2D>() ?? other.GetComponentInParent<Rigidbody2D>();
            if (targetRb != null)
            {
                Vector2 dir = (other.transform.position - transform.position).normalized;
                targetRb.AddForce(dir * executeKnockbackForce, ForceMode2D.Impulse);
            }

            // 처형 이펙트
            if (enemyHealth != null)
            {
                var he = enemyHealth.GetComponent<HitEffect>();
                if (he != null) he.PlayExecuteEffect();

                var hpe = enemyHealth.GetComponent<HitParticleEffect>();
                if (hpe != null) hpe.PlayExecuteParticle(other.transform.position);
            }

            // 히트 이펙트 (강함)
            HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Strong, EShakeStrength.Strong, other.transform.position);

            // 적 죽이기
            if (enemyHealth != null)
            {
                enemyHealth.ForceDieWithFade(1f);
            }
        }
        // 2. 그로기 아니면 일반 피해
        else
        {
            Debug.Log($"[SpinHammer] 일반 타격 {enemyCtrl.name} ({normalDamage} 데미지)");

            // 데미지
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(normalDamage);
            }

            // 빠른 스턴 적용
            if (quickStun > 0f)
            {
                enemyCtrl.ApplyStun(quickStun);
            }

            // 넉백 적용
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>() ?? other.GetComponentInParent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 dir = (other.transform.position - transform.position).normalized;
                rb.AddForce(dir * normalDamage, ForceMode2D.Impulse); // normalDamage를 넉백으로 사용 (조정 가능)
            }

            // 히트 이펙트 (약함)
            HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Weak, EShakeStrength.Weak, other.transform.position);

            // 히트 등록 + 스택 소모
            enemyCtrl.RegisterHit(1, transform);
            enemyCtrl.ConsumeStacks(true, true, playerCombat);
        }
    }
}