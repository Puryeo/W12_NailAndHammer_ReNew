using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Windmill Execution (윈드밀 처형)
/// - 플레이어에게 직접 붙여서 사용하는 스킬 컴포넌트
/// - 플레이어 중심으로 망치를 360도 회전시키며 휘두름
/// - 그로기 상태 적이 있으면 처형 발동
/// - 적 처치 시마다 1초씩 연장 (최대 6초)
/// </summary>
public class WindmillExecution : MonoBehaviour, ISecondaryChargedAttack
{
    [Header("Hammer Settings")]
    [Tooltip("망치 프리팹 (HammerSwingController 컴포넌트 필요)")]
    [SerializeField] private GameObject hammerPrefab;

    [Tooltip("망치 회전 반경 (플레이어로부터 거리)")]
    [SerializeField] private float rotationRadius = 1.5f;

    [Header("Rotation Settings")]
    [Tooltip("기본 회전 지속 시간 (초)")]
    [SerializeField] private float baseDuration = 2f;

    [Tooltip("적 처치 시 연장 시간 (초)")]
    [SerializeField] private float extensionPerKill = 1f;

    [Tooltip("최대 회전 지속 시간 (초)")]
    [SerializeField] private float maxDuration = 6f;

    [Tooltip("회전 속도 (회전/초) - 1.0 = 1초에 360도")]
    [SerializeField] private float rotationsPerSecond = 2f;

    [Header("Damage & Effects")]
    [Tooltip("공격 데미지")]
    [SerializeField] private float damage = 30f;

    [Tooltip("넉백 강도")]
    [SerializeField] private float knockbackForce = 10f;

    [Tooltip("처형 시 플레이어 회복량")]
    [SerializeField] private float executeHealAmount = 30f;

    [Tooltip("처형 시 넉백 배율")]
    [SerializeField] private float executeKnockbackMultiplier = 1.5f;

    [Tooltip("일반 적 히트 쿨다운 (초) - 같은 적에게 다시 데미지를 입히기까지의 간격")]
    [SerializeField] private float hitCooldown = 0.3f;

    [Header("Detection Settings")]
    [Tooltip("그로기 적 탐지 범위")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("적 탐지에 사용할 레이어 마스크")]
    [SerializeField] private LayerMask enemyLayer = -1;

    [Header("Animation")]
    [Tooltip("회전 속도 조절 커브 (시간 진행에 따른 속도 변화)")]
    [SerializeField] private AnimationCurve rotationSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // 인터페이스 구현
    public void Execute(PlayerCombat owner, Transform ownerTransform)
    {
        if (hammerPrefab == null)
        {
            Debug.LogWarning($"[{GetAttackName()}] hammerPrefab이 할당되지 않았습니다.");
            return;
        }

        // 그로기 적 탐지 - 실제로 그로기 상태인 적이 있는지 확인
        bool hasGroggyEnemy = CheckForGroggyEnemies(ownerTransform.position);

        if (!hasGroggyEnemy)
        {
            Debug.Log($"[{GetAttackName()}] 그로기 상태의 적이 없어 처형이 취소되었습니다.");
            return;
        }

        // 코루틴 실행
        StartCoroutine(ExecuteWindmillRotation(owner, ownerTransform));
    }

    public string GetAttackName()
    {
        return "Windmill Execution";
    }

    /// <summary>
    /// 윈드밀 360도 회전 공격 코루틴 (Pivot 회전 방식)
    /// </summary>
    private IEnumerator ExecuteWindmillRotation(PlayerCombat owner, Transform ownerTransform)
    {
        if (showDebugLogs) Debug.Log($"[{GetAttackName()}] 윈드밀 처형 시작!");

        // Pivot 생성 (플레이어 자식)
        GameObject pivotObj = new GameObject("WindmillPivot");
        pivotObj.transform.SetParent(ownerTransform);
        pivotObj.transform.localPosition = Vector3.zero;
        pivotObj.transform.localRotation = Quaternion.identity;

        // 망치 생성 (Pivot의 자식으로, 오프셋 위치에)
        GameObject hammerObj = Instantiate(hammerPrefab, pivotObj.transform.position, Quaternion.identity);
        hammerObj.transform.SetParent(pivotObj.transform);
        hammerObj.transform.localPosition = Vector3.right * rotationRadius;
        hammerObj.transform.localRotation = Quaternion.identity;

        // HammerSwingController가 있다면 비활성화
        var hammerController = hammerObj.GetComponent<HammerSwingController>();
        if (hammerController != null)
        {
            hammerController.enabled = false;
        }

        // 망치에 WindmillHammerBehavior 추가
        var windmillBehavior = hammerObj.AddComponent<WindmillHammerBehavior>();
        windmillBehavior.Initialize(
            owner: owner,
            damage: damage,
            knockback: knockbackForce,
            executeHealAmount: executeHealAmount,
            executeKnockbackMultiplier: executeKnockbackMultiplier,
            hitCooldown: hitCooldown,
            showDebugLogs: showDebugLogs
        );

        // 플레이어와 망치 충돌 무시
        Collider2D hammerCol = hammerObj.GetComponent<Collider2D>();
        Collider2D playerCol = ownerTransform.GetComponent<Collider2D>();
        if (hammerCol != null && playerCol != null)
        {
            Physics2D.IgnoreCollision(hammerCol, playerCol, true);
        }

        // Collider 활성화
        if (hammerCol != null)
        {
            hammerCol.enabled = true;
            hammerCol.isTrigger = true;
        }

        // 회전 시작
        float elapsedTime = 0f;
        float currentMaxDuration = baseDuration;
        float currentAngle = 0f;

        while (elapsedTime < currentMaxDuration)
        {
            // 적 처치 시 시간 연장 체크
            int newKills = windmillBehavior.GetAndConsumeKills();
            if (newKills > 0)
            {
                float extensionTime = newKills * extensionPerKill;
                float prevDuration = currentMaxDuration;
                currentMaxDuration = Mathf.Min(currentMaxDuration + extensionTime, maxDuration);

                float actualExtension = currentMaxDuration - prevDuration;
                if (actualExtension > 0f)
                {
                    if (showDebugLogs)
                        Debug.Log($"[{GetAttackName()}] 적 {newKills}명 처치! 시간 연장 +{actualExtension:F1}초 (현재: {currentMaxDuration:F1}초)");
                }
            }

            float normalizedTime = Mathf.Clamp01(elapsedTime / Mathf.Max(0.01f, currentMaxDuration));
            float speedMultiplier = rotationSpeedCurve.Evaluate(normalizedTime);

            // 각도 증가
            float angleIncrement = 360f * rotationsPerSecond * speedMultiplier * Time.deltaTime;
            currentAngle += angleIncrement;

            // Pivot 회전 (Z축) - 망치가 원형 궤도를 그림
            pivotObj.transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Pivot과 망치 제거
        Destroy(pivotObj);

        int totalKills = windmillBehavior.GetTotalKills();
        if (showDebugLogs)
            Debug.Log($"[{GetAttackName()}] 윈드밀 처형 종료! (총 {elapsedTime:F1}초, 처치: {totalKills}명)");
    }

    /// <summary>
    /// 그로기 상태의 적 탐지
    /// </summary>
    private bool CheckForGroggyEnemies(Vector3 center)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, detectionRange, enemyLayer);

        Debug.Log($"[{GetAttackName()}] 탐지 시작: 범위={detectionRange}, 발견된 콜라이더={colliders.Length}");

        foreach (var col in colliders)
        {
            if (col == null) continue;

            var enemyCtrl = col.GetComponent<EnemyController>();
            if (enemyCtrl == null)
            {
                enemyCtrl = col.GetComponentInParent<EnemyController>();
            }

            if (enemyCtrl != null)
            {
                bool isGroggy = enemyCtrl.IsGroggy();
                Debug.Log($"[{GetAttackName()}] 적 발견: {enemyCtrl.name}, 그로기={isGroggy}");

                if (isGroggy)
                {
                    Debug.Log($"[{GetAttackName()}] ✅ 그로기 적 발견! 처형 발동: {enemyCtrl.name}");
                    return true;
                }
            }
        }

        Debug.LogWarning($"[{GetAttackName()}] ❌ 범위 내 그로기 적 없음 (탐지 범위: {detectionRange})");
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        // 그로기 탐지 범위 시각화
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 망치 회전 반경 시각화
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rotationRadius);
    }
}

/// <summary>
/// Windmill Hammer의 충돌 처리를 담당하는 컴포넌트
/// - 적과 충돌 시 데미지 및 처형 판정
/// - 일반 적은 쿨다운마다 반복 데미지
/// </summary>
public class WindmillHammerBehavior : MonoBehaviour
{
    private PlayerCombat owner;
    private float damage;
    private float knockbackForce;
    private float executeHealAmount;
    private float executeKnockbackMultiplier;
    private float hitCooldown;
    private bool showDebugLogs;

    // 처치 카운트 (시간 연장용)
    private int killCountPending = 0;
    private int totalKills = 0;

    // 처형된 적 추적 (그로기 적 1회 처형 방지)
    private HashSet<int> executedEnemies = new HashSet<int>();

    // 일반 적 히트 쿨다운 관리
    private Dictionary<int, float> lastHitTimes = new Dictionary<int, float>();

    public void Initialize(
        PlayerCombat owner,
        float damage,
        float knockback,
        float executeHealAmount,
        float executeKnockbackMultiplier,
        float hitCooldown,
        bool showDebugLogs)
    {
        this.owner = owner;
        this.damage = damage;
        this.knockbackForce = knockback;
        this.executeHealAmount = executeHealAmount;
        this.executeKnockbackMultiplier = executeKnockbackMultiplier;
        this.hitCooldown = hitCooldown;
        this.showDebugLogs = showDebugLogs;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision == null) return;

        // Enemy 태그 확인
        if (!collision.CompareTag("Enemy"))
        {
            return;
        }

        var enemyCtrl = collision.GetComponent<EnemyController>();
        if (enemyCtrl == null)
        {
            enemyCtrl = collision.GetComponentInParent<EnemyController>();
        }

        if (enemyCtrl == null)
        {
            if (showDebugLogs)
                Debug.Log($"[WindmillHammer] EnemyController 없음: {collision.name}");
            return;
        }

        // 이미 죽은 적은 무시
        if (enemyCtrl.IsDeadState())
        {
            return;
        }

        int enemyId = enemyCtrl.GetInstanceID();
        var enemyHealth = enemyCtrl.GetComponent<HealthSystem>();

        // 그로기 상태인 경우 → 처형 (1회만)
        if (enemyCtrl.IsGroggy())
        {
            if (executedEnemies.Contains(enemyId))
            {
                // 이미 처형된 적은 무시
                return;
            }

            executedEnemies.Add(enemyId);

            if (showDebugLogs)
                Debug.Log($"[WindmillHammer] 처형 시작: {enemyCtrl.name}");

            // 스택 소모 (즉시 회수 모드 - startReturn=false)
            enemyCtrl.ConsumeStacks(startReturn: false, awardImmediatelyToPlayer: false, awardTarget: null);

            // 처형 플래그 설정 (일반 보상 중복 방지)
            enemyCtrl.MarkExecuted();

            // 플레이어 보상
            if (owner != null)
            {
                owner.OnExecutionSuccess(executeHealAmount, ammoReward: 0);
            }

            // 넉백 (처형 시 강화)
            Rigidbody2D targetRb = collision.GetComponent<Rigidbody2D>();
            if (targetRb == null)
            {
                targetRb = collision.GetComponentInParent<Rigidbody2D>();
            }

            if (targetRb != null)
            {
                Vector2 hitDir = ((Vector2)collision.transform.position - (Vector2)transform.position).normalized;
                targetRb.AddForce(hitDir * knockbackForce * executeKnockbackMultiplier, ForceMode2D.Impulse);
            }

            // 이펙트 (처형 전용)
            if (enemyHealth != null)
            {
                var he = enemyHealth.GetComponent<HitEffect>();
                if (he != null) he.PlayExecuteEffect();

                var hpe = enemyHealth.GetComponent<HitParticleEffect>();
                if (hpe != null) hpe.PlayExecuteParticle(collision.transform.position);
            }

            HitEffectManager.PlayHitEffect(
                EHitSource.Hammer,
                EHitStopStrength.Strong,
                EShakeStrength.Strong,
                collision.transform.position
            );

            // 적 처치 (페이드 아웃 1초)
            if (enemyHealth != null)
            {
                enemyHealth.ForceDieWithFade(1f);
            }

            // 킬 카운트 증가 (시간 연장용)
            killCountPending++;
            totalKills++;

            if (showDebugLogs)
                Debug.Log($"[WindmillHammer] 처형 완료: {enemyCtrl.name} (총 처치: {totalKills})");
        }
        // 일반 상태인 경우 → 반복 데미지 (쿨다운 적용)
        else
        {
            // 히트 쿨다운 체크
            float currentTime = Time.time;
            if (lastHitTimes.ContainsKey(enemyId))
            {
                if (currentTime - lastHitTimes[enemyId] < hitCooldown)
                {
                    return; // 쿨다운 중
                }
            }

            lastHitTimes[enemyId] = currentTime;

            if (showDebugLogs)
                Debug.Log($"[WindmillHammer] 일반 공격: {enemyCtrl.name}");

            // 데미지
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }

            // 스턴
            enemyCtrl.ApplyStun(0.12f);

            // 넉백
            Rigidbody2D rb = collision.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = collision.GetComponentInParent<Rigidbody2D>();
            }

            if (rb != null)
            {
                Vector2 hitDir = ((Vector2)collision.transform.position - (Vector2)transform.position).normalized;
                rb.AddForce(hitDir * knockbackForce, ForceMode2D.Impulse);
            }

            HitEffectManager.PlayHitEffect(
                EHitSource.Hammer,
                EHitStopStrength.Weak,
                EShakeStrength.Weak,
                collision.transform.position
            );

            // 스택 관리
            if (owner != null)
            {
                enemyCtrl.RegisterHit(1, owner.transform);
                enemyCtrl.ConsumeStacks(startReturn: true, awardImmediatelyToPlayer: true, awardTarget: owner);
            }
        }
    }

    /// <summary>
    /// 현재 누적된 킬 카운트를 반환하고 초기화 (시간 연장 처리용)
    /// </summary>
    public int GetAndConsumeKills()
    {
        int count = killCountPending;
        killCountPending = 0;
        return count;
    }

    /// <summary>
    /// 총 처치 수 반환 (디버그용)
    /// </summary>
    public int GetTotalKills()
    {
        return totalKills;
    }
}