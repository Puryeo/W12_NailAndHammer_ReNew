using UnityEngine;

/// <summary>
/// 우클릭 차징공격 A (MonoBehaviour 버전)
/// - 기존 SO 기반 SecondaryChargedAttackA와 동일한 동작을 컴포넌트로 제공
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Combat/Secondary Charged Attack A (Mono)")]
public class SecondaryChargedAttackAComponent : MonoBehaviour, ISecondaryChargedAttack
{
    [Header("Hammer Settings")]
    [SerializeField] private GameObject hammerPrefab;
    [SerializeField] private Vector2 hammerSpawnOffset = new Vector2(1.2f, 0f);
    [SerializeField] private float hammerSwingAngle = 150f;
    [SerializeField] private float hammerSwingDuration = 0.35f;

    [Header("Damage & Effects")]
    [SerializeField] private float damage = 30f;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float executeHealAmount = 30f;

    [Header("Animation")]
    [SerializeField] private AnimationCurve hammerSwingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public void Execute(PlayerCombat owner, Transform ownerTransform)
    {
        if (hammerPrefab == null)
        {
            Debug.LogWarning($"[{GetAttackName()}] hammerPrefab이 할당되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = ownerTransform.position + (Vector3)(ownerTransform.rotation * hammerSpawnOffset);
        GameObject go = Instantiate(hammerPrefab, spawnPos, Quaternion.identity);
        var hc = go.GetComponent<HammerSwingController>();

        if (hc == null)
        {
            Debug.LogWarning($"[{GetAttackName()}] hammerPrefab에 HammerSwingController 컴포넌트가 없습니다.");
            Destroy(go);
            return;
        }

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
            enableExecution: true
        );

        IgnorePlayerHammerCollision(ownerTransform, go);
    }

    public string GetAttackName() => "차징공격 A (컴포넌트)";

    public SecondaryChargedAttackType GetSkillType()
    {
        return SecondaryChargedAttackType.None;
    }

    private void IgnorePlayerHammerCollision(Transform ownerTransform, GameObject hammer)
    {
        Collider2D hammerCol = hammer.GetComponent<Collider2D>();
        Collider2D playerCol = ownerTransform.GetComponent<Collider2D>();
        if (hammerCol != null && playerCol != null)
        {
            Physics2D.IgnoreCollision(hammerCol, playerCol, true);
        }
    }
}
