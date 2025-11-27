using UnityEngine;

/// <summary>
/// 투사체 테스트용 모드 스위치
/// - 1, 2, 3 키로 투사체 Config만 전환
/// - 실제 발사는 PlayerCombat이 담당
/// - PlayerCombat이 GetCurrentConfig()로 현재 선택된 Config 가져감
/// </summary>
public class ProjectileTestController : MonoBehaviour
{
    [Header("Test Configs")]
    [Tooltip("1번 키: 기본 말뚝 (StickToEnemy + Simple)")]
    public ProjectileConfig config1_Basic;

    [Tooltip("2번 키: 회수 도중 충돌 시 속박")]
    public ProjectileConfig config2_Pull;

    [Tooltip("3번 키: 끌어오기 회수 (StickToEnemy + Pull)")]
    public ProjectileConfig config3_ImpaleBinding;

    [Header("Debug")]
    [SerializeField] private bool showDebugUI = true;
    [SerializeField] private bool showDebugLogs = true;

    [Header("UI")]
    [Tooltip("모드 선택 시 Time.timeScale이 0으로 멈춰있다면 자동으로 복구할지 여부")]
    [SerializeField] private bool resumeTimeOnModeSelect = true;

    [Tooltip("버튼 클릭 후 자동으로 닫을 패널 (Inspector에 할당)")]
    [SerializeField] private GameObject modePanel;

    // 내부 상태
    private int currentMode = 1; // 1, 2, 3

    private void Update()
    {
        HandleModeSwitch();
    }

    private void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetMode1();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetMode2();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetMode3();
        }
    }

    // ========== Public 메서드: UI 버튼에서 호출 가능 ==========

    /// <summary>
    /// 모드 1로 변경: 기본 말뚝 (StickToEnemy + Simple)
    /// UI 버튼의 OnClick에 연결 가능
    /// </summary>
    public void SetMode1()
    {
        currentMode = 1;
        if (showDebugLogs) Debug.Log($"[ProjectileTest] Mode 1: 기본 말뚝 (StickToEnemy + Simple)");
        ResumeIfNeeded();
    }

    /// <summary>
    /// 모드 2로 변경: 끌어오기 회수 (StickToEnemy + Pull)
    /// UI 버튼의 OnClick에 연결 가능
    /// </summary>
    public void SetMode2()
    {
        currentMode = 2;
        if (showDebugLogs) Debug.Log($"[ProjectileTest] Mode 2: 끌어오기 회수 (StickToEnemy + Pull)");
        ResumeIfNeeded();
    }

    /// <summary>
    /// 모드 3으로 변경: 꿰뚫기 + 속박 (ImpaleAndCarry + Binding)
    /// UI 버튼의 OnClick에 연결 가능
    /// </summary>
    public void SetMode3()
    {
        currentMode = 3;
        if (showDebugLogs) Debug.Log($"[ProjectileTest] Mode 3: 꿰뚫기 + 속박 (ImpaleAndCarry + Binding)");
        ResumeIfNeeded();
    }

    /// <summary>
    /// 범용 모드 변경 메서드
    /// UI 버튼의 OnClick에 연결 가능 (인자로 1, 2, 3 전달)
    /// </summary>
    /// <param name="mode">변경할 모드 (1, 2, 3)</param>
    public void SetMode(int mode)
    {
        if (mode < 1 || mode > 3)
        {
            Debug.LogWarning($"[ProjectileTest] 잘못된 모드: {mode} (1~3만 가능)");
            return;
        }

        currentMode = mode;
        if (showDebugLogs)
        {
            Debug.Log($"[ProjectileTest] Mode {mode}: {GetModeName()}");
        }

        ResumeIfNeeded();
    }

    /// <summary>
    /// 버튼에서 패널을 닫고(TimeScale 복구) 모드도 설정하는 편의 메서드들.
    /// Inspector에서 버튼 OnClick에 연결하세요.
    /// </summary>
    public void ClosePanelAndResume()
    {
        if (modePanel != null)
        {
            modePanel.SetActive(false);
            if (showDebugLogs) Debug.Log($"[ProjectileTest] Closed panel '{modePanel.name}'.");
        }
        ResumeIfNeeded();
    }

    public void ClosePanelAndResume_SetMode(int mode)
    {
        SetMode(mode);
        ClosePanelAndResume();
    }

    public void ClosePanelAndResume_SetMode1() => ClosePanelAndResume_SetMode(1);
    public void ClosePanelAndResume_SetMode2() => ClosePanelAndResume_SetMode(2);
    public void ClosePanelAndResume_SetMode3() => ClosePanelAndResume_SetMode(3);

    /// <summary>
    /// 현재 선택된 Config 반환 (PlayerCombat에서 호출)
    /// </summary>
    public ProjectileConfig GetCurrentConfig()
    {
        switch (currentMode)
        {
            case 1: return config1_Basic;
            case 2: return config2_Pull;
            case 3: return config3_ImpaleBinding;
            default: return config1_Basic;
        }
    }

    /// <summary>
    /// 현재 모드 번호 반환 (1, 2, 3)
    /// </summary>
    public int GetCurrentMode() => currentMode;

    /// <summary>
    /// 현재 모드 이름 반환 (UI 텍스트 표시용)
    /// </summary>
    public string GetModeName()
    {
        switch (currentMode)
        {
            case 1: return "기본 말뚝";
            case 2: return "끌어오기 회수";
            case 3: return "꿰뚫기 + 속박";
            default: return "Unknown";
        }
    }

    /// <summary>
    /// 특정 모드의 이름 반환 (UI 버튼 텍스트용)
    /// </summary>
    public string GetModeNameByIndex(int mode)
    {
        switch (mode)
        {
            case 1: return "기본 말뚝";
            case 2: return "끌어오기 회수";
            case 3: return "꿰뚫기 + 속박";
            default: return "Unknown";
        }
    }

    /// <summary>
    /// 현재 모드의 색상 반환 (UI 색상 표시용)
    /// </summary>
    public Color GetModeColor()
    {
        switch (currentMode)
        {
            case 1: return Color.white;
            case 2: return Color.cyan;
            case 3: return Color.magenta;
            default: return Color.white;
        }
    }

    /// <summary>
    /// 특정 모드의 색상 반환 (UI 버튼 색상용)
    /// </summary>
    public Color GetModeColorByIndex(int mode)
    {
        switch (mode)
        {
            case 1: return Color.white;
            case 2: return Color.cyan;
            case 3: return Color.magenta;
            default: return Color.white;
        }
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        // 배경
        GUI.Box(new Rect(10, 10, 350, 100), "");

        // 제목
        GUI.Label(new Rect(20, 15, 300, 30), "=== Projectile Test ===", style);

        // 현재 모드
        style.fontSize = 16;
        style.normal.textColor = GetModeColor();
        GUI.Label(new Rect(20, 45, 300, 25), $"Current Mode: [{currentMode}] {GetModeName()}", style);

        // 설명
        style.fontSize = 12;
        style.normal.textColor = Color.gray;
        GUI.Label(new Rect(20, 70, 300, 20), "1/2/3 키 또는 UI 버튼: 모드 전환", style);
        GUI.Label(new Rect(20, 85, 300, 20), "좌클릭: 발사 (PlayerCombat)", style);
    }

    // TimeScale 복구 유틸리티
    private void ResumeIfNeeded()
    {
        if (!resumeTimeOnModeSelect) return;

        // 일시정지(패널)로 Time.timeScale이 0으로 되어 있다면 복구
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 1f;
            if (showDebugLogs) Debug.Log("[ProjectileTest] Time.timeScale restored to 1 on mode select");
        }
    }

    // 외부에서 명시적으로 재개시킬 필요가 있으면 public으로도 제공
    public void ResumeGame() => Time.timeScale = 1f;
}

/*
 * ========== UI 버튼 연결 예제 ==========
 *
 * 인스펙터 사용:
 * 1. Canvas에 모드 선택 패널을 만들고, 그 패널 GameObject를 ProjectileTestController.modePanel에 드래그하여 연결.
 * 2. 각 버튼의 OnClick()에 ProjectileTestController.SetMode1/2/3 또는 SetMode(int)를 연결.
 * 3. 버튼 클릭 시 자동으로 패널이 닫힙니다.
 *
 * 코드 변경 없이 Inspector에서만 할 경우:
 * - 버튼의 OnClick()에 패널 GameObject를 드래그하고 __GameObject.SetActive__(false)를 추가하면 동일한 효과를 얻을 수 있습니다.
 *
 */
