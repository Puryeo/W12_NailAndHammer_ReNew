using UnityEngine;

/// <summary>
/// 그로기 상태 UI 효과 - IGroggyEffect 구현
/// 프리팹을 Instantiate하여 GroggyUIView를 통해 UI를 제어합니다.
/// </summary>
public class GroggyUIEffect : MonoBehaviour, IGroggyEffect
{
    [Header("UI Prefab")]
    [Tooltip("그로기 UI 프리팹 (GroggyUIView가 부착된 Canvas)")]
    [SerializeField] private GameObject uiPrefab;

    [Header("UI Settings")]
    [Tooltip("UI 오프셋 (몬스터 머리 위 위치)")]
    [SerializeField] private Vector3 uiOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("UI 스케일 (X, Y 축)")]
    [SerializeField] private float uiScale = 0.005f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // 내부 상태
    private GroggyUIView uiView;
    private GameObject uiInstance;
    private EnemyController currentController;

    /// <summary>
    /// 그로기 진입 시: UI 생성 및 표시
    /// </summary>
    public void OnGroggyEnter(EnemyController controller)
    {
        currentController = controller;

        if (showDebugLogs) Debug.Log($"GroggyUIEffect [{controller.name}]: 그로기 진입");

        // UI 생성 (아직 없다면)
        if (uiView == null)
        {
            CreateUIInstance();
        }

        // GroggySettings 정보 가져오기
        var settings = GetGroggySettings(controller);
        if (settings != null && uiView != null)
        {
            bool canRecover = settings.enableRecovery;
            float recoveryDelay = settings.recoveryDelay;

            if (showDebugLogs) Debug.Log($"GroggyUIEffect: UI 표시 (canRecover={canRecover}, delay={recoveryDelay})");

            // UI에 표시 요청
            uiView.Show(canRecover, recoveryDelay);
        }
        else
        {
            if (uiView == null)
                Debug.LogError($"GroggyUIEffect [{controller.name}]: UI View를 생성하지 못했습니다!");
        }
    }

    /// <summary>
    /// 그로기 회복 완료 시: UI 숨김
    /// </summary>
    public void OnGroggyComplete(EnemyController controller)
    {
        if (showDebugLogs) Debug.Log($"GroggyUIEffect [{controller.name}]: 부활 완료 - UI 숨김");

        if (uiView != null)
        {
            uiView.Hide();
        }

        currentController = null;
    }

    /// <summary>
    /// 처형 등으로 중단 시: UI 정리
    /// </summary>
    public void Cleanup()
    {
        if (showDebugLogs) Debug.Log($"GroggyUIEffect [{gameObject.name}]: Cleanup");

        if (uiView != null)
        {
            uiView.Hide();
        }

        currentController = null;
    }

    /// <summary>
    /// UI 인스턴스 생성
    /// </summary>
    private void CreateUIInstance()
    {
        if (uiPrefab == null)
        {
            Debug.LogError($"GroggyUIEffect [{gameObject.name}]: UI Prefab이 할당되지 않았습니다! Inspector에서 할당하세요.");
            return;
        }

        // 프리팹 인스턴스화
        uiInstance = Instantiate(uiPrefab);
        uiInstance.name = $"GroggyUI_{gameObject.name}";

        // 자식으로 설정 (worldPositionStays=false로 로컬 설정 초기화)
        uiInstance.transform.SetParent(transform, worldPositionStays: false);

        // 위치는 월드 좌표로 설정 (몬스터 위치 + 오프셋)
        uiInstance.transform.position = transform.position + uiOffset;

        // 회전은 월드 좌표 기준으로 고정 (몬스터 회전 무시)
        uiInstance.transform.rotation = Quaternion.identity;

        // 스케일 적용 (X, Y만 적용, Z는 1 유지)
        uiInstance.transform.localScale = new Vector3(uiScale, uiScale, 1f);

        // GroggyUIView 가져오기
        uiView = uiInstance.GetComponent<GroggyUIView>();

        if (uiView == null)
        {
            Debug.LogError($"GroggyUIEffect [{gameObject.name}]: 프리팹에 GroggyUIView 컴포넌트가 없습니다!");
            Destroy(uiInstance);
            uiInstance = null;
            return;
        }

        // 초기 상태: 숨김
        uiView.Hide();

        if (showDebugLogs) Debug.Log($"GroggyUIEffect [{gameObject.name}]: UI 인스턴스 생성 완료 (자식으로 배치, 월드 좌표 사용)");
    }

    /// <summary>
    /// UI 위치 업데이트 (LateUpdate에서 몬스터 위치 추적)
    /// 회전과 무관하게 월드 좌표 기준 오프셋 위치에 고정
    /// </summary>
    private void LateUpdate()
    {
        if (uiView != null && uiView.IsVisible() && uiInstance != null)
        {
            // 월드 좌표 기준 오프셋 적용 (회전 무시)
            uiInstance.transform.position = transform.position + uiOffset;

            // UI 회전은 항상 월드 좌표 기준으로 고정 (몬스터 회전 무시)
            uiInstance.transform.rotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// 컴포넌트 파괴 시 UI도 정리
    /// UI가 자식으로 설정되어 있으므로 자동으로 파괴되지만, 명시적으로 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        // 자식으로 설정되어 있으므로 자동 파괴되지만, 명시적 정리
        if (uiInstance != null)
        {
            Destroy(uiInstance);
            uiInstance = null;
            uiView = null;
        }
    }

    /// <summary>
    /// EnemyController에서 GroggySettings 가져오기 (리플렉션)
    /// </summary>
    private GroggySettings GetGroggySettings(EnemyController controller)
    {
        if (controller == null) return null;

        var field = controller.GetType().GetField("groggySettings",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            return field.GetValue(controller) as GroggySettings;
        }

        return null;
    }

    // 기즈모로 UI 위치 시각화
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + uiOffset, 0.2f);
    }
}
