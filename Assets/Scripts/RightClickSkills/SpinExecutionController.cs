using UnityEngine;
using System.Collections;

public class SpinExecutionController : MonoBehaviour
{
    private bool isSpinning = false;
    private GameObject currentHammer;
    private PlayerCombat playerCombat;

    private float duration;
    private float rotationsPerSecond;
    private float normalDamage;
    private float executeHealAmount;
    private int executeAmmoReward;

    public void StartSpin(
        float duration,
        float rotationsPerSecond,
        GameObject hammerPrefab,
        float hammerDistance,
        float normalDamage,
        float executeHealAmount,
        int executeAmmoReward,
        PlayerCombat playerCombat)
    {
        if (isSpinning)
        {
            Debug.LogWarning("[SpinExecution] 이미 회전 중입니다!");
            return;
        }

        this.duration = duration;
        this.rotationsPerSecond = rotationsPerSecond;
        this.normalDamage = normalDamage;
        this.executeHealAmount = executeHealAmount;
        this.executeAmmoReward = executeAmmoReward;
        this.playerCombat = playerCombat;

        StartCoroutine(SpinRoutine(hammerPrefab, hammerDistance));
    }

    private IEnumerator SpinRoutine(GameObject hammerPrefab, float hammerDistance)
    {
        isSpinning = true;

        Debug.Log($"[SpinExecution] 팽이 처형 시작! ({duration}초)");

        // 망치 생성
        if (hammerPrefab != null)
        {
            Vector3 hammerOffset = new Vector3(hammerDistance, 0f, 0f);
            currentHammer = Instantiate(hammerPrefab, transform.position + hammerOffset, Quaternion.identity);
            currentHammer.transform.SetParent(transform);

            // SpinHammerCollision 컴포넌트 추가
            SpinHammerCollision hammerCollision = currentHammer.GetComponent<SpinHammerCollision>();
            if (hammerCollision == null)
            {
                hammerCollision = currentHammer.AddComponent<SpinHammerCollision>();
            }

            hammerCollision.Initialize(this, normalDamage, executeHealAmount, executeAmmoReward, playerCombat);

            // 플레이어와 망치 충돌 무시
            Collider2D hammerCol = currentHammer.GetComponent<Collider2D>();
            Collider2D playerCol = GetComponent<Collider2D>();
            if (hammerCol != null && playerCol != null)
            {
                Physics2D.IgnoreCollision(hammerCol, playerCol, true);
            }
        }

        // 회전 시작
        float elapsed = 0f;
        float degreesPerSecond = 360f * rotationsPerSecond;

        while (elapsed < duration)
        {
            float rotationThisFrame = degreesPerSecond * Time.deltaTime;
            transform.Rotate(0f, 0f, rotationThisFrame);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 정리
        if (currentHammer != null)
        {
            Destroy(currentHammer);
        }

        transform.rotation = Quaternion.identity;

        isSpinning = false;
        Debug.Log("[SpinExecution] 팽이 처형 종료!");
    }

    public bool IsSpinning() => isSpinning;
}