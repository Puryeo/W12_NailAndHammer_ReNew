using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ExecutionProjectile
/// - 그로기 적 처형 시 발사되는 투사체
/// - 적과 충돌 시 해당 적 뒤쪽으로 부채꼴 범위 공격 발동
/// - 부채꼴 범위 내 모든 적에게 데미지
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ExecutionProjectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private PlayerCombat owner;
    private bool hasHit = false;

    // 처형된 적 정보 (충돌 무시용)
    private EnemyController executedEnemy;
    private int executedEnemyInstanceID = -1;

    [Header("Fan Attack Settings")]
    [Tooltip("부채꼴 각도 (도)")]
    [SerializeField] private float fanAngle = 60f;
    [Tooltip("부채꼴 반경")]
    [SerializeField] private float fanRadius = 3f;
    [Tooltip("부채꼴 범위 내 적에게 줄 데미지")]
    [SerializeField] private float fanDamage = 25f;
    [Tooltip("부채꼴 시각화 지속 시간")]
    [SerializeField] private float visualDuration = 0.3f;

    [Header("Detection")]
    [Tooltip("감지할 레이어 (Enemy)")]
    [SerializeField] private LayerMask enemyLayer = 0;
    [Tooltip("최대 감지 가능한 적 수")]
    [SerializeField] private int maxDetectCount = 32;

    [Header("Visual")]
    [Tooltip("부채꼴 라인 색상")]
    [SerializeField] private Color fanColor = new Color(1f, 0.5f, 0f, 0.8f);
    [Tooltip("라인 두께")]
    [SerializeField] private float lineWidth = 0.1f;

    [Header("Effects")]
    [Tooltip("넉백 강도")]
    [SerializeField] private float knockbackForce = 8f;
    [Tooltip("경직 시간")]
    [SerializeField] private float stunDuration = 0.15f;

    [Header("Lifetime")]
    [Tooltip("투사체 최대 생존 시간 (초)")]
    [SerializeField] private float maxLifetime = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private Collider2D[] overlapBuffer;
    private Rigidbody2D rb;

    private void Awake()
    {
        overlapBuffer = new Collider2D[maxDetectCount];
        rb = GetComponent<Rigidbody2D>();

        // Rigidbody2D 설정: 벽이나 다른 오브젝트와 충돌하지 않도록
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.gravityScale = 0f; // 중력 없음
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // 회전 고정
        }

        // Collider 설정: Trigger 모드로 설정
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// 투사체 초기화
    /// </summary>
    /// <param name="dir">발사 방향</param>
    /// <param name="spd">속도</param>
    /// <param name="own">플레이어 컴뱃</param>
    /// <param name="executed">처형된 적 (충돌 무시용)</param>
    public void Initialize(Vector2 dir, float spd, PlayerCombat own, EnemyController executed = null)
    {
        direction = dir.normalized;
        speed = spd;
        owner = own;
        hasHit = false;
        executedEnemy = executed;

        // 처형된 적의 InstanceID 저장 (null 체크용)
        if (executed != null)
        {
            executedEnemyInstanceID = executed.GetInstanceID();

            // 처형된 적의 모든 Collider와 투사체 Collider 충돌 무시
            Collider2D projCollider = GetComponent<Collider2D>();
            if (projCollider != null)
            {
                Collider2D[] enemyColliders = executed.GetComponentsInChildren<Collider2D>();
                foreach (var enemyCol in enemyColliders)
                {
                    if (enemyCol != null)
                    {
                        Physics2D.IgnoreCollision(projCollider, enemyCol, true);
                        if (showDebugLogs)
                        {
                            Debug.Log($"[ExecutionProjectile] Ignoring collision with executed enemy collider: {enemyCol.name}");
                        }
                    }
                }
            }
        }

        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
        }

        // 투사체 회전 (진행 방향으로)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (showDebugLogs)
        {
            Debug.Log($"[ExecutionProjectile] Initialized - Direction: {direction}, Speed: {speed}, FanDamage: {fanDamage}, Lifetime: {maxLifetime}s, ExecutedEnemy: {executed?.name ?? "None"}");
        }

        // 5초 후 자동 파괴 (벽에 안 맞고 계속 날아가는 경우 방지)
        Destroy(gameObject, maxLifetime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return;
        if (collision == null) return;

        // Enemy 태그 체크
        if (!collision.CompareTag("Enemy")) return;

        // Enemy 레이어 체크 (추가 안전장치)
        if (enemyLayer != 0 && ((enemyLayer & (1 << collision.gameObject.layer)) == 0))
        {
            if (showDebugLogs)
            {
                Debug.Log($"[ExecutionProjectile] Ignored non-enemy layer: {LayerMask.LayerToName(collision.gameObject.layer)}");
            }
            return;
        }

        // 처형된 적과의 충돌 무시 (추가 안전장치)
        var enemyCtrl = collision.GetComponent<EnemyController>() ?? collision.GetComponentInParent<EnemyController>();
        if (enemyCtrl != null && executedEnemyInstanceID != -1)
        {
            if (enemyCtrl.GetInstanceID() == executedEnemyInstanceID)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[ExecutionProjectile] Ignored executed enemy: {collision.name}");
                }
                return;
            }
        }

        // 이미 충돌했으면 무시
        hasHit = true;

        if (showDebugLogs)
        {
            Debug.Log($"[ExecutionProjectile] Hit enemy: {collision.name}");
        }

        // 충돌 지점
        Vector2 hitPos = collision.transform.position;

        // 부채꼴 범위 공격 (투사체 진행 방향 = 적 뒤쪽 방향)
        // 충돌한 적 정보 전달하여 무조건 데미지 적용
        PerformFanAttack(hitPos, direction, enemyCtrl);

        // 투사체 제거
        Destroy(gameObject);
    }

    /// <summary>
    /// 부채꼴 범위 공격 실행
    /// </summary>
    /// <param name="center">부채꼴 중심 (충돌 지점)</param>
    /// <param name="forwardDir">부채�ol 방향 (투사체 진행 방향)</param>
    /// <param name="hitEnemy">투사체에 직접 충돌한 적 (무조건 데미지 적용)</param>
    private void PerformFanAttack(Vector2 center, Vector2 forwardDir, EnemyController hitEnemy = null)
    {
        // 원형 검사
        int layerMask = (enemyLayer == 0) ? ~0 : (int)enemyLayer;
        int count = Physics2D.OverlapCircleNonAlloc(center, fanRadius, overlapBuffer, layerMask);

        if (showDebugLogs)
        {
            Debug.Log($"[ExecutionProjectile] FanAttack - Center: {center}, Detected: {count} enemies, HitEnemy: {hitEnemy?.name ?? "None"}");
        }

        HashSet<int> hitEnemies = new HashSet<int>();
        int hitCount = 0;

        // 1. 먼저 충돌한 적에게 무조건 데미지 (각도 체크 없음)
        if (hitEnemy != null)
        {
            int hitEnemyId = hitEnemy.GetInstanceID();
            hitEnemies.Add(hitEnemyId);

            // 충돌한 적의 Collider 찾기
            Collider2D hitEnemyCollider = hitEnemy.GetComponent<Collider2D>() ?? hitEnemy.GetComponentInChildren<Collider2D>();
            if (hitEnemyCollider != null)
            {
                DamageEnemy(hitEnemy, hitEnemyCollider, center);
                hitCount++;

                if (showDebugLogs)
                {
                    Debug.Log($"[ExecutionProjectile] FanAttack hit (direct collision): {hitEnemy.name}");
                }
            }
        }

        // 2. 부채꼴 범위 내 다른 적들 검사
        for (int i = 0; i < count; i++)
        {
            var col = overlapBuffer[i];
            if (col == null) continue;

            var enemyCtrl = col.GetComponent<EnemyController>() ?? col.GetComponentInParent<EnemyController>();
            if (enemyCtrl == null) continue;

            int id = enemyCtrl.GetInstanceID();
            if (hitEnemies.Contains(id)) continue; // 이미 처리한 적(충돌한 적) 스킵

            // 부채꼴 각도 체크
            Vector2 toEnemy = ((Vector2)col.transform.position - center);

            // 중심과 너무 가까운 적은 무조건 포함 (충돌한 적 제외하고는 여기까지 오기 어려움)
            if (toEnemy.sqrMagnitude < 0.0001f)
            {
                hitEnemies.Add(id);
                DamageEnemy(enemyCtrl, col, center);
                hitCount++;

                if (showDebugLogs)
                {
                    Debug.Log($"[ExecutionProjectile] FanAttack hit (center): {enemyCtrl.name}");
                }
                continue;
            }

            Vector2 dirToEnemy = toEnemy.normalized;
            float angleBetween = Vector2.Angle(forwardDir, dirToEnemy);

            // 부채꼴 범위 내인지 확인
            if (angleBetween <= fanAngle * 0.5f)
            {
                hitEnemies.Add(id);
                DamageEnemy(enemyCtrl, col, center);
                hitCount++;

                if (showDebugLogs)
                {
                    Debug.Log($"[ExecutionProjectile] FanAttack hit (angle check): {enemyCtrl.name}, Angle: {angleBetween:F1}");
                }
            }
            else
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[ExecutionProjectile] FanAttack miss (out of angle): {enemyCtrl.name}, Angle: {angleBetween:F1} > {fanAngle * 0.5f:F1}");
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"[ExecutionProjectile] FanAttack completed - Hit {hitCount} enemies");
        }

        // 부채꼴 시각화 (코루틴이 아닌 직접 호출)
        VisualizeFan(center, forwardDir);
    }

    /// <summary>
    /// 적에게 데미지 적용
    /// </summary>
    private void DamageEnemy(EnemyController enemyCtrl, Collider2D col, Vector2 attackCenter)
    {
        var enemyHealth = col.GetComponent<HealthSystem>() ?? col.GetComponentInParent<HealthSystem>();

        // 데미지 (인스펙터에서 설정한 fanDamage 사용)
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(fanDamage);
        }

        // 경직
        if (stunDuration > 0f)
        {
            enemyCtrl.ApplyStun(stunDuration);
        }

        // 넉백
        Rigidbody2D rb = col.GetComponent<Rigidbody2D>() ?? col.GetComponentInParent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 pushDir = ((Vector2)col.transform.position - attackCenter).normalized;
            rb.AddForce(pushDir * knockbackForce, ForceMode2D.Impulse);
        }

        // 히트 이펙트
        HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Weak, EShakeStrength.Weak, col.transform.position);

        // 히트 등록
        if (owner != null)
        {
            enemyCtrl.RegisterHit(1, owner.transform);
        }
    }

    /// <summary>
    /// 부채꼴 시각화 (Mesh + LineRenderer)
    /// </summary>
    private void VisualizeFan(Vector2 center, Vector2 forwardDir)
    {
        GameObject visual = new GameObject("FanVisual");
        visual.transform.position = center;

        float angleRad = Mathf.Atan2(forwardDir.y, forwardDir.x);
        float halfAngleRad = fanAngle * 0.5f * Mathf.Deg2Rad;
        int arcSegments = 30;

        // === 1. Mesh로 부채꼴 내부 채우기 ===
        MeshFilter meshFilter = visual.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = visual.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "FanMesh";

        // 버텍스 생성: 중심 + 아크 포인트들
        Vector3[] vertices = new Vector3[arcSegments + 2];
        vertices[0] = Vector3.zero; // 중심점 (로컬 좌표)

        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float currentAngle = angleRad - halfAngleRad + (fanAngle * Mathf.Deg2Rad * t);
            Vector2 point = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * fanRadius;
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
        fillMaterial.color = fanColor;
        meshRenderer.material = fillMaterial;

        // === 2. LineRenderer로 외곽선 그리기 ===
        LineRenderer lr = visual.AddComponent<LineRenderer>();
        lr.useWorldSpace = false; // 로컬 좌표 사용
        lr.loop = false;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = new Color(fanColor.r, fanColor.g, fanColor.b, 1f); // 외곽선은 불투명하게
        lr.endColor = new Color(fanColor.r, fanColor.g, fanColor.b, 1f);

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
        Vector3 leftPoint = new Vector3(Mathf.Cos(leftAngle), Mathf.Sin(leftAngle), 0) * fanRadius;
        lr.SetPosition(index++, leftPoint);

        // 아크
        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float currentAngle = angleRad - halfAngleRad + (fanAngle * Mathf.Deg2Rad * t);
            Vector3 point = new Vector3(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle), 0) * fanRadius;
            lr.SetPosition(index++, point);
        }

        // 중심으로 돌아오기
        lr.SetPosition(index++, Vector3.zero);

        // === 3. 자동 파괴 (코루틴 없이 직접 호출) ===
        Destroy(visual, visualDuration);

        if (showDebugLogs)
        {
            Debug.Log($"[ExecutionProjectile] Fan visual created, will destroy in {visualDuration}s");
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 에디터에서 부채꼴 범위 미리보기
        Gizmos.color = fanColor;
        Vector2 center = transform.position;
        Vector2 forward = transform.right; // 투사체 방향

        float angleRad = Mathf.Atan2(forward.y, forward.x);
        float halfAngleRad = fanAngle * 0.5f * Mathf.Deg2Rad;

        // 부채꼴 외곽선
        Vector2 leftEdge = new Vector2(Mathf.Cos(angleRad - halfAngleRad), Mathf.Sin(angleRad - halfAngleRad)) * fanRadius;
        Vector2 rightEdge = new Vector2(Mathf.Cos(angleRad + halfAngleRad), Mathf.Sin(angleRad + halfAngleRad)) * fanRadius;

        Gizmos.DrawLine(center, center + leftEdge);
        Gizmos.DrawLine(center, center + rightEdge);

        // 아크
        int segments = 20;
        Vector3 prev = center + leftEdge;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float currentAngle = angleRad - halfAngleRad + (fanAngle * Mathf.Deg2Rad * t);
            Vector2 point = center + new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * fanRadius;
            Gizmos.DrawLine(prev, point);
            prev = point;
        }
    }
}
