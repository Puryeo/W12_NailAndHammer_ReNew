using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    [Header("Hit Stop Settings")]
    [Tooltip("기본 히트 스탑 시간 (초)")]
    [SerializeField] private float defaultStopDuration = 0.05f;
    
    [Tooltip("약한 히트 스탑 (일반 공격)")]
    [SerializeField] private float weakStopDuration = 0.03f;
    
    [Tooltip("중간 히트 스탑 (강한 공격)")]
    [SerializeField] private float mediumStopDuration = 0.05f;
    
    [Tooltip("강한 히트 스탑 (크리티컬)")]
    [SerializeField] private float strongStopDuration = 0.1f;

    [Header("Tuning")]
    [Tooltip("모든 히트스탑 지속시간에 곱해지는 전역 배율 (0 = 비활성, 1 = 원래값)")]
    [Range(0f, 2f)]
    [SerializeField] private float durationMultiplier = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private static HitStopManager instance;
    private bool isHitStopping = false;

    private void Awake()
    {
        // 싱글톤 패턴
        if (instance == null)
        {
            instance = this;
            Debug.Log($"HitStopManager: Awake on GameObject '{gameObject.name}' (instance assigned).");
        }
        else if (instance != this)
        {
            Debug.Log($"HitStopManager: Duplicate instance on GameObject '{gameObject.name}' destroyed.");
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// 싱글톤 인스턴스 가져오기
    /// </summary>
    public static HitStopManager Instance
    {
        get
        {
            if (instance == null)
            {
                // 씬에서 찾기
                instance = FindFirstObjectByType<HitStopManager>();
                
                // 없으면 자동 생성
                if (instance == null)
                {
                    GameObject obj = new GameObject("HitStopManager");
                    instance = obj.AddComponent<HitStopManager>();
                    Debug.Log("HitStopManager: 자동 생성됨 (싱글톤)");
                }
            }
            return instance;
        }
    }

    /// <summary>
    /// 약한 히트 스탑 (일반 공격)
    /// </summary>
    public void StopWeak()
    {
        Stop(weakStopDuration);
    }

    /// <summary>
    /// 중간 히트 스탑 (강한 공격)
    /// </summary>
    public void StopMedium()
    {
        Stop(mediumStopDuration);
    }

    /// <summary>
    /// 강한 히트 스탑 (크리티컬)
    /// </summary>
    public void StopStrong()
    {
        Stop(strongStopDuration);
    }

    /// <summary>
    /// 커스텀 히트 스탑
    /// </summary>
    public void Stop(float duration)
    {
        // 이미 히트 스탑 중이면 무시 (중복 방지)
        if (isHitStopping)
        {
            if (showDebugLogs)
            {
                Debug.Log($"HitStopManager: 이미 히트 스탑 중 - 무시");
            }
            return;
        }

        // 전역 배율 적용
        float scaledDuration = Mathf.Max(0f, duration * durationMultiplier);
        if (scaledDuration <= 0f)
        {
            if (showDebugLogs) Debug.Log("HitStopManager: 전역 배율로 인해 히트스탑이 0으로 설정됨, 실행 안함");
            return;
        }

        StartCoroutine(HitStopCoroutine(scaledDuration));
    }

    /// <summary>
    /// 히트 스탑 코루틴
    /// </summary>
    private IEnumerator HitStopCoroutine(float duration)
    {
        isHitStopping = true;

        if (showDebugLogs)
        {
            Debug.Log($"HitStopManager: 히트 스탑 시작 ({duration:F3}초)");
        }

        // 시간 정지
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        // 실제 시간(unscaled time) 기준으로 대기
        yield return new WaitForSecondsRealtime(duration);

        // 시간 복구
        Time.timeScale = originalTimeScale;

        if (showDebugLogs)
        {
            Debug.Log($"HitStopManager: 히트 스탑 종료");
        }

        isHitStopping = false;
    }

    /// <summary>
    /// 히트 스탑 즉시 중단 (긴급 상황용)
    /// </summary>
    public void CancelHitStop()
    {
        StopAllCoroutines();
        Time.timeScale = 1f;
        isHitStopping = false;

        if (showDebugLogs)
        {
            Debug.Log("HitStopManager: 히트 스탑 강제 취소");
        }
    }

    /// <summary>
    /// 현재 히트 스탑 중인지 확인
    /// </summary>
    public bool IsHitStopping()
    {
        return isHitStopping;
    }

    /// <summary>
    /// 설정 커스터마이징 (런타임 중 변경 가능)
    /// </summary>
    public void SetWeakDuration(float duration)
    {
        weakStopDuration = duration;
    }

    public void SetMediumDuration(float duration)
    {
        mediumStopDuration = duration;
    }

    public void SetStrongDuration(float duration)
    {
        strongStopDuration = duration;
    }

    public void SetDurationMultiplier(float multiplier)
    {
        durationMultiplier = multiplier;
    }

    private void OnDestroy()
    {
        // 싱글톤 정리 시 timeScale 복구
        if (instance == this)
        {
            Time.timeScale = 1f;
            instance = null;
        }
    }
}
