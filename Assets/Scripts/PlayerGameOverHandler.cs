using System.Collections;
using UnityEngine;

/// <summary>
/// HealthSystem을 수정하지 않고 플레이어 체력 0(=OnZeroHealth) 시
/// 지정된 Canvas(GameObject)를 활성화하여 게임오버 화면을 띄웁니다.
/// </summary>
[RequireComponent(typeof(HealthSystem))]
public class PlayerGameOverHandler : MonoBehaviour
{
    [Tooltip("제로 헬스(플레이어) 도달 시 활성화할 Canvas(GameObject). 에디터에서 비활성화 상태로 두세요.")]
    [SerializeField] private GameObject gameOverCanvas = null;

    [Tooltip("OnZeroHealth 호출 후 게임오버 UI를 띄우기까지의 지연(초)")]
    [SerializeField] private float showDelay = 0f;

    [Tooltip("게임오버 시 플레이어 컨트롤(예: PlayerController, PlayerCombat)을 비활성화할지 여부")]
    [SerializeField] private bool disablePlayerOnGameOver = true;

    private HealthSystem healthSystem;

    private void Awake()
    {
        healthSystem = GetComponent<HealthSystem>();
        if (healthSystem == null)
        {
            Debug.LogWarning($"PlayerGameOverHandler [{gameObject.name}]: HealthSystem을 찾을 수 없습니다.");
        }
    }

    private void OnEnable()
    {
        if (healthSystem != null) healthSystem.OnZeroHealth.AddListener(HandleZeroHealth);
    }

    private void OnDisable()
    {
        if (healthSystem != null) healthSystem.OnZeroHealth.RemoveListener(HandleZeroHealth);
    }

    private void HandleZeroHealth()
    {
        // 코루틴으로 지연을 지원
        StartCoroutine(ShowGameOverCoroutine());
    }

    private IEnumerator ShowGameOverCoroutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, showDelay));

        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
            Debug.Log($"PlayerGameOverHandler [{gameObject.name}]: gameOverCanvas 활성화");
        }
        else
        {
            Debug.LogWarning($"PlayerGameOverHandler [{gameObject.name}]: gameOverCanvas가 할당되지 않았습니다.");
        }

        if (disablePlayerOnGameOver)
        {
            var pc = GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false;

            var combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = false;
        }
    }
}