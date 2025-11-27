using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 회수 옵션 A: 경로상 몬스터에게 데미지 + 속박 상태이상
/// </summary>
public class BindingRetrievalBehavior : IProjectileRetrievalBehavior
{
    private ProjectileConfig config;

    [SerializeField] private bool showDebugLogs = false;

    public BindingRetrievalBehavior(ProjectileConfig cfg, bool debugLogs = false)
    {
        this.config = cfg;
        this.showDebugLogs = debugLogs;
    }

    public void StartRetrieval(AttackProjectile projectile, Transform player)
    {
        if (showDebugLogs)
            Debug.Log($"BindingRetrievalBehavior: 회수 시작 (player={player?.name})");

        // 코루틴 시작
        projectile.StartCoroutine(RetrievalRoutine(projectile, player));
    }

    private IEnumerator RetrievalRoutine(AttackProjectile projectile, Transform player)
    {
        // 회수 시작 준비
        projectile.PrepareForRetrieval();

        // ===== 추가: 이미 말뚝이 적에 꽂혀(Host) 있으면 즉시 데미지/속박 적용 =====
        var hostEnemy = projectile.LastHostEnemy;
        if (hostEnemy != null)
        {
            if (showDebugLogs) Debug.Log($"BindingRetrievalBehavior: projectile이 {hostEnemy.name}에 꽂혀있음 - 즉시 회수 데미지/속박 적용");

            var hostHealth = hostEnemy.GetComponent<HealthSystem>();
            if (hostHealth != null)
            {
                float damage = config.damage * config.returnDamageRatio;
                hostHealth.TakeDamage(damage);
                if (showDebugLogs) Debug.Log($"BindingRetrievalBehavior: {hostEnemy.name}에게 즉시 데미지 {damage}");
            }

            var hostCtrl = hostEnemy.GetComponent<EnemyController>();
            if (hostCtrl != null)
            {
                var binding = hostEnemy.gameObject.AddComponent<BindingEffect>();
                binding.Initialize(config.bindingDuration, config.bindingSlowAmount, showDebugLogs);
                if (showDebugLogs) Debug.Log($"BindingRetrievalBehavior: {hostEnemy.name}에게 즉시 속박 적용 ({config.bindingDuration}초)");
            }
        }
        // ===============================================================

        // 투사체에 BindingTrigger 컴포넌트 추가 (충돌 감지용)
        var bindingTrigger = projectile.gameObject.AddComponent<BindingTrigger>();
        bindingTrigger.Initialize(this, projectile, player, config, showDebugLogs);

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
        if (bindingTrigger != null)
            Object.Destroy(bindingTrigger);

        ProjectileLineRendererUtil.Cleanup(lineRenderer, decorator);

        // 도착
        projectile.CompleteRetrieval();
    }

    public void OnReturnPathHit(AttackProjectile projectile, Collider2D target)
    {
        var enemyHealth = target.GetComponent<HealthSystem>();
        var enemyCtrl = target.GetComponent<EnemyController>();

        // 데미지
        if (enemyHealth != null)
        {
            float damage = config.damage * config.returnDamageRatio;
            enemyHealth.TakeDamage(damage);

            if (showDebugLogs)
                Debug.Log($"BindingRetrievalBehavior: {target.name}에게 데미지 {damage}");
        }

        // 속박 상태이상 적용
        if (enemyCtrl != null)
        {
            var binding = target.gameObject.AddComponent<BindingEffect>();
            binding.Initialize(config.bindingDuration, config.bindingSlowAmount, showDebugLogs);

            if (showDebugLogs)
                Debug.Log($"BindingRetrievalBehavior: {target.name}에게 속박 적용 ({config.bindingDuration}초)");
        }
    }
}

/// <summary>
/// 투사체가 돌아오면서 적과의 충돌을 감지하는 컴포넌트
/// </summary>
public class BindingTrigger : MonoBehaviour
{
    private BindingRetrievalBehavior behavior;
    private AttackProjectile projectile;
    private Transform player;
    private ProjectileConfig config;
    private bool showDebugLogs;
    private HashSet<Collider2D> hitEnemies = new HashSet<Collider2D>();

    public void Initialize(BindingRetrievalBehavior behavior, AttackProjectile projectile, Transform player, ProjectileConfig config, bool debugLogs)
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
            Debug.Log($"BindingTrigger: {collision.name}과 충돌 감지");
    }
}

/// <summary>
/// 속박 상태이상 컴포넌트
/// </summary>
public class BindingEffect : MonoBehaviour
{
    private float duration;
    private float slowAmount;
    private float appliedMultiplier;
    private EnemyController enemy;
    private float timer = 0f;

    [SerializeField] private bool showDebugLogs = false;

    public void Initialize(float dur, float slow, bool debugLogs = false)
    {
        this.duration = dur;
        this.slowAmount = slow;
        this.showDebugLogs = debugLogs;

        enemy = GetComponent<EnemyController>();

        if (enemy != null)
        {
            // Rigidbody2D 속도와 관성 초기화 (기존 이동 관성 제거)
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;

                if (showDebugLogs)
                    Debug.Log($"BindingEffect: Rigidbody2D 속도/관성 초기화");
            }

            // Apply speed reduction (1.0 - slowAmount)
            // Example: slowAmount = 0.5 means 50% slow → multiplier = 0.5
            appliedMultiplier = 1f - slowAmount;
            enemy.ApplySpeedMultiplier(appliedMultiplier);

            if (showDebugLogs)
                Debug.Log($"BindingEffect: 속박 시작 ({duration}초, 감속 {slowAmount * 100}%, 속도 배율 {appliedMultiplier:F2})");
        }
        else
        {
            Debug.LogError($"BindingEffect: EnemyController를 찾을 수 없습니다!");
            Destroy(this);
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= duration)
        {
            // 속박 해제
            if (showDebugLogs)
                Debug.Log($"BindingEffect: 속박 해제");

            // 속도 복원
            if (enemy != null)
            {
                enemy.RemoveSpeedMultiplier(appliedMultiplier);
            }

            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        // Safety: ensure multiplier is removed even if destroyed externally
        if (enemy != null && appliedMultiplier > 0f)
        {
            enemy.RemoveSpeedMultiplier(appliedMultiplier);
        }
    }
}
