using UnityEngine;

/// <summary>
/// 플레이어에게 직접 붙여서 사용하는 스킬 컴포넌트.
/// 인터페이스를 상속받아 Execute 시 '내려찍는 해머'를 소환합니다.
/// </summary>
public class SplashSkill : MonoBehaviour, ISecondaryChargedAttack
{
    [Header("Hammer Settings")]
    [Tooltip("SplashHammerController가 붙어있는 해머 프리팹")]
    [SerializeField] private GameObject splashHammerPrefab;

    [Tooltip("해머 소환 위치 오프셋 (플레이어 기준)")]
    [SerializeField] private Vector2 spawnOffset = new Vector2(0.5f, 0.5f);

    [Tooltip("내려찍는 속도(시간)")]
    [SerializeField] private float smashDuration = 0.2f;

    [Tooltip("타격 범위 반경")]
    [SerializeField] private float impactRadius = 2.0f;

    [Header("Stats")]
    [Tooltip("일반 공격 데미지")]
    [SerializeField] private float damage = 40f;

    [Tooltip("일반 공격 넉백 힘")]
    [SerializeField] private float knockbackForce = 15f;

    [Header("Projectile Settings")]
    [Tooltip("소환될 투사체 프리팹 (SplashProjectile)")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("투사체 발사 속도")]
    [SerializeField] private float projectileSpeed = 10f;

    [Tooltip("투사체 지속 시간")]
    [SerializeField] private float projectileLifetime = 2f;

    [Tooltip("투사체가 일반 적에게 주는 데미지 (그로기로 만듦)")]
    [SerializeField] private float projectileDamageToNormal = 50f;

    [Tooltip("투사체가 그로기 적에게 주는 데미지 (처형)")]
    [SerializeField] private float projectileDamageToGroggy = 999f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // 인터페이스 구현
    public void Execute(PlayerCombat owner, Transform ownerTransform)
    {
        if (splashHammerPrefab == null)
        {
            Debug.LogWarning("[SplashSkill] splashHammerPrefab이 연결되지 않았습니다!");
            return;
        }

        // 마우스 방향 계산
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = (mousePos - ownerTransform.position).normalized;
        float aimAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 해머 소환 위치 및 회전
        Vector3 spawnPos = ownerTransform.position + (Vector3)(ownerTransform.rotation * spawnOffset);
        Quaternion spawnRot = ownerTransform.rotation;

        GameObject hammerObj = Instantiate(splashHammerPrefab, spawnPos, spawnRot, owner.transform);

        var controller = hammerObj.GetComponent<SplashHammerController>();
        if (controller != null)
        {
            controller.Initialize(
                owner: owner,
                damage: damage,
                knockback: knockbackForce,
                swingDuration: smashDuration,
                hitRadius: impactRadius,
                projectilePrefab: projectilePrefab,
                projectileSpeed: projectileSpeed,
                projectileLifetime: projectileLifetime,
                projectileDamageToNormal: projectileDamageToNormal,
                projectileDamageToGroggy: projectileDamageToGroggy,
                targetAngle: aimAngle,
                showDebugLogs: showDebugLogs
            );

            // 플레이어와 충돌 무시
            Collider2D hammerCol = hammerObj.GetComponent<Collider2D>();
            Collider2D playerCol = ownerTransform.GetComponent<Collider2D>();
            if (hammerCol != null && playerCol != null)
            {
                Physics2D.IgnoreCollision(hammerCol, playerCol, true);
            }
        }
        else
        {
            Debug.LogError("[SplashSkill] 프리팹에 SplashHammerController가 없습니다!");
        }
    }

    public string GetAttackName()
    {
        return "Splash Execution Skill";
    }

    public SecondaryChargedAttackType GetSkillType()
    {
        return SecondaryChargedAttackType.Splash;
    }
}