using UnityEngine;

public class SimpleRelicTest : MonoBehaviour
{
    [SerializeField] private GameObject panelToShow;

    void Start()
    {
        Debug.Log("SimpleRelicTest: 시작!");

        // 패널 확인
        if (panelToShow == null)
        {
            Debug.LogError("SimpleRelicTest: 패널이 연결 안됨!");
        }
        else
        {
            Debug.Log("SimpleRelicTest: 패널 연결됨!");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"SimpleRelicTest: 뭔가 닿음! 이름: {other.gameObject.name}, Tag: {other.tag}");

        // Player 태그 체크!
        if (other.CompareTag("Player"))
        {
            Debug.Log("SimpleRelicTest: 플레이어 확인됨!");

            if (panelToShow != null)
            {
                Debug.Log("SimpleRelicTest: 패널 켜기 시도!");
                panelToShow.SetActive(true);
                Time.timeScale = 0; // 게임 멈추기
            }

            Destroy(gameObject); // 유물 삭제
        }
        else
        {
            Debug.Log($"SimpleRelicTest: Player가 아님! Tag: {other.tag}");
        }
    }
}