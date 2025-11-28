using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 스테이지별 적 처치 목표와 보상을 관리하는 매니저
/// - 목표 달성 시 자동으로 보상 패널 표시 및 다음 스테이지 준비
/// </summary>
public class StageManager : MonoBehaviour
{
    [System.Serializable]
    public class StageData
    {
        [Tooltip("스테이지 번호 (표시용)")]
        public int stageNumber = 1;

        [Tooltip("이 스테이지에서 죽여야 하는 적의 수")]
        public int targetKillCount = 5;

        [Tooltip("목표 달성 시 활성화할 보상 오브젝트")]
        public GameObject rewardObject;
    }

    [Header("스테이지 설정")]
    [Tooltip("스테이지별 데이터 배열")]
    [SerializeField] private StageData[] stages;

    [Header("UI 참조")]
    [Tooltip("적 처치 현황 UI 스크립트")]
    [SerializeField] private StageKillUI killUI;

    [Header("디버그")]
    [SerializeField] private bool showDebugLogs = true;

    // 현재 스테이지 인덱스 (0부터 시작)
    private int currentStageIndex = 0;

    // 현재 스테이지에서 죽인 적의 수
    private int currentKillCount = 0;

    // 싱글톤 인스턴스
    public static StageManager Instance { get; private set; }

    private void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 초기화: 모든 보상 오브젝트를 비활성화
        foreach (var stage in stages)
        {
            if (stage.rewardObject != null)
            {
                stage.rewardObject.SetActive(false);
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"StageManager: 초기화 완료. 총 {stages.Length}개의 스테이지");
        }
    }

    private void Start()
    {
        // 게임 시작 시 첫 번째 스테이지 UI 업데이트
        UpdateUI();

        // 씬에 있는 모든 적의 HealthSystem을 찾아서 OnDeath 이벤트에 구독
        RegisterAllEnemies();
    }

    /// <summary>
    /// 씬에 있는 모든 적의 OnDeath 이벤트에 구독합니다
    /// </summary>
    private void RegisterAllEnemies()
    {
        // "Enemy" 태그를 가진 모든 오브젝트 찾기
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        if (showDebugLogs)
        {
            Debug.Log($"StageManager: 씬에서 {enemies.Length}명의 적을 발견했습니다.");
        }

        foreach (var enemyObj in enemies)
        {
            HealthSystem healthSystem = enemyObj.GetComponent<HealthSystem>();
            if (healthSystem != null)
            {
                // OnDeath 이벤트에 OnEnemyKilled 메서드 연결
                healthSystem.OnDeath.AddListener(() => OnEnemyKilled(enemyObj));

                if (showDebugLogs)
                {
                    Debug.Log($"StageManager: {enemyObj.name}의 OnDeath 이벤트에 구독했습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"StageManager: {enemyObj.name}에 HealthSystem이 없습니다!");
            }
        }
    }

    /// <summary>
    /// 적이 죽었을 때 호출되는 메서드
    /// </summary>
    private void OnEnemyKilled(GameObject enemy)
    {
        if (showDebugLogs)
        {
            Debug.Log($"StageManager: {enemy.name}이(가) 사망했습니다!");
        }

        // 킬 카운트 증가
        currentKillCount++;

        // UI 업데이트
        UpdateUI();

        // 목표 달성 확인
        CheckStageComplete();
    }

    /// <summary>
    /// 현재 스테이지의 목표를 달성했는지 확인
    /// - 목표 달성 시 자동으로 보상 패널 표시 및 다음 스테이지 준비
    /// </summary>
    private void CheckStageComplete()
    {
        if (currentStageIndex >= stages.Length)
        {
            if (showDebugLogs)
            {
                Debug.Log("StageManager: 모든 스테이지를 클리어했습니다!");
            }
            return;
        }

        StageData currentStage = stages[currentStageIndex];

        // 목표 달성 확인
        if (currentKillCount >= currentStage.targetKillCount)
        {
            if (showDebugLogs)
            {
                Debug.Log($"StageManager: 스테이지 {currentStage.stageNumber} 목표 달성! ({currentKillCount}/{currentStage.targetKillCount})");
            }

            // 보상 오브젝트 활성화
            if (currentStage.rewardObject != null)
            {
                currentStage.rewardObject.SetActive(true);

                if (showDebugLogs)
                {
                    Debug.Log($"StageManager: 보상 오브젝트 [{currentStage.rewardObject.name}] 활성화!");
                }
            }
            else
            {
                Debug.LogWarning($"StageManager: 스테이지 {currentStage.stageNumber}의 보상 오브젝트가 설정되지 않았습니다!");
            }

            // ✅ 자동으로 다음 스테이지로 진행
            PrepareNextStage();
        }
    }

    /// <summary>
    /// 다음 스테이지 준비 (자동 호출)
    /// </summary>
    private void PrepareNextStage()
    {
        if (showDebugLogs)
        {
            Debug.Log($"StageManager: 다음 스테이지 준비 중...");
        }

        // 다음 스테이지로 이동
        currentStageIndex++;
        currentKillCount = 0;

        // 다음 스테이지가 있으면 UI 업데이트
        if (currentStageIndex < stages.Length)
        {
            UpdateUI();

            // 다음 스테이지의 적들도 등록 (만약 동적으로 생성된다면)
            RegisterAllEnemies();

            if (showDebugLogs)
            {
                Debug.Log($"StageManager: 스테이지 {stages[currentStageIndex].stageNumber} 시작!");
            }
        }
        else
        {
            if (showDebugLogs)
            {
                Debug.Log("StageManager: 🎉 모든 스테이지 클리어! 게임 종료");
            }

            // 모든 스테이지 클리어 시 UI 처리
            if (killUI != null)
            {
                killUI.UpdateKillCount(0, 0);
            }
        }
    }

    /// <summary>
    /// UI를 현재 킬 카운트로 업데이트
    /// </summary>
    private void UpdateUI()
    {
        if (currentStageIndex >= stages.Length)
        {
            return;
        }

        StageData currentStage = stages[currentStageIndex];

        if (killUI != null)
        {
            killUI.UpdateKillCount(currentKillCount, currentStage.targetKillCount);
        }
        else
        {
            Debug.LogWarning("StageManager: killUI가 연결되지 않았습니다!");
        }
    }

    /// <summary>
    /// 보상 획득 후 보상 패널 닫기 (버튼에서 호출 - 선택사항)
    /// </summary>
    public void CloseRewardPanel()
    {
        if (currentStageIndex <= 0 || currentStageIndex > stages.Length)
        {
            return;
        }

        // 이전 스테이지의 보상 패널 닫기
        StageData previousStage = stages[currentStageIndex - 1];
        if (previousStage.rewardObject != null)
        {
            previousStage.rewardObject.SetActive(false);

            if (showDebugLogs)
            {
                Debug.Log($"StageManager: 보상 패널 [{previousStage.rewardObject.name}] 닫기");
            }
        }
    }

    // ==================== 디버그 ====================

    /// <summary>
    /// 디버그용: 현재 상태 확인
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugLogs) return;

        GUI.Label(new Rect(10, 10, 300, 20), $"현재 스테이지: {(currentStageIndex < stages.Length ? stages[currentStageIndex].stageNumber.ToString() : "완료")}");
        GUI.Label(new Rect(10, 30, 300, 20), $"킬 카운트: {currentKillCount}");
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 적 1마리 죽이기")]
    private void TestKillEnemy()
    {
        OnEnemyKilled(gameObject); // 임시로 자기 자신을 적으로 간주
    }

    [ContextMenu("테스트: 스테이지 즉시 클리어")]
    private void TestCompleteStage()
    {
        if (currentStageIndex < stages.Length)
        {
            currentKillCount = stages[currentStageIndex].targetKillCount;
            CheckStageComplete();
        }
    }
#endif
}