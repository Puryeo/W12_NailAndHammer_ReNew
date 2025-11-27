using UnityEngine;

/// <summary>
/// 투사체 충돌 처리 인터페이스
/// 다양한 충돌 처리 방식을 Strategy Pattern으로 구현
/// </summary>
public interface IProjectileCollisionBehavior
{
    /// <summary>
    /// 적과 충돌 시 처리
    /// </summary>
    void OnHitEnemy(AttackProjectile projectile, Collider2D enemy, Vector2 collisionPoint);

    /// <summary>
    /// 벽과 충돌 시 처리
    /// </summary>
    void OnHitWall(AttackProjectile projectile, Collider2D wall);
}
