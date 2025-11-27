using UnityEngine;

/// <summary>
/// 패널 닫기 전용 스크립트
/// </summary>
public class PanelCloser : MonoBehaviour
{
    [SerializeField] private GameObject panelToClose;

    public void ClosePanel()
    {
        Debug.Log("PanelCloser: 패널 닫기!");

        if (panelToClose != null)
        {
            panelToClose.SetActive(false);
        }
        else
        {
            Debug.LogError("PanelCloser: 닫을 패널이 연결되지 않음!");
        }

        Time.timeScale = 1; // 게임 재개
    }
}