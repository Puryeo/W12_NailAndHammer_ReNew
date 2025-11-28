using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Splash Projectile (스플래쉬 투사체)
/// - 직선으로 날아가며 적과 충돌
/// - 일반 적 → 그로기 상태로 전환
/// - 그로기 적 → 죽음 상태로 전환 (처형, 추가 투사체 미발생)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SplashProjectile : MonoBehaviour
{
    private Vector2 direction;
    public float speed;
    public float lifetime;
    private float damageToNormal;
    private float damageToGroggy;
    private bool showDebugLogs;

    private Rigidbody2D rb;
    private float aliveTime = 0f;

    // 이미 타격한 적 추적 (중복 타격 방지)
    private HashSet<int> hitEnemies = new HashSet<int>();

    /// <summary>
    /// 투사체 초기화
    /// </summary>
    public void Initialize(
        Vector2 direction,
        float speed,
        float lifetime,
        float damageToNormal,
        float damageToGroggy,
        bool showDebugLogs)
    {
        this.direction = direction.normalized;
        this.speed = speed;
        this.lifetime = lifetime;
        this.damageToNormal = damageToNormal;
        this.damageToGroggy = damageToGroggy;
        this.showDebugLogs = showDebugLogs;

        // Rigidbody2D 설정
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Collider 설정
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // 투사체 회전 (진행 방향으로)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (showDebugLogs)
            Debug.Log($"[SplashProjectile] 초기화 완료 - 방향: {direction}, 속도: {speed}");
    }

    private void Update()
    {
        // 이동
        transform.position += (Vector3)(direction * speed * Time.deltaTime);

        // 수명 체크
        aliveTime += Time.deltaTime;
        if (aliveTime >= lifetime)
        {
            if (showDebugLogs)
                Debug.Log($"[SplashProjectile] 수명 종료 - 파괴");
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null) return;

        // Enemy 태그 확인
        if (!collision.CompareTag("Enemy"))
        {
            return;
        }

        var enemyCtrl = collision.GetComponent<EnemyController>();
        if (enemyCtrl == null)
        {
            enemyCtrl = collision.GetComponentInParent<EnemyController>();
        }

        if (enemyCtrl == null)
        {
            if (showDebugLogs)
                Debug.Log($"[SplashProjectile] EnemyController 없음: {collision.name}");
            return;
        }

        // 이미 타격한 적은 무시
        int enemyId = enemyCtrl.GetInstanceID();
        if (hitEnemies.Contains(enemyId))
        {
            return;
        }

        hitEnemies.Add(enemyId);

        // 이미 죽은 적은 무시
        if (enemyCtrl.IsDeadState())
        {
            return;
        }

        var enemyHealth = enemyCtrl.GetComponent<HealthSystem>();
        if (enemyHealth == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[SplashProjectile] HealthSystem 없음: {enemyCtrl.name}");
            return;
        }

        // 그로기 상태 체크
        bool isGroggy = enemyCtrl.IsGroggy();

        if (isGroggy)
        {
            // 그로기 적 → 처형 (죽음 상태로 전환)
            if (showDebugLogs)
                Debug.Log($"[SplashProjectile] 그로기 적 처형: {enemyCtrl.name}");

            // 높은 데미지로 즉사
            enemyHealth.TakeDamage(damageToGroggy);

            // 처형 이펙트 재생
            var he = enemyHealth.GetComponent<HitEffect>();
            if (he != null) he.PlayExecuteEffect();

            var hpe = enemyHealth.GetComponent<HitParticleEffect>();
            if (hpe != null) hpe.PlayExecuteParticle(collision.transform.position);

            // 강한 히트 효과
            HitEffectManager.PlayHitEffect(
                EHitSource.Hammer,
                EHitStopStrength.Strong,
                EShakeStrength.Strong,
                collision.transform.position
            );

            // 즉시 사망 처리
            enemyHealth.ForceDieWithFade(1f);

            // 스택 소모 (즉시 회수 모드)
            enemyCtrl.ConsumeStacks(startReturn: false, awardImmediatelyToPlayer: false, awardTarget: null);

            // 처형 플래그 설정
            enemyCtrl.MarkExecuted();
        }
        else
        {
            // 일반 적 → 그로기 상태로 전환
            if (showDebugLogs)
                Debug.Log($"[SplashProjectile] 일반 적 타격 (그로기 전환): {enemyCtrl.name}");

            // 데미지 적용
            enemyHealth.TakeDamage(damageToNormal);

            // 스턴 적용
            enemyCtrl.ApplyStun(0.3f);

            // 일반 히트 효과
            HitEffectManager.PlayHitEffect(
                EHitSource.Hammer,
                EHitStopStrength.Medium,
                EShakeStrength.Medium,
                collision.transform.position
            );

            // 히트 등록 (플레이어 찾기)
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                var playerCombat = playerObj.GetComponent<PlayerCombat>();
                if (playerCombat != null)
                {
                    enemyCtrl.RegisterHit(1, playerCombat.transform);
                }
            }
        }

        // 투사체 파괴 (적 1명만 타격)
        Destroy(gameObject);
    }
}