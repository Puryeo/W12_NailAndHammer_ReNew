using UnityEngine;
using TMPro;

/// <summary>
/// 적 처치 현황 UI를 표시합니다 (0/5 형식)
/// </summary>
public class StageKillUI : MonoBehaviour
{
    [Header("UI 설정")]
    [Tooltip("적 처치 현황을 표시할 TextMeshPro 텍스트")]
    [SerializeField] private TextMeshProUGUI killCountText;

    [Header("디버그")]
    [SerializeField] private bool showDebugLogs = true;

    /// <summary>
    /// UI 텍스트 업데이트 (외부에서 호출)
    /// </summary>
    public void UpdateKillCount(int currentKills, int targetKills)
    {
        if (killCountText != null)
        {
            killCountText.text = $"{currentKills}/{targetKills}";

            if (showDebugLogs)
            {
                Debug.Log($"StageKillUI: UI 업데이트 → {currentKills}/{targetKills}");
            }
        }
        else
        {
            Debug.LogWarning("StageKillUI: killCountText가 연결되지 않았습니다!");
        }
    }
}