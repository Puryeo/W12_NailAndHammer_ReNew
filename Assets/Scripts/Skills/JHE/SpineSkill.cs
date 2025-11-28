using UnityEngine;

/// <summary>
/// 플레이어에게 직접 붙여서 사용하는 스킬 컴포넌트.
/// 인터페이스를 상속받아 Execute 시 '내려찍는 해머'를 소환합니다.
/// </summary>
public class SpineSkill : MonoBehaviour, ISecondaryChargedAttack
{
    [Header("New Hammer Settings")]
    [Tooltip("SpineHammerController가 붙어있는 '새로운' 해머 프리팹")]
    [SerializeField] private GameObject spineHammerPrefab;

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

    [Header("Spine Settings")]
    [Tooltip("소환될 가시 프리팹")]
    [SerializeField] private GameObject spinePrefab;
    [SerializeField] private int spineCount = 6;
    [SerializeField] private float spineRadius = 2.5f;

    // 인터페이스 구현
    public void Execute(PlayerCombat owner, Transform ownerTransform)
    {
        if (spineHammerPrefab == null)
        {
            Debug.LogWarning("[SpineSkill] SpineHammerPrefab이 연결되지 않았습니다!");
            return;
        }

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = (mousePos - ownerTransform.position).normalized;
        float aimAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 플레이어가 바라보는 방향 회전 적용
        Vector3 spawnPos = ownerTransform.position + (Vector3)(ownerTransform.rotation * spawnOffset);
        Quaternion spawnRot = ownerTransform.rotation; // 플레이어 회전 따라감

        GameObject hammerObj = Instantiate(spineHammerPrefab, spawnPos, spawnRot, owner.transform);

        var controller = hammerObj.GetComponent<SpineHammerController>();
        if (controller != null)
        {
            controller.Initialize(
                owner: owner,
                damage: damage,
                knockback: knockbackForce,
                swingDuration: smashDuration,
                hitRadius: impactRadius,
                spinePrefab: spinePrefab,
                spineCount: spineCount,
                spineRadius: spineRadius,
                targetAngle: aimAngle
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
            Debug.LogError("[SpineSkill] 프리팹에 SpineHammerController가 없습니다!");
        }
    }

    public string GetAttackName()
    {
        return "Spine Smash Skill";
    }

    public SecondaryChargedAttackType GetSkillType()
    {
        return SecondaryChargedAttackType.Thorns;
    }
}