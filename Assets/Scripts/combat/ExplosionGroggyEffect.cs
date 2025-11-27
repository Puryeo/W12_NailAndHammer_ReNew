using UnityEngine;

/// <summary>
/// 그로기 회복 시 폭발하는 효과 예제
/// IGroggyEffect를 구현한 커스텀 그로기 효과입니다.
/// EnemyController에 컴포넌트로 추가하고 GroggySettings.additionalEffects에 등록하세요.
/// </summary>
public class ExplosionGroggyEffect : MonoBehaviour, IGroggyEffect
{
    [Header("Explosion Settings")]
    [Tooltip("폭발 반경")]
    [SerializeField] private float explosionRadius = 5f;

    [Tooltip("폭발 데미지")]
    [SerializeField] private float explosionDamage = 50f;

    [Tooltip("폭발 이펙트 프리팹 (선택 사항)")]
    [SerializeField] private GameObject explosionPrefab;

    [Tooltip("폭발 시점 (진입 시 / 회복 완료 시)")]
    [SerializeField] private ExplodeTime explodeTime = ExplodeTime.OnComplete;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public enum ExplodeTime
    {
        OnEnter,    // 그로기 진입 시 즉시 폭발
        OnComplete  // 그로기 회복 시 폭발 (디폴트)
    }

    /// <summary>
    /// 그로기 진입 시 호출
    /// </summary>
    public void OnGroggyEnter(EnemyController controller)
    {
        if (showDebugLogs) Debug.Log($"ExplosionGroggyEffect [{controller.name}]: 그로기 진입 감지!");

        if (explodeTime == ExplodeTime.OnEnter)
        {
            Explode(controller.transform.position);
        }
    }

    /// <summary>
    /// 그로기 회복 완료 시 호출 (부활 시점)
    /// </summary>
    public void OnGroggyComplete(EnemyController controller)
    {
        if (showDebugLogs) Debug.Log($"ExplosionGroggyEffect [{controller.name}]: 그로기 회복 완료!");

        if (explodeTime == ExplodeTime.OnComplete)
        {
            Explode(controller.transform.position);
        }
    }

    /// <summary>
    /// 정리 작업 (처형 등으로 중단 시)
    /// </summary>
    public void Cleanup()
    {
        if (showDebugLogs) Debug.Log($"ExplosionGroggyEffect [{gameObject.name}]: Cleanup 호출");
        // 필요시 타이머나 코루틴 정리
    }

    /// <summary>
    /// 폭발 실행
    /// </summary>
    private void Explode(Vector3 position)
    {
        if (showDebugLogs) Debug.Log($"ExplosionGroggyEffect: 💥 폭발! 위치={position}, 반경={explosionRadius}, 데미지={explosionDamage}");

        // 폭발 이펙트 생성
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
            // 이펙트 자동 파괴 (3초 후)
            Destroy(explosion, 3f);
        }

        // 범위 내 모든 객체에 데미지
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, explosionRadius);
        int hitCount = 0;

        foreach (var hit in hits)
        {
            // 자기 자신은 제외
            if (hit.gameObject == gameObject) continue;

            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(explosionDamage);
                hitCount++;

                if (showDebugLogs) Debug.Log($"ExplosionGroggyEffect: {hit.name}에게 {explosionDamage} 데미지!");
            }
        }

        if (showDebugLogs) Debug.Log($"ExplosionGroggyEffect: 폭발 완료 - {hitCount}개 대상 피해");
    }

    // 기즈모로 폭발 범위 시각화
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, explosionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
