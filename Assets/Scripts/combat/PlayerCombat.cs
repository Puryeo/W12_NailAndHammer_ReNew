using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PlayerCombat (Silver Nail) - DeckManager 연동 완료
/// - LMB: 탭 = 나무 말뚝 발사
/// - LMB hold+release: 차지샷
/// - RMB: 망치 휘두르기 (처형) — DeckManager의 스킬 카드 시스템 사용
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Status")]
    [SerializeField] private HealthSystem healthSystem;

    [Header("Deck System")]
    [SerializeField] private DeckManager deckManager;

    [Header("Weapon 1: Wood Stake (Basic)")]
    [SerializeField] private int maxAmmo = 1;
    [SerializeField] private int currentAmmo;
    [SerializeField] private float woodDamage = 10f;
    [SerializeField] private float fireRate = 0.5f;

    [Header("Blood Stake (HP 기반 발사)")]
    [Tooltip("최대 체력의 몇 %를 소모하여 BloodStake를 발사할지 (예: 0.10 = 10%)")]
    [SerializeField] private float bloodStakeHpPercent = 0.10f;

    [Header("Charge Shot")]
    [Tooltip("홀드해야 하는 최소 시간 (초)")]
    [SerializeField] private float chargeTimeRequired = 1.0f;
    [Tooltip("차지샷 사거리")]
    [SerializeField] private float chargedRange = 15f;
    [Tooltip("차징샷 활성화 여부")]
    [SerializeField] private bool isChargeShotEnabled = true;

    [Header("Weapon 3: Hammer (Execution)")]
    [SerializeField] private GameObject hammerPrefab;
    [Tooltip("해머가 스폰될 로컬 오프셋 (플레이어 기준)")]
    [SerializeField] private Vector2 hammerSpawnOffset = new Vector2(0.8f, 0f);
    [Tooltip("휘두를 각도(총 회전각)")]
    [SerializeField] private float hammerSwingAngle = 120f;
    [Tooltip("휘두르는 시간(초)")]
    [SerializeField] private float hammerSwingDuration = 0.28f;

    [SerializeField] private float hammerDamage = 10f;
    [SerializeField] private float hammerCooldown = 1.5f;
    [SerializeField] private float hammerKnockbackForce = 8f;
    [SerializeField] private float hammerExecuteHeal = 20f;

    [Tooltip("스윙 진행을 제어하는 애니메이션 커브 (시간 0->1)")]
    [SerializeField] private AnimationCurve hammerSwingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("우클릭 공격 (Secondary)")]
    [Tooltip("우클릭 처형 공격 활성화 여부")]
    [SerializeField] private bool isSecondaryChargeShotEnabled = true;

    [Header("Execution Settings")]
    [Tooltip("처형 감지 반경")]
    [SerializeField] private float executionRange = 3.5f;
    [Tooltip("처형 감지 각도 (부채꼴 전체 각도)")]
    [SerializeField] private float executionAngle = 245f;
    [Tooltip("감지할 적 레이어")]
    [SerializeField] private LayerMask enemyLayer;

    private MonoBehaviour currentSecondaryChargedAttackComponent;
    private ISecondaryChargedAttack currentSecondaryChargedAttack;

    // 스킬 관리 시스템
    private Dictionary<SecondaryChargedAttackType, ISecondaryChargedAttack> skillDictionary;
    private SecondaryChargedAttackType currentSkillType = SecondaryChargedAttackType.None;

    [Header("Skill Test Mode")]
    [Tooltip("테스트 모드 활성화 (5,6,7,8 키로 스킬 교체)")]
    [SerializeField] private bool enableSkillTestMode = false;

    [Header("Skill: Chain Retrieve")]
    [SerializeField] private float retrieveRange = 10f;
    [SerializeField] private LayerMask projectileLayer;

    // 내부 쿨타임 변수
    private float fireTimer = 0f;
    private float hammerTimer = 0f;

    // 차지 상태
    private bool isCharging = false;
    private float chargeTimer = 0f;

    private ProjectileTestController testController;

    private void Awake()
    {
        if (healthSystem == null) healthSystem = GetComponent<HealthSystem>();
        currentAmmo = maxAmmo;

        // DeckManager 찾기
        if (deckManager == null)
        {
            deckManager = FindObjectOfType<DeckManager>();
            if (deckManager == null)
            {
                Debug.LogWarning("[PlayerCombat] DeckManager를 찾을 수 없습니다!");
            }
        }

        testController = GetComponent<ProjectileTestController>();

        // 스킬 Dictionary 초기화
        InitializeSkillDictionary();

        // DeckManager 연동은 Start()에서 처리 (DeckManager.Start() 이후에 실행되도록)
    }

    private void Start()
    {
        // DeckManager에서 준비된 스킬을 가져와서 장착
        if (deckManager != null)
        {
            SecondaryChargedAttackType readySkill = deckManager.GetReadySkillType();
            if (readySkill != SecondaryChargedAttackType.None)
            {
                EquipSkill(readySkill);
                Debug.Log($"[Combat] DeckManager에서 초기 스킬 로드: {readySkill}");
            }
            else
            {
                Debug.LogWarning("[Combat] DeckManager에 준비된 스킬이 없습니다!");
                // 기존 방식으로 폴백
                if (currentSecondaryChargedAttackComponent != null)
                {
                    if (currentSecondaryChargedAttackComponent is ISecondaryChargedAttack attackComponent)
                    {
                        currentSecondaryChargedAttack = attackComponent;
                        currentSkillType = attackComponent.GetSkillType();
                    }
                }
                else
                {
                    EquipFirstAvailableSkill();
                }
            }
        }
        else
        {
            // DeckManager가 없으면 기존 방식 사용
            if (currentSecondaryChargedAttackComponent == null)
            {
                var tempClass = GetComponent<ISecondaryChargedAttack>();
                currentSecondaryChargedAttackComponent = tempClass as MonoBehaviour;
            }

            if (currentSecondaryChargedAttackComponent != null)
            {
                if (currentSecondaryChargedAttackComponent is ISecondaryChargedAttack attackComponent)
                {
                    currentSecondaryChargedAttack = attackComponent;
                    currentSkillType = attackComponent.GetSkillType();
                    Debug.Log($"[Combat] 우클릭 차징공격 초기화(Component): {currentSecondaryChargedAttack.GetAttackName()}, Type: {currentSkillType}");
                }
            }
            else
            {
                EquipFirstAvailableSkill();
            }
        }
    }

    /// <summary>
    /// 모든 ISecondaryChargedAttack 컴포넌트를 수집하여 Dictionary에 등록
    /// </summary>
    private void InitializeSkillDictionary()
    {
        skillDictionary = new Dictionary<SecondaryChargedAttackType, ISecondaryChargedAttack>();

        MonoBehaviour[] components = GetComponents<MonoBehaviour>();

        foreach (var comp in components)
        {
            if (comp is ISecondaryChargedAttack skill)
            {
                SecondaryChargedAttackType skillType = skill.GetSkillType();

                if (skillDictionary.ContainsKey(skillType))
                {
                    Debug.LogWarning($"[Combat] 중복된 스킬 타입 발견: {skillType}. 기존 스킬을 유지합니다.");
                    continue;
                }

                skillDictionary[skillType] = skill;
                Debug.Log($"[Combat] 스킬 등록: {skillType} - {skill.GetAttackName()}");
            }
        }

        Debug.Log($"[Combat] 총 {skillDictionary.Count}개의 스킬이 등록되었습니다.");
    }

    /// <summary>
    /// Dictionary에서 첫 번째 None이 아닌 스킬을 장착
    /// </summary>
    private void EquipFirstAvailableSkill()
    {
        if (skillDictionary == null || skillDictionary.Count == 0)
        {
            Debug.LogWarning("[Combat] 사용 가능한 스킬이 없습니다.");
            return;
        }

        foreach (var kvp in skillDictionary)
        {
            if (kvp.Key != SecondaryChargedAttackType.None)
            {
                EquipSkill(kvp.Key);
                Debug.Log($"[Combat] 기본 스킬 자동 장착: {kvp.Key}");
                return;
            }
        }

        if (skillDictionary.ContainsKey(SecondaryChargedAttackType.None))
        {
            EquipSkill(SecondaryChargedAttackType.None);
            Debug.Log("[Combat] None 스킬 장착");
        }
    }

    private void Update()
    {
        if (fireTimer > 0) fireTimer -= Time.deltaTime;
        if (hammerTimer > 0) hammerTimer -= Time.deltaTime;

        if (isCharging)
        {
            chargeTimer += Time.deltaTime;
        }

        if (enableSkillTestMode)
        {
            HandleSkillTestInput();
        }
    }

    /// <summary>
    /// 테스트 모드 키 입력 처리 (5,6,7,8 키로 스킬 교체)
    /// </summary>
    private void HandleSkillTestInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            if (EquipSkill(SecondaryChargedAttackType.Windmill))
            {
                Debug.Log($"[TEST] 스킬 교체: Windmill (윈드밀)");
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            if (EquipSkill(SecondaryChargedAttackType.Thorns))
            {
                Debug.Log($"[TEST] 스킬 교체: Thorns (가시소환)");
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            if (EquipSkill(SecondaryChargedAttackType.Guardian))
            {
                Debug.Log($"[TEST] 스킬 교체: Guardian (가디언)");
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            if (EquipSkill(SecondaryChargedAttackType.Sector))
            {
                Debug.Log($"[TEST] 스킬 교체: Sector (부채꼴)");
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            if (EquipSkill(SecondaryChargedAttackType.Splash))
            {
                Debug.Log($"[TEST] 스킬 교체: Splash");
            }
        }
    }

    public void OnPrimaryDown()
    {
        if (fireTimer > 0) return;
        isCharging = true;
        chargeTimer = 0f;
    }

    public void OnPrimaryUp()
    {
        if (!isCharging) return;
        isCharging = false;

        if (isChargeShotEnabled && chargeTimer >= chargeTimeRequired)
        {
            TryFireChargedStake();
        }
        else
        {
            TryFireWoodStake();
        }
    }

    public void TryFireWoodStake()
    {
        if (fireTimer > 0) return;

        if (currentAmmo <= 0)
        {
            TryFireBloodStake();
            return;
        }

        fireTimer = fireRate;
        currentAmmo--;

        Vector2 dir = GetAimDirection();

        ProjectileConfig testConfig = null;
        if (testController != null)
        {
            testConfig = testController.GetCurrentConfig();
        }

        if (AttackManager.Instance != null)
        {
            AttackManager.Instance.FireStake(
                origin: transform.position,
                direction: dir,
                damage: woodDamage,
                isBloodStake: false,
                attacker: transform,
                bloodHpCost: 0f,
                configOverride: testConfig
            );
        }

        Debug.Log($"[Combat] 나무 말뚝 발사! 남은 탄환: {currentAmmo}/{maxAmmo}");
    }

    private void TryFireChargedStake()
    {
        if (fireTimer > 0) return;

        int chargedAmmoCost = AttackManager.Instance != null ? AttackManager.Instance.chargedAmmoCost : 1;

        if (currentAmmo >= chargedAmmoCost)
        {
            currentAmmo -= chargedAmmoCost;
            fireTimer = fireRate;

            Vector2 dir = GetAimDirection();

            if (AttackManager.Instance != null)
            {
                AttackManager.Instance.FireChargedStakeProjectile(
                    origin: transform.position,
                    direction: dir,
                    attacker: transform,
                    isBloodCharge: false,
                    bloodHpCost: 0f
                );
            }

            Debug.Log($"[Combat] 차지샷 발사! 탄약 -{chargedAmmoCost} (남음: {currentAmmo}/{maxAmmo})");
        }
        else
        {
            TryFireBloodChargedStake();
        }
    }

    private void TryFireBloodChargedStake()
    {
        if (healthSystem == null) return;

        int chargedAmmoCost = AttackManager.Instance != null ? AttackManager.Instance.chargedAmmoCost : 1;
        float hpCost = Mathf.Max(0f, healthSystem.GetMaxHealth() * bloodStakeHpPercent * chargedAmmoCost);

        if (healthSystem.GetCurrentHealth() <= hpCost)
        {
            Debug.Log("❌ HP가 부족하여 블러드 차징샷을 발사할 수 없습니다.");
            return;
        }

        healthSystem.TakeDamage(hpCost);
        fireTimer = fireRate;

        Vector2 dir = GetAimDirection();

        if (AttackManager.Instance != null)
        {
            AttackManager.Instance.FireChargedStakeProjectile(
                origin: transform.position,
                direction: dir,
                attacker: transform,
                isBloodCharge: true,
                bloodHpCost: hpCost
            );
        }

        Debug.Log($"[Combat] 블러드 차징샷 발사! HP -{hpCost:F1}");
    }

    public void TrySwingHammer(bool enableExecution = true)
    {
        if (hammerTimer > 0) return;
        if (hammerPrefab == null)
        {
            Debug.LogWarning("PlayerCombat: hammerPrefab이 할당되지 않았습니다.");
            hammerTimer = hammerCooldown;
            return;
        }

        hammerTimer = hammerCooldown;

        Vector3 spawnPos = transform.position + (Vector3)(transform.rotation * hammerSpawnOffset);

        GameObject go = Instantiate(hammerPrefab, spawnPos, Quaternion.identity);
        var hc = go.GetComponent<HammerSwingController>();
        if (hc != null)
        {
            hc.Initialize(
                owner: this,
                ownerTransform: transform,
                damage: hammerDamage,
                knockback: hammerKnockbackForce,
                swingAngle: hammerSwingAngle,
                swingDuration: hammerSwingDuration,
                executeHealAmount: hammerExecuteHeal,
                localOffset: hammerSpawnOffset,
                speedCurve: hammerSwingCurve,
                enableExecution: enableExecution
            );
        }
        else
        {
            Destroy(go);
        }

        Collider2D hammerCol = go.GetComponent<Collider2D>();
        Collider2D playerCol = GetComponent<Collider2D>();
        if (hammerCol != null && playerCol != null)
        {
            Physics2D.IgnoreCollision(hammerCol, playerCol, true);
        }
    }

    public void TryRetrieveStake()
    {
        var allProjectiles = FindObjectsOfType<AttackProjectile>();

        Debug.Log($"[Combat] TryRetrieveStake: {allProjectiles.Length}개 투사체 발견");

        bool any = false;
        foreach (var proj in allProjectiles)
        {
            if (proj == null) continue;
            if (!proj.gameObject.activeInHierarchy) continue;

            proj.StartReturn(suppressAmmo: false, immediatePickup: false, useRetrievalBehavior: true);
            any = true;
        }

        if (!any) Debug.Log("[Combat] TryRetrieveStake: 회수 가능한 투사체 없음");
    }

    public void RecoverAmmo(int amount)
    {
        currentAmmo = Mathf.Min(currentAmmo + amount, maxAmmo);
        Debug.Log($"[Combat] 탄환 회복! ({currentAmmo}/{maxAmmo})");
    }

    public void OnExecutionSuccess(float healAmount, int ammoReward)
    {
        if (healthSystem != null && healAmount > 0f)
        {
            healthSystem.Heal(healAmount);
        }

        if (ammoReward > 0)
        {
            RecoverAmmo(ammoReward);
        }
    }

    public int GetCurrentAmmo() => currentAmmo;
    public int GetMaxAmmo() => maxAmmo;

    public void EnableChargeShot() => isChargeShotEnabled = true;
    public void DisableChargeShot() => isChargeShotEnabled = false;
    public void SetChargeShotEnabled(bool enabled) => isChargeShotEnabled = enabled;
    public bool IsChargeShotEnabled() => isChargeShotEnabled;

    public float ChargeTimeRequired => chargeTimeRequired;
    public float GetChargeProgress() => chargeTimeRequired > 0f ? Mathf.Clamp01(chargeTimer / chargeTimeRequired) : 1f;
    public bool IsCharging() => isCharging;
    public bool IsChargeReady() => isCharging && chargeTimer >= chargeTimeRequired;

    private Vector2 GetAimDirection()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return (mousePos - transform.position).normalized;
    }

    public void TryFireBloodStake()
    {
        if (fireTimer > 0) return;
        if (healthSystem == null) return;

        float hpCost = Mathf.Max(0f, healthSystem.GetMaxHealth() * bloodStakeHpPercent);

        if (healthSystem.GetCurrentHealth() <= hpCost)
        {
            Debug.Log("❌ HP가 부족하여 피 말뚝을 발사할 수 없습니다.");
            return;
        }

        healthSystem.TakeDamage(hpCost);
        fireTimer = fireRate;

        Vector2 dir = GetAimDirection();

        ProjectileConfig testConfig = null;
        if (testController != null)
        {
            testConfig = testController.GetCurrentConfig();
        }

        if (AttackManager.Instance != null)
        {
            AttackManager.Instance.FireStake(
                origin: transform.position,
                direction: dir,
                damage: woodDamage,
                isBloodStake: true,
                attacker: transform,
                bloodHpCost: hpCost,
                configOverride: testConfig
            );
        }

        Debug.Log($"[Combat] 피 말뚝 발사! HP -{hpCost:F1}");
    }

    // ==================== 우클릭 공격 시스템 (DeckManager 연동) ====================

    /// <summary>
    /// 우클릭 누름 - 그로기 검증 및 스킬 사용
    /// </summary>
    public void OnSecondaryDown()
    {
        if (hammerTimer > 0) return;

        Collider2D targetEnemy = CheckForExecutionTarget();

        if (targetEnemy != null)
        {
            TryFireSecondaryChargedAttack();
            Debug.Log($"[Combat] 처형 대상 발견: {targetEnemy.name}");
        }
        else
        {
            TrySwingHammer(enableExecution: false);
            Debug.Log("[Combat] 처형 대상 없음 - 기본 망치 공격 실행");
        }
    }

    private Collider2D CheckForExecutionTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, executionRange, enemyLayer);

        Collider2D closestEnemy = null;
        float closestDist = float.MaxValue;

        Vector2 aimDir = GetAimDirection();

        foreach (var hit in hits)
        {
            Vector2 targetDir = (hit.transform.position - transform.position).normalized;
            float angleToTarget = Vector2.Angle(aimDir, targetDir);

            if (angleToTarget <= executionAngle / 2f)
            {
                var enemyCtrl = hit.GetComponent<EnemyController>() ?? hit.GetComponentInParent<EnemyController>();
                if (enemyCtrl != null && enemyCtrl.IsGroggy())
                {
                    float dist = Vector2.Distance(transform.position, hit.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestEnemy = hit;
                    }
                }
            }
        }
        return closestEnemy;
    }

    /// <summary>
    /// 우클릭 처형 공격 실행 (DeckManager 연동)
    /// </summary>
    private void TryFireSecondaryChargedAttack()
    {
        if (hammerTimer > 0) return;

        // DeckManager 체크
        if (deckManager == null)
        {
            Debug.LogWarning("[Combat] DeckManager가 없어 스킬을 사용할 수 없습니다!");
            return;
        }

        // 현재 준비된 스킬 확인 (핸드의 첫 번째 카드)
        Card readyCard = deckManager.GetReadySkillCard();

        if (readyCard == null)
        {
            Debug.LogWarning("[Combat] 준비된 스킬 카드가 없습니다!");
            return;
        }

        // 스킬 타입이 다르면 자동 장착
        if (readyCard.skillType != currentSkillType)
        {
            if (!EquipSkill(readyCard.skillType))
            {
                Debug.LogWarning($"[Combat] 스킬 장착 실패: {readyCard.skillType}");
                return;
            }
        }

        // 스킬 실행
        if (currentSecondaryChargedAttack == null)
        {
            Debug.LogWarning("[Combat] 장착된 스킬 컴포넌트가 없습니다!");
            return;
        }

        hammerTimer = hammerCooldown;

        // 전략패턴 Execute 호출
        currentSecondaryChargedAttack.Execute(this, transform);

        Debug.Log($"[Combat] 처형 스킬 발동: {readyCard.cardName} ({readyCard.skillType})");

        // DeckManager에 카드 사용 알림
        bool success = deckManager.UseCard(0);

        if (success)
        {
            Debug.Log($"[Combat] 스킬 카드 사용 완료. 다음 스킬로 교체됨.");
        }
        else
        {
            Debug.LogWarning($"[Combat] 스킬 카드 사용 실패!");
        }
    }

    public void SetSecondaryChargedAttack(ISecondaryChargedAttack attack)
    {
        currentSecondaryChargedAttack = attack;
        Debug.Log($"[Combat] 우클릭 처형 공격 교체: {attack?.GetAttackName()}");
    }

    // ==================== 스킬 관리 시스템 ====================

    /// <summary>
    /// 스킬 장착 (enum 타입으로 스킬 변경)
    /// </summary>
    public bool EquipSkill(SecondaryChargedAttackType skillType)
    {
        if (skillDictionary == null)
        {
            Debug.LogWarning("[Combat] skillDictionary가 초기화되지 않았습니다.");
            return false;
        }

        if (!skillDictionary.ContainsKey(skillType))
        {
            Debug.LogWarning($"[Combat] 스킬 타입 '{skillType}'을(를) 보유하고 있지 않습니다.");
            return false;
        }

        currentSecondaryChargedAttack = skillDictionary[skillType];
        currentSkillType = skillType;

        Debug.Log($"[Combat] 스킬 장착: {skillType} - {currentSecondaryChargedAttack.GetAttackName()}");
        return true;
    }

    public SecondaryChargedAttackType GetCurrentSkillType()
    {
        return currentSkillType;
    }

    public bool HasSkill(SecondaryChargedAttackType skillType)
    {
        return skillDictionary != null && skillDictionary.ContainsKey(skillType);
    }

    public string GetCurrentSkillName()
    {
        if (currentSecondaryChargedAttack != null)
        {
            return currentSecondaryChargedAttack.GetAttackName();
        }
        return "없음";
    }

    // ==================== 디버그 ====================

#if UNITY_EDITOR
    [ContextMenu("현재 스킬 정보")]
    private void PrintCurrentSkillInfo()
    {
        Debug.Log($"=== PlayerCombat 스킬 정보 ===");
        Debug.Log($"현재 장착된 스킬: {currentSkillType} ({GetCurrentSkillName()})");

        if (deckManager != null)
        {
            Debug.Log($"\n[DeckManager 연동 상태]");

            Card readyCard = deckManager.GetReadySkillCard();
            if (readyCard != null)
            {
                Debug.Log($"준비된 스킬 (핸드[0]): {readyCard.skillType} - {readyCard.cardName}");
            }
            else
            {
                Debug.Log($"준비된 스킬: 없음 (핸드 비어있음)");
            }

            Card nextCard = deckManager.GetNextSkillCard();
            if (nextCard != null)
            {
                Debug.Log($"다음 스킬 (덱[0]): {nextCard.skillType} - {nextCard.cardName}");
            }
            else
            {
                Debug.Log($"다음 스킬: 없음 (덱 비어있음)");
            }

            Debug.Log($"\n덱 상태:");
            Debug.Log($"  덱: {deckManager.GetDeckCount()}장");
            Debug.Log($"  핸드: {deckManager.GetHandCount()}장");
            Debug.Log($"  버린 더미: {deckManager.GetDiscardPileCount()}장");
        }
        else
        {
            Debug.LogWarning("DeckManager가 연결되지 않았습니다!");
        }
    }

    [ContextMenu("테스트: 스킬 사용")]
    private void TestUseSkill()
    {
        TryFireSecondaryChargedAttack();
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, executionRange);

        Vector3 rightDir = Quaternion.Euler(0, 0, executionAngle * 0.5f) * Vector3.right;
        Vector3 leftDir = Quaternion.Euler(0, 0, -executionAngle * 0.5f) * Vector3.right;

        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawRay(transform.position, rightDir * executionRange);
        Gizmos.DrawRay(transform.position, leftDir * executionRange);
    }
}