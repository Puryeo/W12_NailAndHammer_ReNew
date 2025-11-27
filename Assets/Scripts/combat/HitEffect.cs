using System.Collections;
using UnityEngine;

/// <summary>
/// HitEffect - 피격 시 시각적 효과 (색상 변경 + 크기 왜곡)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HitEffect : MonoBehaviour
{
    [Header("Hit Flash Settings")]
    [Tooltip("피격 시 색상 (빨간색)")]
    [SerializeField] private Color hitColor = Color.red;
    [Tooltip("피격 효과 지속 시간 (초)")]
    [SerializeField] private float flashDuration = 0.5f;

    [Header("Squash & Stretch Settings")]
    [Tooltip("피격 시 크기 왜곡 사용 여부")]
    [SerializeField] private bool useSquashStretch = true;
    [Tooltip("크기 왜곡 강도 (1.0 = 없음, 1.2 = 20% 증가)")]
    [SerializeField] private float squashStrength = 1.2f;
    [Tooltip("크기 왜곡 지속 시간 (초)")]
    [SerializeField] private float squashDuration = 0.5f;

    [Header("Execute (처형) Settings")]
    [Tooltip("처형 시 색상 (강한 플래시)")]
    [SerializeField] private Color executeColor = Color.white;
    [Tooltip("처형 시 플래시 지속시간")]
    [SerializeField] private float executeFlashDuration = 0.2f;
    [Tooltip("처형 시 크기 왜곡 강도")]
    [SerializeField] private float executeSquashStrength = 1.5f;
    [Tooltip("처형 시 크기 왜곡 지속시간")]
    [SerializeField] private float executeSquashDuration = 0.35f;

    // References
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 originalScale;

    // Saved originals (안전 보관)
    private Color savedOriginalColor;
    private Vector3 savedOriginalScale;
    private bool hasInitialized = false;
    private bool isPlayingEffect = false;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        InitializeOriginalValues();
    }

    private void InitializeOriginalValues()
    {
        if (hasInitialized) return;
        if (spriteRenderer != null)
        {
            savedOriginalColor = spriteRenderer.color;
            originalColor = savedOriginalColor;
        }
        savedOriginalScale = transform.localScale;
        originalScale = savedOriginalScale;
        hasInitialized = true;
    }

    public void PlayHitEffect()
    {
        if (!hasInitialized) InitializeOriginalValues();

        if (isPlayingEffect)
        {
            StopAllCoroutines();
            RestoreOriginalValues();
        }

        StartCoroutine(HitEffectCoroutine());
    }

    private IEnumerator HitEffectCoroutine()
    {
        isPlayingEffect = true;

        originalColor = savedOriginalColor;
        originalScale = savedOriginalScale;

        float elapsed = 0f;
        float maxDuration = Mathf.Max(flashDuration, squashDuration);

        while (elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;

            if (spriteRenderer != null && elapsed < flashDuration)
            {
                float flashT = elapsed / flashDuration;
                spriteRenderer.color = Color.Lerp(hitColor, originalColor, flashT);
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }

            if (useSquashStretch && elapsed < squashDuration)
            {
                float squashT = elapsed / squashDuration;
                float easeOut = 1f - Mathf.Pow(1f - squashT, 2f);
                float scaleX = Mathf.Lerp(squashStrength, 1f, easeOut);
                float scaleY = Mathf.Lerp(1f / squashStrength, 1f, easeOut);
                transform.localScale = new Vector3(
                    originalScale.x * scaleX,
                    originalScale.y * scaleY,
                    originalScale.z
                );
            }
            else
            {
                transform.localScale = originalScale;
            }

            yield return null;
        }

        RestoreOriginalValues();
        isPlayingEffect = false;
    }

    private void RestoreOriginalValues()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = savedOriginalColor;
        }
        transform.localScale = savedOriginalScale;
    }

    // ----- Execute effect -----
    /// <summary>
    /// 처형(Execution) 전용 이펙트 재생
    /// - 기본 피격보다 강한 플래시/스케일을 재생합니다.
    /// </summary>
    public void PlayExecuteEffect()
    {
        if (!hasInitialized) InitializeOriginalValues();

        if (isPlayingEffect)
        {
            StopAllCoroutines();
            RestoreOriginalValues();
        }

        StartCoroutine(ExecuteEffectCoroutine());
    }

    private IEnumerator ExecuteEffectCoroutine()
    {
        isPlayingEffect = true;

        // Use current savedOriginalColor as base to avoid overwriting groggy saved color
        Color baseOriginal = savedOriginalColor;
        Vector3 baseScale = savedOriginalScale;

        float elapsed = 0f;
        float maxDuration = Mathf.Max(executeFlashDuration, executeSquashDuration);

        while (elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;

            // Flash (executeColor -> baseOriginal)
            if (spriteRenderer != null && elapsed < executeFlashDuration)
            {
                float t = elapsed / executeFlashDuration;
                spriteRenderer.color = Color.Lerp(executeColor, baseOriginal, t);
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.color = baseOriginal;
            }

            // Squash & Stretch (stronger)
            if (useSquashStretch && elapsed < executeSquashDuration)
            {
                float squashT = elapsed / executeSquashDuration;
                float easeOut = 1f - Mathf.Pow(1f - squashT, 2f);
                float scaleX = Mathf.Lerp(executeSquashStrength, 1f, easeOut);
                float scaleY = Mathf.Lerp(1f / executeSquashStrength, 1f, easeOut);
                transform.localScale = new Vector3(
                    baseScale.x * scaleX,
                    baseScale.y * scaleY,
                    baseScale.z
                );
            }
            else
            {
                transform.localScale = baseScale;
            }

            yield return null;
        }

        // Restore to saved original (do not override savedOriginalColor here)
        if (spriteRenderer != null) spriteRenderer.color = savedOriginalColor;
        transform.localScale = savedOriginalScale;

        isPlayingEffect = false;
    }

    // ----- API helpers -----
    /// <summary>
    /// 외부에서 savedOriginalColor를 강제로 설정합니다.
    /// (예: 그로기 상태 진입 시 HitEffect가 나중에 원래색으로 덮어쓰는 것을 방지)
    /// </summary>
    public void ForceSetSavedOriginalColor(Color c)
    {
        if (!hasInitialized) InitializeOriginalValues();
        savedOriginalColor = c;
        originalColor = c;
    }

    /// <summary>
    /// savedOriginalColor를 스프라이트 현재 색상으로 동기화
    /// </summary>
    public void ResetSavedOriginalColorToCurrentSprite()
    {
        if (!hasInitialized) InitializeOriginalValues();
        if (spriteRenderer != null) savedOriginalColor = spriteRenderer.color;
        originalColor = savedOriginalColor;
    }

    public void ResetOriginalValues()
    {
        hasInitialized = false;
        InitializeOriginalValues();
    }

    private void OnDestroy()
    {
        if (isPlayingEffect)
        {
            StopAllCoroutines();
            RestoreOriginalValues();
        }
    }
}
