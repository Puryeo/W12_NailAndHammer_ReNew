using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CooldownRUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("SimpleStakeRetrieval 컴포넌트 (인스펙터에 할당)")]
    [SerializeField] private SimpleStakeRetrieval retrieval;

    [Tooltip("Fill 이미지 (Image Type = Filled)")]
    [SerializeField] private Image coolDownBar;

    [Tooltip("시간 표시용 TextMeshProUGUI")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Display")]
    [Tooltip("바 보간 속도")]
    [SerializeField] private float lerpSpeed = 10f;

    [Tooltip("냉각 중 색상 / 사용 가능 색상")]
    [SerializeField] private Color coolingColor = Color.red;
    [SerializeField] private Color readyColor = Color.green;

    private float targetFill = 1f;
    private bool wasCooling = false;

    private void Reset()
    {
        // 기본 구성 시 안전 장치
        if (coolDownBar == null)
        {
            var img = GetComponentInChildren<Image>();
            if (img != null) coolDownBar = img;
        }

        if (timeText == null)
        {
            var tmp = GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) timeText = tmp;
        }
    }

    private void Start()
    {
        // 기본 상태: 준비(가득 찬)로 표시
        if (coolDownBar != null)
            coolDownBar.fillAmount = 1f;
    }

    private void Update()
    {
        if (retrieval == null)
        {
            // 인스펙터에 할당 안했으면 씬에서 자동 탐색 (최초 한 번)
            retrieval = FindObjectOfType<SimpleStakeRetrieval>();
            if (retrieval == null) return;
        }

        // UI는 realtime/unscaled 기준으로 표시 (히트스탑의 영향을 받지 않음)
        float remainingUnscaled = retrieval.GetCooldownRemainingUnscaled();
        float duration = retrieval.GetCooldownDuration();

        bool isCooling = remainingUnscaled > 0f;

        // 쿨다운이 방금 시작되면 fill을 0으로 리셋하여 "0부터 시작"하게 함
        if (isCooling && !wasCooling)
        {
            if (coolDownBar != null)
                coolDownBar.fillAmount = 0f;
        }
        wasCooling = isCooling;

        // duration이 0이면 항상 준비 상태로 처리
        if (duration <= 0f)
        {
            targetFill = 1f;
        }
        else
        {
            // fill: 1 = ready, 0 = 막 시작한 상태(남음 == duration)
            targetFill = Mathf.Clamp01(1f - (remainingUnscaled / duration));
        }

        if (coolDownBar != null)
        {
            // unscaledDeltaTime 사용하여 히트스탑 영향을 받지 않도록 함
            coolDownBar.fillAmount = Mathf.MoveTowards(coolDownBar.fillAmount, targetFill, Time.unscaledDeltaTime * lerpSpeed);
            coolDownBar.color = (remainingUnscaled > 0f) ? coolingColor : readyColor;
        }

        if (timeText != null)
        {
            if (remainingUnscaled > 0f)
            {
                timeText.text = $"{remainingUnscaled:F1}s";
                timeText.enabled = true;
            }
            else
            {
                timeText.text = "R";
            }
        }
    }
}