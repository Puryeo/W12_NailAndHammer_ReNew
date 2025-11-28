using UnityEngine;

/// <summary>
/// 우클릭 차징공격 - 망치 + 처형 시 투사체 발사 (MonoBehaviour 버전)
/// - 기존 HammerSwingControllerWithCallback를 사용하여 망치 휘두르기
/// - 그로기 적 처형 시 ExecutionProjectile 발사
/// - 투사체는 적 충돌 시 부채꼴 범위 공격
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Combat/Secondary Charged Attack Hammer With Projectile (Mono)")]
public class SecondaryChargedAttackHammerWithProjectileComponent : MonoBehaviour, ISecondaryChargedAttack
{
    [Header("Hammer Settings")]
    [Tooltip("망치 프리펩 (HammerSwingControllerWithCallback 컴포넌트 필요)")]
    [SerializeField] private GameObject hammerPrefab;
    [SerializeField] private Vector2 hammerSpawnOffset = new Vector2(1.2f, 0f);
    [SerializeField] private float hammerSwingAngle = 150f;
    [SerializeField] private float hammerSwingDuration = 0.35f;

    [Header("Damage & Effects")]
    [SerializeField] private float damage = 30f;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float executeHealAmount = 30f;

    [Header("Animation")]
    [SerializeField] private AnimationCurve hammerSwingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Execution Projectile")]
    [Tooltip("처형 시 발사할 투사체 프리펩 (ExecutionProjectile 컴포넌트 필요)")]
    [SerializeField] private GameObject executionProjectilePrefab;
    [Tooltip("투사체 속도")]
    [SerializeField] private float projectileSpeed = 10f;
    [Tooltip("투사체 발사 위치 오프셋 (플레이어 기준)")]
    [SerializeField] private Vector2 projectileSpawnOffset = Vector2.zero;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // 현재 공격의 owner 저장 (콜백에서 사용)
    private PlayerCombat currentOwner;
    private Transform currentOwnerTransform;

    public void Execute(PlayerCombat owner, Transform ownerTransform)
    {
        if (hammerPrefab == null)
        {
            Debug.LogWarning($"[{GetAttackName()}] hammerPrefab이 할당되지 않았습니다.");
            return;
        }

        // 현재 공격 정보 저장
        currentOwner = owner;
        currentOwnerTransform = ownerTransform;

        // 망치 생성
        Vector3 spawnPos = ownerTransform.position + (Vector3)(ownerTransform.rotation * hammerSpawnOffset);
        GameObject go = Instantiate(hammerPrefab, spawnPos, Quaternion.identity);
        var hc = go.GetComponent<HammerSwingControllerWithCallback>();

        if (hc == null)
        {
            Debug.LogWarning($"[{GetAttackName()}] hammerPrefab에 HammerSwingControllerWithCallback 컴포넌트가 없습니다.");
            Destroy(go);
            return;
        }

        // Initialize with callback
        hc.Initialize(
            owner: owner,
            ownerTransform: ownerTransform,
            damage: damage,
            knockback: knockbackForce,
            swingAngle: hammerSwingAngle,
            swingDuration: hammerSwingDuration,
            executeHealAmount: executeHealAmount,
            localOffset: hammerSpawnOffset,
            speedCurve: hammerSwingCurve,
            enableExecution: true,
            executionCallback: OnEnemyExecuted // 콜백 등록
        );

        IgnorePlayerHammerCollision(ownerTransform, go);

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] Hammer created with execution callback");
        }
    }

    public string GetAttackName() => "차징공격 망치+투사체 (컴포넌트)";

    public SecondaryChargedAttackType GetSkillType()
    {
        return SecondaryChargedAttackType.None;
    }

    /// <summary>
    /// 적 처형 시 호출되는 콜백
    /// </summary>
    /// <param name="playerPos">플레이어 위치</param>
    /// <param name="enemyPos">처형된 적 위치</param>
    /// <param name="executedEnemy">처형된 적 컨트롤러</param>
    private void OnEnemyExecuted(Vector2 playerPos, Vector2 enemyPos, EnemyController executedEnemy)
    {
        if (executionProjectilePrefab == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"[{GetAttackName()}] executionProjectilePrefab이 할당되지 않아 투사체를 발사할 수 없습니다.");
            }
            return;
        }

        // 플레이어 -> 적 방향 계산
        Vector2 direction = (enemyPos - playerPos).normalized;

        // 투사체 생성 위치 (플레이어 위치 + 오프셋)
        Vector2 spawnPos = playerPos + projectileSpawnOffset;

        // 투사체 생성
        GameObject proj = Instantiate(executionProjectilePrefab, spawnPos, Quaternion.identity);
        var projScript = proj.GetComponent<ExecutionProjectile>();

        if (projScript == null)
        {
            Debug.LogWarning($"[{GetAttackName()}] executionProjectilePrefab에 ExecutionProjectile 컴포넌트가 없습니다.");
            Destroy(proj);
            return;
        }

        // 투사체 초기화 (처형된 적 정보 전달하여 충돌 무시)
        // 데미지는 투사체 프리펩의 인스펙터에서 설정
        projScript.Initialize(direction, projectileSpeed, currentOwner, executedEnemy);

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] Projectile launched - Direction: {direction}, Speed: {projectileSpeed}, ExecutedEnemy: {executedEnemy?.name ?? "None"}");
        }
    }

    private void IgnorePlayerHammerCollision(Transform ownerTransform, GameObject hammer)
    {
        Collider2D hammerCol = hammer.GetComponent<Collider2D>();
        Collider2D playerCol = ownerTransform.GetComponent<Collider2D>();
        if (hammerCol != null && playerCol != null)
        {
            Physics2D.IgnoreCollision(hammerCol, playerCol, true);
        }
    }
}
