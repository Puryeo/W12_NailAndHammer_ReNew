using System.Collections;
using UnityEngine;

/// <summary>
/// AttackProjectile (리팩토링 버전)
/// - 상태 머신 기반 (ProjectileStateController)
/// - Strategy Pattern 기반 Collision/Retrieval Behavior
/// - Impaling 시스템 통합
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class AttackProjectile : MonoBehaviour
{
    // ========== 설정 (Inspector) ==========
    [Header("Advanced Config (Optional)")]
    [Tooltip("고급 기능(꿰뚫기, 특수 회수 등)을 사용하려면 ProjectileConfig SO를 할당하세요. 비워두면 기본 동작으로 작동합니다.")]
    [SerializeField] private ProjectileConfig configOverride = null;

    [Header("General Settings")]
    [SerializeField] private float stuckLifetime = 0f;
    [SerializeField] private float returnSpeed = 20f;
    [SerializeField] private float returnDamageRatio = 0.5f;
    [SerializeField] private float pickupDistance = 0.65f;
    [SerializeField] private float autoRetrieveRange = 3f;

    [Header("Hit Feedback")]
    [SerializeField] private EHitStopStrength hitStopOnEnemyHit = EHitStopStrength.Weak;
    [SerializeField] private EShakeStrength shakeOnEnemyHit = EShakeStrength.Weak;
    [SerializeField] private EHitStopStrength hitStopOnReturnHit = EHitStopStrength.Weak;
    [SerializeField] private EShakeStrength shakeOnReturnHit = EShakeStrength.Weak;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true; // 디버깅을 위해 true로 변경

    // ========== 새 구조 (리팩토링) ==========
    private ProjectileConfig config;
    private ProjectileStateController stateController;
    private IProjectileCollisionBehavior collisionBehavior;
    private IProjectileRetrievalBehavior retrievalBehavior;
    private ImpaledEnemyManager impaledManager;

    // ========== 기존 데이터 (호환성) ==========
    private AttackPatternData pattern; // 기존 코드 호환용
    private float damage;
    private string targetTag;
    private string wallTag = "Wall";
    private Transform attacker;
    private bool isBloodStake = false;
    private float bloodHpCost = 0f;

    // ========== 컴포넌트 ==========
    private Rigidbody2D rb2D;
    private Collider2D col2D;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private CameraShake cameraShake;

    // ========== 상태 (기존 호환) ==========
    private bool isInitialized = false;
    private bool isStuck = false; // 기존 코드 호환
    private bool isReturning = false; // 기존 코드 호환
    private float lifeTimer;
    private bool suppressAmmoOnReturn = false;
    private bool hasBeenCollected = false;

    // ========== Impaling 전용 ==========
    private float impalingTraveledDistance = 0f;

    // ========== 기존 Stick 관련 ==========
    private EnemyController currentHostEnemy;
    private EnemyController lastHostEnemy; // UnregisterFromHost 직전의 적 정보 저장
    private Transform stuckHostTransform;
    private Vector3 stuckLocalPosition;
    private Quaternion stuckLocalRotation;
    private Transform playerTransform;
    private Vector3 originalLocalScale;

    // ========== 프로퍼티 ==========
    public ProjectileConfig Config => config;
    public Transform Attacker => attacker;
    public ImpaledEnemyManager ImpaledManager => impaledManager;
    public ProjectileState CurrentState => stateController != null ? stateController.CurrentState : ProjectileState.Inactive;
    public EnemyController LastHostEnemy => lastHostEnemy;

    // ========== Unity Lifecycle ==========
    private void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        col2D = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (Camera.main != null) cameraShake = Camera.main.GetComponent<CameraShake>();

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;

        originalLocalScale = transform.localScale;

        // 상태 컨트롤러 초기화
        stateController = new ProjectileStateController(this, showDebugLogs);
    }

    /// <summary>
    /// 런타임에 config를 설정합니다. Initialize 전에 호출해야 합니다.
    /// </summary>
    public void SetRuntimeConfig(ProjectileConfig config)
    {
        this.configOverride = config;
        if (showDebugLogs) Debug.Log($"AttackProjectile: 런타임 config 설정 ({config?.name})");
    }

    /// <summary>
    /// 투사체 초기화 (AttackManager에서 호출)
    /// 기존 시그니처 유지 + 새로운 config 기반 초기화
    /// </summary>
    public void Initialize(
        AttackPatternData pattern,
        float damage,
        string targetTag,
        Transform attacker,
        Color? overrideColor = null,
        Vector2? overrideDirection = null,
        bool isBloodStake = false,
        float bloodHpCost = 0f)
    {
        // 기존 데이터 저장
        this.pattern = pattern;
        this.damage = damage;
        this.targetTag = targetTag;
        this.attacker = attacker;
        this.isBloodStake = isBloodStake;
        this.bloodHpCost = bloodHpCost;

        // ProjectileConfig 설정 (우선순위: configOverride > 런타임 생성)
        if (configOverride != null)
        {
            this.config = configOverride;
            if (showDebugLogs) Debug.Log($"AttackProjectile: configOverride 사용 ({configOverride.name})");
        }
        else
        {
            this.config = CreateConfigFromPattern(pattern, damage);
            if (showDebugLogs) Debug.Log("AttackProjectile: 런타임 config 생성 (기본 동작)");
        }

        // Behavior 설정
        SetupBehaviors();

        // ImpaledEnemyManager 초기화 (Impaling이 가능한 경우)
        if (config.canImpale)
        {
            impaledManager = new ImpaledEnemyManager(this, config, showDebugLogs);
        }

        // Collider 초기화
        if (col2D != null) col2D.isTrigger = true;

        if (pattern != null && spriteRenderer != null && pattern.attackSprite != null)
            spriteRenderer.sprite = pattern.attackSprite;

        if (overrideColor.HasValue && spriteRenderer != null)
            spriteRenderer.color = overrideColor.Value;

        // 물리 안전 초기화
        rb2D.gravityScale = 0f;
        rb2D.linearDamping = 0f;
        rb2D.angularDamping = 0f;

        currentHostEnemy = null;
        stuckHostTransform = null;
        isStuck = false;
        isReturning = false;
        suppressAmmoOnReturn = false;
        hasBeenCollected = false;

        // 복원할 스케일 보장
        transform.localScale = originalLocalScale;

        if (pattern != null)
        {
            if (pattern.attackType == EAttackType.Projectile)
            {
                rb2D.bodyType = RigidbodyType2D.Dynamic;

                Vector2 direction = overrideDirection ?? (attacker != null ? (Vector2)attacker.right : Vector2.right);
                if (direction == Vector2.zero) direction = Vector2.right;
                direction.Normalize();

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);

                rb2D.linearVelocity = direction * pattern.projectileSpeed;
                lifeTimer = pattern.projectileLifetime;
            }
            else if (pattern.attackType == EAttackType.MeleeSlash)
            {
                rb2D.bodyType = RigidbodyType2D.Kinematic;
                transform.SetParent(attacker);
                transform.localPosition = pattern.hitboxOffset;
                lifeTimer = pattern.attackDuration;
            }
        }

        isInitialized = true;
        impalingTraveledDistance = 0f;

        // 상태 전환
        stateController.ChangeState(ProjectileState.Flying);
    }

    /// <summary>
    /// AttackPatternData를 ProjectileConfig로 변환
    /// </summary>
    private ProjectileConfig CreateConfigFromPattern(AttackPatternData pattern, float damage)
    {
        var cfg = ScriptableObject.CreateInstance<ProjectileConfig>();

        cfg.damage = damage;
        cfg.speed = pattern != null ? pattern.projectileSpeed : 14f;
        cfg.lifetime = pattern != null ? pattern.projectileLifetime : 5f;
        cfg.isRetrievable = pattern != null ? pattern.isRetrievable : true;

        // 기본값 (인스펙터에서 수정 가능하도록 나중에 변경 가능)
        cfg.collisionType = CollisionBehaviorType.StickToEnemy;
        cfg.retrievalType = RetrievalBehaviorType.Simple;

        cfg.returnSpeed = this.returnSpeed;
        cfg.returnDamageRatio = this.returnDamageRatio;

        // Impaling 기본 비활성화 (나중에 활성화 가능)
        cfg.canImpale = false;

        return cfg;
    }

    /// <summary>
    /// Behavior 설정
    /// </summary>
    private void SetupBehaviors()
    {
        collisionBehavior = CreateCollisionBehavior(config.collisionType);
        retrievalBehavior = CreateRetrievalBehavior(config.retrievalType);
    }

    private IProjectileCollisionBehavior CreateCollisionBehavior(CollisionBehaviorType type)
    {
        switch (type)
        {
            case CollisionBehaviorType.StickToEnemy:
                return new StickToEnemyBehavior(showDebugLogs);
            case CollisionBehaviorType.ImpaleAndCarry:
                return new ImpaleEnemyBehavior(showDebugLogs);
            default:
                return new StickToEnemyBehavior(showDebugLogs);
        }
    }

    private IProjectileRetrievalBehavior CreateRetrievalBehavior(RetrievalBehaviorType type)
    {
        switch (type)
        {
            case RetrievalBehaviorType.Simple:
                return new SimpleRetrievalBehavior(config, showDebugLogs);
            case RetrievalBehaviorType.Binding:
                return new BindingRetrievalBehavior(config, showDebugLogs);
            case RetrievalBehaviorType.Pull:
                return new PullRetrievalBehavior(config, showDebugLogs);
            case RetrievalBehaviorType.StuckEnemyPull:
                return new StuckEnemyPullRetrievalBehavior(config, showDebugLogs);
            default:
                return new SimpleRetrievalBehavior(config, showDebugLogs);
        }
    }

    private void Update()
    {
        if (!isInitialized) return;

        // 상태 업데이트
        if (stateController != null)
        {
            stateController.UpdateState();
        }

        // 기존 박힌 상태 로직 (하위 호환)
        if (isStuck && !isReturning && stuckHostTransform != null)
        {
            if (stuckHostTransform == null)
            {
                ForceDetachFromHost();
                return;
            }

            transform.position = stuckHostTransform.TransformPoint(stuckLocalPosition);
            transform.rotation = stuckHostTransform.rotation * stuckLocalRotation;

            // 자동 회수
            if (stuckHostTransform.CompareTag(wallTag))
            {
                if (playerTransform == null)
                {
                    var p = GameObject.FindGameObjectWithTag("Player");
                    if (p != null) playerTransform = p.transform;
                }

                if (playerTransform != null)
                {
                    float dist = Vector2.Distance(playerTransform.position, transform.position);
                    if (dist <= autoRetrieveRange)
                    {
                        if (showDebugLogs)
                            Debug.Log($"AttackProjectile [{gameObject.name}]: 자동 회수 범위 진입");
                        StartReturn();
                        return;
                    }
                }
            }

            if (stuckLifetime > 0f)
            {
                lifeTimer -= Time.deltaTime;
                if (lifeTimer <= 0) DissolveAndDestroy();
            }

            return;
        }

        // 회수 중 거리 기반 흡수
        if (isReturning)
        {
            if (attacker == null)
            {
                ReturnOrDestroy();
                return;
            }

            float sqr = (attacker.position - transform.position).sqrMagnitude;
            if (sqr <= pickupDistance * pickupDistance)
            {
                if (!hasBeenCollected)
                {
                    if (showDebugLogs)
                        Debug.Log($"AttackProjectile [{gameObject.name}]: Auto-pickup");
                    CompleteRetrieval();
                    return;
                }
            }

            Vector2 dir = (attacker.position - transform.position).normalized;
            transform.Translate(dir * returnSpeed * Time.deltaTime, Space.World);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            return;
        }

        // 생명 시간 감소
        if (!isStuck)
        {
            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0) ReturnOrDestroy();
        }
    }

    // ========== 상태별 콜백 (StateController에서 호출) ==========
    public void OnStateLaunching()
    {
        if (showDebugLogs)
            Debug.Log($"AttackProjectile: OnStateLaunching");
    }

    public void OnStateFlying()
    {
        isStuck = false;
        isReturning = false;
        if (showDebugLogs)
            Debug.Log($"AttackProjectile: OnStateFlying");
    }

    public void OnStateImpaling(ProjectileState prevState)
    {
        if (showDebugLogs)
            Debug.Log($"AttackProjectile: OnStateImpaling (from {prevState})");
    }

    public void OnStateStuck()
    {
        isStuck = true;
        isReturning = false;

        // Static body일 때는 velocity를 설정할 수 없으므로, 먼저 bodyType 확인
        if (rb2D.bodyType != RigidbodyType2D.Static)
        {
            rb2D.linearVelocity = Vector2.zero;
            rb2D.angularVelocity = 0f;
        }

        rb2D.bodyType = RigidbodyType2D.Static;

        if (showDebugLogs)
            Debug.Log($"AttackProjectile: OnStateStuck");
    }

    public void OnStateReturning()
    {
        isReturning = true;
        isStuck = false;

        // Impaled 적 해제
        if (impaledManager != null)
        {
            impaledManager.ReleaseAllEnemies();
        }

        // Behavior 기반 회수 시작
        retrievalBehavior?.StartRetrieval(this, attacker);

        if (showDebugLogs)
            Debug.Log($"AttackProjectile: OnStateReturning");
    }

    public void OnStateCollected()
    {
        CompleteRetrieval();
    }

    // ========== 상태별 업데이트 ==========
    public void UpdateFlying()
    {
        // 비행 중 로직
    }

    public void UpdateImpaling()
    {
        // Impaling 중 거리 체크
        float distance = rb2D.linearVelocity.magnitude * Time.deltaTime;
        impalingTraveledDistance += distance;

        if (impalingTraveledDistance >= config.maxImpalingDistance)
        {
            StopImpalingInAir();
        }
    }

    public void UpdateReturning()
    {
        // 회수 중 로직 (Behavior에서 처리)
    }

    // ========== 충돌 처리 ==========
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInitialized) return;

        // 이미 박힌 말뚝(stuck)인 경우 충돌 무시
        if (isStuck && !isReturning)
        {
            return;
        }

        // Friendly-fire 방지
        var otherEnemy = other.GetComponentInParent<EnemyController>();
        var attackerEnemy = attacker != null ? attacker.GetComponentInParent<EnemyController>() : null;
        if (attackerEnemy != null && otherEnemy != null)
        {
            return;
        }

        // 회수 중 플레이어 도착
        if (isReturning)
        {
            if (attacker != null && (other.transform == attacker || other.transform.IsChildOf(attacker)))
            {
                CompleteRetrieval();
                return;
            }

            // 회수 경로상 적 히트
            if (other.CompareTag(targetTag))
            {
                if (!(attacker != null && (other.transform == attacker || other.transform.IsChildOf(attacker))))
                {
                    ApplyDamageAndEffects(other, returnDamageRatio);
                    ApplyHitFeedback(hitStopOnReturnHit, shakeOnReturnHit);
                }
                return;
            }

            return;
        }

        // 비행 중 or Impaling 중 충돌
        if (CurrentState == ProjectileState.Flying || CurrentState == ProjectileState.Impaling)
        {
            // 벽 충돌
            if (other.CompareTag(wallTag))
            {
                collisionBehavior?.OnHitWall(this, other);
                return;
            }

            // 적 충돌
            if (other.CompareTag(targetTag))
            {
                // 발사자 무시
                if (attacker != null && (other.transform == attacker || other.transform.IsChildOf(attacker)))
                    return;

                // Impaling 중 이미 꿰뚫린 적 무시
                if (CurrentState == ProjectileState.Impaling && impaledManager != null)
                {
                    if (impaledManager.ImpaledEnemies.Contains(other.transform))
                        return;
                }

                // 충돌 지점 계산 (투사체에 가장 가까운 적의 지점)
                Vector2 collisionPoint = other.ClosestPoint(transform.position);
                collisionBehavior?.OnHitEnemy(this, other, collisionPoint);
                return;
            }
        }
    }

    // ========== Public API ==========
    public void ChangeState(ProjectileState newState)
    {
        if (stateController != null)
        {
            stateController.ChangeState(newState);

            // 기존 플래그 동기화
            isStuck = (newState == ProjectileState.Stuck);
            isReturning = (newState == ProjectileState.Returning);
        }
    }

    /// <summary>
    /// 벽에 박힘 (기존 메서드 유지)
    /// </summary>
    public void StickToWall(Collider2D wallCollider)
    {
        if (isStuck) return;
        isStuck = true;

        if (showDebugLogs)
            Debug.Log($"AttackProjectile: StickToWall");

        rb2D.linearVelocity = Vector2.zero;
        rb2D.angularVelocity = 0f;
        rb2D.bodyType = RigidbodyType2D.Static;

        if (spriteRenderer != null) spriteRenderer.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        AttachToColliderWithScale(wallCollider);

        lifeTimer = stuckLifetime;

        if (cameraShake != null) cameraShake.ShakeWeak();
    }

    /// <summary>
    /// 적에 박힘 (기존 메서드 유지)
    /// </summary>
    public void StickToEnemy(Collider2D enemyCollider)
    {
        if (isStuck) return;
        isStuck = true;

        if (showDebugLogs)
            Debug.Log($"AttackProjectile: StickToEnemy");

        rb2D.linearVelocity = Vector2.zero;
        rb2D.angularVelocity = 0f;
        rb2D.bodyType = RigidbodyType2D.Static;

        if (spriteRenderer != null) spriteRenderer.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        AttachToColliderWithScale(enemyCollider);

        EnemyController enemyCtrl = enemyCollider.GetComponent<EnemyController>() ?? enemyCollider.GetComponentInParent<EnemyController>();
        if (enemyCtrl != null)
        {
            currentHostEnemy = enemyCtrl;
            enemyCtrl.RegisterStuckProjectile(this);
        }

        lifeTimer = stuckLifetime;

        if (cameraShake != null) cameraShake.ShakeWeak();
    }

    private void AttachToColliderWithScale(Collider2D hitCollider)
    {
        if (hitCollider == null) return;

        Vector2 contactWorld = hitCollider.ClosestPoint(transform.position);
        Vector2 offsetDir = ((Vector2)transform.position - contactWorld);
        if (offsetDir.sqrMagnitude > 1e-6f) offsetDir = offsetDir.normalized;
        else offsetDir = (Vector2)transform.right;

        float baseOffset = 0.02f;
        float sizeBased = baseOffset;
        if (spriteRenderer != null)
        {
            sizeBased = Mathf.Max(sizeBased, spriteRenderer.bounds.extents.magnitude * 0.08f);
        }
        else if (col2D != null)
        {
            sizeBased = Mathf.Max(sizeBased, col2D.bounds.extents.magnitude * 0.08f);
        }

        Vector3 worldPos = contactWorld + offsetDir * sizeBased;
        Quaternion worldRot = transform.rotation;

        stuckHostTransform = hitCollider.transform;
        stuckLocalPosition = stuckHostTransform.InverseTransformPoint(worldPos);
        stuckLocalRotation = Quaternion.Inverse(stuckHostTransform.rotation) * worldRot;

        transform.SetParent(null);
        transform.position = worldPos;
        transform.rotation = worldRot;

        transform.localScale = originalLocalScale;
    }

    private void StopImpalingInAir()
    {
        rb2D.linearVelocity = Vector2.zero;
        rb2D.bodyType = RigidbodyType2D.Static;

        ChangeState(ProjectileState.Stuck);

        if (showDebugLogs)
            Debug.Log($"AttackProjectile: Impaling 공중에서 멈춤 (거리: {impalingTraveledDistance:F2}m)");
    }

    /// <summary>
    /// 회수 시작 (기존 호환 + 새로운 Behavior 지원)
    /// useRetrievalBehavior: true면 config의 retrievalBehavior 사용, false면 기존 즉시 회수
    /// </summary>
    public void StartReturn(bool suppressAmmo = false, bool immediatePickup = true, bool useRetrievalBehavior = false)
    {
        if (showDebugLogs)
            Debug.Log($"AttackProjectile.StartReturn() 호출: isReturning={isReturning}, useRetrievalBehavior={useRetrievalBehavior}, retrievalBehavior={(retrievalBehavior != null ? "존재" : "null")}");

        if (isReturning)
        {
            if (showDebugLogs)
                Debug.LogWarning($"AttackProjectile: 이미 회수 중이므로 StartReturn 무시 (isReturning=true)");
            return;
        }

        UnregisterFromHost();

        isStuck = false;
        isReturning = true;
        suppressAmmoOnReturn = suppressAmmo;
        hasBeenCollected = false;

        if (showDebugLogs)
            Debug.Log($"AttackProjectile: StartReturn (useRetrievalBehavior={useRetrievalBehavior})");

        stuckHostTransform = null;
        transform.SetParent(null);

        if (attacker == null)
        {
            if (playerTransform != null) attacker = playerTransform;
            else
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) { attacker = p.transform; playerTransform = attacker; }
            }
        }

        // ========== 새로운 Behavior 기반 회수 ==========
        if (useRetrievalBehavior && retrievalBehavior != null && attacker != null)
        {
            if (showDebugLogs)
                Debug.Log($"AttackProjectile: retrievalBehavior 사용 ({config?.retrievalType})");

            // Behavior에게 회수 위임
            retrievalBehavior.StartRetrieval(this, attacker);
            ChangeState(ProjectileState.Returning);
            return;
        }
        else if (useRetrievalBehavior)
        {
            if (showDebugLogs)
                Debug.LogWarning($"AttackProjectile: useRetrievalBehavior=true이지만 조건 미충족! retrievalBehavior={(retrievalBehavior != null ? "존재" : "null")}, attacker={(attacker != null ? "존재" : "null")}, config={(config != null ? "존재" : "null")}");
        }

        // ========== 기존 즉시 회수 로직 (하위 호환) ==========
        if (showDebugLogs)
            Debug.Log($"AttackProjectile: 기존 즉시 회수 로직 사용 (immediatePickup={immediatePickup})");
        if (rb2D != null)
        {
            rb2D.bodyType = RigidbodyType2D.Kinematic;
            rb2D.linearVelocity = Vector2.zero;
            rb2D.angularVelocity = 0f;
        }

        if (col2D != null)
        {
            col2D.enabled = true;
            col2D.isTrigger = true;
        }

        transform.localScale = originalLocalScale;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;

        if (immediatePickup)
        {
            if (attacker != null)
            {
                transform.position = attacker.position;
                transform.rotation = attacker.rotation;
                CompleteRetrieval();
            }
            else
            {
                ReturnOrDestroy();
            }
        }

        // 상태 전환
        ChangeState(ProjectileState.Returning);
    }

    /// <summary>
    /// 회수 준비 (Behavior에서 호출)
    /// </summary>
    public void PrepareForRetrieval()
    {
        rb2D.bodyType = RigidbodyType2D.Kinematic;
        rb2D.linearVelocity = Vector2.zero;
        
        // 콜라이더를 Trigger로 변경 (Pull 회수 시 충돌 감지 위해 활성화 유지)
        if (col2D != null)
        {
            col2D.enabled = true;
            col2D.isTrigger = true;
        }
    }

    /// <summary>
    /// 즉시 회수 (SimpleRetrievalBehavior용)
    /// </summary>
    public void StartReturnImmediate(Transform player)
    {
        // 기존 StartReturn 호출
        StartReturn(suppressAmmo: false, immediatePickup: true);
    }

    public void CompleteRetrieval()
    {
        if (hasBeenCollected) return;
        hasBeenCollected = true;
        isReturning = false;

        if (showDebugLogs)
            Debug.Log($"AttackProjectile: CompleteRetrieval");

        PlayerCombat pc = null;

        if (attacker != null)
        {
            pc = attacker.GetComponent<PlayerCombat>();
        }

        if (pc == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) pc = playerObj.GetComponent<PlayerCombat>();
        }

        if (pc != null)
        {
            if (isBloodStake)
            {
                pc.OnExecutionSuccess(bloodHpCost, 0);
            }
            else
            {
                if (!suppressAmmoOnReturn)
                {
                    pc.RecoverAmmo(1);
                }
            }
        }

        UnregisterFromHost();

        // 벽에 박혔다면 OnWallImpact()에서 이미 적들이 해제됨
        // 회수될 때는 OnStateReturning()에서 이미 적들이 해제됨

        if (AttackManager.Instance != null)
            AttackManager.Instance.ReleaseStake(gameObject);
        else
            Destroy(gameObject);
    }

    public void ForceDetachFromHost()
    {
        currentHostEnemy = null;
        stuckHostTransform = null;
        transform.SetParent(null);

        isStuck = false;
        isReturning = false;

        // 벽에 박혔다면 OnWallImpact()에서 이미 적들이 해제됨
        // 회수될 때는 OnStateReturning()에서 이미 적들이 해제됨

        if (rb2D != null) rb2D.bodyType = RigidbodyType2D.Kinematic;
        if (col2D != null) col2D.isTrigger = true;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;

        transform.localScale = originalLocalScale;
    }

    private void UnregisterFromHost()
    {
        if (currentHostEnemy != null)
        {
            lastHostEnemy = currentHostEnemy; // 회수 전 적 정보 저장
            currentHostEnemy.RemoveStuckProjectile(this);
            currentHostEnemy = null;
        }
    }

    private void ApplyDamageAndEffects(Collider2D target, float damageRatio)
    {
        float finalDamage = damage * damageRatio;

        HealthSystem hs = target.GetComponent<HealthSystem>() ?? target.GetComponentInParent<HealthSystem>();
        if (hs != null)
        {
            hs.TakeDamage(finalDamage);
        }

        var enemyCtrl = target.GetComponent<EnemyController>() ?? target.GetComponentInParent<EnemyController>();
        if (enemyCtrl != null)
        {
            enemyCtrl.RegisterHit(1, attacker);
        }
    }

    private void ApplyHitFeedback(EHitStopStrength hs, EShakeStrength ss)
    {
        HitEffectManager.PlayHitEffect(EHitSource.Stake, hs, ss, transform.position);
    }

    private void DissolveAndDestroy()
    {
        UnregisterFromHost();

        // 벽에 박혔다면 OnWallImpact()에서 이미 적들이 해제됨
        // 회수될 때는 OnStateReturning()에서 이미 적들이 해제됨

        if (AttackManager.Instance != null)
            AttackManager.Instance.ReleaseStake(gameObject);
        else
            Destroy(gameObject);
    }

    private void ReturnOrDestroy()
    {
        UnregisterFromHost();

        // 벽에 박혔다면 OnWallImpact()에서 이미 적들이 해제됨
        // 회수될 때는 OnStateReturning()에서 이미 적들이 해제됨

        if (AttackManager.Instance != null)
            AttackManager.Instance.ReleaseStake(gameObject);
        else
            Destroy(gameObject);
    }

    public void ClearAttacker()
    {
        attacker = null;
    }

    public EnemyController GetHostEnemy()
    {
        return currentHostEnemy;
    }

    // ========== 기존 RetrievalAnimationRoutine 유지 (하위 호환) ==========
    public System.Collections.IEnumerator RetrievalAnimationRoutine(
        Transform collector,
        float pullDistance = 0.6f,
        float hitStopSeconds = 0.1f,
        float moveToPlayerDuration = 0.25f,
        LineRenderer linePrefab = null,
        global::EHitStopStrength hitStop = global::EHitStopStrength.Weak,
        global::EShakeStrength shake = global::EShakeStrength.Weak,
        GameObject endDecorator = null)
    {
        // 기존 코드 유지 (StakeRetrievalSkill에서 사용)
        if (collector == null) yield break;

        UnregisterFromHost();

        isStuck = false;
        isReturning = false;
        suppressAmmoOnReturn = false;
        hasBeenCollected = false;

        attacker = collector;

        if (rb2D != null) rb2D.bodyType = RigidbodyType2D.Kinematic;
        if (col2D != null) { col2D.enabled = false; col2D.isTrigger = true; }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 dirToPlayer = (collector.position - startPos).normalized;
        if (dirToPlayer == Vector3.zero) dirToPlayer = transform.right;
        Vector3 pullPos = startPos + dirToPlayer * pullDistance;

        // LineRenderer 준비
        LineRenderer lr = null;
        GameObject lrGo = null;
        if (linePrefab != null)
        {
            bool isSceneInstance = false;
            try
            {
                isSceneInstance = linePrefab.gameObject.scene.IsValid();
            }
            catch { }

            if (isSceneInstance)
            {
                lr = linePrefab;
                lrGo = lr.gameObject;
            }
            else
            {
                lr = Instantiate(linePrefab);
                lrGo = lr?.gameObject;
            }
        }
        else
        {
            lrGo = new GameObject($"RetrievalLine_{gameObject.name}");
            lr = lrGo.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.positionCount = 2;
            lr.startWidth = lr.endWidth = 0.05f;
            lr.numCapVertices = 4;
            lr.useWorldSpace = true;
            lr.sortingOrder = 1000;
        }

        if (lr != null)
        {
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.SetPosition(0, collector.position);
            lr.SetPosition(1, transform.position);
        }

        GameObject endDec = null;
        if (endDecorator != null)
        {
            try
            {
                endDec = Instantiate(endDecorator, transform.position, transform.rotation);
                if (endDec != null) endDec.SetActive(true);
            }
            catch { }
        }

        // 1) 짧은 앞당김
        float stepDuration = 0.08f;
        float t = 0f;
        while (t < stepDuration)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / stepDuration);
            transform.position = Vector3.Lerp(startPos, pullPos, f);
            transform.rotation = startRot;
            if (lr != null)
            {
                lr.SetPosition(0, collector.position);
                lr.SetPosition(1, transform.position);
            }
            if (endDec != null) endDec.transform.position = transform.position;
            yield return null;
        }

        // 2) 히트 이펙트
        try
        {
            HitEffectManager.PlayHitEffect(EHitSource.Stake, hitStop, shake, transform.position);
        }
        catch { }

        if (hitStopSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(hitStopSeconds);
        }

        // 2b) 히트 파티클
        try
        {
            GameObject tmp = new GameObject($"Temp_HitParticle_{GetInstanceID()}");
            var hpe = tmp.AddComponent<HitParticleEffect>();
            hpe.PlayHitParticle(transform.position);
            Destroy(tmp, 2.0f);
        }
        catch { }

        // 3) 플레이어로 흡수
        t = 0f;
        Vector3 midStart = transform.position;
        while (t < moveToPlayerDuration)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / moveToPlayerDuration);
            transform.position = Vector3.Lerp(midStart, collector.position, f);
            transform.rotation = startRot;
            if (lr != null)
            {
                lr.SetPosition(0, collector.position);
                lr.SetPosition(1, transform.position);
            }
            if (endDec != null) endDec.transform.position = transform.position;
            yield return null;
        }

        // 4) 선 정리
        if (lrGo != null) Destroy(lrGo);
        if (endDec != null) Destroy(endDec);

        // 5) 도착 처리
        try
        {
            CompleteRetrieval();
        }
        catch
        {
            try
            {
                UnregisterFromHost();
                if (AttackManager.Instance != null)
                    AttackManager.Instance.ReleaseStake(gameObject);
                else
                    Destroy(gameObject);
            }
            catch { }
        }

        yield break;
    }
}
