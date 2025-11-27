using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyCombat))]
public class EnemyController : MonoBehaviour, IStunnable
{
    public enum EnemyState { Normal, Groggy, Dead }

    [Header("AI Settings")]
    [SerializeField] private float followRange = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float moveSpeed = 3f;

    [Header("Groggy Settings")]
    [SerializeField] private GroggySettings groggySettings = new GroggySettings();

    [Header("Hit -> Ammo Reward")]
    [Tooltip("몇 번의 히트마다 1 ammo를 보상할지")]
    [SerializeField] private int hitsPerAmmo = 3;
    [Tooltip("한 적에게서 최대 회복 가능한 ammo")]
    [SerializeField] private int maxAmmoPerKill = 3;

    // Facing 설정 (회전)
    [Header("Facing")]
    [Tooltip("플레이어가 followRange 내에 있을 때 자동으로 플레이어 방향(Z축 회전)으로 회전합니다.")]
    [SerializeField] private bool enableAutoRotateTowardsPlayer = true;

    [SerializeField] private GameObject stunEffectTMP;

    private bool isStunned = false;
    private float stunTimer = 0f;

    private Rigidbody2D rb2D;
    private EnemyCombat enemyCombat;
    private Transform playerTarget;
    private HealthSystem healthSystem;
    private SpriteRenderer spriteRenderer;

    [SerializeField] private EnemyState currentState = EnemyState.Normal;
    private Color originalColor;
    private bool hasOriginalColor = false;

    // Stuck projectiles (말뚝 스택)
    private readonly List<AttackProjectile> stuckProjectiles = new List<AttackProjectile>();

    // 히트 카운트 관련
    private int hitCount = 0;
    private Transform lastHitter = null;
    private bool wasExecuted = false; // 해머 처형으로 인한 사망 여부 플래그

    // 캐시된 플레이어 컴포넌트 (중복 Find 방지)
    private PlayerCombat cachedPlayerCombat = null;

    // Speed multipliers (for status effects like binding)
    private readonly List<float> speedMultipliers = new List<float>();

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;

    // Movement stop / temporary speed support (used by Suicide sequence)
    private bool movementStopped = false;
    private float savedMoveSpeed = 0f;
    private Coroutine movementStopCoroutine = null;

    private void Start()
    {
        rb2D = GetComponent<Rigidbody2D>();
        enemyCombat = GetComponent<EnemyCombat>();
        healthSystem = GetComponent<HealthSystem>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            hasOriginalColor = true;
        }

        if (healthSystem != null)
        {
            // 변경: 그로기 진입은 HP 비율 검사로 제어 (OnZeroHealth 구독 제거)
            // 사망 시 히트카운트 보상은 페이드 완료 시점에서 호출되므로 OnDeath 구독하지 않음
        }
        else
        {
            if (showDebugLogs) Debug.LogWarning($"EnemyController [{gameObject.name}]: HealthSystem이 없습니다. 그로기/사망 처리가 불가할 수 있습니다.");
        }

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
            cachedPlayerCombat = playerObj.GetComponent<PlayerCombat>();
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 플레이어 발견 ({playerTarget.name})");
        }
        else
        {
            Debug.LogError($"EnemyController [{gameObject.name}]: 'Player' 태그를 가진 오브젝트를 찾을 수 없습니다!");
        }

        if (enemyCombat == null) Debug.LogError($"EnemyController [{gameObject.name}]: EnemyCombat 컴포넌트가 없습니다!");

        if (showDebugLogs)
        {
            Debug.Log($"EnemyController [{gameObject.name}]: 초기화 완료 (추적거리: {followRange}, 공격거리: {attackRange}, 속도: {moveSpeed})");
        }
    }

    private void Update()
    {
        // PullEffect가 활성화되어 있으면 AI 중지 (끌려가는 중)
        if (GetComponent<PullEffect>() != null)
            return;

        // 그로기 상태 진입 조건 체크
        if (groggySettings.enableGroggy && healthSystem != null && currentState != EnemyState.Groggy && currentState != EnemyState.Dead)
        {
            float ratio = healthSystem.GetCurrentHealth() / Mathf.Max(1f, healthSystem.GetMaxHealth());
            if (ratio <= groggySettings.groggyHpPercent)
            {
                EnterGroggy();
            }
        }
        // 그로기 비활성화 시 즉시 사망 처리
        else if (!groggySettings.enableGroggy && healthSystem != null && currentState != EnemyState.Dead)
        {
            float currentHp = healthSystem.GetCurrentHealth();
            if (currentHp <= 0 && !healthSystem.IsDead())
            {
                healthSystem.ForceDie();
            }
        }

        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0)
            {
                isStunned = false;
                stunEffectTMP.SetActive(false);
                if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 경직 해제!");
            }
            return;
        }
       

        if (playerTarget == null) return;

        float distance = Vector2.Distance(transform.position, playerTarget.position);

        // 회전: followRange 기준으로 플레이어가 범위 내이면 항상 바라보도록 처리
        if (enableAutoRotateTowardsPlayer && currentState != EnemyState.Dead && distance <= followRange)
        {
            FacePlayerRotation();
        }

        if (distance <= attackRange)
        {
            HandleAttackState();
        }
        else if (distance <= followRange)
        {
            HandleFollowState();
        }
        else
        {
            HandleIdleState();
        }
    }

    private void HandleAttackState()
    {
        rb2D.linearVelocity = Vector2.zero;
        if (enemyCombat != null) enemyCombat.TryAttack(playerTarget);
    }

    private void HandleFollowState()
    {
        float currentSpeed = GetCurrentMoveSpeed();
        Vector2 direction = (playerTarget.position - transform.position).normalized;
        rb2D.linearVelocity = direction * currentSpeed;

        if (showDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"EnemyController [{gameObject.name}]: 플레이어 추적 중 (거리: {Vector2.Distance(transform.position, playerTarget.position):F1}, 속도: {currentSpeed:F2})");
        }
    }

    private void HandleIdleState()
    {
        rb2D.linearVelocity = Vector2.zero;

        if (showDebugLogs && Time.frameCount % 120 == 0)
        {
            Debug.Log($"EnemyController [{gameObject.name}]: 대기 중 (플레이어 거리: {Vector2.Distance(transform.position, playerTarget.position):F1})");
        }
    }

    // 플레이어 방향으로 Z축 회전 (적의 "앞쪽"은 +X로 가정)
    private void FacePlayerRotation()
    {
        if (playerTarget == null) return;

        Vector2 dir = (playerTarget.position - transform.position);
        if (dir.sqrMagnitude <= Mathf.Epsilon) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (showDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"EnemyController [{gameObject.name}]: 회전 적용 angle={angle:F1}");
        }
    }

    /// <summary>
    /// 속박 상태 메서드 
    /// </summary>
    public void ApplyImpale(float duration)
    {
        stunEffectTMP.SetActive(true);

        // 이동 정지 (기존 메서드 활용, 속도 0)
        ApplyMovementStop(duration, 0f);

        // 공격 등 행동 불가 (Stun 활용)
        ApplyStun(duration);

        if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 가시에 찔림(Impaled)! {duration}초간 행동 불가");
    }

    public void ApplyStun(float duration)
    {
        isStunned = true;
        stunTimer = duration;
        rb2D.linearVelocity = Vector2.zero;

        if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 경직! (지속시간: {duration}초)");
    }

    /// <summary>
    /// Temporarily set moveSpeed to targetMoveSpeed for the given duration, then restore previous speed.
    /// Subsequent calls reset the duration.
    /// - If targetMoveSpeed is null: legacy behavior (set speed = 0)
    /// - If targetMoveSpeed has value: set speed = that value
    /// </summary>
    public void ApplyMovementStop(float duration, float? targetMoveSpeed = null)
    {
        if (movementStopCoroutine != null)
        {
            StopCoroutine(movementStopCoroutine);
            movementStopCoroutine = null;
        }

        if (!movementStopped)
        {
            savedMoveSpeed = moveSpeed;
            movementStopped = true;
        }

        // If caller provided a specific speed, use it; otherwise fallback to 0 (legacy)
        moveSpeed = targetMoveSpeed.HasValue ? targetMoveSpeed.Value : 0f;

        movementStopCoroutine = StartCoroutine(MovementStopRoutine(duration));

        if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 이동 정지/고정 속도 적용 (targetSpeed={(targetMoveSpeed.HasValue ? targetMoveSpeed.Value : 0f):F2}, duration={duration:F2}s)");
    }

    /// <summary>
    /// 새로운 API: 폭발 퓨즈 등에서 '속도를 완전히 0으로 만들지 않고' 임시로 지정한 moveSpeed 값으로 변경하고 복원한다.
    /// </summary>
    public void ApplyTemporaryMoveSpeed(float duration, float targetMoveSpeed)
    {
        if (movementStopCoroutine != null)
        {
            StopCoroutine(movementStopCoroutine);
            movementStopCoroutine = null;
        }

        if (!movementStopped)
        {
            savedMoveSpeed = moveSpeed;
            movementStopped = true;
        }

        moveSpeed = targetMoveSpeed;
        movementStopCoroutine = StartCoroutine(MovementStopRoutine(duration));

        if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 임시 이동속도 적용 targetSpeed={targetMoveSpeed:F2}, duration={duration:F2}s");
    }

    private IEnumerator MovementStopRoutine(float duration)
    {
        rb2D.constraints = RigidbodyConstraints2D.FreezeRotation;

        yield return new WaitForSeconds(duration);
        // restore only if we previously stopped movement
        if (movementStopped)
        {
            moveSpeed = savedMoveSpeed;
            movementStopped = false;
            rb2D.constraints = RigidbodyConstraints2D.None;
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 이동 복원 (moveSpeed={moveSpeed:F2})");
        }
        movementStopCoroutine = null;
    }

    public bool IsStunned() => isStunned;

    // 그로기 진입
    public void EnterGroggy()
    {
        if (currentState == EnemyState.Groggy || currentState == EnemyState.Dead) return;

        currentState = EnemyState.Groggy;

        // 색상 변경
        if (spriteRenderer != null) spriteRenderer.color = groggySettings.groggyColor;

        var he = GetComponent<HitEffect>();
        if (he != null) he.ForceSetSavedOriginalColor(groggySettings.groggyColor);

        // GroggySettings에 처리 위임 (부활 타이머 시작 등)
        groggySettings.OnEnterGroggy(this);

        if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 그로기 상태 진입 (HP ratio <= {groggySettings.groggyHpPercent})");
    }

    public void ExitGroggy()
    {
        if (currentState != EnemyState.Groggy) return;

        // GroggySettings에 정리 요청 (타이머 중단 등)
        groggySettings.OnExitGroggy(this);

        // 색상 복원
        if (spriteRenderer != null && hasOriginalColor) spriteRenderer.color = originalColor;

        var he = GetComponent<HitEffect>();
        if (he != null) he.ForceSetSavedOriginalColor(originalColor);

        currentState = EnemyState.Normal;

        if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 그로기 해제 — 정상 상태 복귀");
    }

    private float GetCurrentMoveSpeed()
    {
        float speed = moveSpeed;

        // Apply groggy penalty
        if (currentState == EnemyState.Groggy)
            speed *= 0.1f;

        // Apply all speed multipliers (for status effects like binding)
        foreach (var multiplier in speedMultipliers)
        {
            speed *= multiplier;
        }

        return speed;
    }

    /// <summary>
    /// Adds a speed multiplier effect (0.0 ~ 1.0). Returns the multiplier value for later removal.
    /// </summary>
    public float ApplySpeedMultiplier(float multiplier)
    {
        speedMultipliers.Add(multiplier);
        if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: Speed multiplier applied ({multiplier:F2}), total multipliers: {speedMultipliers.Count}");
        return multiplier;
    }

    /// <summary>
    /// Removes a specific speed multiplier effect.
    /// </summary>
    public void RemoveSpeedMultiplier(float multiplier)
    {
        if (speedMultipliers.Remove(multiplier))
        {
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: Speed multiplier removed ({multiplier:F2}), remaining: {speedMultipliers.Count}");
        }
        else
        {
            if (showDebugLogs) Debug.LogWarning($"EnemyController [{gameObject.name}]: Speed multiplier not found ({multiplier:F2})");
        }
    }

    /// <summary>
    /// Clears all speed multipliers.
    /// </summary>
    public void ClearSpeedMultipliers()
    {
        int count = speedMultipliers.Count;
        speedMultipliers.Clear();
        if (showDebugLogs && count > 0) Debug.Log($"EnemyController [{gameObject.name}]: Cleared {count} speed multipliers");
    }

    // 외부 접근자
    public bool IsGroggy() => currentState == EnemyState.Groggy;
    public bool IsDeadState() => currentState == EnemyState.Dead;

    // Stuck projectiles API
    public void RegisterStuckProjectile(AttackProjectile proj)
    {
        if (proj == null) return;
        if (!stuckProjectiles.Contains(proj))
        {
            stuckProjectiles.Add(proj);
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 말뚝 등록 (총 {stuckProjectiles.Count}) -> {proj.gameObject.name} id={proj.GetInstanceID()} active={proj.gameObject.activeInHierarchy}");
        }
        else
        {
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: RegisterStuckProjectile - already contains {proj.gameObject.name} id={proj.GetInstanceID()}");
        }
    }

    public void RemoveStuckProjectile(AttackProjectile proj)
    {
        if (proj == null) return;
        if (stuckProjectiles.Remove(proj))
        {
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: 말뚝 제거 (남음 {stuckProjectiles.Count}) -> {proj.gameObject.name} id={proj.GetInstanceID()}");
        }
        else
        {
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: RemoveStuckProjectile - not found {proj?.gameObject.name}");
        }
    }

    public int GetStuckCount() => stuckProjectiles.Count;

    /// <summary>
    /// 부착된 말뚝을 모두 회수(또는 제거)하고 개수를 반환합니다.
    /// - AttackManager.ReleaseStake를 호출해 풀로 반환합니다.
    /// </summary>
    public int ConsumeStacks(bool startReturn = true, bool awardImmediatelyToPlayer = false, PlayerCombat awardTarget = null)
    {
        if (stuckProjectiles.Count == 0) 
        {
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: ConsumeStacks called but stuckProjectiles empty");
            return 0;
        }

        if (showDebugLogs)
        {
            Debug.Log($"EnemyController [{gameObject.name}]: ConsumeStacks START (startReturn={startReturn}, awardImmediate={awardImmediatelyToPlayer}) - stuckCount={stuckProjectiles.Count}");
            foreach (var p in stuckProjectiles)
            {
                if (p == null) Debug.Log($"  - null entry");
                else Debug.Log($"  - {p.gameObject.name} id={p.GetInstanceID()} active={p.gameObject.activeInHierarchy}");
            }
        }

        // 복사 + 즉시 클리어 — 활성 말뚝은 따로 모아서 루프 밖에서 안전하게 StartReturn 호출
        var copy = stuckProjectiles.ToArray();
        stuckProjectiles.Clear();

        int consumed = 0;
        var toReturn = new List<AttackProjectile>();

        foreach (var proj in copy)
        {
            if (proj == null) continue;
            try
            {
                // 이미 풀로 반환되어 비활성 상태라면 '반환된 것'으로 간주하여 카운트만 올립니다.
                if (!proj.gameObject.activeInHierarchy)
                {
                    consumed++;
                    if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: ConsumeStacks - {proj.gameObject.name} is inactive, counted as returned");
                    continue;
                }

                if (startReturn)
                {
                    // 즉시 StartReturn 호출 대신 안전하게 모아두고 루프 후에 호출
                    toReturn.Add(proj);
                    if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: ConsumeStacks -> queued StartReturn for {proj.gameObject.name} id={proj.GetInstanceID()}");
                }
                else
                {
                    // 즉시 풀/파괴로 반환 (해머 처형 시 사용)
                    // 안전: 풀 반환 전에 말뚝 상태 정리 (호스트 참조/물리/콜라이더 등)
                    try
                    {
                        proj.ForceDetachFromHost();
                    }
                    catch { /* 안전: ForceDetach 실패해도 Release 호출 */ }

                    if (AttackManager.Instance != null)
                        AttackManager.Instance.ReleaseStake(proj.gameObject);
                    else
                        Destroy(proj.gameObject);

                    if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: ConsumeStacks -> Released {proj.gameObject.name} to pool");
                }
                consumed++;
            }
            catch (System.Exception ex)
            {
                if (showDebugLogs) Debug.LogWarning($"EnemyController [{gameObject.name}]: ConsumeStacks exception for {proj?.gameObject.name} -> {ex.Message}");
                // 실패 시 안전하게 풀에 반환
                try
                {
                    try
                    {
                        proj?.ForceDetachFromHost();
                    }
                    catch { }

                    if (AttackManager.Instance != null)
                        AttackManager.Instance.ReleaseStake(proj.gameObject);
                    else
                        Destroy(proj.gameObject);
                }
                catch { }
            }
        }

        // startReturn 모드일 때: 루프가 끝난 뒤 코루틴으로 순차적 StartReturn 호출 (레이스 예방)
        if (startReturn && toReturn.Count > 0)
        {
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: PerformDeferredReturns scheduled for {toReturn.Count} items");
            StartCoroutine(PerformDeferredReturns(toReturn));
        }

        // 즉시 보상 옵션: 호출자(예: Hammer)에서 원하면 여기서 바로 지급
        if (awardImmediatelyToPlayer && consumed > 0)
        {
            if (awardTarget != null)
            {
                awardTarget.RecoverAmmo(consumed);
                if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: ConsumeStacks -> Immediately awarded {consumed} ammo to {awardTarget.name}");
            }
            else
            {
                // 기존: GameObject.FindGameObjectWithTag 호출 대신 캐시된 PlayerCombat 우선 사용
                if (cachedPlayerCombat != null)
                {
                    cachedPlayerCombat.RecoverAmmo(consumed);
                    if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: ConsumeStacks -> Immediately awarded {consumed} ammo to cached player {cachedPlayerCombat.name}");
                }
                else
                {
                    var playerObj = GameObject.FindGameObjectWithTag("Player");
                    if (playerObj != null)
                    {
                        var pc = playerObj.GetComponent<PlayerCombat>();
                        if (pc != null)
                        {
                            pc.RecoverAmmo(consumed);
                            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: ConsumeStacks -> Immediately awarded {consumed} ammo to player {pc.name}");
                        }
                    }
                }
            }
        }

        if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: ConsumeStacks 완료 - count: {consumed} (startReturn={startReturn})");
        return consumed;
    }

    // Deferred returns coroutine: 한 프레임씩(또는 아주 작은 지연) 나눠 StartReturn 호출하여 race 방지
    private System.Collections.IEnumerator PerformDeferredReturns(List<AttackProjectile> list)
    {
        // 안전: 짧은 지연을 두어 호출 순서/풀 재사용 레이스 완화
        foreach (var proj in list)
        {
            if (proj == null) continue;
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: PerformDeferredReturns processing {proj.gameObject.name} id={proj.GetInstanceID()} active={proj.gameObject.activeInHierarchy} time={Time.time:F3}");

            try
            {
                // 한 프레임 기다렸다가 StartReturn 호출
                // -> yield는 try 블록 내부에 있으면 안 되므로, yield를 try 바깥으로 이동합니다.
            }
            catch (System.Exception ex)
            {
                if (showDebugLogs) Debug.LogWarning($"EnemyController [{gameObject.name}]: PerformDeferredReturns exception for {proj?.gameObject.name} -> {ex.Message}");
                try
                {
                    if (proj != null)
                    {
                        if (AttackManager.Instance != null) AttackManager.Instance.ReleaseStake(proj.gameObject);
                        else Destroy(proj.gameObject);
                    }
                }
                catch { }
                continue;
            }

            // yield는 반드시 try 바깥에서 수행
            yield return null;

            // 실제 호출은 별도 try로 감싼다(위의 예외 분리 목적)
            try
            {
                proj.StartReturn();
                if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: PerformDeferredReturns -> StartReturn for {proj.gameObject.name} id={proj.GetInstanceID()} at time={Time.time:F3}");
            }
            catch (System.Exception ex)
            {
                if (showDebugLogs) Debug.LogWarning($"EnemyController [{gameObject.name}]: PerformDeferredReturns exception during StartReturn for {proj?.gameObject.name} -> {ex.Message}");
                try
                {
                    if (proj != null)
                    {
                        if (AttackManager.Instance != null) AttackManager.Instance.ReleaseStake(proj.gameObject);
                        else Destroy(proj.gameObject);
                    }
                }
                catch { }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, followRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (isStunned)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

        if (currentState == EnemyState.Groggy)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.6f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        if (playerTarget != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, playerTarget.position);
        }
    }

    // 히트카운트 누적
    public void RegisterHit(int amount = 1, Transform hitter = null)
    {
        if (amount <= 0) return;
        hitCount += amount;
        if (hitter != null) lastHitter = hitter;

        if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: RegisterHit +{amount} (total {hitCount}) from {hitter?.name}");
    }

    // 해머 처형 플래그
    public void MarkExecuted()
    {
        wasExecuted = true;
    }

    /// <summary>
    /// 페이드 완료 시점에서 호출되어 히트카운트 기반 보상을 지급합니다.
    /// (public으로 변경되어 HealthSystem에서 호출)
    /// </summary>
    public void HandleDeath()
    {
        if (wasExecuted)
        {
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: wasExecuted=true -> skipping hit-based ammo reward");
            return;
        }

        if (hitCount <= 0 || hitsPerAmmo <= 0)
        {
            hitCount = 0;
            lastHitter = null;
            return;
        }

        int ammoReward = Mathf.Clamp(hitCount / Mathf.Max(1, hitsPerAmmo), 0, maxAmmoPerKill);
        if (ammoReward <= 0)
        {
            hitCount = 0;
            lastHitter = null;
            return;
        }

        PlayerCombat pc = null;
        if (lastHitter != null) pc = lastHitter.GetComponent<PlayerCombat>();

        if (pc == null)
        {
            // 기존: GameObject.FindGameObjectWithTag 호출 대신 캐시된 PlayerCombat 우선 사용
            if (cachedPlayerCombat != null) pc = cachedPlayerCombat;
            else
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) pc = playerObj.GetComponent<PlayerCombat>();
            }
        }

        if (pc != null)
        {
            pc.RecoverAmmo(ammoReward);
            if (showDebugLogs) Debug.Log($"EnemyController [{gameObject.name}]: Granting {ammoReward} ammo to {pc.name} (hitCount {hitCount})");
        }
        else
        {
            if (showDebugLogs) Debug.LogWarning($"EnemyController [{gameObject.name}]: No PlayerCombat found to grant ammo ({ammoReward})");
        }

        hitCount = 0;
        lastHitter = null;
    }
}