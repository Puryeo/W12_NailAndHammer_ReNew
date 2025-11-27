using UnityEngine;

/// <summary>
/// 우클릭 차징공격 B
/// - 차징공격 A와 다른 설정 사용 (예: 더 긴 스윙, 더 큰 데미지)
/// </summary>
[CreateAssetMenu(fileName = "SecondaryChargedAttackB", menuName = "Combat/Secondary Charged Attack B")]
public class SecondaryChargedAttackB : ScriptableObject, ISecondaryChargedAttack
{
    [Header("Hammer Settings")]
    [Tooltip("망치 프리팹 (HammerSwingController 컴포넌트 필요)")]
    [SerializeField] private GameObject hammerPrefab;

    [Tooltip("해머가 스폰될 로컬 오프셋")]
    [SerializeField] private Vector2 hammerSpawnOffset = new Vector2(1.5f, 0f);

    [Tooltip("휘두를 각도(총 회전각)")]
    [SerializeField] private float hammerSwingAngle = 180f;

    [Tooltip("휘두르는 시간(초)")]
    [SerializeField] private float hammerSwingDuration = 0.4f;

    [Header("Damage & Effects")]
    [Tooltip("공격 데미지")]
    [SerializeField] private float damage = 50f;

    [Tooltip("넉백 강도")]
    [SerializeField] private float knockbackForce = 15f;

    [Tooltip("처형 시 플레이어 회복량")]
    [SerializeField] private float executeHealAmount = 40f;

    [Header("Animation")]
    [Tooltip("스윙 속도 조절 커브")]
    [SerializeField] private AnimationCurve hammerSwingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public void Execute(PlayerCombat owner, Transform ownerTransform)
    {
        if (hammerPrefab == null)
        {
            Debug.LogWarning($"[{GetAttackName()}] hammerPrefab이 할당되지 않았습니다.");
            return;
        }

        // 스폰 위치 계산
        Vector3 spawnPos = ownerTransform.position + (Vector3)(ownerTransform.rotation * hammerSpawnOffset);

        // 망치 생성
        GameObject go = Object.Instantiate(hammerPrefab, spawnPos, Quaternion.identity);
        var hc = go.GetComponent<HammerSwingController>();

        if (hc != null)
        {
            // 망치 초기화 (처형 활성화)
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
                enableExecution: true // 처형 활성화
            );

            // 플레이어와 해머 충돌 무시
            Collider2D hammerCol = go.GetComponent<Collider2D>();
            Collider2D playerCol = ownerTransform.GetComponent<Collider2D>();
            if (hammerCol != null && playerCol != null)
            {
                Physics2D.IgnoreCollision(hammerCol, playerCol, true);
            }
        }
        else
        {
            Debug.LogWarning($"[{GetAttackName()}] hammerPrefab에 HammerSwingController 컴포넌트가 없습니다.");
            Object.Destroy(go);
        }
    }

    public string GetAttackName() => "차징공격 B";
}
