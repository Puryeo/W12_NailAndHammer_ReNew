using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투사체에 꿰뚫린 적들을 관리하는 컴포넌트
/// - 적 추가/제거
/// - 위치 재배치 (꼬치처럼 일렬로)
/// - 벽 충돌 시 처리
/// </summary>
public class ImpaledEnemyManager
{
    private AttackProjectile projectile;
    private ProjectileConfig config;

    // 꿰뚫린 적들 (순서대로 저장: a, b, c)
    private List<Transform> impaledEnemies = new List<Transform>();

    // 각 적의 원래 상태 백업
    private Dictionary<Transform, EnemyBackupData> enemyBackups = new Dictionary<Transform, EnemyBackupData>();

    private bool showDebugLogs = false;

    public int ImpaledCount => impaledEnemies.Count;
    public bool IsFull => impaledEnemies.Count >= config.maxImpaleCount;
    public List<Transform> ImpaledEnemies => impaledEnemies;

    private struct EnemyBackupData
    {
        public RigidbodyType2D originalBodyType;
        public Vector2 originalVelocity;
        public Transform originalParent;
    }

    public ImpaledEnemyManager(AttackProjectile proj, ProjectileConfig cfg, bool debugLogs = false)
    {
        this.projectile = proj;
        this.config = cfg;
        this.showDebugLogs = debugLogs;
    }

    /// <summary>
    /// 적을 꿰뚫기 (충돌 지점 보존)
    /// </summary>
    public bool TryImpaleEnemy(Collider2D enemyCollider, Vector2 collisionPoint)
    {
        // 최대 개수 체크
        if (IsFull)
        {
            if (showDebugLogs)
                Debug.Log($"ImpaledEnemyManager: 최대 꿰뚫기 수 도달 ({config.maxImpaleCount})");
            return false;
        }

        var enemyTransform = enemyCollider.transform;

        // 중복 체크
        if (impaledEnemies.Contains(enemyTransform))
        {
            if (showDebugLogs)
                Debug.Log($"ImpaledEnemyManager: 이미 꿰뚫린 적 {enemyTransform.name}");
            return false;
        }

        // 1. 적의 원래 상태 백업
        BackupEnemyState(enemyTransform);

        // 2. 적 물리 비활성화
        var enemyRb = enemyCollider.GetComponent<Rigidbody2D>();
        if (enemyRb != null)
        {
            enemyRb.bodyType = RigidbodyType2D.Kinematic;
            enemyRb.linearVelocity = Vector2.zero;
        }

        // 3. 투사체의 자식으로 설정
        enemyTransform.SetParent(projectile.transform);

        // 4. 충돌 지점을 투사체 로컬 좌표로 변환하여 배치 (텔레포트 방지!)
        Vector3 localCollisionPoint = projectile.transform.InverseTransformPoint(collisionPoint);

        // 적의 앞쪽 표면이 충돌 지점이 되도록 조정 (빈틈없는 배치)
        float enemyHalfWidth = GetEnemyWidth(enemyTransform) * 0.5f;
        localCollisionPoint.x -= enemyHalfWidth; // 적의 중심을 뒤로 밀어서 앞쪽 끝이 충돌 지점이 되도록

        enemyTransform.localPosition = localCollisionPoint;

        if (showDebugLogs)
            Debug.Log($"ImpaledEnemy {enemyTransform.name}: 충돌 지점 = {collisionPoint}, 로컬 위치 = {localCollisionPoint}, 적 너비 = {enemyHalfWidth * 2:F2}");

        // 5. 기존 적들을 뒤로 밀어냄
        PushBackExistingEnemies(enemyTransform);

        // 6. 리스트에 추가 (밀어내기 후에 추가)
        impaledEnemies.Add(enemyTransform);

        // 7. 초기 데미지 적용
        ApplyImpaleDamage(enemyCollider);

        if (showDebugLogs)
            Debug.Log($"ImpaledEnemyManager: {enemyTransform.name} 꿰뚫림 (총 {impaledEnemies.Count}마리)");

        return true;
    }

    /// <summary>
    /// 적의 원래 상태 백업
    /// </summary>
    private void BackupEnemyState(Transform enemy)
    {
        var rb = enemy.GetComponent<Rigidbody2D>();

        var backup = new EnemyBackupData
        {
            originalBodyType = rb != null ? rb.bodyType : RigidbodyType2D.Dynamic,
            originalVelocity = rb != null ? rb.linearVelocity : Vector2.zero,
            originalParent = enemy.parent
        };

        enemyBackups[enemy] = backup;
    }

    /// <summary>
    /// 새로운 적이 추가될 때 기존 적들을 투사체 진행 방향 반대로 밀어냄
    /// - 빈틈없는 배치: 새 적의 뒤쪽 끝과 기존 적의 앞쪽 끝이 config.enemySpacing만큼만 떨어지도록
    /// </summary>
    private void PushBackExistingEnemies(Transform newEnemy)
    {
        if (impaledEnemies.Count == 0) return; // 첫 적이면 밀 필요 없음

        float newEnemyWidth = GetEnemyWidth(newEnemy);

        // 기존 적들을 뒤로 밀어냄
        for (int i = 0; i < impaledEnemies.Count; i++)
        {
            var enemy = impaledEnemies[i];
            if (enemy == null) continue;

            // 투사체 진행 방향의 반대(-X)로 밀어냄
            // 새 적의 전체 너비 + 간격만큼 이동
            // (새 적의 앞쪽 끝이 충돌 지점이므로, 전체 너비만큼 밀면 새 적의 뒤쪽 끝과 기존 적의 앞쪽 끝이 만남)
            float pushDistance = newEnemyWidth + config.enemySpacing;

            enemy.localPosition += new Vector3(-pushDistance, 0f, 0f);

            if (showDebugLogs)
                Debug.Log($"ImpaledEnemy[{i}] {enemy.name}: 뒤로 밀림 {pushDistance:F2} (새 적 너비={newEnemyWidth:F2}, 간격={config.enemySpacing:F2}), 새 위치 = {enemy.localPosition}");
        }
    }

    /// <summary>
    /// 적의 너비 계산 (Collider bounds 기준)
    /// - 투사체 진행 방향(로컬 X축)의 크기를 반환
    /// - 빈틈없는 배치를 위해 정확한 크기 계산
    /// </summary>
    private float GetEnemyWidth(Transform enemy)
    {
        var collider = enemy.GetComponent<Collider2D>();
        if (collider != null)
        {
            // 월드 좌표계 기준 X축 크기
            // (적이 투사체의 자식이므로 투사체와 같은 회전을 가짐)
            return collider.bounds.size.x;
        }
        return 1.0f; // 기본값
    }

    /// <summary>
    /// 꿰뚫기 데미지 적용
    /// </summary>
    private void ApplyImpaleDamage(Collider2D enemy)
    {
        var health = enemy.GetComponent<HealthSystem>() ?? enemy.GetComponentInParent<HealthSystem>();
        if (health != null)
        {
            health.TakeDamage(config.damage);
        }

        var enemyCtrl = enemy.GetComponent<EnemyController>() ?? enemy.GetComponentInParent<EnemyController>();
        if (enemyCtrl != null)
        {
            enemyCtrl.RegisterHit(1, projectile.Attacker);
        }
    }

    /// <summary>
    /// 벽 충돌 시 모든 꿰뚫린 적 처리
    /// - 위치 조정 → 데미지/스턴 적용 → 바로 부모-자식 관계 해제
    /// </summary>
    public void OnWallImpact()
    {
        // 1. 먼저 적들의 위치를 조정 (벽을 뚫지 않도록)
        AdjustEnemiesForWallCollision();

        // 2. 데미지와 스턴 적용
        foreach (var enemy in impaledEnemies)
        {
            if (enemy == null) continue;

            var health = enemy.GetComponent<HealthSystem>();
            var enemyCtrl = enemy.GetComponent<EnemyController>();

            // 벽 충돌 추가 데미지
            if (health != null && config.wallImpactDamage > 0f)
            {
                health.TakeDamage(config.wallImpactDamage);
                if (showDebugLogs)
                    Debug.Log($"ImpaledEnemy {enemy.name}: 벽 충돌 데미지 {config.wallImpactDamage}");
            }

            // 스턴 적용
            if (enemyCtrl != null && config.applyStunOnWallImpact)
            {
                enemyCtrl.ApplyStun(config.wallImpactStunDuration);
                if (showDebugLogs)
                    Debug.Log($"ImpaledEnemy {enemy.name}: 스턴 {config.wallImpactStunDuration}초");
            }
        }

        // 3. 벽에 박힌 위치 그대로 부모-자식 관계 해제
        // (투사체는 언제 사라지든 상관없이 적들은 이미 독립적으로 벽에 박혀있음)
        ReleaseAllEnemies();

        if (showDebugLogs)
            Debug.Log($"ImpaledEnemyManager: 벽 충돌 시 모든 적 해제 완료 (벽에 박힌 상태로 독립)");
    }

    /// <summary>
    /// 벽 충돌 시 적들이 벽을 뚫지 않도록 위치 조정
    /// - 적의 절반 이상이 벽에 박히지 않도록 처리
    /// - 적의 중심이 벽 뒤쪽으로 가지 않도록 방지
    /// </summary>
    private void AdjustEnemiesForWallCollision()
    {
        foreach (var enemy in impaledEnemies)
        {
            if (enemy == null) continue;

            float enemyHalfWidth = GetEnemyWidth(enemy) * 0.5f;

            // 투사체가 벽에 박혔을 때:
            // - localPosition.x = 0: 적의 중심이 투사체(벽) 위치
            // - localPosition.x < 0: 적이 벽 뒤쪽에 있음
            // - localPosition.x = -enemyHalfWidth: 적의 절반이 벽 뒤쪽

            // 적의 절반 이상이 벽에 박히는 경우 (중심이 벽 뒤쪽)
            float minAllowedX = -enemyHalfWidth * 0.4f; // 최대 40%만 벽에 박히도록

            if (enemy.localPosition.x < minAllowedX)
            {
                Vector3 pos = enemy.localPosition;
                pos.x = minAllowedX;
                enemy.localPosition = pos;

                if (showDebugLogs)
                    Debug.Log($"ImpaledEnemy {enemy.name}: 벽 뚫림 방지 조정 - 새 localPosition.x = {pos.x:F2} (halfWidth={enemyHalfWidth:F2})");
            }
        }
    }

    /// <summary>
    /// 모든 꿰뚫린 적 해제 (회수 시)
    /// - 벽에 박힌 위치 그대로 유지하면서 부모-자식 관계만 해제
    /// </summary>
    public void ReleaseAllEnemies()
    {
        foreach (var enemy in impaledEnemies)
        {
            if (enemy == null) continue;

            // 부모 해제 - 현재 월드 위치 유지 (벽에 박힌 위치 그대로)
            enemy.SetParent(null, worldPositionStays: true);

            if (showDebugLogs)
                Debug.Log($"ImpaledEnemy {enemy.name}: 부모 해제 - 월드 위치 유지 (position={enemy.position})");

            // 물리 복원
            var rb = enemy.GetComponent<Rigidbody2D>();
            if (rb != null && enemyBackups.ContainsKey(enemy))
            {
                rb.bodyType = enemyBackups[enemy].originalBodyType;
                rb.linearVelocity = Vector2.zero;

                if (showDebugLogs)
                    Debug.Log($"ImpaledEnemy {enemy.name}: Rigidbody 복원 (bodyType={rb.bodyType})");
            }
        }

        impaledEnemies.Clear();
        enemyBackups.Clear();

        if (showDebugLogs)
            Debug.Log("ImpaledEnemyManager: 모든 적 해제 완료 (월드 위치 유지)");
    }

    /// <summary>
    /// 특정 적 제거
    /// - 현재 위치 유지하면서 부모-자식 관계 해제
    /// </summary>
    public void RemoveEnemy(Transform enemy)
    {
        if (impaledEnemies.Remove(enemy))
        {
            // 부모 해제 - 현재 월드 위치 유지
            enemy.SetParent(null, worldPositionStays: true);

            var rb = enemy.GetComponent<Rigidbody2D>();
            if (rb != null && enemyBackups.TryGetValue(enemy, out var backup))
            {
                rb.bodyType = backup.originalBodyType;
                rb.linearVelocity = Vector2.zero;
            }

            enemyBackups.Remove(enemy);

            if (showDebugLogs)
                Debug.Log($"ImpaledEnemyManager: {enemy.name} 제거됨 (월드 위치 유지)");
        }
    }
}
