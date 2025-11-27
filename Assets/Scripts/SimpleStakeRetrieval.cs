using UnityEngine;

/// <summary>
/// 간단한 말뚝 회수 시스템 (SO Behavior 기반)
/// - R키로 모든 투사체 회수
/// - 각 AttackProjectile의 retrievalBehavior가 알아서 처리
/// - SO만 바꾸면 회수 동작이 자동으로 변경됨
///
/// 사용법: Player에 컴포넌트 추가하면 됨
/// </summary>
public class SimpleStakeRetrieval : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("회수 키")]
    [SerializeField] private KeyCode retrievalKey = KeyCode.R;

    [Tooltip("회수 쿨타임 (초)")]
    [SerializeField] private float cooldown = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showDebugUI = true;

    private float cooldownTimer = 0f;

    // 추가: realtime 기준 쿨다운 종료 시각 (hitstop 등 Timescale 변경과 무관하게 UI에 실시간 표시하기 위함)
    private float cooldownEndRealtime = 0f;

    private void Update()
    {
        // 쿨다운 감소 (기존 게임 로직은 Time.deltaTime 기반 유지)
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer < 0f) cooldownTimer = 0f;
        }

        // R키 입력
        if (Input.GetKeyDown(retrievalKey))
        {
            TryRetrieve();
        }
    }

    private void TryRetrieve()
    {
        // 쿨다운 체크
        if (cooldownTimer > 0f)
        {
            if (showDebugLogs)
                Debug.Log($"[SimpleStakeRetrieval] 쿨다운 중 ({cooldownTimer:F2}초 남음)");
            return;
        }

        // 모든 투사체 찾기
        var allProjectiles = FindObjectsOfType<AttackProjectile>();
        if (allProjectiles == null || allProjectiles.Length == 0)
        {
            if (showDebugLogs)
                Debug.Log("[SimpleStakeRetrieval] 회수할 투사체 없음");
            return;
        }

        int retrievedCount = 0;

        foreach (var projectile in allProjectiles)
        {
            if (projectile == null) continue;

            projectile.StartReturn(
                suppressAmmo: false,
                immediatePickup: false,
                useRetrievalBehavior: true
            );

            retrievedCount++;
        }

        if (showDebugLogs)
            Debug.Log($"[SimpleStakeRetrieval] {retrievedCount}개 투사체 회수 시작 (Behavior 기반)");

        // 쿨다운 시작 (기존 게임 로직용)
        cooldownTimer = cooldown;

        // 추가: realtime 기준 종료 시각 기록( UI는 이 값을 사용해 리얼타임 표시 )
        cooldownEndRealtime = Time.realtimeSinceStartup + cooldown;
    }

    // 외부 API
    public bool IsOnCooldown() => cooldownTimer > 0f;
    public float GetCooldownRemaining() => cooldownTimer;

    // 추가: 총 쿨다운 길이(초)를 외부에서 읽을 수 있도록 함
    public float GetCooldownDuration() => cooldown;

    // 추가: 히트스탑/타임스케일과 상관없이 '실시간'으로 읽을 수 있는 남은시간 반환
    public float GetCooldownRemainingUnscaled()
    {
        float remaining = cooldownEndRealtime - Time.realtimeSinceStartup;
        return remaining > 0f ? remaining : 0f;
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;

        // 쿨다운 표시
        if (cooldownTimer > 0f)
        {
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(10, 140, 300, 25), $"Retrieval CD: {cooldownTimer:F1}s", style);
        }
        else
        {
            style.normal.textColor = Color.green;
            GUI.Label(new Rect(10, 140, 300, 25), $"Retrieval Ready (Press {retrievalKey})", style);
        }
    }
}
