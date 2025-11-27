using UnityEngine;

/// <summary>
/// AttackManager - 중앙 공격 관리자 (풀링 포함)
/// - FireStake, FireChargedStake, SwingHammer, ReleaseStake 제공
/// - stakePrefab 풀을 사용하여 성능 최적화
/// </summary>
public class AttackManager : MonoBehaviour
{
    public static AttackManager Instance { get; private set; }

    [Header("Prefabs / Pool")]
    [Tooltip("말뚝(투사체) 프리팹 (AttackProjectile 포함)")]
    public GameObject stakePrefab;
    [Tooltip("BloodStake 전용 프리팹 (설정하면 BloodStake는 이 프리팹을 사용합니다)")]
    public GameObject bloodStakePrefab;
    [Tooltip("차징샷 전용 프리팹 (설정하면 차징샷은 이 프리팹을 사용합니다)")]
    public GameObject chargedStakePrefab;
    [Tooltip("블러드 차징샷 전용 프리팹 (설정하면 블러드 차징샷은 이 프리팹을 사용합니다)")]
    public GameObject bloodChargedStakePrefab;
    [Tooltip("스택 프리웜 수")]
    public int stakePoolInitialSize = 8;

    private GameObjectPool stakePool;

    [Header("Wood Stake (기본)")]
    public float woodDamage = 10f;
    public float woodSpeed = 14f;
    public float woodLifetime = 5f;
    public bool woodRetrievable = true;

    [Header("Charged Stake")]
    [Tooltip("차지샷 발사 시 소모되는 탄약 수 (PlayerCombat에서 참조)")]
    [Range(1, 100)]
    public int chargedAmmoCost = 10;
    
    [Tooltip("차지샷 데미지")]
    public float chargedDamage = 80f;
    
    [Tooltip("차지샷 사거리 (기존 레이캐스트 방식용, 현재는 투사체 방식 사용)")]
    public float chargedRange = 15f;
    
    [Tooltip("차징샷 회수 가능 여부")]
    public bool chargedRetrievable = false;
    
    [Tooltip("차징샷 전용 ProjectileConfig SO (설정하면 오버라이드됨)")]
    public ProjectileConfig chargedStakeConfig;

    [Header("Hammer (Execution)")]
    public float hammerDamage = 10f; // 기본 약한 피해
    public float hammerExecuteHeal = 20f; // 처형 시 플레이어 회복량
    public int hammerAmmoReward = 3; // 처형 시 지급 탄약(설정 가능)
    public float hammerKnockbackForce = 8f;

    [Header("Common")]
    public string enemyTag = "Enemy";
    public string playerTag = "Player";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        if (stakePrefab != null)
        {
            stakePool = new GameObjectPool(stakePrefab, Mathf.Max(1, stakePoolInitialSize), this.transform);
        }
    }

    /// <summary>
    /// 말뚝 발사
    /// - isBloodStake: BloodStake 여부
    /// - bloodHpCost: BloodStake 발사 시 소모한 HP(회수 시 플레이어에게 이 값 만큼 회복)
    /// - configOverride: 테스트용 ProjectileConfig (null이면 기본 동작)
    /// </summary>
    public GameObject FireStake(Vector3 origin, Vector2 direction, float damage, bool isBloodStake, Transform attacker, float bloodHpCost = 0f, ProjectileConfig configOverride = null)
    {
        GameObject go = null;

        // 우선: BloodStake 전용 프리팹이 설정되어 있고 isBloodStake이면 그걸 사용
        if (isBloodStake && bloodStakePrefab != null)
        {
            go = Instantiate(bloodStakePrefab, origin, Quaternion.identity);
        }
        else
        {
            // 기본 stake 풀/프리팹 사용
            if (stakePool != null)
                go = stakePool.Get(origin, Quaternion.identity);
            else if (stakePrefab != null)
                go = Instantiate(stakePrefab, origin, Quaternion.identity);
        }

        if (go == null)
        {
            Debug.LogWarning("AttackManager: stake 프리팹/풀에서 인스턴스 생성 실패");
            return null;
        }

        // 런타임 패턴 생성 (ScriptableObject.CreateInstance -> new)
        AttackPatternData pattern = new AttackPatternData()
        {
            attackType = EAttackType.Projectile,
            projectileSpeed = woodSpeed,
            projectileLifetime = woodLifetime,
            attackSprite = null,
            damage = damage,
            // BloodStake도 회수 가능하도록 woodRetrievable값을 그대로 사용
            isRetrievable = woodRetrievable,
            hitboxOffset = Vector2.zero,
            hitboxSize = Vector2.one,
            hitboxRadius = 1f,
            stunDuration = 0f,
            knockbackStrength = EKnockbackStrength.None,
            knockbackDistance = 0f
        };

        var proj = go.GetComponent<AttackProjectile>();
        if (proj != null)
        {
            // 테스트용 config가 있으면 먼저 설정 (Initialize 전에 호출 필수)
            if (configOverride != null)
            {
                proj.SetRuntimeConfig(configOverride);
            }

            // 새 Initialize 시그니처에 맞춰 isBloodStake와 bloodHpCost 전달
            proj.Initialize(pattern, damage, enemyTag, attacker, null, direction, isBloodStake, bloodHpCost);
        }
        else
        {
            Debug.LogWarning("AttackManager: stakePrefab에 AttackProjectile 컴포넌트가 없습니다.");
        }

        return go;
    }

    /// <summary>
    /// 차징샷 투사체 발사 (프리팹 + SO 오버라이드 방식)
    /// - chargedStakePrefab 사용 (없으면 stakePrefab)
    /// - chargedStakeConfig SO 오버라이드 (있으면)
    /// - Retrieval Behavior 구조 호환 (SimpleRetrievalBehavior, BindingRetrievalBehavior, PullRetrievalBehavior 등)
    /// </summary>
    /// <param name="isBloodCharge">블러드 차징샷 여부 (true면 bloodChargedStakePrefab 사용)</param>
    /// <param name="bloodHpCost">블러드 차징샷 발사 시 소모한 HP (회수 시 회복량)</param>
    public GameObject FireChargedStakeProjectile(Vector3 origin, Vector2 direction, Transform attacker, bool isBloodCharge = false, float bloodHpCost = 0f)
    {
        GameObject go = null;

        // 1. 프리팹 선택 우선순위
        if (isBloodCharge && bloodChargedStakePrefab != null)
        {
            // 블러드 차징샷 전용 프리팹
            go = Instantiate(bloodChargedStakePrefab, origin, Quaternion.identity);
        }
        else if (!isBloodCharge && chargedStakePrefab != null)
        {
            // 일반 차징샷 전용 프리팹
            go = Instantiate(chargedStakePrefab, origin, Quaternion.identity);
        }
        else if (isBloodCharge && bloodStakePrefab != null)
        {
            // 블러드 차징샷 프리팹이 없으면 일반 블러드 프리팹 사용
            go = Instantiate(bloodStakePrefab, origin, Quaternion.identity);
        }
        else if (chargedStakePrefab != null)
        {
            // 차징샷 프리팹 사용
            go = Instantiate(chargedStakePrefab, origin, Quaternion.identity);
        }
        else if (stakePrefab != null)
        {
            // 기본 프리팹 사용
            go = Instantiate(stakePrefab, origin, Quaternion.identity);
        }

        if (go == null)
        {
            Debug.LogWarning("AttackManager: 차징샷 프리팹 생성 실패");
            return null;
        }

        // 2. 패턴 데이터 생성 (chargedStakeConfig가 있으면 그 값 사용)
        float speed = chargedStakeConfig != null ? chargedStakeConfig.speed : woodSpeed * 1.5f;
        float lifetime = chargedStakeConfig != null ? chargedStakeConfig.lifetime : woodLifetime;
        
        AttackPatternData pattern = new AttackPatternData()
        {
            attackType = EAttackType.Projectile,
            projectileSpeed = speed,
            projectileLifetime = lifetime,
            attackSprite = null,
            damage = chargedDamage,
            isRetrievable = chargedRetrievable,
            hitboxOffset = Vector2.zero,
            hitboxSize = Vector2.one,
            hitboxRadius = 1f,
            stunDuration = 0f,
            knockbackStrength = EKnockbackStrength.None,
            knockbackDistance = 0f
        };

        var proj = go.GetComponent<AttackProjectile>();
        if (proj != null)
        {
            // 3. 차징샷 SO 오버라이드 (3번 키 전환 방식과 동일)
            // SetRuntimeConfig()는 Initialize() 이전에 호출되어야 함
            if (chargedStakeConfig != null)
            {
                proj.SetRuntimeConfig(chargedStakeConfig);
            }

            // 4. Initialize 호출 (isBloodStake, bloodHpCost 전달)
            proj.Initialize(pattern, chargedDamage, enemyTag, attacker, null, direction, isBloodCharge, bloodHpCost);
        }
        else
        {
            Debug.LogWarning("AttackManager: 차징샷 프리팹에 AttackProjectile 컴포넌트가 없습니다.");
        }

        return go;
    }

    /// <summary>
    /// 차지샷: 지정 범위 내 적 즉사 처리 (기존 레이캐스트 방식 - 호환성 유지)
    /// </summary>
    public void FireChargedStake(Transform attacker, Vector2 direction, float rangeOverride = -1f)
    {
        float rng = rangeOverride > 0f ? rangeOverride : chargedRange;
        RaycastHit2D[] hits = Physics2D.RaycastAll(attacker.position, direction, rng);
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (!hit.collider.CompareTag(enemyTag)) continue;

            var health = hit.collider.GetComponent<HealthSystem>();
            if (health != null) health.ForceDie();
        }
    }

    /// <summary>
    /// 망치 휘두르기: 히트박스 검사 + 그로기 처형/일반 데미지 적용
    /// </summary>
    public void SwingHammer(Transform owner, Vector2 offset, Vector2 hitboxSize, float damage)
    {
        Vector2 worldPos = (Vector2)owner.position + (Vector2)(owner.rotation * offset);
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldPos, hitboxSize, 0f);
        bool anyHit = false;

        foreach (var col in hits)
        {
            if (col == null) continue;
            if (!col.CompareTag(enemyTag)) continue;
            anyHit = true;

            Rigidbody2D targetRb = col.GetComponent<Rigidbody2D>();
            if (targetRb != null)
            {
                Vector2 knockDir = (col.transform.position - owner.position).normalized;
                targetRb.AddForce(knockDir * hammerKnockbackForce, ForceMode2D.Impulse);
            }

            var enemyCtrl = col.GetComponent<EnemyController>();
            var enemyHealth = col.GetComponent<HealthSystem>();

            if (enemyCtrl != null && enemyCtrl.IsGroggy())
            {
                // 처형: 적에 박혀있는 말뚝 개수를 소비하여 보상으로 지급
                int stackReward = enemyCtrl.ConsumeStacks();

                if (enemyHealth != null) enemyHealth.ForceDie();

                var pc = owner.GetComponent<PlayerCombat>();
                if (pc != null)
                {
                    pc.OnExecutionSuccess(hammerExecuteHeal, stackReward);
                }
            }
            else
            {
                if (enemyHealth != null) enemyHealth.TakeDamage(damage);
            }
        }

        if (!anyHit) Debug.Log("[AttackManager] SwingHammer: 히트 없음");
    }

    /// <summary>
    /// 풀 반환 API: AttackProjectile 등에서 사용
    /// </summary>
    public void ReleaseStake(GameObject go)
    {
        if (stakePool != null) stakePool.Release(go);
        else Destroy(go);
    }

    /// <summary>
    /// 일반화된 공격 실행 API (EnemyCombat 등에서 사용)
    /// - pattern: AttackPatternData (런타임 클래스)
    /// - attacker: 발사자/시전자 Transform
    /// - damage: 적용할 대미지
    /// - targetTag: 피해 대상 태그 (예: "Player", "Enemy")
    /// </summary>
    public void ExecuteAttack(AttackPatternData pattern, Transform attacker, float damage, string targetTag)
    {
        if (pattern == null || attacker == null)
        {
            Debug.LogWarning("AttackManager.ExecuteAttack: pattern 또는 attacker 누락");
            return;
        }

        if (pattern.attackType == EAttackType.Projectile)
        {
            Vector3 origin = attacker.position;
            Vector2 dir = attacker.right;
            GameObject go = null;
            if (stakePool != null && stakePrefab != null)
                go = stakePool.Get(origin, Quaternion.identity);
            else if (stakePrefab != null)
                go = Instantiate(stakePrefab, origin, Quaternion.identity);

            if (go == null)
            {
                Debug.LogWarning("AttackManager.ExecuteAttack: 프리팹 생성 실패");
                return;
            }

            var proj = go.GetComponent<AttackProjectile>();
            if (proj != null)
            {
                // 기본으로 isBloodStake=false, bloodHpCost=0
                proj.Initialize(pattern, damage, targetTag, attacker, null, dir, false, 0f);
            }
        }
        else // MeleeSlash / Hitbox -> 즉시 히트박스 검사 적용
        {
            Vector2 worldPos = (Vector2)attacker.position + pattern.hitboxOffset;
            if (pattern.hitboxType == EHitboxType.Box)
            {
                Collider2D[] cols = Physics2D.OverlapBoxAll(worldPos, pattern.hitboxSize, 0f);
                foreach (var col in cols)
                {
                    if (col == null) continue;
                    if (!col.CompareTag(targetTag)) continue;
                    var hs = col.GetComponent<HealthSystem>();
                    if (hs != null) hs.TakeDamage(damage);
                }
            }
            else // Circle
            {
                Collider2D[] cols = Physics2D.OverlapCircleAll(worldPos, pattern.hitboxRadius);
                foreach (var col in cols)
                {
                    if (col == null) continue;
                    if (!col.CompareTag(targetTag)) continue;
                    var hs = col.GetComponent<HealthSystem>();
                    if (hs != null) hs.TakeDamage(damage);
                }
            }
        }
    }
}