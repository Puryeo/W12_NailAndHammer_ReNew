using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그로기 UI 표현 전용 View 스크립트
/// Canvas(World Space) 프리팹의 루트에 부착됩니다.
/// UI 표시, 숨김, Fill 애니메이션 등 UI 관련 기능만 처리합니다.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class GroggyUIView : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("해골 아이콘 이미지")]
    [SerializeField] private Image skullIcon;

    [Tooltip("타이머 Fill 이미지 (Radial 360 Fill)")]
    [SerializeField] private Image timerFillImage;

    [Header("Colors")]
    [Tooltip("부활 불가능 시 Fill 색상")]
    [SerializeField] private Color noRecoveryFillColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    [Tooltip("부활 가능 시 Fill 색상")]
    [SerializeField] private Color recoveryFillColor = new Color(1f, 0.8f, 0f, 0.8f);

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // 내부 상태
    private Canvas canvas;
    private Coroutine fillCoroutine;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();

        // 초기 상태: 숨김
        gameObject.SetActive(false);

        // 자동 검증
        ValidateReferences();
    }

    /// <summary>
    /// UI 표시 (부활 가능 여부에 따라 다르게 표현)
    /// </summary>
    /// <param name="canRecover">부활 가능 여부</param>
    /// <param name="recoveryDuration">부활까지 걸리는 시간 (초)</param>
    public void Show(bool canRecover, float recoveryDuration = 0f)
    {
        gameObject.SetActive(true);

        if (timerFillImage != null)
        {
            if (canRecover && recoveryDuration > 0f)
            {
                // 부활 가능: Fill 애니메이션 시작
                timerFillImage.color = recoveryFillColor;
                timerFillImage.fillAmount = 0f;

                if (fillCoroutine != null)
                    StopCoroutine(fillCoroutine);

                fillCoroutine = StartCoroutine(AnimateFill(recoveryDuration));

                if (showDebugLogs) Debug.Log($"GroggyUIView: 부활 타이머 시작 ({recoveryDuration}초)");
            }
            else
            {
                // 부활 불가능: Fill 0으로 고정
                timerFillImage.color = noRecoveryFillColor;
                timerFillImage.fillAmount = 0f;

                if (showDebugLogs) Debug.Log($"GroggyUIView: 영구 그로기 표시 (Fill 0 고정)");
            }
        }

        if (showDebugLogs) Debug.Log($"GroggyUIView: UI 표시 (canRecover={canRecover})");
    }

    /// <summary>
    /// UI 숨김
    /// </summary>
    public void Hide()
    {
        // Fill 애니메이션 중단
        if (fillCoroutine != null)
        {
            StopCoroutine(fillCoroutine);
            fillCoroutine = null;
        }

        gameObject.SetActive(false);

        if (showDebugLogs) Debug.Log($"GroggyUIView: UI 숨김");
    }

    /// <summary>
    /// Fill 양 수동 설정 (0~1)
    /// </summary>
    public void SetFillAmount(float amount)
    {
        if (timerFillImage != null)
        {
            timerFillImage.fillAmount = Mathf.Clamp01(amount);
        }
    }

    /// <summary>
    /// Fill 색상 변경
    /// </summary>
    public void SetFillColor(Color color)
    {
        if (timerFillImage != null)
        {
            timerFillImage.color = color;
        }
    }

    /// <summary>
    /// Fill 애니메이션: 0 → 1로 채우기
    /// </summary>
    private IEnumerator AnimateFill(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            if (timerFillImage != null)
            {
                timerFillImage.fillAmount = progress;
            }

            yield return null;
        }

        // 완료 보장
        if (timerFillImage != null)
        {
            timerFillImage.fillAmount = 1f;
        }

        fillCoroutine = null;

        if (showDebugLogs) Debug.Log($"GroggyUIView: Fill 애니메이션 완료");
    }

    /// <summary>
    /// 레퍼런스 검증
    /// </summary>
    private void ValidateReferences()
    {
        if (skullIcon == null)
        {
            Debug.LogWarning($"GroggyUIView [{gameObject.name}]: skullIcon이 할당되지 않았습니다! Inspector에서 할당하세요.");
        }

        if (timerFillImage == null)
        {
            Debug.LogWarning($"GroggyUIView [{gameObject.name}]: timerFillImage가 할당되지 않았습니다! Inspector에서 할당하세요.");
        }
        else
        {
            // Fill 타입 검증
            if (timerFillImage.type != Image.Type.Filled)
            {
                Debug.LogWarning($"GroggyUIView [{gameObject.name}]: timerFillImage의 Type이 'Filled'가 아닙니다! Inspector에서 변경하세요.");
            }
        }

        if (canvas == null)
        {
            Debug.LogError($"GroggyUIView [{gameObject.name}]: Canvas 컴포넌트가 없습니다!");
        }
        else if (canvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning($"GroggyUIView [{gameObject.name}]: Canvas Render Mode가 'World Space'가 아닙니다! 월드 좌표에 표시되지 않을 수 있습니다.");
        }
    }

    /// <summary>
    /// 현재 표시 중인지 여부
    /// </summary>
    public bool IsVisible()
    {
        return gameObject.activeSelf;
    }

    /// <summary>
    /// Canvas 접근자 (외부에서 위치 조정용)
    /// </summary>
    public Canvas GetCanvas()
    {
        return canvas;
    }
}
