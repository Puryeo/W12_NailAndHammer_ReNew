using UnityEngine;

/// <summary>
/// 꿰뚫기 충돌 처리 Behavior
/// - 적을 꿰뚫고 계속 날아감
/// - 벽에 충돌 시 적들과 함께 박힘
/// </summary>
public class ImpaleEnemyBehavior : IProjectileCollisionBehavior
{
    [SerializeField] private bool showDebugLogs = false;

    public ImpaleEnemyBehavior(bool debugLogs = false)
    {
        this.showDebugLogs = debugLogs;
    }

    public void OnHitEnemy(AttackProjectile projectile, Collider2D enemy, Vector2 collisionPoint)
    {
        // ImpaledEnemyManager를 통해 적 꿰뚫기 (충돌 지점 전달)
        bool impaled = projectile.ImpaledManager.TryImpaleEnemy(enemy, collisionPoint);

        if (impaled)
        {
            // 첫 번째 적을 꿰뚫었을 때 상태 전환
            if (projectile.ImpaledManager.ImpaledCount == 1)
            {
                projectile.ChangeState(ProjectileState.Impaling);

                // (옵션) 속도 증가
                if (projectile.Config.accelerateOnImpale)
                {
                    var rb = projectile.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.linearVelocity *= projectile.Config.impaleSpeedMultiplier;
                        if (showDebugLogs)
                            Debug.Log($"ImpaleEnemyBehavior: 투사체 가속 (x{projectile.Config.impaleSpeedMultiplier})");
                    }
                }
            }

            // 계속 비행 (멈추지 않음!)
        }
    }

    public void OnHitWall(AttackProjectile projectile, Collider2D wall)
    {
        // 벽에 박힘
        var rb = projectile.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        // 투사체를 벽에 부착
        projectile.StickToWall(wall);

        // 꿰뚫린 적들 처리
        projectile.ImpaledManager.OnWallImpact();

        // 상태 변경
        projectile.ChangeState(ProjectileState.Stuck);

        // 피격 이펙트
        HitEffectManager.PlayHitEffect(
            EHitSource.Stake,
            EHitStopStrength.Medium,
            EShakeStrength.Medium,
            projectile.transform.position
        );

        if (showDebugLogs)
            Debug.Log($"ImpaleEnemyBehavior: 벽에 박힘 (꿰뚫린 적: {projectile.ImpaledManager.ImpaledCount}마리)");
    }
}
