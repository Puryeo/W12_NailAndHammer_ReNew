using UnityEngine;
using System.Collections;

/// <summary>
/// Splash Execution (스플래쉬 처형)
/// - 우클릭 해머 공격으로 적을 처형하면 4방향 투사체 발사
/// - 그로기 상태에서 죽음 상태로 전환된 적에게만 발동
/// </summary>
public class SplashExecution : MonoBehaviour, ISecondaryChargedAttack
{
    [Header("Projectile Settings")]
    [Tooltip("발사할 투사체 프리팹 (SplashProjectile 컴포넌트 필요)")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("투사체 발사 속도")]
    [SerializeField] private float projectileSpeed = 10f;

    [Tooltip("투사체 지속 시간 (초)")]
    [SerializeField] private float projectileLifetime = 2f;

    [Header("Damage Settings")]
    [Tooltip("투사체가 일반 적에게 주는 데미지 (그로기로 만듦)")]
    [SerializeField] private float damageToNormal = 50f;

    [Tooltip("투사체가 그로기 적에게 주는 데미지 (처형)")]
    [SerializeField] private float damageToGroggy = 999f;

    [Header("Detection Settings")]
    [Tooltip("처형 감지 범위 (해머 타격 지점 주변)")]
    [SerializeField] private float detectionRadius = 1f;

    [Tooltip("적 레이어 마스크")]
    [SerializeField] private LayerMask enemyLayer = -1;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // 처형 발생 지점 저장
    private Vector3 lastExecutionPosition = Vector3.zero;
    private bool executionTriggered = false;

    public void Execute(PlayerCombat owner, Transform ownerTransform)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[{GetAttackName()}] projectilePrefab이 할당되지 않았습니다.");
            return;
        }

        // 플레이어 위치에서 처형 감지 시작
        StartCoroutine(MonitorForExecution(owner, ownerTransform));
    }

    public string GetAttackName()
    {
        return "Splash Execution";
    }

    public SecondaryChargedAttackType GetSkillType()
    {
        return SecondaryChargedAttackType.Splash;
    }

    /// <summary>
    /// 처형 발생 모니터링 - 그로기 -> 죽음 전환 감지
    /// </summary>
    private IEnumerator MonitorForExecution(PlayerCombat owner, Transform ownerTransform)
    {
        if (showDebugLogs) Debug.Log($"[{GetAttackName()}] 처형 모니터링 시작");

        // 0.5초간 주변 적들을 모니터링
        float monitorDuration = 0.5f;
        float elapsed = 0f;

        while (elapsed < monitorDuration && !executionTriggered)
        {
            // 주변 적 탐색
            Collider2D[] colliders = Physics2D.OverlapCircleAll(ownerTransform.position, detectionRadius, enemyLayer);

            foreach (var col in colliders)
            {
                if (col == null) continue;

                var enemyCtrl = col.GetComponent<EnemyController>();
                if (enemyCtrl == null)
                {
                    enemyCtrl = col.GetComponentInParent<EnemyController>();
                }

                if (enemyCtrl == null) continue;

                // 죽음 상태로 전환된 적 발견 시
                if (enemyCtrl.IsDeadState())
                {
                    // 처형 위치 저장
                    lastExecutionPosition = col.transform.position;
                    executionTriggered = true;

                    if (showDebugLogs)
                        Debug.Log($"[{GetAttackName()}] ✅ 처형 감지! 위치: {lastExecutionPosition}");

                    // 투사체 발사
                    SpawnProjectiles(lastExecutionPosition);

                    yield break; // 코루틴 종료
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (showDebugLogs && !executionTriggered)
        {
            Debug.Log($"[{GetAttackName()}] 처형 미발생 (모니터링 종료)");
        }

        executionTriggered = false;
    }

    /// <summary>
    /// 4방향 투사체 발사 (위, 아래, 왼쪽, 오른쪽)
    /// </summary>
    private void SpawnProjectiles(Vector3 spawnPosition)
    {
        // 2D 4방향 벡터
        Vector2[] directions = new Vector2[]
        {
            Vector2.up,       // 위
            Vector2.down,     // 아래
            Vector2.left,     // 왼쪽
            Vector2.right     // 오른쪽
        };

        foreach (var direction in directions)
        {
            // 투사체 생성
            GameObject projObj = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

            // SplashProjectile 컴포넌트 초기화
            var splashProj = projObj.GetComponent<SplashProjectile>();
            if (splashProj != null)
            {
                splashProj.Initialize(
                    direction: direction,
                    speed: projectileSpeed,
                    lifetime: projectileLifetime,
                    damageToNormal: damageToNormal,
                    damageToGroggy: damageToGroggy,
                    showDebugLogs: showDebugLogs
                );
            }
            else
            {
                Debug.LogError($"[{GetAttackName()}] projectilePrefab에 SplashProjectile 컴포넌트가 없습니다!");
                Destroy(projObj);
            }
        }

        if (showDebugLogs)
            Debug.Log($"[{GetAttackName()}] 4방향 투사체 발사 완료 (위치: {spawnPosition})");
    }

    private void OnDrawGizmosSelected()
    {
        // 탐지 범위 시각화
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}