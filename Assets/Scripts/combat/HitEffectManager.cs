using UnityEngine;

/// <summary>
/// HitEffectManager - 히트스톱, 카메라 셰이크, VFX 등을 무기/소스별로 중앙 관리
/// - 호출: HitEffectManager.PlayHitEffect(source, hitStop, shake, position)
/// - 향후 VFX(파티클) 프리팹 연결 지점으로 확장 가능
/// </summary>
public static class HitEffectManager
{
    public static void PlayHitEffect(EHitSource source, EHitStopStrength hitStop, EShakeStrength shake, Vector3 position)
    {
        // 디버그 로그 추가
        Debug.Log($"HitEffectManager: PlayHitEffect source={source} hitStop={hitStop} shake={shake} position={position}");

        // HitStop
        if (hitStop != EHitStopStrength.None && HitStopManager.Instance != null)
        {
            if (Debug.isDebugBuild) Debug.Log($"HitEffectManager: 요청된 HitStop -> {hitStop}");
            switch (hitStop)
            {
                case EHitStopStrength.Weak: HitStopManager.Instance.StopWeak(); break;
                case EHitStopStrength.Medium: HitStopManager.Instance.StopMedium(); break;
                case EHitStopStrength.Strong: HitStopManager.Instance.StopStrong(); break;
            }
        }

        // Camera Shake
        CameraShake cam = null;
        if (Camera.main != null) cam = Camera.main.GetComponent<CameraShake>();
        if (shake != EShakeStrength.None && cam != null)
        {
            if (Debug.isDebugBuild) Debug.Log($"HitEffectManager: CameraShake 호출 -> {shake}");
            switch (shake)
            {
                case EShakeStrength.Weak: cam.ShakeWeak(); break;
                case EShakeStrength.Medium: cam.ShakeMedium(); break;
                case EShakeStrength.Strong: cam.ShakeStrong(); break;
            }
        }
    }
}