using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyProjectile - 경량 적 전용 투사체 (풀 친화적)
/// - Initialize(...)로 런타임 설정
/// - Collider2D는 Trigger여야 함 (이 스크립트는 Trigger 충돌을 기대)
/// - 충돌 대상 태그가 "Player"일 때 데미지 적용 후 반환/파괴
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Defaults (Inspector)")]
    [SerializeField] private float defaultSpeed = 10f;
    [SerializeField] private float defaultLifetime = 5f;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool showDebugLogs = false;

    // 런타임 상태
    private Vector2 moveDirection = Vector2.right;
    private float speed = 10f;
    private float lifetime = 5f;
    private float lifeTimer = 0f;
    private float damage = 1f;
    private GameObjectPool ownerPool = null;
    private Rigidbody2D rb2D;
    private Collider2D col2D;
    private SpriteRenderer sr;

    private void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        col2D = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        if (col2D != null)
        {
            col2D.isTrigger = true; // 요구사항: isTrigger = true
        }
    }

    /// <summary>
    /// 런타임 초기화. poolOwner은 null이면 Destroy로 처리.
    /// </summary>
    public void Initialize(Vector2 direction, float speed, float lifetime, float damage, Color? tint = null, GameObjectPool poolOwner = null, Transform owner = null)
    {
        moveDirection = (direction == Vector2.zero) ? Vector2.right : direction.normalized;
        this.speed = (speed > 0f) ? speed : defaultSpeed;
        this.lifetime = (lifetime > 0f) ? lifetime : defaultLifetime;
        this.lifeTimer = this.lifetime;
        this.damage = damage;
        this.ownerPool = poolOwner;

        if (sr != null && tint.HasValue)
        {
            sr.color = tint.Value;
        }

        // Rigidbody 기반이면 속도 설정
        if (rb2D != null)
        {
            rb2D.gravityScale = 0f;
            rb2D.angularVelocity = 0f;
            rb2D.linearVelocity = moveDirection * this.speed;
        }

        gameObject.SetActive(true);

        if (showDebugLogs)
            Debug.Log($"EnemyProjectile [{gameObject.name}] Initialize speed={this.speed} lifetime={this.lifetime} damage={this.damage} dir={moveDirection}");
    }

    private void Update()
    {
        // Rigidbody가 없으면 transform 이동
        if (rb2D == null)
        {
            transform.Translate((Vector3)(moveDirection * speed) * Time.deltaTime, Space.World);
        }

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            ReturnOrDestroy();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // 플레이어 태그 검사
        if (!other.CompareTag(targetTag)) return;

        // 데미지 적용
        var hs = other.GetComponent<HealthSystem>() ?? other.GetComponentInParent<HealthSystem>();
        if (hs != null)
        {
            hs.TakeDamage(damage);
        }

        if (showDebugLogs)
            Debug.Log($"EnemyProjectile [{gameObject.name}] hit {other.name}, dmg={damage}");

        // 충돌 후 반환 또는 파괴
        ReturnOrDestroy();
    }

    private void ReturnOrDestroy()
    {
        // 풀에 소속되어 있으면 반환
        if (ownerPool != null)
        {
            try
            {
                // 안전하게 콜라이더 / 물리 초기화
                if (rb2D != null)
                {
                    rb2D.linearVelocity = Vector2.zero;
                    rb2D.angularVelocity = 0f;
                }
                if (col2D != null) col2D.enabled = false; // 풀 반환 시 Reset은 Release에서 처리될 수 있으므로 비활성화
                ownerPool.Release(gameObject);
            }
            catch
            {
                Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 풀에서 다시 사용될 때 호출되는 경우가 있으므로 OnEnable에서 콜라이더 활성화
    private void OnEnable()
    {
        if (col2D != null) col2D.enabled = true;
    }
}