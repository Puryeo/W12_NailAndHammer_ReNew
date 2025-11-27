using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class SpineSpike : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float impaleDuration = 3.0f; // 속박 시간
    [SerializeField] private float riseSpeed = 8f;        // 솟아오르는 속도
    [SerializeField] private float lifeTime = 4.0f;       // 가시 유지 시간

    private void Start()
    {
        // 가시가 땅에서 솟아오르는 연출
        StartCoroutine(RiseRoutine());

        // 시간 지나면 자동 삭제
        Destroy(gameObject, lifeTime);
    }

    private IEnumerator RiseRoutine()
    {
        // Y축 스케일을 0 -> 1로 변경
        Vector3 originalScale = transform.localScale;
        Vector3 startScale = originalScale;
        startScale.y = 0f;

        transform.localScale = startScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * riseSpeed; // 속도 조절
            transform.localScale = Vector3.Lerp(startScale, originalScale, t);
            yield return null;
        }
        transform.localScale = originalScale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.ApplyImpale(impaleDuration);
            }

            var health = other.GetComponent<HealthSystem>();
            if (health != null)
            {
                // health.TakeDamage(damage);
            }
        }
    }
}