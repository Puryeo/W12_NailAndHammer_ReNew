using UnityEngine;

/// <summary>
/// 스테이지 보상 오브젝트에 붙일 스크립트
/// 플레이어와 충돌 시 보상 패널을 띄우고 자동으로 보상 획득 처리
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class StageReward : MonoBehaviour
{
    [Header("보상 패널 설정")]
    [Tooltip("띄울 보상 패널 UI (Canvas 안의 Panel)")]
    [SerializeField] private GameObject rewardPanel;

    [Header("자동 닫기 설정")]
    [Tooltip("보상 패널을 자동으로 닫을지 여부 (true면 자동, false면 수동)")]
    [SerializeField] private bool autoClosePanel = false;

    [Tooltip("자동으로 패널을 닫을 때까지 대기 시간 (초)")]
    [SerializeField] private float autoCloseDelay = 2f;

    [Header("디버그")]
    [SerializeField] private bool showDebugLogs = true;

    // 패널이 현재 켜져있는지 여부
    private bool isPanelActive = false;

    // 보상 획득 처리가 완료되었는지
    private bool hasClaimedReward = false;

    private void Awake()
    {
        // 처음엔 보상 패널을 꺼둡니다
        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
        }

        // Collider가 Trigger로 설정되어 있는지 확인
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            if (showDebugLogs)
            {
                Debug.Log($"StageReward [{gameObject.name}]: Collider를 Trigger로 자동 설정했습니다.");
            }
        }
    }

    private void Update()
    {
        // 패널이 켜져있고 아직 보상을 받지 않았을 때만 엔터 키 입력 감지
        if (isPanelActive && !hasClaimedReward && Input.GetKeyDown(KeyCode.Return))
        {
            if (showDebugLogs)
            {
                Debug.Log($"StageReward [{gameObject.name}]: 엔터 키 입력! 보상 획득 처리");
            }
            ClaimReward();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 이미 보상을 받았으면 무시
        if (hasClaimedReward)
        {
            return;
        }

        // 플레이어와 충돌했는지 확인
        if (collision.CompareTag("Player"))
        {
            if (showDebugLogs)
            {
                Debug.Log($"StageReward [{gameObject.name}]: 플레이어와 충돌! 보상 패널을 띄웁니다.");
            }

            // 보상 패널 활성화
            if (rewardPanel != null)
            {
                // 일시정지
                Time.timeScale = 0f;

                rewardPanel.SetActive(true);
                isPanelActive = true;

                // ✅ 자동으로 보상 획득 처리
                ClaimReward();

                // 자동 닫기 옵션이 켜져있으면 일정 시간 후 패널 닫기
                if (autoClosePanel)
                {
                    Invoke(nameof(ClosePanel), autoCloseDelay);
                }
            }
            else
            {
                Debug.LogWarning($"StageReward [{gameObject.name}]: 보상 패널이 연결되지 않았습니다!");
            }
        }
    }

    /// <summary>
    /// 보상 획득 처리 (자동 호출)
    /// </summary>
    private void ClaimReward()
    {
        if (hasClaimedReward)
        {
            return; // 중복 처리 방지
        }

        hasClaimedReward = true;

        // StageManager는 이미 CheckStageComplete()에서 다음 스테이지 준비를 완료한 상태
        // 추가 처리 필요 없음

        if (showDebugLogs)
        {
            Debug.Log($"StageReward [{gameObject.name}]: 보상 획득 완료! (StageManager가 자동으로 다음 스테이지 준비 완료)");
        }
    }

    /// <summary>
    /// 보상 패널 닫기 (수동 또는 자동)
    /// </summary>
    public void ClosePanel()
    {
        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
            isPanelActive = false;

            if (showDebugLogs)
            {
                Debug.Log($"StageReward [{gameObject.name}]: 보상 패널 닫기");
            }
        }

        // 보상 오브젝트 자체도 비활성화 (재사용 방지)
        gameObject.SetActive(false);
    }
}