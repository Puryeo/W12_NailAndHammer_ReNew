using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 회수 옵션 B: 경로상 몬스터를 플레이어 방향으로 끌어옴
/// - 투사체는 벽 통과
/// - 몬스터는 벽 충돌 시 데미지
/// - 투사체가 돌아오면서 충돌한 적들에게만 순차적으로 끌어오기 효과 적용
/// </summary>
public class PullRetrievalBehavior : IProjectileRetrievalBehavior
{
    private ProjectileConfig config;

    [SerializeField] private bool showDebugLogs = false;

    public PullRetrievalBehavior(ProjectileConfig cfg, bool debugLogs = false)
    {
        this.config = cfg;
        this.showDebugLogs = debugLogs;
    }

    public void StartRetrieval(AttackProjectile projectile, Transform player)
    {
        if (showDebugLogs)
            Debug.Log($"PullRetrievalBehavior: 회수 시작 (player={player?.name})");

        projectile.StartCoroutine(RetrievalRoutine(projectile, player));
    }

    private IEnumerator RetrievalRoutine(AttackProjectile projectile, Transform player)
    {
        projectile.PrepareForRetrieval();

        // 투사체에 PullTrigger 컴포넌트 추가 (충돌 감지용)
        var pullTrigger = projectile.gameObject.AddComponent<PullTrigger>();
        pullTrigger.Initialize(this, projectile, player, config, showDebugLogs);

        // LineRenderer 생성
        LineRenderer lineRenderer = null;
        GameObject decorator = null;

        if (config.enableLineRenderer)
        {
            ProjectileLineRendererUtil.CreateLineRenderer(projectile, config, out lineRenderer, out decorator);
        }

        // 히트 피드백 적용
        ProjectileLineRendererUtil.ApplyHitFeedback(config);

        // 투사체는 벽 통과하며 플레이어에게 복귀
        Vector3 startPos = projectile.transform.position;
        float elapsed = 0f;
        float duration = config.moveToPlayerDuration;

        while (player != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 애니메이션 커브 적용
            float curveValue = config.retrievalCurve.Evaluate(t);
            projectile.transform.position = Vector3.Lerp(startPos, player.position, curveValue);

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

        // 클린업
        if (pullTrigger != null)
            Object.Destroy(pullTrigger);

        ProjectileLineRendererUtil.Cleanup(lineRenderer, decorator);

        projectile.CompleteRetrieval();
    }

    public void OnReturnPathHit(AttackProjectile projectile, Collider2D target)
    {
        // 데미지 (항상 처리)
        var health = target.GetComponent<HealthSystem>();
        if (health != null)
        {
            float damage = config.damage * config.returnDamageRatio;
            health.TakeDamage(damage);

            if (showDebugLogs)
                Debug.Log($"PullRetrievalBehavior: {target.name}에게 데미지 {damage}");
        }

        // 이미 PullEffect가 있으면 얼리 리턴 (끌어오기는 한 번만, 데미지만 누적)
        var existingPullEffect = target.GetComponent<PullEffect>();
        if (existingPullEffect != null)
        {
            if (showDebugLogs)
                Debug.Log($"PullRetrievalBehavior: {target.name} 이미 끌려오는 중 - 데미지만 처리하고 건너뜀");
            return; // 얼리 리턴
        }

        // PullEffect 적용 (플레이어는 projectile.Attacker 또는 태그로 찾기) - 첫 번째 투사체만
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            var pullEffect = target.gameObject.AddComponent<PullEffect>();
            pullEffect.Initialize(playerObj.transform, config.pullForce, config.pullWallImpactDamage, config.pullStopDistance, showDebugLogs);

            if (showDebugLogs)
                Debug.Log($"PullRetrievalBehavior: {target.name}에게 끌어오기 적용");
        }
    }
}

/// <summary>
/// 투사체가 돌아오면서 적과의 충돌을 감지하는 컴포넌트
/// </summary>
public class PullTrigger : MonoBehaviour
{
    private PullRetrievalBehavior behavior;
    private AttackProjectile projectile;
    private Transform player;
    private ProjectileConfig config;
    private bool showDebugLogs;
    private HashSet<Collider2D> hitEnemies = new HashSet<Collider2D>();

    public void Initialize(PullRetrievalBehavior behavior, AttackProjectile projectile, Transform player, ProjectileConfig config, bool debugLogs)
    {
        this.behavior = behavior;
        this.projectile = projectile;
        this.player = player;
        this.config = config;
        this.showDebugLogs = debugLogs;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 이미 처리한 적은 무시
        if (hitEnemies.Contains(collision))
            return;

        // Enemy 태그만 처리
        if (!collision.CompareTag("Enemy"))
            return;

        hitEnemies.Add(collision);
        behavior.OnReturnPathHit(projectile, collision);

        if (showDebugLogs)
            Debug.Log($"PullTrigger: {collision.name}과 충돌 감지");
    }
}

/// <summary>
/// 끌어오기 효과 컴포넌트
/// </summary>
public class PullEffect : MonoBehaviour
{
    private Transform target;
    private float force;
    private float wallImpactDamage;
    private float stopDistance;
    private Rigidbody2D rb;
    private bool isActive = true;
    private float maxDuration = 1.0f; // 최대 끌어오기 시간
    private RigidbodyConstraints2D originalConstraints; // 원래 제약 저장

    [SerializeField] private bool showDebugLogs = false;

    public void Initialize(Transform player, float pullForce, float wallDamage, float pullStopDistance, bool debugLogs = false)
    {
        this.target = player;
        this.force = pullForce;
        this.wallImpactDamage = wallDamage;
        this.stopDistance = pullStopDistance;
        this.showDebugLogs = debugLogs;

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("PullEffect: Rigidbody2D가 없습니다!");
            Destroy(this);
            return;
        }

        // AddForce가 제대로 작동하려면 Dynamic body여야 함
        // 그로기 상태나 다른 이유로 bodyType이 변경되었을 수 있으므로 강제 설정
        if (rb.bodyType != RigidbodyType2D.Dynamic)
        {
            if (showDebugLogs)
                Debug.Log($"PullEffect: Rigidbody2D bodyType을 {rb.bodyType} -> Dynamic으로 변경");
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        // 원래 제약 저장 및 회전 제약 설정 (빙글빙글 도는 문제 방지)
        originalConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // 안전: 기존 velocity 초기화
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (showDebugLogs)
            Debug.Log($"PullEffect: 초기화 (target={player?.name}, force={pullForce}, stopDistance={pullStopDistance}, maxDuration={maxDuration}, bodyType={rb.bodyType}, constraints={rb.constraints})");

        StartCoroutine(PullRoutine());
    }

    private IEnumerator PullRoutine()
    {
        float elapsed = 0f;
        float lastLogTime = 0f;
        float logInterval = 0.2f; // 0.2초마다 로그 출력

        if (showDebugLogs)
            Debug.Log($"PullEffect [{gameObject.name}]: PullRoutine 시작 (distance: {Vector2.Distance(transform.position, target.position):F2})");

        while (isActive && target != null)
        {
            elapsed += Time.fixedDeltaTime;

            // 제한 시간 초과 시 취소
            if (elapsed >= maxDuration)
            {
                if (showDebugLogs)
                    Debug.Log($"PullEffect [{gameObject.name}]: ⏱️ 제한 시간 초과 ({elapsed:F2}s >= {maxDuration}s) - 끌어오기 취소");

                CleanupAndDestroy();
                yield break;
            }

            float distance = Vector2.Distance(transform.position, target.position);

            // 정지 거리에 도달하면 멈춤
            if (distance <= stopDistance)
            {
                if (showDebugLogs)
                    Debug.Log($"PullEffect [{gameObject.name}]: ✅ 정지 거리 도달 ({distance:F2} <= {stopDistance}) - elapsed: {elapsed:F2}s");

                CleanupAndDestroy();
                yield break;
            }

            // 플레이어 방향으로 힘 가하기
            Vector2 direction = (target.position - transform.position).normalized;
            rb.AddForce(direction * force);

            // 주기적 디버그 로그
            if (showDebugLogs && (elapsed - lastLogTime >= logInterval))
            {
                lastLogTime = elapsed;
                Debug.Log($"PullEffect [{gameObject.name}]: 끌려오는 중... distance: {distance:F2}, velocity: {rb.linearVelocity.magnitude:F2}, elapsed: {elapsed:F2}s");
            }

            yield return new WaitForFixedUpdate();
        }

        if (showDebugLogs)
            Debug.Log($"PullEffect [{gameObject.name}]: PullRoutine 종료 (isActive={isActive}, target={target?.name})");
    }

    /// <summary>
    /// PullEffect 종료 시 정리 작업 수행
    /// </summary>
    private void CleanupAndDestroy()
    {
        if (rb != null)
        {
            // 속도 초기화
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // 원래 제약 복원
            rb.constraints = originalConstraints;

            if (showDebugLogs)
                Debug.Log($"PullEffect [{gameObject.name}]: 정리 완료 - constraints 복원 ({originalConstraints})");
        }

        isActive = false;
        Destroy(this);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 벽 충돌 시 데미지
        if (collision.collider.CompareTag("Wall"))
        {
            var health = GetComponent<HealthSystem>();
            if (health != null)
            {
                health.TakeDamage(wallImpactDamage);
                if (showDebugLogs)
                    Debug.Log($"PullEffect [{gameObject.name}]: 벽 충돌 데미지 {wallImpactDamage}");
            }

            CleanupAndDestroy();
        }
    }
}
