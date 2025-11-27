using System.Collections;
using UnityEngine;

/// <summary>
/// CameraShake - 카메라 흔들림 효과 (오프셋만 계산, Transform 적용은 TopDownCamera에서 수행)
/// 개선: 애니메이션 커브로 흔들림 강도/감쇠 모양 제어 가능
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [SerializeField] private float defaultMagnitude = 0.3f;
    [SerializeField] private float defaultDuration = 0.2f;

    [Header("Shake Presets")]
    [SerializeField] private float weakMagnitude = 0.15f;
    [SerializeField] private float weakDuration = 0.2f;
    [SerializeField] private float mediumMagnitude = 0.3f;
    [SerializeField] private float mediumDuration = 0.25f;
    [SerializeField] private float strongMagnitude = 0.5f;
    [SerializeField] private float strongDuration = 0.35f;

    [Header("Curves (shape of shake over normalized time 0..1)")]
    [Tooltip("기본 커브: 시간 0에서 1로 선형 감소 (Inspector에서 편집 가능)")]
    [SerializeField] private AnimationCurve defaultCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Tooltip("약한 셰이크에 사용할 커브 (null이면 defaultCurve 사용)")]
    [SerializeField] private AnimationCurve weakCurve = null;
    [Tooltip("중간 셰이크에 사용할 커브 (null이면 defaultCurve 사용)")]
    [SerializeField] private AnimationCurve mediumCurve = null;
    [Tooltip("강한 셰이크에 사용할 커브 (null이면 defaultCurve 사용)")]
    [SerializeField] private AnimationCurve strongCurve = null;

    [Header("Advanced Settings")]
    [SerializeField] private bool allowAccumulation = false;
    [SerializeField] private float maxAccumulatedMagnitude = 1.0f;

    [Header("Debug")]
    [Tooltip("셰이크 호출/코루틴 시작/종료 로그 출력")]
    [SerializeField] private bool debugLogs = false;

    private bool isShaking = false;
    private float currentAccumulatedMagnitude = 0f;

    // 현재 프레임에서 적용할 오프셋 (TopDownCamera가 읽어 최종 위치에 더함)
    private Vector3 currentOffset = Vector3.zero;

    // 외부에서 읽을 수 있도록 getter 제공 (복사 반환)
    public Vector3 GetCurrentOffset() => currentOffset;

    // 기존 편의 호출들: 커브를 지정하지 않으면 프리셋 커브 또는 defaultCurve 사용
    public void ShakeWeak() => Shake(weakMagnitude, weakDuration, weakCurve);
    public void ShakeMedium() => Shake(mediumMagnitude, mediumDuration, mediumCurve);
    public void ShakeStrong() => Shake(strongMagnitude, strongDuration, strongCurve);

    /// <summary>
    /// 기본 Shake 호출 (커브 선택적)
    /// </summary>
    /// <param name="magnitude">기본 강도</param>
    /// <param name="duration">지속시간</param>
    /// <param name="curve">애니메이션 커브 (시간 비율 0..1 -> 스케일 0..1). null이면 defaultCurve 사용.</param>
    public void Shake(float magnitude = -1f, float duration = -1f, AnimationCurve curve = null)
    {
        if (magnitude < 0f) magnitude = defaultMagnitude;
        if (duration < 0f) duration = defaultDuration;

        if (debugLogs)
        {
            Debug.Log($"CameraShake: Shake() 호출 magnitude={magnitude:F3} duration={duration:F3} curve={(curve!=null?"custom":"default")}");
        }

        if (allowAccumulation && isShaking)
        {
            currentAccumulatedMagnitude = Mathf.Min(currentAccumulatedMagnitude + magnitude, maxAccumulatedMagnitude);
            if (debugLogs)
                Debug.Log($"CameraShake: 누적 모드 - currentAccumulatedMagnitude={currentAccumulatedMagnitude:F3}");
            return;
        }

        if (isShaking)
        {
            if (debugLogs) Debug.Log("CameraShake: 기존 코루틴 중단 후 새로 시작");
            StopAllCoroutines();
        }

        currentAccumulatedMagnitude = magnitude;
        StartCoroutine(ShakeCoroutine(magnitude, duration, curve));
    }

    private IEnumerator ShakeCoroutine(float magnitude, float duration, AnimationCurve curve)
    {
        if (debugLogs) Debug.Log("CameraShake: ShakeCoroutine 시작");
        isShaking = true;
        float elapsed = 0f;

        // 사용 커브 결정 (null이면 defaultCurve 사용)
        AnimationCurve useCurve = curve != null ? curve : defaultCurve;

        while (elapsed < duration)
        {
            // unscaled 시간 사용
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 커브로 시간에 따른 강도 스케일을 얻음 (0..1)
            float curveEval = useCurve.Evaluate(t);

            // 실제 사용할 강도 (누적 모드 반영)
            float baseMagnitude = allowAccumulation ? currentAccumulatedMagnitude : magnitude;
            float effective = baseMagnitude * curveEval;

            // 랜덤 오프셋 생성 (2D)
            float x = Random.Range(-1f, 1f) * effective;
            float y = Random.Range(-1f, 1f) * effective;

            currentOffset = new Vector3(x, y, 0f);

            // 코루틴 대기는 프레임 단위로 유지 (unscaledDeltaTime로 진행하므로 Time.timeScale에 영향받지 않음)
            yield return null;
        }

        // 완료: 오프셋 초기화
        currentOffset = Vector3.zero;
        currentAccumulatedMagnitude = 0f;
        isShaking = false;

        if (debugLogs) Debug.Log("CameraShake: ShakeCoroutine 종료");
    }

    public void StopShake()
    {
        if (debugLogs) Debug.Log("CameraShake: StopShake 호출");
        StopAllCoroutines();
        currentOffset = Vector3.zero;
        currentAccumulatedMagnitude = 0f;
        isShaking = false;
    }

    // 런타임에서 커브/옵션을 변경할 수 있도록 공개 API 제공
    public void SetDefaultCurve(AnimationCurve c) { if (c != null) defaultCurve = c; }
    public void SetWeakCurve(AnimationCurve c) { weakCurve = c; }
    public void SetMediumCurve(AnimationCurve c) { mediumCurve = c; }
    public void SetStrongCurve(AnimationCurve c) { strongCurve = c; }
    public void SetAllowAccumulation(bool allow) => allowAccumulation = allow;

    // ---- 새로 추가: Inspector/외부에서 조절할 수 있는 Setter들 ----
    public void SetDefaultMagnitude(float v) => defaultMagnitude = v;
    public void SetDefaultDuration(float v) => defaultDuration = v;

    public void SetWeakMagnitude(float v) => weakMagnitude = v;
    public void SetWeakDuration(float v) => weakDuration = v;

    public void SetMediumMagnitude(float v) => mediumMagnitude = v;
    public void SetMediumDuration(float v) => mediumDuration = v;

    public void SetStrongMagnitude(float v) => strongMagnitude = v;
    public void SetStrongDuration(float v) => strongDuration = v;

    public void SetMaxAccumulatedMagnitude(float v) => maxAccumulatedMagnitude = v;
    public void SetDebugLogs(bool v) => debugLogs = v;
}
