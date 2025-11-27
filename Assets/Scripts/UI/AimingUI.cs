using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// AimingUI - 플레이어의 조준 시각적 피드백
/// Role (SRP): 마우스 위치에 조준경 UI를 표시하고, 플레이어와 점선으로 연결
/// 또한 PlayerCombat의 차지 상태를 폴링하여 차지 게이지(원형)와 Ready 텍스트를 표시합니다.
/// </summary>
public class AimingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform reticuleRect;
    [Tooltip("조준경으로 사용할 UI Image의 RectTransform")]

    [Header("World References")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("'Player' 오브젝트의 Transform")]

    [Header("Visual Settings")]
    [SerializeField] private bool showReticule = true;

    [Header("Charge UI (Canvas)")]
    [Tooltip("차지 UI 루트 (Canvas 내 GameObject). 예: Canvas > Charge Time_Bar")]
    [SerializeField] private GameObject chargeUiRoot;
    [Tooltip("차지 진행을 표시하는 Image (Fill Method = Radial360 권장)")]
    [SerializeField] private Image chargeFill;
    [Tooltip("차지 완료 시 'Ready!'를 표시할 TextMeshProUGUI (ReadyToChargeShot)")]
    [SerializeField] private TextMeshProUGUI chargeReadyText;
    [Tooltip("UI를 Canvas 가운데에 고정할지 여부(true=가운데, false=reticule/마우스 위치)")]
    [SerializeField] private bool chargeUiCentered = true;
    [Tooltip("reticule/마우스 기준 위치일 때의 오프셋")]
    [SerializeField] private Vector2 chargeUiOffset = Vector2.zero;
    [Tooltip("홀드 판정까지 보여주지 않을 최소 시간 (초). 인스펙터에서 조절 가능, 기본 0.1초.")]
    [SerializeField] private float minHoldToShowUI = 0.1f;

    private Camera mainCamera;
    private Canvas parentCanvas;
    private PlayerCombat playerCombat;

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("AimingUI: Main Camera not found!");
            enabled = false;
            return;
        }

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogError("AimingUI: 부모 Canvas를 찾을 수 없습니다!");
            enabled = false;
            return;
        }

        if (playerTransform == null)
        {
            var pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo != null)
            {
                playerTransform = pgo.transform;
                Debug.Log("AimingUI: Player Transform 자동 할당 완료");
            }
        }

        // PlayerCombat 자동 할당 시도
        if (playerCombat == null && playerTransform != null)
        {
            playerCombat = playerTransform.GetComponent<PlayerCombat>();
        }
        if (playerCombat == null)
        {
            // 최후의 수단으로 장면 전체에서 찾기
            playerCombat = FindObjectOfType<PlayerCombat>();
        }

        // 조준경 초기 상태
        if (reticuleRect != null)
            reticuleRect.gameObject.SetActive(showReticule);

        // Charge UI 초기화: 비활성화, fill 초기화, readyText 초기화
        if (chargeUiRoot != null)
            chargeUiRoot.SetActive(false);
        if (chargeFill != null)
            chargeFill.fillAmount = 0f;
        if (chargeReadyText != null)
            chargeReadyText.text = string.Empty;
    }

    private void LateUpdate()
    {
        if (showReticule && reticuleRect != null)
            UpdateReticulePosition();

        UpdateChargeUI();
    }

    private void UpdateReticulePosition()
    {
        if (parentCanvas == null || reticuleRect == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            Input.mousePosition,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
            out localPoint
        );

        reticuleRect.anchoredPosition = localPoint;
    }

    /// <summary>
    /// PlayerCombat 상태를 읽어 Charge UI를 폴링하여 자동 노출/갱신
    /// - minHoldToShowUI 미만이면 UI 미표시
    /// - progress는 0..1, 준비 시 Ready! 텍스트 표시
    /// </summary>
    private void UpdateChargeUI()
    {
        if (chargeUiRoot == null || chargeFill == null || chargeReadyText == null)
            return;

        // PlayerCombat 참조 보장
        if (playerCombat == null)
        {
            playerCombat = FindObjectOfType<PlayerCombat>();
            if (playerCombat == null)
            {
                // PlayerCombat이 없으면 UI 보이지 않게 함
                if (chargeUiRoot.activeSelf) chargeUiRoot.SetActive(false);
                return;
            }
        }

        bool charging = playerCombat.IsCharging();
        if (!charging)
        {
            if (chargeUiRoot.activeSelf) chargeUiRoot.SetActive(false);
            chargeFill.fillAmount = 0f;
            chargeReadyText.text = string.Empty;
            return;
        }

        float chargeReq = playerCombat.ChargeTimeRequired;
        float progress = playerCombat.GetChargeProgress(); // 0..1
        float heldTime = chargeReq * progress;

        if (heldTime < minHoldToShowUI)
        {
            if (chargeUiRoot.activeSelf) chargeUiRoot.SetActive(false);
            chargeFill.fillAmount = 0f;
            chargeReadyText.text = string.Empty;
            return;
        }

        // UI 노출 및 위치 지정
        if (!chargeUiRoot.activeSelf) chargeUiRoot.SetActive(true);
        PositionChargeUI(chargeUiCentered ? (Vector2) (parentCanvas.transform as RectTransform).position : Input.mousePosition);

        chargeFill.fillAmount = Mathf.Clamp01(progress);
        chargeReadyText.text = playerCombat.IsChargeReady() ? "Ready!" : string.Empty;
    }

    /// <summary>
    /// chargeUiRoot의 RectTransform을 화면 좌표에 매핑하여 배치
    /// screenPosition: 스크린 좌표(예: Input.mousePosition) - 가운데 고정일 땐 무시됨
    /// </summary>
    public void PositionChargeUI(Vector2 screenPosition)
    {
        if (chargeUiRoot == null || parentCanvas == null) return;

        var rt = chargeUiRoot.GetComponent<RectTransform>();
        if (rt == null) return;

        if (chargeUiCentered)
        {
            rt.anchoredPosition = Vector2.zero;
        }
        else
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                screenPosition,
                parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                out localPoint
            );
            rt.anchoredPosition = localPoint + chargeUiOffset;
        }
    }

    /// <summary>
    /// 조준경 표시 토글
    /// </summary>
    public void ToggleReticule(bool show)
    {
        showReticule = show;
        if (reticuleRect != null)
            reticuleRect.gameObject.SetActive(show);
    }

    private void OnValidate()
    {
        if (reticuleRect != null)
            reticuleRect.gameObject.SetActive(showReticule);

        if (chargeFill != null)
            chargeFill.fillAmount = Mathf.Clamp01(chargeFill.fillAmount);

        if (chargeUiRoot != null && Application.isPlaying == false)
            chargeUiRoot.SetActive(false);
    }

    // 아래 메서드를 클래스 내부(예: OnValidate나 마지막 영역) 어딘가에 추가하세요.
    public Vector2 GetMouseWorldPosition2D()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return Vector2.zero;

        Vector3 mouse = Input.mousePosition;
        Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, mainCamera.nearClipPlane));
        return new Vector2(world.x, world.y);
    }
}
