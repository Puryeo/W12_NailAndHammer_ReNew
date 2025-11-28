using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SplashHammerController
/// - 망치 이동(내려찍기) 제어
/// - 타격 판정 및 결과 처리 (일반 타격 vs 처형 + 투사체 발사)
/// </summary>
public class SplashHammerController : MonoBehaviour
{
    [Header("Motion Settings")]
    [Tooltip("스윙 속도 곡선")]
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("스윙 시작 각도 (목표 각도 기준 뒤로 몇 도?)")]
    [SerializeField] private float swingRange = 120f;

    [Tooltip("스프라이트 회전 보정값 (-90: 위쪽이 앞인 스프라이트)")]
    [SerializeField] private float spriteRotationOffset = -90f;

    [Header("Behaviour")]
    [SerializeField] private float quickStun = 0.12f; // 일반 타격 경직 시간

    // 내부 변수
    private PlayerCombat ownerCombat;
    private float damage;
    private float knockback;
    private float swingDuration;
    private float hitRadius;

    // 투사체 데이터
    private GameObject projectilePrefab;
    private float projectileSpeed;
    private float projectileLifetime;
    private float projectileDamageToNormal;
    private float projectileDamageToGroggy;
    private bool showDebugLogs;

    private float targetZAngle;
    private bool isSwinging = false;

    // 초기화 (SplashSkill에서 호출)
    public void Initialize(
        PlayerCombat owner,
        float damage,
        float knockback,
        float swingDuration,
        float hitRadius,
        GameObject projectilePrefab,
        float projectileSpeed,
        float projectileLifetime,
        float projectileDamageToNormal,
        float projectileDamageToGroggy,
        float targetAngle,
        bool showDebugLogs)
    {
        this.ownerCombat = owner;
        this.damage = damage;
        this.knockback = knockback;
        this.swingDuration = Mathf.Max(0.01f, swingDuration);
        this.hitRadius = hitRadius;

        this.projectilePrefab = projectilePrefab;
        this.projectileSpeed = projectileSpeed;
        this.projectileLifetime = projectileLifetime;
        this.projectileDamageToNormal = projectileDamageToNormal;
        this.projectileDamageToGroggy = projectileDamageToGroggy;
        this.targetZAngle = targetAngle;
        this.showDebugLogs = showDebugLogs;

        StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        isSwinging = true;

        // 1. 각도 계산
        float finalAngle = targetZAngle + spriteRotationOffset;
        float startAngle = finalAngle + swingRange;
        float elapsed = 0f;

        // 2. 스윙 애니메이션
        while (elapsed < swingDuration)
        {
            float t = Mathf.Clamp01(elapsed / swingDuration);
            float curveValue = speedCurve.Evaluate(t);
            float currentAngle = Mathf.Lerp(startAngle, finalAngle, curveValue);

            transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. 최종 위치 확정
        transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);

        // 4. 타격 판정 실행
        CheckImpact();

        // 5. 잠시 후 삭제
        Destroy(gameObject, 0.1f);
        isSwinging = false;
    }

    /// <summary>
    /// 타격 판정 로직
    /// </summary>
    private void CheckImpact()
    {
        // 타격점: 해머 머리 부분
        Vector2 impactPoint = transform.TransformPoint(new Vector3(1.5f, 0, 0));

        // 범위 내 적 검출
        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPoint, hitRadius);
        HashSet<int> alreadyHitIds = new HashSet<int>();

        foreach (var c in hits)
        {
            if (!c.CompareTag("Enemy")) continue;

            var enemyCtrl = c.GetComponent<EnemyController>() ?? c.GetComponentInParent<EnemyController>();
            if (enemyCtrl == null) continue;

            // 중복 타격 방지
            int id = enemyCtrl.GetInstanceID();
            if (alreadyHitIds.Contains(id)) continue;
            alreadyHitIds.Add(id);

            // 그로기 상태인가?
            if (enemyCtrl.IsGroggy())
            {
                // 처형 + 투사체 발사
                ProcessGroggyHit(enemyCtrl, c.transform.position, c.GetComponent<Collider2D>());
            }
            else
            {
                // 일반 타격
                ProcessNormalHit(enemyCtrl, c.transform.position, impactPoint);
            }
        }
    }

    // -----------------------------------------------------------------------
    // [처형 로직] - 투사체 발사 포함 (SpineSkill 방식 그대로)
    // -----------------------------------------------------------------------
    private void ProcessGroggyHit(EnemyController enemy, Vector3 hitPos, Collider2D enemyCollider)
    {
        if (showDebugLogs)
            Debug.Log($"[SplashHammer] {enemy.name} 처형 시작 - 투사체 발사!");

        // 말뚝 회수 및 처형 마킹
        enemy.ConsumeStacks(startReturn: false, awardImmediatelyToPlayer: false, awardTarget: null);
        enemy.MarkExecuted();

        // 처형 이펙트 및 사망 처리
        var enemyHealth = enemy.GetComponent<HealthSystem>();
        if (enemyHealth != null)
        {
            var he = enemyHealth.GetComponent<HitEffect>();
            if (he != null) he.PlayExecuteEffect();

            var hpe = enemyHealth.GetComponent<HitParticleEffect>();
            if (hpe != null) hpe.PlayExecuteParticle(hitPos);

            // 즉시 사망 처리 (ForceDieWithFade)
            enemyHealth.ForceDieWithFade(1f);
        }

        // 강한 타격감
        HitEffectManager.PlayHitEffect(
            EHitSource.Hammer,
            EHitStopStrength.Strong,
            EShakeStrength.Strong,
            hitPos
        );

        // 플레이어 보상 (선택 사항 - SpineSkill에는 없지만 필요하면 유지)
        if (ownerCombat != null)
        {
            ownerCombat.OnExecutionSuccess(healAmount: 30f, ammoReward: 0);
        }

        // 투사체 8방향 발사 (처형된 적 콜라이더 무시)
        SpawnProjectiles(hitPos, enemyCollider);

        if (showDebugLogs)
            Debug.Log($"[SplashHammer] {enemy.name} 처형 완료!");
    }

    // 일반 타격
    private void ProcessNormalHit(EnemyController enemy, Vector3 hitPos, Vector2 sourcePos)
    {
        if (showDebugLogs)
            Debug.Log($"[SplashHammer] {enemy.name} 일반 타격");

        var health = enemy.GetComponent<HealthSystem>();
        var rb = enemy.GetComponent<Rigidbody2D>();

        // 데미지
        if (health != null) health.TakeDamage(damage);

        // 경직 (Stun)
        if (quickStun > 0f) enemy.ApplyStun(quickStun);

        // 넉백 (Knockback)
        if (rb != null)
        {
            Vector2 dir = (hitPos - (Vector3)sourcePos).normalized;
            rb.AddForce(dir * knockback, ForceMode2D.Impulse);
        }

        // 타격 이펙트 (일반 강도)
        HitEffectManager.PlayHitEffect(
            EHitSource.Hammer,
            EHitStopStrength.Weak,
            EShakeStrength.Weak,
            hitPos
        );

        // 말뚝 회수 및 히트 등록
        enemy.RegisterHit(1, ownerCombat.transform);
        enemy.ConsumeStacks(startReturn: true, awardImmediatelyToPlayer: true, awardTarget: ownerCombat);
    }

    /// <summary>
    /// 투사체 8방향 발사 (상하좌우 + 대각선)
    /// 처형된 적의 콜라이더와 충돌 무시
    /// </summary>
    private void SpawnProjectiles(Vector3 spawnPosition, Collider2D ignoreCollider)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[SplashHammer] projectilePrefab이 없어 투사체를 발사할 수 없습니다!");
            return;
        }

        // 2D 8방향 벡터
        Vector2[] directions = new Vector2[]
        {
            Vector2.up,                          // 위 (0도)
            new Vector2(1, 1).normalized,        // 우상 (45도)
            Vector2.right,                       // 오른쪽 (90도)
            new Vector2(1, -1).normalized,       // 우하 (135도)
            Vector2.down,                        // 아래 (180도)
            new Vector2(-1, -1).normalized,      // 좌하 (225도)
            Vector2.left,                        // 왼쪽 (270도)
            new Vector2(-1, 1).normalized        // 좌상 (315도)
        };

        foreach (var direction in directions)
        {
            // 투사체 생성
            GameObject projObj = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

            // SplashProjectile 컴포넌트 초기화
            var splashProj = projObj.GetComponent<SplashProjectile>();
            if (splashProj != null)
            {
                splashProj.Initialize(
                    direction: direction,
                    speed: projectileSpeed,
                    lifetime: projectileLifetime,
                    damageToNormal: projectileDamageToNormal,
                    damageToGroggy: projectileDamageToGroggy,
                    showDebugLogs: true // 디버그 활성화
                );

                // 처형된 적의 콜라이더와 충돌 무시
                if (ignoreCollider != null)
                {
                    Collider2D projCollider = projObj.GetComponent<Collider2D>();
                    if (projCollider != null)
                    {
                        Physics2D.IgnoreCollision(projCollider, ignoreCollider, true);

                        if (showDebugLogs)
                            Debug.Log($"[SplashHammer] 투사체-적 충돌 무시 설정 완료");
                    }
                }
            }
            else
            {
                Debug.LogError("[SplashHammer] projectilePrefab에 SplashProjectile 컴포넌트가 없습니다!");
                Destroy(projObj);
            }
        }

        if (showDebugLogs)
            Debug.Log($"[SplashHammer] 8방향 투사체 발사 완료 (위치: {spawnPosition})");
    }

    private void OnDrawGizmos()
    {
        if (isSwinging)
        {
            Gizmos.color = Color.red;
            Vector2 impactPoint = transform.TransformPoint(new Vector3(1.5f, 0, 0));
            Gizmos.DrawWireSphere(impactPoint, hitRadius);
        }
    }
}