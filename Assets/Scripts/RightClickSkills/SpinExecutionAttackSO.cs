using UnityEngine;

[CreateAssetMenu(fileName = "SpinExecutionAttack", menuName = "Skills/Secondary Charged Attack/Spin Execution")]
public class SpinExecutionAttackSO : ScriptableObject, ISecondaryChargedAttack
{
    [Header("스킬 기본 설정")]
    [SerializeField] private float duration = 3f;
    [SerializeField] private float rotationsPerSecond = 3f;

    [Header("망치 설정")]
    [SerializeField] private GameObject hammerPrefab;
    [SerializeField] private float hammerDistance = 1.2f;

    [Header("데미지 설정")]
    [SerializeField] private float normalDamage = 50f;
    [SerializeField] private float executeHealAmount = 30f;
    [SerializeField] private int executeAmmoReward = 2;

    public string GetAttackName() => "팽이 처형";

    public void Execute(PlayerCombat owner, Transform ownerTransform)
    {
        SpinExecutionController controller = ownerTransform.GetComponent<SpinExecutionController>();

        if (controller == null)
        {
            controller = ownerTransform.gameObject.AddComponent<SpinExecutionController>();
        }

        controller.StartSpin(
            duration: duration,
            rotationsPerSecond: rotationsPerSecond,
            hammerPrefab: hammerPrefab,
            hammerDistance: hammerDistance,
            normalDamage: normalDamage,
            executeHealAmount: executeHealAmount,
            executeAmmoReward: executeAmmoReward,
            playerCombat: owner
        );
    }
}