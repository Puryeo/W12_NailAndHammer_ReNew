using UnityEngine;
using System.Collections;

/// <summary>
/// 기본 회수 Behavior
/// - 단순히 투사체를 플레이어에게 복귀시킴
/// - 경로상 적 처리 없음
/// - 라인렌더러, 애니메이션 커브, 히트피드백 지원
/// </summary>
public class SimpleRetrievalBehavior : IProjectileRetrievalBehavior
{
    private ProjectileConfig config;

    [SerializeField] private bool showDebugLogs = false;

    public SimpleRetrievalBehavior(ProjectileConfig cfg, bool debugLogs = false)
    {
        this.config = cfg;
        this.showDebugLogs = debugLogs;
    }

    public void StartRetrieval(AttackProjectile projectile, Transform player)
    {
        if (showDebugLogs)
            Debug.Log($"SimpleRetrievalBehavior: 회수 시작 (player={player?.name})");

        projectile.StartCoroutine(RetrievalRoutine(projectile, player));
    }

    private IEnumerator RetrievalRoutine(AttackProjectile projectile, Transform player)
    {
        projectile.PrepareForRetrieval();

        // 콜라이더 비활성화 (경로 충돌 없음)
        var collider = projectile.GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // LineRenderer 생성
        LineRenderer lineRenderer = null;
        GameObject decorator = null;

        if (config.enableLineRenderer)
        {
            ProjectileLineRendererUtil.CreateLineRenderer(projectile, config, out lineRenderer, out decorator);
        }

        // 회수 애니메이션
        Vector3 startPos = projectile.transform.position;
        float elapsed = 0f;
        float duration = config.moveToPlayerDuration;

        // 히트 피드백 적용
        ProjectileLineRendererUtil.ApplyHitFeedback(config);

        while (player != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 애니메이션 커브 적용
            float curveValue = config.retrievalCurve.Evaluate(t);
            Vector3 targetPos = player.position;
            projectile.transform.position = Vector3.Lerp(startPos, targetPos, curveValue);

            // LineRenderer 업데이트
            if (lineRenderer != null)
            {
                ProjectileLineRendererUtil.UpdateLineRenderer(lineRenderer, player.position, projectile.transform.position);
                ProjectileLineRendererUtil.UpdateDecorator(decorator, projectile.transform.position);
            }

            // 도착 체크
            float distance = Vector2.Distance(projectile.transform.position, player.position);
            if (distance < 0.5f)
            {
                break;
            }

            yield return null;
        }

        // 정리
        ProjectileLineRendererUtil.Cleanup(lineRenderer, decorator);
        projectile.CompleteRetrieval();
    }

    public void OnReturnPathHit(AttackProjectile projectile, Collider2D target)
    {
        // 단순 회수는 경로 히트 처리 없음
    }
}
