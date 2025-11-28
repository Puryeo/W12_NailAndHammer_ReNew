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

    [Header("Shockwave Visual")]
    [Tooltip("충격파 외곽선 색상")]
    [SerializeField] private Color shockwaveColor = new Color(1f, 0.8f, 0f, 1f);
    [Tooltip("부채꼴 내부 채우기 색상")]
    [SerializeField] private Color sectorFillColor = new Color(1f, 0.8f, 0f, 0.5f);
    [Tooltip("충격파 확장 시간")]
    [SerializeField] private float shockwaveExpandTime = 0.15f;
    [Tooltip("확장 완료 후 부채꼴 유지 시간")]
    [SerializeField] private float sectorHoldTime = 0.1f;
    [Tooltip("부채꼴 페이드아웃 시간")]
    [SerializeField] private float sectorFadeOutTime = 0.1f;
    [Tooltip("충격파 라인 두께")]
    [SerializeField] private float shockwaveLineWidth = 0.2f;
    [Tooltip("충격파 확장 커브")]
    [SerializeField] private AnimationCurve shockwaveExpandCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Pixel Particle Effect")]
    [Tooltip("둘레 픽셀 파티클 개수")]
    [SerializeField] private int pixelCount = 30;
    [Tooltip("중심에서 생성될 픽셀 개수")]
    [SerializeField] private int centerPixelCount = 60;
    [Tooltip("픽셀 크기")]
    [SerializeField] private float pixelSize = 0.08f;
    [Tooltip("픽셀 흩어지는 거리")]
    [SerializeField] private float pixelScatterDistance = 0.4f;
    [Tooltip("픽셀 흩어지는 시간 (수명)")]
    [SerializeField] private float pixelScatterTime = 0.4f;
    [Tooltip("픽셀 랜덤 속도 변화")]
    [SerializeField] private float pixelRandomSpeedVariation = 0.3f;

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
    /// 부채꼴 충격파 시각화 (확장 애니메이션 + 픽셀 파티클)
    /// </summary>
    private void VisualizeSector(Vector2 center, Vector2 forwardDir)
    {
        GameObject visual = new GameObject("ShockwaveVisual");
        visual.transform.position = center;

        // 충격파 확장 코루틴 시작
        StartCoroutine(ShockwaveExpansionCoroutine(visual, center, forwardDir));

        if (showDebugLogs)
        {
            Debug.Log($"[{GetAttackName()}] Shockwave visual started at {center}");
        }
    }

    /// <summary>
    /// 충격파 확장 코루틴 - 반지름이 증가하며 호를 그리고 픽셀 파티클 생성
    /// </summary>
    private System.Collections.IEnumerator ShockwaveExpansionCoroutine(GameObject visualParent, Vector2 center, Vector2 forwardDir)
    {
        // 부채꼴 각도 계산
        float angleRad = Mathf.Atan2(forwardDir.y, forwardDir.x);
        float halfAngleRad = sectorAngle * 0.5f * Mathf.Deg2Rad;
        int arcSegments = 40; // 부드러운 호를 위해 세그먼트 증가

        // Mesh 생성 (부채꼴 내부 채우기)
        MeshFilter meshFilter = visualParent.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = visualParent.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "SectorMesh";
        meshFilter.mesh = mesh;

        Material fillMaterial = new Material(Shader.Find("Sprites/Default"));
        fillMaterial.color = sectorFillColor;
        meshRenderer.material = fillMaterial;

        // LineRenderer 생성 (외곽선)
        LineRenderer lr = visualParent.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.startWidth = shockwaveLineWidth;
        lr.endWidth = shockwaveLineWidth;
        lr.startColor = shockwaveColor;
        lr.endColor = shockwaveColor;

        Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lr.material = lineMaterial;

        // 픽셀 파티클을 즉시 생성 (확장과 동시에 터져나감)
        SpawnPixelParticles(visualParent, center, forwardDir, angleRad, halfAngleRad, arcSegments);

        // 중심에서 퍼지는 픽셀 생성
        SpawnCenterPixelParticles(visualParent, center, forwardDir, angleRad, halfAngleRad);

        // 확장 애니메이션
        float elapsed = 0f;
        while (elapsed < shockwaveExpandTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shockwaveExpandTime);
            float curveValue = shockwaveExpandCurve.Evaluate(t);
            float currentRadius = sectorRadius * curveValue;

            // === Mesh 업데이트 (부채꼴 내부) ===
            UpdateSectorMesh(mesh, center, angleRad, halfAngleRad, currentRadius, arcSegments);

            // === LineRenderer 업데이트 (외곽선) ===
            // 부채꼴 외곽선 전체 그리기: 중심 -> 왼쪽 직선 -> 호 -> 오른쪽 직선 -> 중심
            int totalPoints = 1 + 1 + arcSegments + 1 + 1; // 중심 + 왼쪽끝 + 호 + 오른쪽끝 + 중심
            lr.positionCount = totalPoints;

            int index = 0;

            // 1. 중심점
            lr.SetPosition(index++, center);

            // 2. 왼쪽 끝점 (왼쪽 직선)
            float leftAngle = angleRad - halfAngleRad;
            Vector3 leftPoint = center + new Vector2(Mathf.Cos(leftAngle), Mathf.Sin(leftAngle)) * currentRadius;
            lr.SetPosition(index++, leftPoint);

            // 3. 호 (왼쪽에서 오른쪽으로)
            for (int i = 0; i <= arcSegments; i++)
            {
                float arcT = i / (float)arcSegments;
                float currentAngle = angleRad - halfAngleRad + (sectorAngle * Mathf.Deg2Rad * arcT);
                Vector3 point = center + new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * currentRadius;
                lr.SetPosition(index++, point);
            }

            // 4. 중심으로 돌아오기 (오른쪽 직선)
            lr.SetPosition(index++, center);

            yield return null;
        }

        // 최종 반지름으로 고정
        UpdateSectorMesh(mesh, center, angleRad, halfAngleRad, sectorRadius, arcSegments);

        int finalTotalPoints = 1 + 1 + arcSegments + 1 + 1;
        lr.positionCount = finalTotalPoints;

        int finalIndex = 0;
        lr.SetPosition(finalIndex++, center);

        float finalLeftAngle = angleRad - halfAngleRad;
        Vector3 finalLeftPoint = center + new Vector2(Mathf.Cos(finalLeftAngle), Mathf.Sin(finalLeftAngle)) * sectorRadius;
        lr.SetPosition(finalIndex++, finalLeftPoint);

        for (int i = 0; i <= arcSegments; i++)
        {
            float arcT = i / (float)arcSegments;
            float currentAngle = angleRad - halfAngleRad + (sectorAngle * Mathf.Deg2Rad * arcT);
            Vector3 point = center + new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * sectorRadius;
            lr.SetPosition(finalIndex++, point);
        }

        lr.SetPosition(finalIndex++, center);

        // 유지 시간 대기
        if (sectorHoldTime > 0f)
        {
            yield return new WaitForSeconds(sectorHoldTime);
        }

        // 페이드아웃 애니메이션
        if (sectorFadeOutTime > 0f)
        {
            Color initialLineColor = lr.startColor;
            Color initialFillColor = meshRenderer.material.color;

            float fadeElapsed = 0f;
            while (fadeElapsed < sectorFadeOutTime)
            {
                fadeElapsed += Time.deltaTime;
                float fadeT = fadeElapsed / sectorFadeOutTime;

                // LineRenderer 페이드아웃
                Color lineColor = initialLineColor;
                lineColor.a = Mathf.Lerp(initialLineColor.a, 0f, fadeT);
                lr.startColor = lineColor;
                lr.endColor = lineColor;

                // Mesh 페이드아웃
                Color fillColor = initialFillColor;
                fillColor.a = Mathf.Lerp(initialFillColor.a, 0f, fadeT);
                meshRenderer.material.color = fillColor;

                yield return null;
            }
        }

        // 부채꼴 파괴 (픽셀은 계속 유지)
        Destroy(lr);
        Destroy(meshRenderer);
        Destroy(meshFilter);

        // 전체 시각 효과 파괴 (픽셀 애니메이션이 끝날 때까지 대기)
        float totalDuration = pixelScatterTime;
        Destroy(visualParent, totalDuration);
    }

    /// <summary>
    /// 부채꼴 Mesh 업데이트 - 현재 반지름에 맞춰 재생성
    /// </summary>
    private void UpdateSectorMesh(Mesh mesh, Vector2 center, float angleRad, float halfAngleRad, float currentRadius, int arcSegments)
    {
        // 버텍스 생성: 중심 + 호 포인트들 (로컬 좌표계 사용)
        Vector3[] vertices = new Vector3[arcSegments + 2];
        vertices[0] = Vector3.zero; // 로컬 중심점 (부모가 center에 위치함)

        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float currentAngle = angleRad - halfAngleRad + (sectorAngle * Mathf.Deg2Rad * t);
            // 로컬 좌표로 변환 (center 기준)
            Vector2 point = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * currentRadius;
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

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    /// <summary>
    /// 픽셀 파티클 생성 - 부채꼴 둘레 전체를 따라 정사각형 픽셀들이 흩어짐
    /// </summary>
    private void SpawnPixelParticles(GameObject parent, Vector2 center, Vector2 forwardDir, float angleRad, float halfAngleRad, int arcSegments)
    {
        // 부채꼴 둘레 길이 계산
        float arcLength = sectorRadius * sectorAngle * Mathf.Deg2Rad;
        float totalPerimeter = 2f * sectorRadius + arcLength; // 왼쪽 직선 + 호 + 오른쪽 직선

        // 둘레를 따라 균등하게 픽셀 배치
        for (int i = 0; i < pixelCount; i++)
        {
            // 둘레 위의 랜덤한 위치 선택 (0~1)
            float perimeterT = Random.Range(0f, 1f);
            float distance = perimeterT * totalPerimeter;

            Vector2 pixelPosition;
            Vector2 normalDir;

            // 왼쪽 직선 위
            if (distance < sectorRadius)
            {
                float lineT = distance / sectorRadius;
                float leftAngle = angleRad - halfAngleRad;
                pixelPosition = center + new Vector2(Mathf.Cos(leftAngle), Mathf.Sin(leftAngle)) * (sectorRadius * lineT);
                normalDir = new Vector2(Mathf.Cos(leftAngle), Mathf.Sin(leftAngle));
            }
            // 호 위
            else if (distance < sectorRadius + arcLength)
            {
                float arcDist = distance - sectorRadius;
                float arcT = arcDist / arcLength;
                float currentAngle = angleRad - halfAngleRad + (sectorAngle * Mathf.Deg2Rad * arcT);
                pixelPosition = center + new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * sectorRadius;
                normalDir = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle));
            }
            // 오른쪽 직선 위
            else
            {
                float lineDist = distance - (sectorRadius + arcLength);
                float lineT = 1f - (lineDist / sectorRadius);
                float rightAngle = angleRad + halfAngleRad;
                pixelPosition = center + new Vector2(Mathf.Cos(rightAngle), Mathf.Sin(rightAngle)) * (sectorRadius * lineT);
                normalDir = new Vector2(Mathf.Cos(rightAngle), Mathf.Sin(rightAngle));
            }

            // 픽셀 GameObject 생성
            GameObject pixel = new GameObject($"Pixel_{i}");
            pixel.transform.SetParent(parent.transform);
            pixel.transform.position = pixelPosition;

            // SpriteRenderer로 정사각형 표현
            SpriteRenderer sr = pixel.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSquareSprite();
            sr.color = shockwaveColor;
            sr.sortingOrder = 10;

            // 픽셀 크기 설정 (크고 작은 변화)
            float size = pixelSize * Random.Range(0.5f, 2.0f);
            pixel.transform.localScale = Vector3.one * size;

            // 흩어지는 방향 계산 (법선 방향 + 약간의 랜덤)
            Vector2 randomOffset = Random.insideUnitCircle * 0.3f;
            Vector2 scatterDir = (normalDir + randomOffset).normalized;

            // 픽셀 애니메이션 시작
            StartCoroutine(AnimatePixelParticle(pixel, pixelPosition, scatterDir));
        }
    }

    /// <summary>
    /// 중심에서 부채꼴 영역으로 퍼지는 픽셀 파티클 생성
    /// </summary>
    private void SpawnCenterPixelParticles(GameObject parent, Vector2 center, Vector2 forwardDir, float angleRad, float halfAngleRad)
    {
        if (centerPixelCount <= 0) return;

        // 부채꼴 영역 내에서 랜덤하게 픽셀 생성
        for (int i = 0; i < centerPixelCount; i++)
        {
            // 랜덤한 각도 (부채꼴 범위 내)
            float randomAngleOffset = Random.Range(-halfAngleRad, halfAngleRad);
            float pixelAngle = angleRad + randomAngleOffset;

            // 랜덤한 거리 (0 ~ sectorRadius)
            float randomDistance = Random.Range(0f, sectorRadius);

            // 목표 위치 계산
            Vector2 targetPosition = center + new Vector2(Mathf.Cos(pixelAngle), Mathf.Sin(pixelAngle)) * randomDistance;

            // 픽셀 GameObject 생성
            GameObject pixel = new GameObject($"CenterPixel_{i}");
            pixel.transform.SetParent(parent.transform);
            pixel.transform.position = center; // 중심에서 시작

            // SpriteRenderer로 정사각형 표현
            SpriteRenderer sr = pixel.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSquareSprite();
            sr.color = shockwaveColor;
            sr.sortingOrder = 10;

            // 픽셀 크기 설정 (크고 작은 변화)
            float size = pixelSize * Random.Range(0.5f, 2.0f);
            pixel.transform.localScale = Vector3.one * size;

            // 픽셀 애니메이션 시작 (확장 시간에 맞춰 이동)
            Vector2 direction = (targetPosition - center).normalized;
            float distance = Vector2.Distance(center, targetPosition);
            StartCoroutine(AnimateCenterPixelParticle(pixel, center, direction, distance));
        }
    }

    /// <summary>
    /// 중심 픽셀 파티클 애니메이션 - 확장 시간에 맞춰 이동하며 페이드아웃
    /// </summary>
    private System.Collections.IEnumerator AnimateCenterPixelParticle(GameObject pixel, Vector2 startPos, Vector2 direction, float targetDistance)
    {
        SpriteRenderer sr = pixel.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        float elapsed = 0f;
        Color initialColor = sr.color;

        // 확장 시간에 맞춰 이동
        while (elapsed < shockwaveExpandTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shockwaveExpandTime);

            // 확장 커브 적용
            float curveValue = shockwaveExpandCurve.Evaluate(t);

            // 위치 업데이트 (중심에서 목표 거리까지)
            pixel.transform.position = startPos + direction * targetDistance * curveValue;

            yield return null;
        }

        // 확장 완료 후 추가로 조금 더 이동하며 페이드아웃
        float fadeElapsed = 0f;
        Vector2 finalPos = pixel.transform.position;
        float extraDistance = pixelScatterDistance;

        while (fadeElapsed < pixelScatterTime)
        {
            fadeElapsed += Time.deltaTime;
            float fadeT = fadeElapsed / pixelScatterTime;

            // 추가 이동
            float moveT = 1f - (1f - fadeT) * (1f - fadeT); // ease-out
            pixel.transform.position = finalPos + direction * extraDistance * moveT;

            // 페이드아웃
            Color col = sr.color;
            col.a = Mathf.Lerp(initialColor.a, 0f, fadeT);
            sr.color = col;

            // 크기 감소
            float scale = Mathf.Lerp(1f, 0.5f, fadeT);
            pixel.transform.localScale = Vector3.one * pixelSize * scale * Random.Range(0.5f, 2.0f);

            yield return null;
        }

        // 애니메이션 종료 후 파괴
        Destroy(pixel);
    }

    /// <summary>
    /// 정사각형 스프라이트 생성
    /// </summary>
    private Sprite CreateSquareSprite()
    {
        // 작은 흰색 정사각형 텍스처 생성
        int size = 8;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point; // 픽셀 아트 스타일
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// 픽셀 파티클 애니메이션 - 흩어지며 페이드아웃
    /// </summary>
    private System.Collections.IEnumerator AnimatePixelParticle(GameObject pixel, Vector2 startPos, Vector2 direction)
    {
        SpriteRenderer sr = pixel.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        float elapsed = 0f;
        float distance = pixelScatterDistance * Random.Range(1f - pixelRandomSpeedVariation, 1f + pixelRandomSpeedVariation);

        while (elapsed < pixelScatterTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pixelScatterTime;

            // 위치 업데이트 (감속 효과)
            float moveT = 1f - (1f - t) * (1f - t); // ease-out
            pixel.transform.position = startPos + direction * distance * moveT;

            // 페이드아웃
            Color col = sr.color;
            col.a = 1f - t;
            sr.color = col;

            // 크기 감소
            float scale = Mathf.Lerp(1f, 0.5f, t);
            pixel.transform.localScale = Vector3.one * pixelSize * scale;

            yield return null;
        }

        // 애니메이션 종료 후 파괴
        Destroy(pixel);
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
