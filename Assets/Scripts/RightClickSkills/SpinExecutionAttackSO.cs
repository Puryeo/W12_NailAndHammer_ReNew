using UnityEngine;

[CreateAssetMenu(fileName = "SpinExecutionAttack", menuName = "Skills/Secondary Charged Attack/Spin Execution")]
public class SpinExecutionAttackSO : ScriptableObject, ISecondaryChargedAttack
{
    [Header("��ų �⺻ ����")]
    [SerializeField] private float duration = 3f;
    [SerializeField] private float rotationsPerSecond = 3f;

    [Header("��ġ ����")]
    [SerializeField] private GameObject hammerPrefab;
    [SerializeField] private float hammerDistance = 1.2f;

    [Header("������ ����")]
    [SerializeField] private float normalDamage = 50f;
    [SerializeField] private float executeHealAmount = 30f;
    [SerializeField] private int executeAmmoReward = 2;

    public string GetAttackName() => "���� ó��";

    public SecondaryChargedAttackType GetSkillType()
    {
        return SecondaryChargedAttackType.None;
    }

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