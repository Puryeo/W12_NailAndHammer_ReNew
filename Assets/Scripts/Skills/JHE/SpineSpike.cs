using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class SpineSpike : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float impaleDuration = 3.0f; // 속박 시간

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