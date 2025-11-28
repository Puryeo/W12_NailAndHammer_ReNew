using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우클릭 차징공격 - 망치 + 처형 시 부채꼴 영역 공격 (MonoBehaviour 버전)
/// - 기존 HammerSwingControllerWithCallback를 사용하여 망치 휘두르기
/// - 그로기 적 처형 시 적 위치 기준으로 부채꼴 범위 공격 발동
/// - 부채꼴 중심: 적 위치에서 플레이어 방향으로 0.5f 떨어진 위치
/// - 부채꼴 방향: 플레이어 → 적 방향
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Combat/Secondary Charged Attack Hammer With Sector (Mono)")]
public class SecondaryChargedAttackHammerWithSectorComponent : MonoBehaviour, ISecondaryChargedAttack
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

    [Header("Sector Attack Settings")]
    [Tooltip("부채꼴 중심 오프셋 거리 (적 위치에서 플레이어 방향으로)")]
    [SerializeField] private float sectorCenterOffset = 0.5f;
    [Tooltip("부채꼴 각도 (도)")]
    [SerializeField] private float sectorAngle = 60f;
    [Tooltip("부채꼴 반경")]
    [SerializeField] private float sectorRadius = 3f;
    [Tooltip("부채꼴 범위 내 적에게 줄 데미지")]
    [SerializeField] private float sectorDamage = 25f;
    [Tooltip("부채꼴 시각화 지속 시간")]
    [SerializeField] private float visualDuration = 0.3f;

    [Header("Sector Effects")]
    [Tooltip("부채꼴 넉백 강도")]
    [SerializeField] private float sectorKnockbackForce = 8f;
    [Tooltip("부채꼴 경직 시간")]
    [SerializeField] private float sectorStunDuration = 0.15f;

    [Header("Detection")]
    [Tooltip("감지할 레이어 (Enemy)")]
    [SerializeField] private LayerMask enemyLayer = 0;
    [Tooltip("최대 감지 가능한 적 수")]
    [SerializeField] private int maxDetectCount = 32;

    [Header("Visual")]
    [Tooltip("부채꼴 라인 색상")]
    [SerializeField] private Color sectorColor = new Color(1f, 0.5f, 0f, 0.8f);
    [Tooltip("라인 두께")]
    [SerializeField] private float lineWidth = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // 현재 공격의 owner 저장 (콜백에서 사용)
    private PlayerCombat currentOwner;
    private Transform currentOwnerTransform;

    // 적 검출용 버퍼
    private Collider2D[] overlapBuffer;

    private void Awake()
    {
        overlapBuffer = new Collider2D[maxDetectCount];
    }

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

    public string GetAttackName() => "차징공격 망치+부채꼴 (컴포넌트)";

    public SecondaryChargedAttackType GetSkillType()
    {
        return SecondaryChargedAttackType.Sector;
    }

    /// <summary>
    /// 적 처형 시 호출되는 콜백 - 부채꼴 영역 공격 발동
    /// </summary>
    /// <param name="playerPos">플레이어 위치</param>
    /// <param name="enemyPos">처형된 적 위치</param>
    /// <param name="executedEnemy">처형된 적 컨트롤러</param>
    private void OnEnemyExecuted(Vector2 playerPos, Vector2 enemyPos, EnemyController executedEnemy)
    {
        // 플레이어 -> 적 방향 계산
        Vector2 playerToEnemy = (enemyPos - playerPos).normalized;

        // 부채꼴 중심 위치: 적 위치에서 플레이어 방향으로 0.5f
        Vector2 sectorCenter = enemyPos - playerToEnemy * sectorCenterOffset;

        // 부채꼴 방향: 플레이어 -> 적 (적을 향하는 방향)
        Vector2 sectorDirection = playerToEnemy;

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] Execution - PlayerPos: {playerPos}, EnemyPos: {enemyPos}, SectorCenter: {sectorCenter}, Direction: {sectorDirection}");
        }

        // 부채꼴 범위 공격 실행
        PerformSectorAttack(sectorCenter, sectorDirection, executedEnemy);
    }

    /// <summary>
    /// 부채꼴 범위 공격 실행
    /// </summary>
    /// <param name="center">부채꼴 중심</param>
    /// <param name="forwardDir">부채꼴 방향</param>
    /// <param name="executedEnemy">처형된 적 (범위 공격에서 제외)</param>
    private void PerformSectorAttack(Vector2 center, Vector2 forwardDir, EnemyController executedEnemy)
    {
        // 원형 검사
        int layerMask = (enemyLayer == 0) ? ~0 : (int)enemyLayer;
        int count = Physics2D.OverlapCircleNonAlloc(center, sectorRadius, overlapBuffer, layerMask);

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] SectorAttack - Center: {center}, Detected: {count} enemies");
        }

        HashSet<int> hitEnemies = new HashSet<int>();
        int hitCount = 0;

        // 처형된 적의 InstanceID (제외용)
        int executedEnemyId = executedEnemy != null ? executedEnemy.GetInstanceID() : -1;

        // 부채꼴 범위 내 적들 검사
        for (int i = 0; i < count; i++)
        {
            var col = overlapBuffer[i];
            if (col == null) continue;

            var enemyCtrl = col.GetComponent<EnemyController>() ?? col.GetComponentInParent<EnemyController>();
            if (enemyCtrl == null) continue;

            int id = enemyCtrl.GetInstanceID();

            // 처형된 적은 제외
            if (id == executedEnemyId)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[{GetAttackName()}] Skipping executed enemy: {enemyCtrl.name}");
                }
                continue;
            }

            // 이미 처리한 적 스킵
            if (hitEnemies.Contains(id)) continue;

            // 부채꼴 각도 체크
            Vector2 toEnemy = ((Vector2)col.transform.position - center);

            // 중심과 너무 가까운 적은 무조건 포함
            if (toEnemy.sqrMagnitude < 0.0001f)
            {
                hitEnemies.Add(id);
                DamageEnemy(enemyCtrl, col, center);
                hitCount++;

                if (showDebugLogs)
                {
                    Debug.Log($"[{GetAttackName()}] SectorAttack hit (center): {enemyCtrl.name}");
                }
                continue;
            }

            Vector2 dirToEnemy = toEnemy.normalized;
            float angleBetween = Vector2.Angle(forwardDir, dirToEnemy);

            // 부채꼴 범위 내인지 확인
            if (angleBetween <= sectorAngle * 0.5f)
            {
                hitEnemies.Add(id);
                DamageEnemy(enemyCtrl, col, center);
                hitCount++;

                if (showDebugLogs)
                {
                    Debug.Log($"[{GetAttackName()}] SectorAttack hit: {enemyCtrl.name}, Angle: {angleBetween:F1}");
                }
            }
            else
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[{GetAttackName()}] SectorAttack miss: {enemyCtrl.name}, Angle: {angleBetween:F1} > {sectorAngle * 0.5f:F1}");
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] SectorAttack completed - Hit {hitCount} enemies");
        }

        // 부채꼴 시각화
        VisualizeSector(center, forwardDir);
    }

    /// <summary>
    /// 적에게 데미지 적용
    /// </summary>
    private void DamageEnemy(EnemyController enemyCtrl, Collider2D col, Vector2 attackCenter)
    {
        var enemyHealth = col.GetComponent<HealthSystem>() ?? col.GetComponentInParent<HealthSystem>();

        // 데미지
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(sectorDamage);
        }

        // 경직
        if (sectorStunDuration > 0f)
        {
            enemyCtrl.ApplyStun(sectorStunDuration);
        }

        // 넉백
        Rigidbody2D rb = col.GetComponent<Rigidbody2D>() ?? col.GetComponentInParent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 pushDir = ((Vector2)col.transform.position - attackCenter).normalized;
            rb.AddForce(pushDir * sectorKnockbackForce, ForceMode2D.Impulse);
        }

        // 히트 이펙트
        HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Weak, EShakeStrength.Weak, col.transform.position);

        // 히트 등록
        if (currentOwner != null)
        {
            enemyCtrl.RegisterHit(1, currentOwner.transform);
        }
    }

    /// <summary>
    /// 부채꼴 시각화 (Mesh + LineRenderer)
    /// </summary>
    private void VisualizeSector(Vector2 center, Vector2 forwardDir)
    {
        GameObject visual = new GameObject("SectorVisual");
        visual.transform.position = center;

        float angleRad = Mathf.Atan2(forwardDir.y, forwardDir.x);
        float halfAngleRad = sectorAngle * 0.5f * Mathf.Deg2Rad;
        int arcSegments = 30;

        // === 1. Mesh로 부채꼴 내부 채우기 ===
        MeshFilter meshFilter = visual.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = visual.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "SectorMesh";

        // 버텍스 생성: 중심 + 아크 포인트들
        Vector3[] vertices = new Vector3[arcSegments + 2];
        vertices[0] = Vector3.zero; // 중심점 (로컬 좌표)

        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float currentAngle = angleRad - halfAngleRad + (sectorAngle * Mathf.Deg2Rad * t);
            Vector2 point = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * sectorRadius;
            vertices[i + 1] = point;
        }

        // 트라이앵글 생성 (부채꼴 삼각형들)
        int[] triangles = new int[arcSegments * 3];
        for (int i = 0; i < arcSegments; i++)
        {
            triangles[i * 3] = 0; // 중심점
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;

        // Material 설정 (내부 채우기)
        Material fillMaterial = new Material(Shader.Find("Sprites/Default"));
        fillMaterial.color = sectorColor;
        meshRenderer.material = fillMaterial;

        // === 2. LineRenderer로 외곽선 그리기 ===
        LineRenderer lr = visual.AddComponent<LineRenderer>();
        lr.useWorldSpace = false; // 로컬 좌표 사용
        lr.loop = false;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = new Color(sectorColor.r, sectorColor.g, sectorColor.b, 1f); // 외곽선은 불투명하게
        lr.endColor = new Color(sectorColor.r, sectorColor.g, sectorColor.b, 1f);

        // Material 설정
        Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lr.material = lineMaterial;

        // 외곽선: 중심 -> 좌측 끝 -> 아크 -> 우측 끝 -> 중심
        int totalPoints = 1 + arcSegments + 1 + 1;
        lr.positionCount = totalPoints;

        int index = 0;
        // 중심점
        lr.SetPosition(index++, Vector3.zero);

        // 좌측 끝
        float leftAngle = angleRad - halfAngleRad;
        Vector3 leftPoint = new Vector3(Mathf.Cos(leftAngle), Mathf.Sin(leftAngle), 0) * sectorRadius;
        lr.SetPosition(index++, leftPoint);

        // 아크
        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float currentAngle = angleRad - halfAngleRad + (sectorAngle * Mathf.Deg2Rad * t);
            Vector3 point = new Vector3(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle), 0) * sectorRadius;
            lr.SetPosition(index++, point);
        }

        // 중심으로 돌아오기
        lr.SetPosition(index++, Vector3.zero);

        // === 3. 자동 파괴 ===
        Destroy(visual, visualDuration);

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] Sector visual created at {center}, will destroy in {visualDuration}s");
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
