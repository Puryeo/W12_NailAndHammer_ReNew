using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우클릭 차징공격 박스형 범위 공격 (MonoBehaviour 버전)
/// - 마우스 커서 방향으로 박스 영역 공격
/// - 그로기 적 처형 시 단계 상승 (최대 3단계)
/// - 3단계에서 사용 시 1단계로 리셋
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Combat/Secondary Charged Attack Box (Mono)")]
public class SecondaryChargedAttackBoxComponent : MonoBehaviour, ISecondaryChargedAttack
{
    [Header("Stage System")]
    [Tooltip("현재 단계 (1~3)")]
    [SerializeField] private int currentStage = 1;
    [Tooltip("누적 처형 횟수 (단계 계산용)")]
    [SerializeField] private int totalExecutionCount = 0;

    [Header("Box Attack Settings")]
    [Tooltip("박스 공격 기본 가로 크기")]
    [SerializeField] private float baseBoxWidth = 3f; 
    [Tooltip("박스 공격 기본 세로 크기")]
    [SerializeField] private float baseBoxHeight = 2f;
    [Tooltip("박스 중심까지의 거리 (플레이어로부터)")]
    [SerializeField] private float boxCenterDistance = 2f;

    [Header("Damage & Effects")]
    [Tooltip("기본 데미지")]
    [SerializeField] private float baseDamage = 30f;
    [Tooltip("넉백 강도")]
    [SerializeField] private float knockbackForce = 10f;
    [Tooltip("처형 시 회복량")]
    [SerializeField] private float executeHealAmount = 30f;
    [Tooltip("처형 시 넉백 강도")]
    [SerializeField] private float executeKnockbackForce = 12f;

    [Header("Visual Settings")]
    [Tooltip("박스 외곽선 표시 지속 시간")]
    [SerializeField] private float visualDuration = 0.3f;
    [Tooltip("박스 외곽선 색상 (1단계)")]
    [SerializeField] private Color stage1Color = Color.yellow;
    [Tooltip("박스 외곽선 색상 (2단계)")]
    [SerializeField] private Color stage2Color = Color.cyan;
    [Tooltip("박스 외곽선 색상 (3단계)")]
    [SerializeField] private Color stage3Color = Color.red;
    [Tooltip("라인 두께")]
    [SerializeField] private float lineWidth = 0.1f;

    [Header("Detection")]
    [Tooltip("감지할 레이어 (Enemy)")]
    [SerializeField] private LayerMask enemyLayer = 0;
    [Tooltip("최대 감지 가능한 적 수")]
    [SerializeField] private int maxDetectCount = 32;

    [Header("Stun")]
    [Tooltip("일반 공격 시 적용할 경직 시간")]
    [SerializeField] private float quickStun = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = true;

    // NonAlloc buffer
    private Collider2D[] overlapBuffer;

    // Public accessors
    public int CurrentStage => currentStage;
    public int TotalExecutionCount => totalExecutionCount;

    private void Awake()
    {
        overlapBuffer = new Collider2D[maxDetectCount];
    }

    public void Execute(PlayerCombat owner, Transform ownerTransform)
    {
        if (ownerTransform == null)
        {
            Debug.LogWarning($"[{GetAttackName()}] ownerTransform이 null입니다.");
            return;
        }

        // 공격 실행 전 단계 저장 (시각화에 사용)
        int stageBeforeAttack = currentStage;

        // 3단계에서 사용하는 경우 플래그
        bool wasStage3 = (currentStage == 3);

        // 현재 단계에 따른 배율 계산
        float sizeMultiplier = currentStage; // 1단계=1, 2단계=2, 3단계=3
        float damageMultiplier = (currentStage == 3) ? 2f : 1f;

        float actualWidth = baseBoxWidth * sizeMultiplier;
        float actualHeight = baseBoxHeight * sizeMultiplier;
        float actualDamage = baseDamage * damageMultiplier;

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] Execute - Stage: {currentStage}, Size: {actualWidth}x{actualHeight}, Damage: {actualDamage}");
        }

        // 마우스 방향 계산
        Vector2 mouseDirection = GetMouseDirection(ownerTransform);
        float angleDeg = Mathf.Atan2(mouseDirection.y, mouseDirection.x) * Mathf.Rad2Deg;

        // 박스 중심 위치
        Vector2 boxCenter = (Vector2)ownerTransform.position + mouseDirection * boxCenterDistance;

        // 박스 영역 검사
        Vector2 boxSize = new Vector2(actualWidth, actualHeight);
        int layerMask = (enemyLayer == 0) ? ~0 : (int)enemyLayer;
        int count = Physics2D.OverlapBoxNonAlloc(boxCenter, boxSize, angleDeg, overlapBuffer, layerMask);

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] OverlapBox detected {count} colliders at center={boxCenter} size={boxSize} angle={angleDeg}");
        }

        // 중복 방지용
        HashSet<int> hitEnemyIds = new HashSet<int>();
        bool anyExecutionThisAttack = false; // 이번 공격에서 처형이 발생했는지

        for (int i = 0; i < count; i++)
        {
            var col = overlapBuffer[i];
            if (col == null) continue;

            // 자기 자신 무시
            if (ownerTransform != null && (col.transform == ownerTransform || col.transform.IsChildOf(ownerTransform))) continue;

            var enemyCtrl = col.GetComponent<EnemyController>() ?? col.GetComponentInParent<EnemyController>();
            var enemyHealth = col.GetComponent<HealthSystem>() ?? col.GetComponentInParent<HealthSystem>();

            if (enemyCtrl == null) continue;

            int enemyId = enemyCtrl.GetInstanceID();
            if (hitEnemyIds.Contains(enemyId)) continue;
            hitEnemyIds.Add(enemyId);

            // 그로기 적 처형
            if (enemyCtrl.IsGroggy())
            {
                ExecuteEnemy(enemyCtrl, enemyHealth, col, boxCenter, owner);
                anyExecutionThisAttack = true; // 처형 발생 플래그
            }
            else
            {
                // 일반 데미지
                DamageEnemy(enemyCtrl, enemyHealth, col, boxCenter, actualDamage, ownerTransform);
            }
        }

        // 시각화 (공격 실행 전 단계 사용) - 단계 업데이트 전에 호출
        VisualizeBox(boxCenter, boxSize, angleDeg, stageBeforeAttack);

        // 단계 업데이트: 처형이 1회 이상 발생했으면 totalExecutionCount를 1만 증가
        if (anyExecutionThisAttack)
        {
            totalExecutionCount++;
            UpdateStage();

            if (showDebugLogs)
            {
                Debug.Log($"[{GetAttackName()}] Execution occurred! TotalExecutionCount: {totalExecutionCount}, CurrentStage: {currentStage} (next attack will use this stage)");
            }
        }

        // 3단계에서 사용했다면 리셋
        if (wasStage3)
        {
            ResetStage();
            if (showDebugLogs)
            {
                Debug.Log($"[{GetAttackName()}] Stage 3 used - resetting to Stage 1");
            }
        }
    }

    public string GetAttackName() => $"차징공격 박스 (단계: {currentStage})";

    public SecondaryChargedAttackType GetSkillType()
    {
        return SecondaryChargedAttackType.None;
    }

    private Vector2 GetMouseDirection(Transform ownerTransform)
    {
        Vector3 mouseWorld = Vector3.zero;

        if (Camera.main != null)
        {
            Vector3 m = Input.mousePosition;
            m.z = Mathf.Abs(Camera.main.transform.position.z - ownerTransform.position.z);
            mouseWorld = Camera.main.ScreenToWorldPoint(m);
            mouseWorld.z = ownerTransform.position.z;
        }
        else
        {
            mouseWorld = ownerTransform.position + ownerTransform.right;
        }

        Vector2 direction = (mouseWorld - ownerTransform.position).normalized;
        return direction;
    }

    private void ExecuteEnemy(EnemyController enemyCtrl, HealthSystem enemyHealth, Collider2D col, Vector2 attackCenter, PlayerCombat owner)
    {
        // 스택 소비 및 처형 마크
        enemyCtrl.ConsumeStacks(true, true, owner);
        enemyCtrl.MarkExecuted();

        // 플레이어 회복
        if (owner != null)
        {
            owner.OnExecutionSuccess(executeHealAmount, 0);
        }

        // 넉백
        Rigidbody2D targetRb = col.GetComponent<Rigidbody2D>() ?? col.GetComponentInParent<Rigidbody2D>();
        if (targetRb != null)
        {
            Vector2 push = ((Vector2)col.transform.position - attackCenter).normalized;
            targetRb.AddForce(push * executeKnockbackForce, ForceMode2D.Impulse);
        }

        // 이펙트
        if (enemyHealth != null)
        {
            var he = enemyHealth.GetComponent<HitEffect>();
            if (he != null) he.PlayExecuteEffect();

            var hpe = enemyHealth.GetComponent<HitParticleEffect>();
            if (hpe != null) hpe.PlayExecuteParticle(col.transform.position);
        }

        HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Strong, EShakeStrength.Strong, col.transform.position);

        // 사망 처리
        if (enemyHealth != null)
        {
            enemyHealth.ForceDieWithFade(1f);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] Executed groggy enemy: {enemyCtrl.name}");
        }
    }

    private void DamageEnemy(EnemyController enemyCtrl, HealthSystem enemyHealth, Collider2D col, Vector2 attackCenter, float damage, Transform ownerTransform)
    {
        // 데미지
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
        }

        // 경직
        if (quickStun > 0f)
        {
            enemyCtrl.ApplyStun(quickStun);
        }

        // 넉백
        Rigidbody2D rb = col.GetComponent<Rigidbody2D>() ?? col.GetComponentInParent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 push = ((Vector2)col.transform.position - attackCenter).normalized;
            rb.AddForce(push * knockbackForce, ForceMode2D.Impulse);
        }

        // 히트 이펙트
        HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Weak, EShakeStrength.Weak, col.transform.position);

        // 히트 등록
        enemyCtrl.RegisterHit(1, ownerTransform);
        enemyCtrl.ConsumeStacks(true, true, null);

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] Damaged enemy: {enemyCtrl.name} for {damage} damage");
        }
    }

    private void UpdateStage()
    {
        // totalExecutionCount에 따라 단계 결정
        if (totalExecutionCount >= 3)
        {
            currentStage = 3;
        }
        else if (totalExecutionCount >= 1)
        {
            currentStage = 2;
        }
        else
        {
            currentStage = 1;
        }
    }

    private void ResetStage()
    {
        currentStage = 1;
        totalExecutionCount = 0;
    }

    private void VisualizeBox(Vector2 center, Vector2 size, float angleDeg, int visualStage)
    {
        GameObject visualObj = new GameObject("BoxVisual");
        visualObj.transform.position = center;
        visualObj.transform.rotation = Quaternion.Euler(0, 0, angleDeg);

        // 공격 실행 전 단계에 따른 색상
        Color visualColor = visualStage switch
        {
            2 => stage2Color,
            3 => stage3Color,
            _ => stage1Color
        };

        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        // === 1. Mesh로 박스 내부 채우기 ===
        MeshFilter meshFilter = visualObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = visualObj.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "BoxMesh";

        // 버텍스: 박스 4개 모서리 (로컬 좌표)
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-halfW, -halfH, 0), // 좌하
            new Vector3(halfW, -halfH, 0),  // 우하
            new Vector3(halfW, halfH, 0),   // 우상
            new Vector3(-halfW, halfH, 0)   // 좌상
        };

        // 삼각형: 2개로 사각형 구성
        // 삼각형 1: 0-1-2 (좌하-우하-우상)
        // 삼각형 2: 0-2-3 (좌하-우상-좌상)
        int[] triangles = new int[6]
        {
            0, 1, 2,  // 첫 번째 삼각형
            0, 2, 3   // 두 번째 삼각형
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;

        // Material 설정 (내부 채우기)
        Material fillMaterial = new Material(Shader.Find("Sprites/Default"));
        fillMaterial.color = visualColor;
        meshRenderer.material = fillMaterial;

        // === 2. LineRenderer로 외곽선 그리기 ===
        LineRenderer lr = visualObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 4;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;

        // 외곽선은 불투명하게
        Color outlineColor = new Color(visualColor.r, visualColor.g, visualColor.b, 1f);
        lr.startColor = outlineColor;
        lr.endColor = outlineColor;

        // Material 설정
        Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lr.material = lineMaterial;

        // 박스 모서리 좌표 (로컬)
        lr.SetPosition(0, new Vector3(-halfW, -halfH, 0));
        lr.SetPosition(1, new Vector3(halfW, -halfH, 0));
        lr.SetPosition(2, new Vector3(halfW, halfH, 0));
        lr.SetPosition(3, new Vector3(-halfW, halfH, 0));

        // === 3. 자동 파괴 ===
        Destroy(visualObj, visualDuration);

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] Box visual created with stage {visualStage}, will destroy in {visualDuration}s");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // 프리뷰용으로 현재 단계 기준 박스 그리기
        Transform t = transform;
        if (t == null) return;

        float sizeMultiplier = currentStage;
        float actualWidth = baseBoxWidth * sizeMultiplier;
        float actualHeight = baseBoxHeight * sizeMultiplier;

        // 간단히 앞쪽으로 박스 그리기 (마우스 방향 시뮬레이션은 플레이 중에만 가능)
        Vector2 boxCenter = (Vector2)t.position + Vector2.right * boxCenterDistance;
        Vector2 boxSize = new Vector2(actualWidth, actualHeight);

        Gizmos.color = currentStage switch
        {
            2 => stage2Color,
            3 => stage3Color,
            _ => stage1Color
        };

        // 박스 그리기 (2D용 간단 구현)
        Vector3 center3 = boxCenter;
        Vector3 size3 = new Vector3(boxSize.x, boxSize.y, 0.1f);
        Gizmos.DrawWireCube(center3, size3);
    }
}
