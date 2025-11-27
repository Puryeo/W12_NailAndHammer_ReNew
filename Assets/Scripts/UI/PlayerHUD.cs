using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PlayerHUD - HP Bar(이미지 Fill) + 텍스트 동시 지원
/// - hpBarImage: Image(Type = Filled)을 할당하면 부드러운 Fill 애니메이션 적용
/// - hpText / hpTMP: 텍스트 형식 중 사용할 것을 인스펙터에서 연결
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("HP Bar Settings")]
    [Tooltip("HP 게이지 이미지 (Image Type: Filled)")]
    [SerializeField] private Image hpBarImage;
    [Tooltip("게이지가 변하는 속도 (높을수록 빠름)")]
    [SerializeField] private float lerpSpeed = 5f;

    [Header("UI References (Legacy Text or TMP)")]
    [Tooltip("Legacy Text (optional)")]
    [SerializeField] private Text hpText;
    [Tooltip("Legacy Text (optional)")]
    [SerializeField] private Text ammoText;

    [Tooltip("TextMeshProUGUI (optional)")]
    [SerializeField] private TextMeshProUGUI hpTMP;
    [Tooltip("TextMeshProUGUI (optional)")]
    [SerializeField] private TextMeshProUGUI ammoTMP;

    private HealthSystem healthSystem;
    private PlayerCombat playerCombat;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            healthSystem = player.GetComponent<HealthSystem>();
            playerCombat = player.GetComponent<PlayerCombat>();
        }
        else
        {
            Debug.LogWarning("PlayerHUD: 'Player' 태그를 가진 오브젝트를 찾을 수 없습니다.");
        }

        if (hpBarImage != null)
        {
            // Image가 Filled 타입인지 확인 (사용자 실수 방지)
            if (hpBarImage.type != Image.Type.Filled)
            {
                Debug.LogWarning("PlayerHUD: hpBarImage의 Type을 'Filled'로 설정하세요. 현재 동작은 예상과 다를 수 있습니다.");
            }

            // 초기값 설정 (HealthSystem이 있으면 비율로, 없으면 1)
            if (healthSystem != null)
                hpBarImage.fillAmount = Mathf.Clamp01(healthSystem.GetCurrentHealth() / Mathf.Max(1f, healthSystem.GetMaxHealth()));
            else
                hpBarImage.fillAmount = 1f;
        }

        // 초기 텍스트 업데이트
        RefreshTextsImmediate();
    }

    private void Update()
    {
        UpdateHP();
        UpdateAmmo();
    }

    private void UpdateHP()
    {
        if (healthSystem == null)
        {
            // HealthSystem이 할당되지 않았으면 텍스트만 클리어 혹은 무시
            return;
        }

        float cur = healthSystem.GetCurrentHealth();
        float max = healthSystem.GetMaxHealth();
        float targetFillAmount = (max > 0f) ? (cur / max) : 0f;

        if (hpBarImage != null)
        {
            // 부드러운 보간 (프레임 독립)
            float current = hpBarImage.fillAmount;
            float next = Mathf.Lerp(current, targetFillAmount, Time.deltaTime * lerpSpeed);
            hpBarImage.fillAmount = next;
        }

        string hpString = $"HP: {cur:F0} / {max:F0}";
        if (hpTMP != null) hpTMP.text = hpString;
        if (hpText != null) hpText.text = hpString;
    }

    private void UpdateAmmo()
    {
        if (playerCombat == null) return;

        int curAmmo = playerCombat.GetCurrentAmmo();
        int maxAmmo = playerCombat.GetMaxAmmo();
        string ammoString = $"남은 말뚝: {curAmmo} / {maxAmmo}";

        if (ammoTMP != null) ammoTMP.text = ammoString;
        if (ammoText != null) ammoText.text = ammoString;
    }

    // 시작 시나 외부에서 즉시 텍스트를 갱신하고 싶을 때 호출
    private void RefreshTextsImmediate()
    {
        if (healthSystem != null)
        {
            float cur = healthSystem.GetCurrentHealth();
            float max = healthSystem.GetMaxHealth();
            string hpString = $"HP: {cur:F0} / {max:F0}";
            if (hpTMP != null) hpTMP.text = hpString;
            if (hpText != null) hpText.text = hpString;
        }

        if (playerCombat != null)
        {
            int curAmmo = playerCombat.GetCurrentAmmo();
            int maxAmmo = playerCombat.GetMaxAmmo();
            string ammoString = $"Ammo: {curAmmo} / {maxAmmo}";
            if (ammoTMP != null) ammoTMP.text = ammoString;
            if (ammoText != null) ammoText.text = ammoString;
        }
    }
}