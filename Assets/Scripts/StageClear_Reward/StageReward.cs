using UnityEngine;

/// <summary>
/// 스테이지 보상 오브젝트에 붙일 스크립트
/// 플레이어와 충돌 시 보상 패널을 띄우고, 엔터 키로 보상 획득
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class StageReward : MonoBehaviour
{
    [Header("보상 패널 설정")]
    [Tooltip("띄울 보상 패널 UI (Canvas 안의 Panel)")]
    [SerializeField] private GameObject rewardPanel;

    [Header("디버그")]
    [SerializeField] private bool showDebugLogs = true;

    // 패널이 현재 켜져있는지 여부
    private bool isPanelActive = false;

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
        // 패널이 켜져있을 때만 엔터 키 입력 감지
        if (isPanelActive && Input.GetKeyDown(KeyCode.Return))
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
                rewardPanel.SetActive(true);
                isPanelActive = true;

                // Time.timeScale = 0으로 게임을 일시정지하고 싶다면 여기에 추가
                // Time.timeScale = 0f;
            }
            else
            {
                Debug.LogWarning($"StageReward [{gameObject.name}]: 보상 패널이 연결되지 않았습니다!");
            }
        }
    }

    /// <summary>
    /// 보상 획득 처리 (엔터 키로 호출)
    /// </summary>
    private void ClaimReward()
    {
        // 패널 끄기
        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
            isPanelActive = false;
        }

        // StageManager에게 다음 스테이지로 넘어가라고 알림
        if (StageManager.Instance != null)
        {
            StageManager.Instance.ClaimRewardAndNextStage();
        }
        else
        {
            Debug.LogWarning($"StageReward [{gameObject.name}]: StageManager를 찾을 수 없습니다!");
        }

        if (showDebugLogs)
        {
            Debug.Log($"StageReward [{gameObject.name}]: 보상 획득 완료! 다음 스테이지로 이동");
        }
    }
}