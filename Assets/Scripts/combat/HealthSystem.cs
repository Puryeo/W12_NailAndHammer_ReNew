using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HealthSystem - 체력 및 사망 시스템
/// Role (SRP): IDamageable 구현, 체력 관리, 사망 이벤트 발생
/// Phase 4: 플레이어와 적 모두에게 사용 가능한 범용 체력 시스템
/// </summary>
public class HealthSystem : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Hit Feedback (New!)")]
    [Tooltip("피격 효과 컴포넌트 (자동 탐색)")]
    [SerializeField] private HitEffect hitEffect;

    [Tooltip("파티클 효과 컴포넌트 (자동 탐색)")]
    [SerializeField] private HitParticleEffect hitParticleEffect;

    [Tooltip("카메라 셰이크 사용 여부")]
    [SerializeField] private bool useCameraShake = true;

    [Tooltip("히트 스탑 사용 여부")]
    [SerializeField] private bool useHitStop = true;

    [Header("Death / Ragdoll")]
    [Tooltip("사망 시 래그돌(물리 보존) 적용 여부")]
    [SerializeField] private bool enableRagdollOnDeath = true;
    [Tooltip("래그돌 모드에서 페이드 아웃 지속 시간(초). 0이면 즉시 제거")]
    [SerializeField] private float ragdollLifetime = 6f;
    [Tooltip("사망 시 Animator(있으면) 비활성화 (래그돌이 제대로 동작하게 하기 위해)")]
    [SerializeField] private bool ragdollDisableAnimator = true;

    [Header("Events")]
    [Tooltip("사망 시 발생하는 이벤트 (LootDropper 등이 구독)")]
    public UnityEvent OnDeath = new UnityEvent();

    // 추가: 체력이 0이 되었을 때 발생하는 이벤트 (그로기 처리 등에서 구독)
    [Tooltip("체력이 0이 되었을 때 발생하는 이벤트 (OnZeroHealth)")]
    public UnityEvent OnZeroHealth = new UnityEvent();

    [SerializeField] private bool isStrongEnemy = false; // 강한 적 여부 (EnemySpawner에 전달용)

    [Header("Zero-Health Options")]
    [Tooltip("제로 헬스 도달 시 자동으로 사망을 수행할지 여부. false면 구독자가 처리를 맡을 수 있습니다.")]
    [SerializeField] private bool autoDieOnZeroHealth = false;
    [Tooltip("autoDieOnZeroHealth=true일 때 대기 시간(초)")]
    [SerializeField] private float autoDieDelay = 5f;

    [Header("Fade / Finalize")]
    [Tooltip("사망 후 페이드 아웃을 할지 여부")]
    [SerializeField] private bool useFadeOutOnDeath = true;
    [Tooltip("페이드 아웃 지속시간(초) — ragdollLifetime과 동일하게 사용됨")]
    [SerializeField] private float fadeOutDuration = 6f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private bool isDead = false;
    private bool zeroHealthPending = false; // 제로 헬스가 발생했으나 아직 Die가 호출되지 않음
    private CameraShake cameraShake; // 싱글톤 카메라 셰이크

    private void Awake()
    {
        // 초기 체력 설정
        currentHealth = maxHealth;

        // HitEffect 자동 탐색
        if (hitEffect == null)
        {
            hitEffect = GetComponent<HitEffect>();
            if (hitEffect == null)
            {
                hitEffect = gameObject.AddComponent<HitEffect>();
                if (showDebugLogs) Debug.Log($"HealthSystem [{gameObject.name}]: HitEffect 자동 생성됨");
            }
        }

        // HitParticleEffect 자동 탐색
        if (hitParticleEffect == null)
        {
            hitParticleEffect = GetComponent<HitParticleEffect>();
            if (hitParticleEffect == null)
            {
                hitParticleEffect = gameObject.AddComponent<HitParticleEffect>();
                if (showDebugLogs) Debug.Log($"HealthSystem [{gameObject.name}]: HitParticleEffect 자동 생성됨");
            }
        }

        // CameraShake 찾기
        if (useCameraShake)
        {
            try
            {
                cameraShake = Object.FindFirstObjectByType<CameraShake>();
            }
            catch
            {
                cameraShake = null;
            }

            if (cameraShake == null && Camera.main != null) cameraShake = Camera.main.GetComponent<CameraShake>();
            if (cameraShake == null) cameraShake = Object.FindObjectOfType<CameraShake>();

            if (cameraShake == null && showDebugLogs) Debug.LogWarning($"HealthSystem [{gameObject.name}]: CameraShake를 찾을 수 없습니다! Main Camera에 추가하세요.");
        }

        if (showDebugLogs) Debug.Log($"HealthSystem [{gameObject.name}]: 초기 체력 {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// IDamageable 인터페이스 구현 - 대미지 받기
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead)
        {
            if (showDebugLogs) Debug.Log($"HealthSystem [{gameObject.name}]: 이미 사망 상태 - 대미지 무시");
            return;
        }

        // 체력 감소
        currentHealth -= damage;

        if (showDebugLogs)
        {
            Debug.Log($"HealthSystem [{gameObject.name}]: {damage} 대미지 받음! 현재 체력: {currentHealth}/{maxHealth}");
        }

        // 체력이 0 이하가 되면 즉시 OnZeroHealth 발생 (사망 전 처리).
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            if (showDebugLogs) Debug.Log($"HealthSystem [{gameObject.name}]: 체력 0 도달 - OnZeroHealth 발생");
            zeroHealthPending = true;
            OnZeroHealth?.Invoke();

            if (autoDieOnZeroHealth)
            {
                // 자동 사망 처리 예약
                StartCoroutine(AutoDieCoroutine());
            }

            // 치명타(사망)인 경우에는 피격 플래시가 이후 색 복구로 그로기 색을 덮지 않도록
            // 여기서는 HitEffect/Particle/HitStop를 실행하지 않고 종료합니다.
            return;
        }

        // 비치명적 히트인 경우에만 피격 효과를 재생
        if (hitEffect != null) hitEffect.PlayHitEffect();

        // 파티클 효과 재생
        if (hitParticleEffect != null) hitParticleEffect.PlayHitParticle();

        // 카메라 셰이크 재생
        if (useCameraShake && cameraShake != null) cameraShake.ShakeWeak();

        // 히트 스탑 재생
        if (useHitStop && HitStopManager.Instance != null) HitStopManager.Instance.StopMedium();
    }

    private System.Collections.IEnumerator AutoDieCoroutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, autoDieDelay));
        if (zeroHealthPending && !isDead)
        {
            Die();
        }
    }

    /// <summary>
    /// 강제 즉사 API (외부에서 즉시 죽여야 할 때 사용)
    /// </summary>
    public void ForceDie()
    {
        if (isDead) return;
        currentHealth = 0;
        if (showDebugLogs) Debug.Log($"HealthSystem [{gameObject.name}]: ForceDie 호출 - 즉시 사망");
        // ForceDie는 즉시 OnZeroHealth 이벤트도 발생시키고 Die를 호출
        OnZeroHealth?.Invoke();
        zeroHealthPending = false;
        Die();
    }

    /// <summary>
    /// 처형 등에서 외부에서 지정한 페이드 시간으로 즉시 사망 처리합니다.
    /// HammerSwingController 등에서 사용하세요.
    /// </summary>
    /// <param name="fadeDuration">페이드 지속시간(초)</param>
    public void ForceDieWithFade(float fadeDuration)
    {
        if (isDead) return;
        // 안전한 최소값 보장
        fadeOutDuration = Mathf.Max(0.01f, fadeDuration);
        useFadeOutOnDeath = true;

        if (showDebugLogs) Debug.Log($"HealthSystem [{gameObject.name}]: ForceDieWithFade 호출 - fadeDuration={fadeOutDuration}");

        // OnZeroHealth -> Die 실행 흐름 재사용
        OnZeroHealth?.Invoke();
        zeroHealthPending = false;
        Die();
    }

    /// <summary>
    /// 사망 처리
    /// - OnDeath 이벤트를 먼저 발생시킨 뒤 컴포넌트 비활성화 및 페이드(래그돌) 진행
    /// </summary>
    private void Die()
    {
        if (isDead) return; // 중복 호출 방지
        isDead = true;
        zeroHealthPending = false;

        if (showDebugLogs) Debug.Log($"HealthSystem [{gameObject.name}]: 💀 사망! OnDeath 이벤트 발생");

        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner != null)
        {
            spawner.OnEnemyDestroyed(isStrongEnemy);
        }

        // 1) OnDeath 이벤트를 먼저 발동 (구독자들이 활성화된 상태에서 처리하도록)
        OnDeath?.Invoke();

        // 2) 컴포넌트 비활성화 및 물리처리
        DisableAllComponents();

        // 3) 페이드 아웃 및 최종화 — ragdollLifetime(또는 fadeOutDuration) 동안 처리
        float fadeDur = useFadeOutOnDeath ? Mathf.Max(0.001f, fadeOutDuration) : 0f;
        StartCoroutine(FadeOutAndFinalize(fadeDur));
    }

    private System.Collections.IEnumerator FadeOutAndFinalize(float duration)
    {
        // 수집할 SpriteRenderer들
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        // 저장된 원래 색상
        Color[] originals = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originals[i] = renderers[i].color;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(1f, 0f, t);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    Color c = originals[i];
                    c.a = alpha;
                    renderers[i].color = c;
                }
            }
            yield return null;
        }

        // 보장: 완전히 투명
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                Color c = originals[i];
                c.a = 0f;
                renderers[i].color = c;
            }
        }

        // --- 페이드 완료 시: 말뚝 회수 시작 (ConsumeStacks) 및 히트카운트 보상 처리 ---
        var ec = GetComponent<EnemyController>();
        if (ec != null)
        {
            // 끌려오기 등으로 다른 오브젝트(투사체)의 자식이면 먼저 부모를 해제하여 풀 반환 시 함께 비활성화되는 것을 방지
            if (transform.parent != null)
            {
                try { transform.SetParent(null, worldPositionStays: true); }
                catch { }
            }

            // 1) 말뚝 회수를 시작하도록 요청 (startReturn=true)
            //    개별 말뚝이 플레이어에 도달하면 AttackProjectile.CompleteRetrieval()에서 RecoverAmmo를 수행하게 함.
            int started = ec.ConsumeStacks(true, false, null); // startReturn=true, awardImmediately=false

            if (showDebugLogs) Debug.Log($"HealthSystem [{gameObject.name}]: Fade 완료 - ConsumeStacks 호출 (started returns: {started})");

            // 2) 히트카운트 기반 보상은 페이드 완료 시점에서 지급
            ec.HandleDeath();
        }

        if (showDebugLogs) Debug.Log($"HealthSystem [{gameObject.name}]: FadeOut 완료, 객체 파괴");
        Destroy(gameObject);
    }

    private void DisableAllComponents()
    {
        // 1. EnemyController 비활성화 (AI 중단)
        EnemyController enemyController = GetComponent<EnemyController>();
        if (enemyController != null) enemyController.enabled = false;

        // 2. EnemyCombat 비활성화 (공격 중단)
        EnemyCombat enemyCombat = GetComponent<EnemyCombat>();
        if (enemyCombat != null) enemyCombat.enabled = false;

        // 3. PlayerController 비활성화 (플레이어인 경우)
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null) playerController.enabled = false;

        // 4. PlayerCombat 비활성화
        PlayerCombat playerCombat = GetComponent<PlayerCombat>();
        if (playerCombat != null) playerCombat.enabled = false;

        // 5. Rigidbody2D 처리
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (enableRagdollOnDeath)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.simulated = true;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Static;
            }
        }

        // 6. Collider2D 처리
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            if (enableRagdollOnDeath)
            {
                col.enabled = true;
            }
            else
            {
                col.enabled = false;
            }
        }

        // 7. Animator 비활성화
        if (ragdollDisableAnimator)
        {
            var animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"HealthSystem [{gameObject.name}]: 사망 처리 완료 (RagdollMode={enableRagdollOnDeath})");
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        if (showDebugLogs)
        {
            Debug.Log($"HealthSystem [{gameObject.name}]: {amount} 회복! 현재 체력: {currentHealth}/{maxHealth}");
        }
    }

    public float GetHealthRatio() => currentHealth / maxHealth;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public bool IsDead() => isDead;

    private void OnDrawGizmos()
    {
        if (!showDebugLogs) return;

        Vector3 barPosition = transform.position + Vector3.up * 2f;
        float barWidth = 1f;
        float barHeight = 0.1f;
        Gizmos.color = Color.red;
        Gizmos.DrawCube(barPosition, new Vector3(barWidth, barHeight, 0.1f));
        float healthRatio = currentHealth / maxHealth;
        Gizmos.color = Color.green;
        Vector3 healthBarPosition = barPosition - new Vector3(barWidth * (1f - healthRatio) * 0.5f, 0, 0);
        Gizmos.DrawCube(healthBarPosition, new Vector3(barWidth * healthRatio, barHeight, 0.11f));
    }
}
