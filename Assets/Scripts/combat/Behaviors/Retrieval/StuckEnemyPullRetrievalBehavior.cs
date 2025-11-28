using UnityEngine;
using System.Collections;

/// <summary>
/// 회수 옵션 B-2: 투사체가 적에게 stick된 경우, 그 적만 플레이어 방향으로 끌어옴
/// - 투사체가 적에게 맞아서 박힌(stick) 상태여야 함 (벽은 해당 없음)
/// - 몬스터를 투사체의 자식으로 설정하여 함께 이동 (속도 일치)
/// - 애니메이션 커브 기반 이동
/// - 투사체 콜라이더로 벽 감지
/// - 3가지 분리 조건: 거리 도달 / 타임아웃 / 벽 충돌
/// </summary>
public class StuckEnemyPullRetrievalBehavior : IProjectileRetrievalBehavior
{
    private ProjectileConfig config;

    [SerializeField] private bool showDebugLogs = true; // 기본값 true로 변경 (디버깅 편의성)

    public StuckEnemyPullRetrievalBehavior(ProjectileConfig cfg, bool debugLogs = true)
    {
        this.config = cfg;
        this.showDebugLogs = debugLogs;
    }

    public void StartRetrieval(AttackProjectile projectile, Transform player)
    {
        if (showDebugLogs)
            Debug.Log($"StuckEnemyPullRetrievalBehavior: 회수 시작 (player={player?.name})");

        projectile.StartCoroutine(RetrievalRoutine(projectile, player));
    }

    private IEnumerator RetrievalRoutine(AttackProjectile projectile, Transform player)
    {
        projectile.PrepareForRetrieval();

        // ===== 1. 초기 설정 및 몬스터 부착 =====
        var stuckEnemy = projectile.LastHostEnemy;

        // 투사체 콜라이더 활성화 (벽 감지용, Trigger)
        var projectileCollider = projectile.GetComponent<Collider2D>();
        if (projectileCollider != null)
        {
            projectileCollider.enabled = true;
            projectileCollider.isTrigger = true;
        }

        // 벽 감지용 컴포넌트 추가 (기존 WallDetector가 있으면 모두 제거)
        var existingWallDetectors = projectile.GetComponents<WallDetector>();
        if (existingWallDetectors.Length > 0)
        {
            foreach (var detector in existingWallDetectors)
            {
                Object.DestroyImmediate(detector);
            }
            if (showDebugLogs)
                Debug.Log($"StuckEnemyPullRetrievalBehavior: 기존 WallDetector {existingWallDetectors.Length}개 제거");
        }

        var wallDetector = projectile.gameObject.AddComponent<WallDetector>();
        bool wallHit = false;
        wallDetector.OnWallHit += () => { wallHit = true; };

        // 몬스터 관련 변수
        Collider2D enemyCollider = null;
        Rigidbody2D enemyRb = null;
        RigidbodyType2D originalBodyType = RigidbodyType2D.Dynamic;
        bool originalIsTrigger = false;
        Vector3 enemyOriginalScale = Vector3.one;
        Quaternion enemyOriginalRotation = Quaternion.identity;
        Transform enemyOriginalParent = null;
        bool isEnemyAttached = false;

        // 몬스터가 있다면 초기 데미지 적용 및 부착
        if (stuckEnemy != null)
        {
            if (showDebugLogs)
                Debug.Log($"StuckEnemyPullRetrievalBehavior: 투사체가 {stuckEnemy.name}에게 stick되어 있음");

            // 데미지 적용
            var health = stuckEnemy.GetComponent<HealthSystem>();
            if (health != null)
            {
                float damage = config.damage * config.returnDamageRatio;
                health.TakeDamage(damage);

                if (showDebugLogs)
                    Debug.Log($"StuckEnemyPullRetrievalBehavior: {stuckEnemy.name}에게 회수 데미지 {damage}");
            }

            // 기존 PullEffect 제거
            var existingPullEffect = stuckEnemy.GetComponent<PullEffect>();
            if (existingPullEffect != null)
            {
                Object.Destroy(existingPullEffect);
                if (showDebugLogs)
                    Debug.Log($"StuckEnemyPullRetrievalBehavior: 기존 PullEffect 제거");
            }

            // 몬스터 콜라이더를 Trigger로 변경 (물리 충돌 방지, 감지는 가능)
            enemyCollider = stuckEnemy.GetComponent<Collider2D>();
            if (enemyCollider != null)
            {
                originalIsTrigger = enemyCollider.isTrigger;
                enemyCollider.isTrigger = true;
            }

            // 몬스터 Rigidbody를 Kinematic으로 변경 (물리 간섭 방지)
            enemyRb = stuckEnemy.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                originalBodyType = enemyRb.bodyType;
                enemyRb.bodyType = RigidbodyType2D.Kinematic;
                enemyRb.linearVelocity = Vector2.zero;
                enemyRb.angularVelocity = 0f;

                if (showDebugLogs)
                    Debug.Log($"StuckEnemyPullRetrievalBehavior: Rigidbody2D를 Kinematic으로 변경 ({originalBodyType} → Kinematic)");
            }

            // 몬스터를 투사체의 자식으로 설정 (속도 일치를 위해)
            enemyOriginalScale = stuckEnemy.transform.localScale;
            enemyOriginalRotation = stuckEnemy.transform.localRotation;
            enemyOriginalParent = stuckEnemy.transform.parent;

            // 디버깅: originalParent가 projectile인지 확인
            if (showDebugLogs)
            {
                Debug.Log($"StuckEnemyPullRetrievalBehavior: 부착 전 - enemy.parent={enemyOriginalParent?.name ?? "null"}, projectile={projectile.name}");
                if (enemyOriginalParent == projectile.transform)
                {
                    Debug.LogWarning($"StuckEnemyPullRetrievalBehavior: ⚠️ 경고! enemy가 이미 projectile의 자식입니다. originalParent를 null로 설정합니다.");
                    enemyOriginalParent = null; // 원래 부모가 projectile이면 null로 설정 (월드 루트로 분리)
                }
            }

            stuckEnemy.transform.SetParent(projectile.transform, worldPositionStays: true);

            // Side effect 방지 - 스케일만 복원 (회전은 박힌 모양 그대로 유지)
            stuckEnemy.transform.localScale = enemyOriginalScale;

            isEnemyAttached = true;

            if (showDebugLogs)
                Debug.Log($"StuckEnemyPullRetrievalBehavior: {stuckEnemy.name}를 투사체의 자식으로 설정 (pullDetachDistance={config.pullDetachDistance})");
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("StuckEnemyPullRetrievalBehavior: 투사체가 적에게 stick되지 않음 - 투사체만 회수");
        }

        // ===== 2. LineRenderer 생성 =====
        LineRenderer lineRenderer = null;
        GameObject decorator = null;

        if (config.enableLineRenderer)
        {
            ProjectileLineRendererUtil.CreateLineRenderer(projectile, config, out lineRenderer, out decorator);
        }

        // 히트 피드백 적용
        ProjectileLineRendererUtil.ApplyHitFeedback(config);

        // ===== 3. 애니메이션 커브 기반 회수 이동 =====
        Vector3 startPos = projectile.transform.position;
        float elapsed = 0f;
        float moveDuration = config.pullDuration; // StuckEnemyPull은 pullDuration을 애니메이션 시간으로 사용

        if (showDebugLogs)
        {
            float initialDistance = Vector2.Distance(startPos, player.position);
            Debug.Log($"StuckEnemyPullRetrievalBehavior: 회수 시작 - 초기 거리={initialDistance:F2}, pullDetachDistance={config.pullDetachDistance}, pullDuration={moveDuration}");
        }

        while (player != null && elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);

            // 애니메이션 커브 적용
            float curveValue = config.retrievalCurve.Evaluate(t);
            projectile.transform.position = Vector3.Lerp(startPos, player.position, curveValue);

            // 투사체를 플레이어 방향으로 회전 (자식인 적도 함께 회전)
            Vector2 directionToPlayer = (player.position - projectile.transform.position).normalized;
            if (directionToPlayer != Vector2.zero)
            {
                float angle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;
                projectile.transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            // LineRenderer 업데이트
            if (lineRenderer != null)
            {
                ProjectileLineRendererUtil.UpdateLineRenderer(lineRenderer, player.position, projectile.transform.position);
                ProjectileLineRendererUtil.UpdateDecorator(decorator, projectile.transform.position);
            }

            // ===== 4. 분리 조건 체크 =====
            float distance = Vector2.Distance(projectile.transform.position, player.position);

            // 조건 1: 플레이어 앞 일정 거리 도달 시 분리
            if (isEnemyAttached && distance <= config.pullDetachDistance)
            {
                if (showDebugLogs)
                    Debug.Log($"StuckEnemyPullRetrievalBehavior: 분리 거리 도달 ({distance:F2} <= {config.pullDetachDistance}) - 몬스터 분리");

                DetachEnemy(stuckEnemy, enemyCollider, enemyRb, originalBodyType, originalIsTrigger, enemyOriginalScale, enemyOriginalRotation, enemyOriginalParent);
                isEnemyAttached = false;
            }
            else if (showDebugLogs && isEnemyAttached && elapsed % 0.2f < Time.deltaTime)
            {
                // 0.2초마다 한 번씩 디버그 로그 (스팸 방지)
                Debug.Log($"StuckEnemyPullRetrievalBehavior: 끌어오는 중... distance={distance:F2}, pullDetachDistance={config.pullDetachDistance}, isEnemyAttached={isEnemyAttached}");
            }

            // 조건 2: 벽 충돌 감지
            if (wallHit && isEnemyAttached)
            {
                if (showDebugLogs)
                    Debug.Log($"StuckEnemyPullRetrievalBehavior: 벽 충돌 감지 - 몬스터에게 데미지 및 분리");

                // 몬스터 벽 충돌 데미지
                var health = stuckEnemy.GetComponent<HealthSystem>();
                if (health != null)
                {
                    health.TakeDamage(config.pullWallImpactDamage);
                }

                DetachEnemy(stuckEnemy, enemyCollider, enemyRb, originalBodyType, originalIsTrigger, enemyOriginalScale, enemyOriginalRotation, enemyOriginalParent);
                isEnemyAttached = false;
            }

            // 조건 3: 플레이어 도착 체크
            if (distance < 0.5f)
            {
                if (showDebugLogs)
                    Debug.Log($"StuckEnemyPullRetrievalBehavior: 플레이어 도착 (distance: {distance:F2})");

                // 플레이어 도착 시 몬스터가 아직 부착되어 있다면 분리 (투사체만 사라지도록)
                if (isEnemyAttached)
                {
                    if (showDebugLogs)
                        Debug.Log($"StuckEnemyPullRetrievalBehavior: 플레이어 도착 - 몬스터 강제 분리");

                    DetachEnemy(stuckEnemy, enemyCollider, enemyRb, originalBodyType, originalIsTrigger, enemyOriginalScale, enemyOriginalRotation, enemyOriginalParent);
                    isEnemyAttached = false;
                }

                break;
            }

            yield return null;
        }

        // ===== 5. 타임아웃 처리 =====
        if (isEnemyAttached)
        {
            if (showDebugLogs)
                Debug.Log($"StuckEnemyPullRetrievalBehavior: 타임아웃 ({elapsed:F2}s >= {moveDuration}s) - 몬스터 분리");

            DetachEnemy(stuckEnemy, enemyCollider, enemyRb, originalBodyType, originalIsTrigger, enemyOriginalScale, enemyOriginalRotation, enemyOriginalParent);
        }

        // ===== 6. 정리 =====
        if (wallDetector != null)
            Object.Destroy(wallDetector);

        ProjectileLineRendererUtil.Cleanup(lineRenderer, decorator);

        projectile.CompleteRetrieval();
    }

    /// <summary>
    /// 몬스터를 투사체에서 분리하고 원래 상태로 복원합니다.
    /// </summary>
    private void DetachEnemy(EnemyController enemy, Collider2D enemyCollider, Rigidbody2D enemyRb,
                             RigidbodyType2D originalBodyType, bool originalIsTrigger, Vector3 originalScale, Quaternion originalRotation, Transform originalParent)
    {
        Debug.Log($"DetachEnemy 호출됨: enemy={enemy?.name ?? "null"}, enemyRb={enemyRb != null}, originalBodyType={originalBodyType}");
        
        if (enemy == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("DetachEnemy: enemy가 null입니다!");
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log($"DetachEnemy: 분리 시작 - enemy={enemy.name}, " +
                      $"현재 parent={enemy.transform.parent?.name ?? "null"}, " +
                      $"복원할 parent={originalParent?.name ?? "null"}");
        }

        // 부모 해제
        if (showDebugLogs)
            Debug.Log($"DetachEnemy: SetParent 호출 직전");
        
        enemy.transform.SetParent(originalParent, worldPositionStays: true);
        
        if (showDebugLogs)
            Debug.Log($"DetachEnemy: SetParent 호출 직후");

        if (showDebugLogs)
        {
            Debug.Log($"DetachEnemy: SetParent 실행 후 - enemy.parent={enemy.transform.parent?.name ?? "null"}");
        }

        // 원래 값 복원
        if (showDebugLogs)
            Debug.Log($"DetachEnemy: Transform 값 복원 시작");
        
        enemy.transform.localScale = originalScale;
        enemy.transform.localRotation = originalRotation;
        
        if (showDebugLogs)
            Debug.Log($"DetachEnemy: Transform 값 복원 완료");

        // 콜라이더 원래대로 복원
        if (showDebugLogs)
            Debug.Log($"DetachEnemy: 콜라이더 복원 시작 (enemyCollider={enemyCollider != null})");

        if (enemyCollider != null)
        {
            enemyCollider.enabled = true;
            enemyCollider.isTrigger = originalIsTrigger;
        }

        if (showDebugLogs)
            Debug.Log($"DetachEnemy: 콜라이더 복원 완료");

        // Rigidbody 복원
        Debug.Log($"DetachEnemy [{enemy.name}]: Rigidbody 체크 시작 (enemyRb={enemyRb != null})");
        
        if (enemyRb != null)
        {
            Debug.Log($"DetachEnemy [{enemy.name}]: Rigidbody 복원 전 - bodyType={enemyRb.bodyType}, originalBodyType={originalBodyType}");
            
            // 무조건 Dynamic으로 복원 (망치 공격 넉백이 작동하도록)
            enemyRb.bodyType = RigidbodyType2D.Dynamic;
            enemyRb.linearVelocity = Vector2.zero;
            enemyRb.angularVelocity = 0f;
            
            Debug.Log($"DetachEnemy [{enemy.name}]: Rigidbody 복원 후 - bodyType={enemyRb.bodyType}, velocity={enemyRb.linearVelocity}");
        }
        else
        {
            Debug.LogWarning($"DetachEnemy [{enemy.name}]: enemyRb가 null입니다!");
        }

        if (showDebugLogs)
            Debug.Log($"StuckEnemyPullRetrievalBehavior: {enemy.name} 분리 및 복원 완료 (최종 parent={enemy.transform.parent?.name ?? "null"})");
    }

    public void OnReturnPathHit(AttackProjectile projectile, Collider2D target)
    {
        // StuckEnemyPull 모드에서는 회수 경로상 충돌을 처리하지 않음
        // 박힌 적 하나만 끌어오기 때문에 경로상 충돌은 무시
        if (showDebugLogs)
            Debug.Log($"StuckEnemyPullRetrievalBehavior: 회수 경로상 {target.name} 충돌 (무시)");
    }
}

/// <summary>
/// 벽 충돌 감지용 컴포넌트
/// </summary>
public class WallDetector : MonoBehaviour
{
    public System.Action OnWallHit;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Wall"))
        {
            OnWallHit?.Invoke();
        }
    }
}
