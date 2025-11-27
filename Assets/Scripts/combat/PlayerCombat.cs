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
    [Tooltip("우클릭 차징 시간 (초)")]
    [SerializeField] private float secondaryChargeTimeRequired = 1.0f;

    [Tooltip("우클릭 차징공격 활성화 여부")]
    [SerializeField] private bool isSecondaryChargeShotEnabled = true;

    [Tooltip("현재 장착된 우클릭 차징공격 (ScriptableObject)")]
    [SerializeField] private ScriptableObject currentSecondaryChargedAttackSO;

    // 내부 상태
    private bool isSecondaryCharging = false;
    private float secondaryChargeTimer = 0f;
    private ISecondaryChargedAttack currentSecondaryChargedAttack;

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

        // 우클릭 차징공격 초기화
        if (currentSecondaryChargedAttackSO != null && currentSecondaryChargedAttackSO is ISecondaryChargedAttack)
        {
            currentSecondaryChargedAttack = currentSecondaryChargedAttackSO as ISecondaryChargedAttack;
            Debug.Log($"[Combat] 우클릭 차징공격 초기화: {currentSecondaryChargedAttack.GetAttackName()}");
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

        // 우클릭 차지 진행
        if (isSecondaryCharging)
        {
            secondaryChargeTimer += Time.deltaTime;
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
    /// 우클릭 누름 - 차징 시작
    /// </summary>
    public void OnSecondaryDown()
    {
        if (hammerTimer > 0) return; // 망치 쿨타임 체크
        isSecondaryCharging = true;
        secondaryChargeTimer = 0f;
    }

    /// <summary>
    /// 우클릭 뗌 - 망치(처형 X) or 차징공격(처형 O)
    /// </summary>
    public void OnSecondaryUp()
    {
        if (!isSecondaryCharging) return;
        isSecondaryCharging = false;

        // 차징 시간 충족 여부에 따라 분기
        if (isSecondaryChargeShotEnabled && secondaryChargeTimer >= secondaryChargeTimeRequired)
        {
            TryFireSecondaryChargedAttack();
        }
        else
        {
            TrySwingHammer(enableExecution: false); // 기본 망치 (처형 X)
        }
    }

    /// <summary>
    /// 우클릭 차징공격 실행 (전략패턴)
    /// </summary>
    private void TryFireSecondaryChargedAttack()
    {
        if (hammerTimer > 0) return;

        if (currentSecondaryChargedAttack == null)
        {
            Debug.LogWarning("현재 장착된 우클릭 차징공격이 없습니다.");
            return;
        }

        hammerTimer = hammerCooldown; // 쿨타임 적용

        // 전략패턴 Execute 호출
        currentSecondaryChargedAttack.Execute(this, transform);

        Debug.Log($"[Combat] 우클릭 차징공격 발사! ({currentSecondaryChargedAttack.GetAttackName()})");
    }

    /// <summary>
    /// 우클릭 차징공격 교체 (외부 업그레이드 시스템에서 호출)
    /// </summary>
    public void SetSecondaryChargedAttack(ISecondaryChargedAttack attack)
    {
        currentSecondaryChargedAttack = attack;
        Debug.Log($"[Combat] 우클릭 차징공격 교체: {attack?.GetAttackName()}");
    }

    /// <summary>
    /// 우클릭 차징공격 상태 조회 (UI용)
    /// </summary>
    public float GetSecondaryChargeProgress() => secondaryChargeTimeRequired > 0f ? Mathf.Clamp01(secondaryChargeTimer / secondaryChargeTimeRequired) : 1f;
    public bool IsSecondaryCharging() => isSecondaryCharging;
    public bool IsSecondaryChargeReady() => isSecondaryCharging && secondaryChargeTimer >= secondaryChargeTimeRequired;
}