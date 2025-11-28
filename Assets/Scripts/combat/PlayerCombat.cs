using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PlayerCombat (Silver Nail)
/// - LMB: 탭 = 나무 말뚝 발사
/// - LMB hold+release: 차지샷
/// - RMB: 망치 휘두르기 (처형) — now uses Hammer prefab rotation swing
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Status")]
    [SerializeField] private HealthSystem healthSystem;

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
    [SerializeField] private GameObject hammerPrefab; // 프리팹을 인스펙터에서 할당하세요
    [Tooltip("해머가 스폰될 로컬 오프셋 (플레이어 기준)")]
    [SerializeField] private Vector2 hammerSpawnOffset = new Vector2(0.8f, 0f);
    [Tooltip("휘두를 각도(총 회전각)")]
    [SerializeField] private float hammerSwingAngle = 120f;
    [Tooltip("휘두르는 시간(초)")]
    [SerializeField] private float hammerSwingDuration = 0.28f;

    // 복원: 해머 동작에 필요한 필드들 (컴파일 에러 방지)
    [SerializeField] private float hammerDamage = 10f; // 약한 기본 피해
    [SerializeField] private float hammerCooldown = 1.5f;
    [SerializeField] private float hammerKnockbackForce = 8f;
    [SerializeField] private float hammerExecuteHeal = 20f; // 처형 시 회복량 (설정 가능)

    // 추가: 애니메이션 커브로 스윙 속도 조절
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
    [Tooltip("테스트 모드 활성화 (1,2,3,4 키로 스킬 교체)")]
    [SerializeField] private bool enableSkillTestMode = false;

    [Header("Skill: Chain Retrieve")]
    [SerializeField] private float retrieveRange = 10f; // 회수 가능 거리
    [SerializeField] private LayerMask projectileLayer; // 투사체 레이어

    // 내부 쿨타임 변수
    private float fireTimer = 0f;
    private float hammerTimer = 0f;

    // 차지 상태
    private bool isCharging = false;
    private float chargeTimer = 0f;

    // 테스트용 (선택적)
    private ProjectileTestController testController;

    private void Awake()
    {
        if (healthSystem == null) healthSystem = GetComponent<HealthSystem>();
        currentAmmo = maxAmmo;

        // 테스트 컨트롤러 찾기 (있으면)
        testController = GetComponent<ProjectileTestController>();

        // 스킬 Dictionary 초기화
        InitializeSkillDictionary();

        // 기존 방식 호환 (currentSecondaryChargedAttackComponent)
        if(currentSecondaryChargedAttackComponent == null)
        {
            var tempClass = GetComponent<ISecondaryChargedAttack>();
            currentSecondaryChargedAttackComponent = tempClass as MonoBehaviour;
        }
        // 우클릭 차징공격 초기화
        if (currentSecondaryChargedAttackComponent != null)
        {
            if (currentSecondaryChargedAttackComponent is ISecondaryChargedAttack attackComponent)
            {
                currentSecondaryChargedAttack = attackComponent;
                currentSkillType = attackComponent.GetSkillType();
                Debug.Log($"[Combat] 우클릭 차징공격 초기화(Component): {currentSecondaryChargedAttack.GetAttackName()}, Type: {currentSkillType}");
            }
            else
            {
                Debug.LogWarning("[Combat] currentSecondaryChargedAttackComponent가 ISecondaryChargedAttack를 구현하지 않습니다.");
            }
        }
        else
        {
            // currentSecondaryChargedAttackComponent가 없으면 Dictionary에서 첫 번째 None이 아닌 스킬 장착
            EquipFirstAvailableSkill();
        }
    }

    /// <summary>
    /// 모든 ISecondaryChargedAttack 컴포넌트를 수집하여 Dictionary에 등록
    /// </summary>
    private void InitializeSkillDictionary()
    {
        skillDictionary = new Dictionary<SecondaryChargedAttackType, ISecondaryChargedAttack>();

        // 이 게임오브젝트에 붙어있는 모든 ISecondaryChargedAttack 구현체를 찾음
        MonoBehaviour[] components = GetComponents<MonoBehaviour>();

        foreach (var comp in components)
        {
            if (comp is ISecondaryChargedAttack skill)
            {
                SecondaryChargedAttackType skillType = skill.GetSkillType();

                // 이미 등록된 타입이면 경고
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

        // None이 아닌 첫 번째 스킬을 찾아서 장착
        foreach (var kvp in skillDictionary)
        {
            if (kvp.Key != SecondaryChargedAttackType.None)
            {
                EquipSkill(kvp.Key);
                Debug.Log($"[Combat] 기본 스킬 자동 장착: {kvp.Key}");
                return;
            }
        }

        // None만 있는 경우
        if (skillDictionary.ContainsKey(SecondaryChargedAttackType.None))
        {
            EquipSkill(SecondaryChargedAttackType.None);
            Debug.Log("[Combat] None 스킬 장착");
        }
    }

    private void Update()
    {
        // 쿨타임 감소
        if (fireTimer > 0) fireTimer -= Time.deltaTime;
        if (hammerTimer > 0) hammerTimer -= Time.deltaTime;

        // 좌클릭 차지 진행 (PlayerController에서 OnPrimaryDown/Up으로 제어)
        if (isCharging)
        {
            chargeTimer += Time.deltaTime;
        }

        // 테스트 모드: 5,6,7,8 키로 스킬 교체
        if (enableSkillTestMode)
        {
            HandleSkillTestInput();
        }
    }

    /// <summary>
    /// 테스트 모드 키 입력 처리 (1,2,3,4 키로 스킬 교체)
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

    // 입력 연동 API (PlayerController가 호출)
    public void OnPrimaryDown()
    {
        // 시작: 차지 상태로 전환
        if (fireTimer > 0) return; // 발사 쿨타임 중이면 무시
        isCharging = true;
        chargeTimer = 0f;
    }

    public void OnPrimaryUp()
    {
        if (!isCharging) return;
        isCharging = false;

        // 차징샷이 활성화되어 있고 차지 시간을 충족한 경우
        if (isChargeShotEnabled && chargeTimer >= chargeTimeRequired)
        {
            TryFireChargedStake();
        }
        else
        {
            TryFireWoodStake();
        }
    }

    /// <summary>
    /// 일반 나무 말뚝 발사 (좌클릭 탭)
    /// - 탄약이 0이면 BloodStake 시도
    /// - ProjectileTestController가 있으면 해당 Config로 발사
    /// </summary>
    public void TryFireWoodStake()
    {
        if (fireTimer > 0) return;

        if (currentAmmo <= 0)
        {
            // 탄약이 없으면 HP 기반 발사 시도
            TryFireBloodStake();
            return;
        }

        fireTimer = fireRate;
        currentAmmo--; // 탄환 소모

        Vector2 dir = GetAimDirection();

        // 테스트 모드: ProjectileTestController가 있으면 선택된 Config 사용
        ProjectileConfig testConfig = null;
        if (testController != null)
        {
            testConfig = testController.GetCurrentConfig();
        }

        // AttackManager로 발사
        if (AttackManager.Instance != null)
        {
            AttackManager.Instance.FireStake(
                origin: transform.position,
                direction: dir,
                damage: woodDamage,
                isBloodStake: false,
                attacker: transform,
                bloodHpCost: 0f,
                configOverride: testConfig  // 테스트 config 전달 (null이면 기본 동작)
            );
        }
        else
        {
            Debug.LogWarning("AttackManager.Instance 없음 - 기본 발사 로직이 실행되지 않음");
        }

        Debug.Log($"[Combat] 나무 말뚝 발사! 남은 탄환: {currentAmmo}/{maxAmmo}" +
                  (testConfig != null ? $" [테스트 모드 {testController.GetCurrentMode()}]" : ""));
    }

    private void TryFireChargedStake()
    {
        if (fireTimer > 0) return;

        int chargedAmmoCost = AttackManager.Instance != null ? AttackManager.Instance.chargedAmmoCost : 1;

        // 탄약이 충분한 경우: 일반 차징샷
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
            else
            {
                Debug.LogWarning("AttackManager.Instance 없음 - 차징샷 발사 실패");
            }

            Debug.Log($"[Combat] 차지샷 발사! 탄약 -{chargedAmmoCost} (남음: {currentAmmo}/{maxAmmo})");
        }
        // 탄약이 부족한 경우: 블러드 차징샷 시도
        else
        {
            TryFireBloodChargedStake();
        }
    }

    /// <summary>
    /// 블러드 차징샷: HP를 소모하여 차징샷 발사
    /// </summary>
    private void TryFireBloodChargedStake()
    {
        if (healthSystem == null)
        {
            Debug.LogWarning("TryFireBloodChargedStake: HealthSystem이 할당되지 않았습니다.");
            return;
        }

        int chargedAmmoCost = AttackManager.Instance != null ? AttackManager.Instance.chargedAmmoCost : 1;

        // HP 비용: (일반 말뚝 HP 비용) × (차징샷 탄약 소모량)
        // 예: 일반 말뚝 = 최대체력 10%, 차징샷 탄약 10개 → 블러드 차징샷 = 최대체력 100%
        float hpCost = Mathf.Max(0f, healthSystem.GetMaxHealth() * bloodStakeHpPercent * chargedAmmoCost);

        // 자살 방지: 현재 HP가 비용보다 커야 발사 가능
        if (healthSystem.GetCurrentHealth() <= hpCost)
        {
            Debug.Log("❌ HP가 부족하여 블러드 차징샷을 발사할 수 없습니다.");
            return;
        }

        // 자기 체력 소모
        healthSystem.TakeDamage(hpCost);

        // 발사 쿨다운 적용
        fireTimer = fireRate;

        Vector2 dir = GetAimDirection();

        // 블러드 차징샷 발사
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
        else
        {
            Debug.LogWarning("AttackManager.Instance 없음 - 블러드 차징샷 발사 실패");
        }

        Debug.Log($"[Combat] 블러드 차징샷 발사! HP -{hpCost:F1} (남은 HP: {healthSystem.GetCurrentHealth():F1})");
    }

    /// <summary>
    /// 망치 휘두르기 (RMB/E) - 프리팹 기반 회전 휘두르기
    /// </summary>
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

        // 스폰 위치 계산 (월드) — 플레이어 회전을 고려하여 로컬 오프셋을 월드로 변환
        Vector3 spawnPos = transform.position + (Vector3)(transform.rotation * hammerSpawnOffset);

        GameObject go = Instantiate(hammerPrefab, spawnPos, Quaternion.identity);
        var hc = go.GetComponent<HammerSwingController>();
        if (hc != null)
        {
            // 로컬 오프셋과 커브를 함께 전달
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
            Debug.LogWarning("hammerPrefab에 HammerSwingController 컴포넌트가 없습니다.");
            Destroy(go);
        }

        // --- 새로 추가: 플레이어와 해머 충돌 무시 설정 ---
        Collider2D hammerCol = go.GetComponent<Collider2D>();
        Collider2D playerCol = GetComponent<Collider2D>();
        if (hammerCol != null && playerCol != null)
        {
            Physics2D.IgnoreCollision(hammerCol, playerCol, true);
        }
        // (기본적으로 해머 프리팹에 Rigidbody2D.BodyType = Kinematic 설정을 권장합니다.)
    }

    /// <summary>
    /// 사슬 회수 시도(예: R키)
    /// - 주변의 회수 가능한 AttackProjectile을 찾아 StartReturn() 호출
    /// </summary>
    public void TryRetrieveStake()
    {
        // StakeRetrievalSkill을 사용하지 않고 RetrievalBehavior를 직접 사용
        // (StuckEnemyPull 등 새로운 회수 로직 적용을 위해)

        // 모든 AttackProjectile 찾기
        var allProjectiles = FindObjectsOfType<AttackProjectile>();

        Debug.Log($"[Combat] TryRetrieveStake: {allProjectiles.Length}개 투사체 발견");

        bool any = false;
        foreach (var proj in allProjectiles)
        {
            if (proj == null) continue;
            if (!proj.gameObject.activeInHierarchy) continue;

            // RetrievalBehavior 사용하여 회수
            proj.StartReturn(suppressAmmo: false, immediatePickup: false, useRetrievalBehavior: true);
            any = true;
        }

        if (!any) Debug.Log("[Combat] TryRetrieveStake: 회수 가능한 투사체 없음");
    }

    /// <summary>
    /// 탄환 회복 (말뚝 회수 시 호출)
    /// </summary>
    public void RecoverAmmo(int amount)
    {
        currentAmmo = Mathf.Min(currentAmmo + amount, maxAmmo);
        Debug.Log($"[Combat] 탄환 회복! ({currentAmmo}/{maxAmmo})");
    }

    /// <summary>
    /// 처형 성공 시 Player에게 회복/탄환 보상을 주는 콜백
    /// - AttackManager/EnemyController/HammerSwingController에서 호출
    /// </summary>
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

    // 공개 접근자: 현재 탄약/최대 탄약
    public int GetCurrentAmmo() => currentAmmo;
    public int GetMaxAmmo() => maxAmmo;

    // 차징샷 활성화/비활성화
    public void EnableChargeShot() => isChargeShotEnabled = true;
    public void DisableChargeShot() => isChargeShotEnabled = false;
    public void SetChargeShotEnabled(bool enabled) => isChargeShotEnabled = enabled;
    public bool IsChargeShotEnabled() => isChargeShotEnabled;

    /// <summary>
    /// 외부에서 읽을 수 있도록 차지 관련 상태/값 공개
    /// - AimingUI나 다른 시스템에서 chargeTimeRequired, 현재 진행도, 준비 상태를 조회할 수 있게 함.
    /// </summary>
    public float ChargeTimeRequired => chargeTimeRequired;
    public float GetChargeProgress() => chargeTimeRequired > 0f ? Mathf.Clamp01(chargeTimer / chargeTimeRequired) : 1f;
    public bool IsCharging() => isCharging;
    public bool IsChargeReady() => isCharging && chargeTimer >= chargeTimeRequired;

    /// <summary>
    /// 마우스 방향 계산 헬퍼
    /// </summary>
    private Vector2 GetAimDirection()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return (mousePos - transform.position).normalized;
    }

    // 추가: Shift+LMB(피 말뚝) 호출 대응 메서드 TryFireBloodStake()
    public void TryFireBloodStake()
    {
        if (fireTimer > 0) return;

        if (healthSystem == null)
        {
            Debug.LogWarning("TryFireBloodStake: HealthSystem이 할당되지 않았습니다.");
            return;
        }

        // HP 비용: 최대체력의 percent
        float hpCost = Mathf.Max(0f, healthSystem.GetMaxHealth() * bloodStakeHpPercent);

        // 자살 방지: 현재 HP가 비용보다 커야 발사 가능
        if (healthSystem.GetCurrentHealth() <= hpCost)
        {
            Debug.Log("❌ HP가 부족하여 피 말뚝을 발사할 수 없습니다.");
            return;
        }

        // 자기 체력 소모
        healthSystem.TakeDamage(hpCost);

        // 발사 쿨다운 적용
        fireTimer = fireRate;

        Vector2 dir = GetAimDirection();

        // 테스트 모드: ProjectileTestController가 있으면 선택된 Config 사용 (일반 stake와 동일)
        ProjectileConfig testConfig = null;
        if (testController != null)
        {
            testConfig = testController.GetCurrentConfig();
        }

        // 말뚝 발사: isBloodStake = true 로 구분하여 처리, bloodHpCost 전달
        if (AttackManager.Instance != null)
        {
            AttackManager.Instance.FireStake(
                origin: transform.position,
                direction: dir,
                damage: woodDamage,
                isBloodStake: true,
                attacker: transform,
                bloodHpCost: hpCost,
                configOverride: testConfig  // 테스트 config 전달 (null이면 기본 동작)
            );
        }
        else
        {
            Debug.LogWarning("AttackManager.Instance 없음 - 피 말뚝 발사 실패");
        }

        Debug.Log($"[Combat] 피 말뚝 발사! HP -{hpCost:F1} (남은 HP: {healthSystem.GetCurrentHealth():F1})");
    }

    // ==================== 우클릭 공격 시스템 ====================

    /// <summary>
    /// 우클릭 누름 - 그로기 검증 시작
    /// </summary>
    public void OnSecondaryDown()
    {
        if (hammerTimer > 0) return; // 망치 쿨타임 체크
        /*        isSecondaryCharging = true;
                secondaryChargeTimer = 0f;*/

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

        // 현재 마우스 방향
        Vector2 aimDir = GetAimDirection();

        foreach(var hit in hits)
        {
            // 타겟 방향 벡터
            Vector2 targetDir = (hit.transform.position - transform.position).normalized;
            // 각도 계산
            float angleToTarget = Vector2.Angle(aimDir, targetDir);

            if (angleToTarget <= executionAngle / 2f)
            {
                // 그로기 상태인지 확인
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
    /// 우클릭 처형 공격 실행 (전략패턴)
    /// </summary>
    private void TryFireSecondaryChargedAttack()
    {
        if (hammerTimer > 0) return;

        if (currentSecondaryChargedAttack == null)
        {
            Debug.LogWarning("현재 장착된 우클릭 처형 공격 없습니다.");
            return;
        }

        hammerTimer = hammerCooldown; // 쿨타임 적용

        // 전략패턴 Execute 호출
        currentSecondaryChargedAttack.Execute(this, transform);

        Debug.Log($"[Combat] 우클릭 처형 공격 발사! ({currentSecondaryChargedAttack.GetAttackName()})");
    }

    /// <summary>
    /// 우클릭 처형 공격 교체 (외부 업그레이드 시스템에서 호출)
    /// </summary>
    public void SetSecondaryChargedAttack(ISecondaryChargedAttack attack)
    {
        currentSecondaryChargedAttack = attack;
        Debug.Log($"[Combat] 우클릭 처형 공격 교체: {attack?.GetAttackName()}");
    }

    // ==================== 스킬 관리 시스템 ====================

    /// <summary>
    /// 스킬 장착 (enum 타입으로 스킬 변경)
    /// </summary>
    /// <param name="skillType">장착할 스킬 타입</param>
    /// <returns>장착 성공 여부</returns>
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

        // 상태패턴: 레퍼런스 캐싱
        currentSecondaryChargedAttack = skillDictionary[skillType];
        currentSkillType = skillType;

        Debug.Log($"[Combat] 스킬 장착: {skillType} - {currentSecondaryChargedAttack.GetAttackName()}");
        return true;
    }

    /// <summary>
    /// 현재 장착된 스킬 타입 반환
    /// </summary>
    public SecondaryChargedAttackType GetCurrentSkillType()
    {
        return currentSkillType;
    }

    /// <summary>
    /// 특정 스킬을 보유하고 있는지 확인
    /// </summary>
    public bool HasSkill(SecondaryChargedAttackType skillType)
    {
        return skillDictionary != null && skillDictionary.ContainsKey(skillType);
    }

    /// <summary>
    /// 현재 장착된 스킬 이름 반환 (디버그/UI용)
    /// </summary>
    public string GetCurrentSkillName()
    {
        if (currentSecondaryChargedAttack != null)
        {
            return currentSecondaryChargedAttack.GetAttackName();
        }
        return "없음";
    }

    /// <summary>
    /// 에디터에서 감지 범위 시각화 (디버깅용)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // 원 그리기
        Gizmos.DrawWireSphere(transform.position, executionRange);

        // 부채꼴 라인 그리기 (현재 바라보는 방향 기준이 아니므로 대략적인 확인용)
        Vector3 rightDir = Quaternion.Euler(0, 0, executionAngle * 0.5f) * Vector3.right;
        Vector3 leftDir = Quaternion.Euler(0, 0, -executionAngle * 0.5f) * Vector3.right;

        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawRay(transform.position, rightDir * executionRange);
        Gizmos.DrawRay(transform.position, leftDir * executionRange);
    }
}
