using System.Collections;
using UnityEngine;

public class SpineWaveController : MonoBehaviour
{
    [Header("Structure")]
    [Tooltip("Spine1, Spine2, Spine3 그룹 부모들을 연결")]
    [SerializeField] private Transform[] spineGroups;

    [Header("Visual Effects (Finale)")]
    [Tooltip("마지막에 터질 파티클 프리팹")]
    [SerializeField] private ParticleSystem finaleParticlePrefab;
    [Tooltip("마지막 가시가 얼마나 더 커질지 (1.0 = 동일, 1.3 = 30% 더 큼)")]
    [SerializeField] private float finalScaleMultiplier = 1.3f;

    [Header("Animation Settings")]
    [SerializeField] private float riseDuration = 0.2f;
    [SerializeField] private float delayBetweenSpikes = 0.04f;

    [Header("Juice (Shake)")]
    [SerializeField] private float posShakeStrength = 0.1f;
    [SerializeField] private float rotShakeStrength = 5.0f;

    [Header("Life Time")]
    [SerializeField] private float destroyDelay = 5.0f;

    // 내부 상태
    private bool isWaveActive = true;
    private CameraShake cameraShake;

    private void Awake()
    {
        if (Camera.main != null) cameraShake = Camera.main.GetComponent<CameraShake>();
    }

    private void Start()
    {
        // 1. 지속 쉐이크 시작 (별도 코루틴)
        StartCoroutine(ContinuousCameraShakeRoutine());

        // 2. 가시 웨이브 시작
        foreach (Transform group in spineGroups)
        {
            if (group != null)
                StartCoroutine(GroupWaveRoutine(group));
        }

        Destroy(gameObject, destroyDelay);
    }

    private IEnumerator ContinuousCameraShakeRoutine()
    {
        // 카메라 쉐이크 스크립트가 없으면 중단
        if (cameraShake == null) yield break;

        // 웨이브가 끝날 때까지 계속 떪
        while (isWaveActive)
        {
            cameraShake.ShakeCustom(0.5f); // 0.2초 동안 흔들림

            // ShakeWeak의 duration이 0.2f이므로, 0.2초 대기 후 다시 호출
            yield return new WaitForSeconds(0.2f);
        }

        // 웨이브가 끝나면 흔들림 강제 종료 (깔끔하게)
        cameraShake.StopShake();
    }

    private IEnumerator GroupWaveRoutine(Transform group)
    {
        int childCount = group.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform spike = group.GetChild(i);

            // 마지막 가시인지 확인 (그룹별 마지막 놈)
            bool isFinalSpike = (i == childCount - 1);

            // 현재 에디터에 설정된 '진짜 스케일' 캡처
            Vector3 realScale = spike.localScale;

            // 코루틴 시작
            StartCoroutine(AnimateSpike(spike, realScale, isFinalSpike));

            // 마지막 가시가 등장했다면 지속 쉐이크 종료 신호
            if (isFinalSpike) isWaveActive = false;

            yield return new WaitForSeconds(delayBetweenSpikes);
        }
    }

    private IEnumerator AnimateSpike(Transform spike, Vector3 targetScale, bool isFinal)
    {
        // 마지막 가시는 좀 더 크게!
        if (isFinal) targetScale *= finalScaleMultiplier;

        // 1. 시작 전 세팅
        Vector3 startScale = targetScale;
        startScale.y = 0f;
        spike.localScale = startScale;
        spike.gameObject.SetActive(true);

        Vector3 originalPos = spike.localPosition;
        Quaternion originalRot = spike.localRotation;

        if (isFinal)
        {
            if (cameraShake != null) cameraShake.ShakeCustom(3f);

            if (finaleParticlePrefab != null)
            {
                ParticleSystem ps = Instantiate(finaleParticlePrefab, spike.position, Quaternion.identity);

                SpriteRenderer sr = spike.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    var main = ps.main;
                    main.startColor = sr.color;
                }

                ps.Play();
                Destroy(ps.gameObject, 2.0f);
            }
        }

        float timer = 0f;
        while (timer < riseDuration)
        {
            timer += Time.deltaTime;
            float t = timer / riseDuration;
            float ease = Mathf.SmoothStep(0f, 1f, t);

            // 스케일 복구
            Vector3 currentScale = targetScale;
            currentScale.y = Mathf.Lerp(0f, targetScale.y, ease);
            spike.localScale = currentScale;

            // 가시 자체의 흔들림
            float shakeFactor = (1f - t);
            Vector3 randomOffset = Random.insideUnitCircle * posShakeStrength * shakeFactor;
            spike.localPosition = originalPos + randomOffset;

            float randomRot = Random.Range(-rotShakeStrength, rotShakeStrength) * shakeFactor;
            spike.localRotation = originalRot * Quaternion.Euler(0, 0, randomRot);

            yield return null;
        }

        // 복구
        spike.localScale = targetScale;
        spike.localPosition = originalPos;
        spike.localRotation = originalRot;
    }
}