using UnityEngine;

/// <summary>
/// 투사체 회수 처리 인터페이스
/// 다양한 회수 방식을 Strategy Pattern으로 구현
/// </summary>
public interface IProjectileRetrievalBehavior
{
    /// <summary>
    /// 회수 시작
    /// </summary>
    void StartRetrieval(AttackProjectile projectile, Transform player);

    /// <summary>
    /// 회수 경로상 적과 충돌 시 처리
    /// </summary>
    void OnReturnPathHit(AttackProjectile projectile, Collider2D target);
}
