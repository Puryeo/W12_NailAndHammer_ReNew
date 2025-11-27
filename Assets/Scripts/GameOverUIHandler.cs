using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임오버 UI용 핸들러
/// - TMP 기반 버튼의 OnClick에 RestartGame 또는 QuitGame을 연결하세요.
/// </summary>
public class GameOverUIHandler : MonoBehaviour
{
    [Tooltip("재시작 시 로드할 씬 이름을 비워두면 현재 씬을 다시 로드합니다.")]
    [SerializeField] private string sceneToLoad = "";

    /// <summary>
    /// 버튼에서 호출: 현재 씬을 재시작하거나 sceneToLoad가 지정되어 있으면 해당 씬을 로드합니다.
    /// </summary>
    public void RestartGame()
    {
        // 일시정지 상태 해제
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex);
        }
        else
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    /// <summary>
    /// 버튼에서 호출: 애플리케이션 종료 (에디터에서는 에디터 재생 중지).
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}