using UnityEngine;

/// <summary>
/// 기본 충돌 처리 Behavior
/// - 적에 충돌 시 바로 박힘 (기존 동작)
/// - 벽에 충돌 시 바로 박힘
/// </summary>
public class StickToEnemyBehavior : IProjectileCollisionBehavior
{
    [SerializeField] private bool showDebugLogs = false;

    public StickToEnemyBehavior(bool debugLogs = false)
    {
        this.showDebugLogs = debugLogs;
    }

    public void OnHitEnemy(AttackProjectile projectile, Collider2D enemy, Vector2 collisionPoint)
    {
        // 데미지 및 히트 처리
        ApplyDamageAndEffects(projectile, enemy);

        // 적에 박힘
        projectile.StickToEnemy(enemy);

        // 상태 변경
        projectile.ChangeState(ProjectileState.Stuck);

        if (showDebugLogs)
            Debug.Log($"StickToEnemyBehavior: 적 {enemy.name}에 박힘");
    }

    public void OnHitWall(AttackProjectile projectile, Collider2D wall)
    {
        // 벽에 박힘
        projectile.StickToWall(wall);

        // 상태 변경
        projectile.ChangeState(ProjectileState.Stuck);

        if (showDebugLogs)
            Debug.Log($"StickToEnemyBehavior: 벽에 박힘");
    }

    private void ApplyDamageAndEffects(AttackProjectile projectile, Collider2D target)
    {
        float finalDamage = projectile.Config.damage;

        HealthSystem hs = target.GetComponent<HealthSystem>() ?? target.GetComponentInParent<HealthSystem>();
        if (hs != null)
        {
            hs.TakeDamage(finalDamage);
        }

        var enemyCtrl = target.GetComponent<EnemyController>() ?? target.GetComponentInParent<EnemyController>();
        if (enemyCtrl != null)
        {
            enemyCtrl.RegisterHit(1, projectile.Attacker);
        }

        // 히트 이펙트
        HitEffectManager.PlayHitEffect(
            EHitSource.Stake,
            EHitStopStrength.Weak,
            EShakeStrength.Weak,
            projectile.transform.position
        );
    }
}
