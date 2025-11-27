using UnityEngine;

/// <summary>
/// TopDownCamera - Main Camera가 Player를 부드럽게 따라다님
/// 변경: 베이스 카메라 위치(basePosition)와 셰이크 오프셋을 분리하여
///        SmoothDamp 시 셰이크로 인한 진동(jitter) 제거
/// 추가: 시간 스케일 독립성 확보를 위해 unscaledDeltaTime 사용 및 셰이크 오프셋 클램프
/// 개선: 플레이어 Rigidbody2D 위치 사용 및 basePosition 초기가동 동기화로 초기 튕김 제거
/// </summary>
[RequireComponent(typeof(Camera))]
public class TopDownCamera : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("플레이어(또는 추적 대상) Transform을 할당. 비어있으면 'Player' 태그로 자동 검색")]
    [SerializeField] private Transform playerTarget;

    [Header("Camera Settings")]
    [Tooltip("플레이어 기준 카메라 오프셋 (Z는 카메라 고정값)")]
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);
    [Tooltip("Orthographic 카메라 사이즈")]
    [SerializeField] private float orthographicSize = 10f;

    [Header("Follow Tuning")]
    [Tooltip("카메라가 목표 위치로 부드럽게 이동하는 시간(초). 작을수록 빠름")]
    [SerializeField] private float followSmoothTime = 0.05f;
    [Tooltip("FixedUpdate와 렌더 프레임 차이로 인한 jitter 완화를 위해 LateUpdate 사용 (권장)")]
    [SerializeField] private bool useSmoothDamp = true;

    [Header("Shake Tuning (Inspector)")]
    [Tooltip("셰이크 오프셋에 곱해지는 전역 스케일(0 = 무시, 1 = 원래 크기)")]
    [Range(0f, 2f)]
    [SerializeField] private float shakeMultiplier = 1f;
    [Tooltip("셰이크 오프셋을 프레임간 부드럽게 보간하는 속도(크면 더 부드러움)")]
    [SerializeField] private float shakeSmoothing = 20f;

    [Header("Optional: CameraShake Presets (applied to CameraShake if present)")]
    [SerializeField] private bool applyPresetsToCameraShake = true;
    [SerializeField] private float weakMagnitude = 0.15f;
    [SerializeField] private float weakDuration = 0.2f;
    [SerializeField] private float mediumMagnitude = 0.3f;
    [SerializeField] private float mediumDuration = 0.25f;
    [SerializeField] private float strongMagnitude = 0.5f;
    [SerializeField] private float strongDuration = 0.35f;
    [SerializeField] private AnimationCurve defaultShakeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private bool allowShakeAccumulation = false;
    [SerializeField] private float maxAccumulatedMagnitude = 1f;
    [SerializeField] private bool shakeDebugLogs = false;

    // Internal
    private Vector3 fixedRotation = new Vector3(0, 0, 0);
    private Camera attachedCamera;
    private CameraShake cameraShake;

    // basePosition: 셰이크를 제외한 카메라의 '목표 추적' 위치 (SmoothDamp는 이 값에 적용)
    private Vector3 basePosition;
    private Vector3 followVelocity = Vector3.zero;

    // lastAppliedShakeOffset: 셰이크 보간된 오프셋 (basePosition에 더해져 실제 transform.position이 됨)
    private Vector3 lastAppliedShakeOffset = Vector3.zero;

    // 캐시된 플레이어 Rigidbody2D (있으면 참고), basePosition 초기화 플래그
    private Rigidbody2D playerRigidbody = null;
    private bool basePositionInitialized = false;

    private void Start()
    {
        attachedCamera = GetComponent<Camera>();
        if (attachedCamera != null)
        {
            attachedCamera.orthographic = true;
            attachedCamera.orthographicSize = orthographicSize;
        }

        transform.rotation = Quaternion.Euler(fixedRotation);

        // CameraShake 캐시 (있으면 사용, 없으면 옵션에 따라 자동 생성)
        cameraShake = GetComponent<CameraShake>();
        if (cameraShake == null && applyPresetsToCameraShake)
        {
            cameraShake = gameObject.AddComponent<CameraShake>();
            Debug.Log("TopDownCamera: CameraShake 컴포넌트가 없어 자동으로 추가했습니다.");
            ApplyShakeSettings();
        }
        else if (cameraShake != null && applyPresetsToCameraShake)
        {
            ApplyShakeSettings();
        }

        // 자동 할당: Player 태그
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTarget = player.transform;
                Debug.Log("TopDownCamera: Player target auto-assigned via tag.");
            }
            else
            {
                Debug.LogWarning("TopDownCamera: playerTarget이 할당되지 않았고 'Player' 태그 오브젝트를 찾을 수 없습니다.");
            }
        }

        if (playerTarget != null)
        {
            playerRigidbody = playerTarget.GetComponent<Rigidbody2D>();
            // 즉시 basePosition 동기화: 초기 프레임에서 스냅/튀지 않도록 타겟과 동일시
            Vector3 targetPos = GetTargetPosition();
            basePosition = targetPos;
            basePositionInitialized = true;
        }
        else
        {
            // 초기 basePosition은 현재 transform에서 셰이크 오프셋을 제거한 값으로 설정
            basePosition = transform.position - lastAppliedShakeOffset;
            basePositionInitialized = true;
        }
    }

    private Vector3 GetTargetPosition()
    {
        if (playerTarget == null) return transform.position;

        // 변경: Rigidbody2D.position 대신 transform.position 사용
        // 이유: Rigidbody interpolation이 켜져 있으면 transform.position이 이미 보간된(렌더 타이밍에 맞춘) 값임.
        // Rigidbody2D.position은 물리 스텝 결과를 바로 반환하여 렌더 보간과 어긋날 수 있어 jitter 유발.
        return new Vector3(playerTarget.position.x, playerTarget.position.y, offset.z);
    }

    private void LateUpdate()
    {
        if (playerTarget == null) return;

        // Use unscaled delta time to make camera independent from Time.timeScale (hitstop, slowmo 등)
        float dt = Time.unscaledDeltaTime;

        // 목표 위치 계산 (Z 고정) — transform.position(보간된 렌더 위치) 사용
        Vector3 targetPosition = GetTargetPosition();

        // Ensure basePosition initialized to avoid initial jump
        if (!basePositionInitialized)
        {
            basePosition = targetPosition;
            basePositionInitialized = true;
            followVelocity = Vector3.zero;
        }

        // SmoothDamp는 basePosition을 기준으로 수행. deltaTime에 unscaled 사용
        float smoothTime = Mathf.Max(0.0001f, followSmoothTime); // 0 허용 방지
        if (useSmoothDamp)
        {
            basePosition = Vector3.SmoothDamp(basePosition, targetPosition, ref followVelocity, smoothTime, Mathf.Infinity, dt);
        }
        else
        {
            // Lerp 계열도 unscaled time 사용
            basePosition = Vector3.Lerp(basePosition, targetPosition, Mathf.Clamp01(dt * (1f / smoothTime)));
        }

        // 셰이크 오프셋 가져오기 및 스무딩 (프레임간 급격한 변화를 완화)
        Vector3 desiredShakeOffset = Vector3.zero;
        if (cameraShake != null)
        {
            desiredShakeOffset = cameraShake.GetCurrentOffset() * shakeMultiplier;
            // 보간에 unscaled delta 적용
            lastAppliedShakeOffset = Vector3.Lerp(lastAppliedShakeOffset, desiredShakeOffset, Mathf.Clamp01(dt * shakeSmoothing));

            // 안전장치: 너무 큰 오프셋으로 인한 튐 방지 (카메라 스크립트 자체에 정의된 최대 누적값 사용)
            if (!allowShakeAccumulation)
            {
                lastAppliedShakeOffset = Vector3.ClampMagnitude(lastAppliedShakeOffset, maxAccumulatedMagnitude * shakeMultiplier);
            }
            else
            {
                // accumulation 허용 시에도 절대 최대값으로 클램프
                lastAppliedShakeOffset = Vector3.ClampMagnitude(lastAppliedShakeOffset, Mathf.Max(0.0001f, maxAccumulatedMagnitude * shakeMultiplier));
            }
        }
        else
        {
            lastAppliedShakeOffset = Vector3.Lerp(lastAppliedShakeOffset, Vector3.zero, Mathf.Clamp01(dt * shakeSmoothing));
        }

        // 최종 위치 적용 (basePosition + shakeOffset)
        transform.position = basePosition + lastAppliedShakeOffset;

        // 고정 회전 유지
        transform.rotation = Quaternion.Euler(fixedRotation);
    }

    /// <summary>
    /// Inspector 값 변경 시 CameraShake에도 즉시 적용 (에디터/런타임 모두 사용 가능)
    /// </summary>
    [ContextMenu("Apply Shake Settings")]
    private void ApplyShakeSettings()
    {
        if (!applyPresetsToCameraShake) return;

        if (cameraShake == null) cameraShake = GetComponent<CameraShake>();
        if (cameraShake == null)
        {
            cameraShake = gameObject.AddComponent<CameraShake>();
            Debug.Log("TopDownCamera: CameraShake 자동 추가 (ApplyShakeSettings).");
        }

        cameraShake.SetWeakMagnitude(weakMagnitude);
        cameraShake.SetWeakDuration(weakDuration);
        cameraShake.SetMediumMagnitude(mediumMagnitude);
        cameraShake.SetMediumDuration(mediumDuration);
        cameraShake.SetStrongMagnitude(strongMagnitude);
        cameraShake.SetStrongDuration(strongDuration);
        cameraShake.SetDefaultCurve(defaultShakeCurve);
        cameraShake.SetAllowAccumulation(allowShakeAccumulation);
        cameraShake.SetMaxAccumulatedMagnitude(maxAccumulatedMagnitude);
        cameraShake.SetDebugLogs(shakeDebugLogs);

        Debug.Log("TopDownCamera: CameraShake 설정 적용 완료.");
    }

    // 런타임으로 연동된 값들을 외부에서 변경할 수 있도록 공개 메서드 제공
    public void SetFollowSmoothTime(float t) => followSmoothTime = Mathf.Max(0f, t);
    public void SetShakeMultiplier(float m) => shakeMultiplier = Mathf.Clamp(m, 0f, 2f);
    public void SetShakeSmoothing(float s) => shakeSmoothing = Mathf.Max(0f, s);
}
