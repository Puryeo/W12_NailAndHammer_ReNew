using UnityEngine;
using System.Collections;
using System.Reflection;

[DisallowMultipleComponent]
public class GuardianSkillController : MonoBehaviour, ISecondaryChargedAttack
{
    [Header("Debug")]
    [Tooltip("테스트용 키 입력 활성화")]
    [SerializeField] private bool enableDebugKey = true;

    [Header("1. Camera Settings")]
    [SerializeField] private float targetCamSize = 10f;
    [SerializeField] private float zoomDuration = 0.5f;

    [Header("2. Guardian Settings")]
    [SerializeField] private GameObject guardianPrefab;
    [Tooltip("플레이어 기준 소환 위치 (예: x=-2.5면 플레이어 등 뒤)")]
    [SerializeField] private Vector2 spawnOffset = new Vector2(-2.5f, 0f);
    [Tooltip("수호신이 바라볼 로컬 회전(옵션)")]
    [SerializeField] private float guardianLocalZRotation = 0f;
    [Tooltip("수호신이 유지되는 시간 (공격 후 페이드 전 대기)")]
    [SerializeField] private float lingerDuration = 1.0f;
    [Tooltip("수호신 페이드 아웃 시간")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("2.5 Hammer Prefab (실제 휘두르는 오브젝트)")]
    [Tooltip("수호신은 정지, 실제 휘두르는 해머 프리팹을 할당하세요 (HammerSwingController 필요)")]
    [SerializeField] private GameObject hammerPrefab;
    [Tooltip("해머가 수호신 기준으로 스폰될 로컬 오프셋")]
    [SerializeField] private Vector2 hammerLocalOffset = Vector2.zero;

    [Header("3. Combat Settings")]
    [SerializeField] private float damage = 80f;
    [SerializeField] private float knockbackForce = 25f;
    [SerializeField] private float swingAngle = 180f;
    [SerializeField] private float swingDuration = 0.6f;
    [SerializeField] private float executeHealAmount = 50f;
    [SerializeField] private AnimationCurve swingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("4. Visual: Hammer Trail (TrailRenderer)")]
    [Tooltip("해머 궤적을 TrailRenderer로 표시")]
    [SerializeField] private bool enableHammerTrail = true;
    [Tooltip("트레일이 남아있는 시간(초) — 해머 제거 후 페이드 아웃 시간")]
    [SerializeField] private float trailTime = 0.6f;
    [Tooltip("트레일 시작 너비")]
    [SerializeField] private float trailStartWidth = 0.14f;
    [Tooltip("트레일 끝 너비")]
    [SerializeField] private float trailEndWidth = 0.0f;
    [Tooltip("트레일 색상 (시작) -> 알파가 감소하며 사라짐)")]
    [SerializeField] private Color trailColor = new Color(0.7f, 0.2f, 1f, 1f);
    [Tooltip("트레일 최소 정점 거리 (작게 하면 더 촘촘)")]
    [SerializeField] private float trailMinVertexDistance = 0.05f;
    [Tooltip("트레일에 쓸 Material(Optional). 할당하지 않으면 기본 Sprites/Default 사용")]
    [SerializeField] private Material trailMaterial = null;
    [Tooltip("해머 내부의 특정 자식 Transform 이름에 트레일을 붙이고 싶다면 이름 입력(예: \"HeadPoint\"). 비워두면 해머 루트에 붙음")]
    [SerializeField] private string trailAttachPointName = "";
    [Tooltip("해머 루트(또는 attach point가 없을 때)에서 사용할 로컬 오프셋")]
    [SerializeField] private Vector3 trailLocalOffset = new Vector3(0.5f, 0f, 0f);

    // 내부 상태
    private PlayerCombat playerCombat;
    private bool isSkillActive = false; // 중복 실행 방지

    private void Awake()
    {
        playerCombat = GetComponent<PlayerCombat>();

        // PlayerCombat이 있으면 자동 등록 (씬 컴포넌트 방식 지원)
        if (playerCombat != null)
        {
            playerCombat.SetSecondaryChargedAttack(this);
            Debug.Log("[GuardianSkillController] PlayerCombat에 자동 등록됨.");
        }
    }

    private void Update()
    {
        // 디버그용 M키 입력
        if (enableDebugKey && Input.GetKeyDown(KeyCode.M))
        {
            if (playerCombat != null)
                Execute(playerCombat, transform);
            else
                Execute(null, transform);
        }
    }

    // 편의용: 파라미터 없는 실행 (자기 transform 사용)
    public void Execute()
    {
        if (playerCombat == null) playerCombat = GetComponent<PlayerCombat>();
        Execute(playerCombat, transform);
    }

    // ISecondaryChargedAttack 진입점 (PlayerCombat에서 호출)
    public void Execute(PlayerCombat owner, Transform ownerTransform)
    {
        if (isSkillActive) return;
        if (guardianPrefab == null)
        {
            Debug.LogWarning("[GuardianSkillController] guardianPrefab이 할당되지 않았습니다.");
            return;
        }
        if (hammerPrefab == null)
        {
            Debug.LogWarning("[GuardianSkillController] hammerPrefab이 할당되지 않았습니다. 수호신은 소환되지만 휘두를 수 없습니다.");
            // 수호신만 소환하는 동작을 원하면 여기서 계속 진행하거나 return 처리 선택 가능
        }

        // owner가 null이어도 ownerTransform으로 동작(테스트용)
        StartCoroutine(SkillSequence(owner, ownerTransform ?? transform));
    }

    public string GetAttackName() => "보라색 수호신(컴포넌트)";

    public SecondaryChargedAttackType GetSkillType()
    {
        return SecondaryChargedAttackType.Guardian;
    }

    private IEnumerator SkillSequence(PlayerCombat owner, Transform ownerTransform)
    {
        isSkillActive = true;
        if (ownerTransform == null) yield break;

        // 카메라 처리: orthographic / perspective 고려
        Camera cam = Camera.main;
        bool camIsOrtho = cam != null && cam.orthographic;
        float originalCamValue = cam != null ? (camIsOrtho ? cam.orthographicSize : cam.fieldOfView) : 0f;

        // Phase 1: 카메라 줌 아웃
        float elapsed = 0f;
        while (elapsed < Mathf.Max(0.0001f, zoomDuration))
        {
            if (owner == null && !enableDebugKey) break;
            float t = elapsed / zoomDuration;
            if (cam != null)
            {
                if (camIsOrtho) cam.orthographicSize = Mathf.Lerp(originalCamValue, targetCamSize, t);
                else cam.fieldOfView = Mathf.Lerp(originalCamValue, targetCamSize, t);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (cam != null)
        {
            if (camIsOrtho) cam.orthographicSize = targetCamSize;
            else cam.fieldOfView = targetCamSize;
        }

        // ------------------------------------------------
        // Phase 2: 수호신 소환 (정지)
        // ------------------------------------------------
        Vector3 guardianWorldPos = ownerTransform.TransformPoint(new Vector3(spawnOffset.x, spawnOffset.y, 0f));
        GameObject guardian = Object.Instantiate(guardianPrefab, guardianWorldPos, ownerTransform.rotation);
        if (guardian == null)
        {
            Debug.LogWarning("[GuardianSkillController] guardian 인스턴스 생성 실패");
            RestoreCamera(cam, camIsOrtho, originalCamValue);
            isSkillActive = false;
            yield break;
        }
        // 로컬 회전 조절(옵션)
        guardian.transform.rotation = ownerTransform.rotation * Quaternion.Euler(0f, 0f, guardianLocalZRotation);

        // ------------------------------------------------
        // Phase 3: 해머 생성 및 휘두르기 (Hammer prefab 필요)
        // - 해머는 guardian을 부모로 삼아 guardian 기준 localOffset에서 휘두름
        // - HammerSwingController 내부에서 ownerCombat(플레이어)을 사용하여 처형 보상 처리 가능
        // 
        // 변경 요점:
        // - 실행 시점의 마우스 월드 좌표를 기록하고, Initialize 이후 리플렉션으로 HammerSwingController.baseLocalAngle을 고정(기억)합니다.
        // - 해머가 플레이어의 실시간 자식이 되지 않도록 '고정 Anchor'를 생성하여 그 Anchor를 ownerTransform으로 넘깁니다.
        // ------------------------------------------------
        GameObject hammer = null;
        GameObject hammerTrailObj = null;
        GameObject aimAnchorGO = null; // 실행 시점 고정 기준 Transform

        if (hammerPrefab != null)
        {
            // 해머의 실제 월드 스폰 위치:
            // 플레이어(ownerTransform) 기준으로 spawnOffset + hammerLocalOffset 합쳐서 계산
            Vector3 hammerWorldPos;
            if (ownerTransform != null)
            {
                Vector3 combinedLocal = new Vector3(spawnOffset.x + hammerLocalOffset.x, spawnOffset.y + hammerLocalOffset.y, 0f);
                hammerWorldPos = ownerTransform.TransformPoint(combinedLocal);
            }
            else
            {
                hammerWorldPos = guardian.transform.TransformPoint(new Vector3(hammerLocalOffset.x, hammerLocalOffset.y, 0f));
            }

            // 실행 시점 마우스 위치 기록 (기존 동작 보존)
            Vector3 initialMouseWorld = Vector3.zero;
            if (Camera.main != null)
            {
                Vector3 m = Input.mousePosition;
                m.z = Mathf.Abs(Camera.main.transform.position.z - (ownerTransform != null ? ownerTransform.position.z : guardian.transform.position.z));
                initialMouseWorld = Camera.main.ScreenToWorldPoint(m);
                initialMouseWorld.z = (ownerTransform != null ? ownerTransform.position.z : guardian.transform.position.z);
            }

            // Anchor 생성: 플레이어의 현재 월드 위치/회전을 복제한 고정 Transform
            if (ownerTransform != null)
            {
                aimAnchorGO = new GameObject("GuardianHammerAnchor");
                aimAnchorGO.transform.position = ownerTransform.position;
                aimAnchorGO.transform.rotation = ownerTransform.rotation;
                // 숨김 옵션(선택): aimAnchorGO.hideFlags = HideFlags.DontSave;
            }

            // Instantiate 시 rotation은 identity로 유지
            hammer = Object.Instantiate(hammerPrefab, hammerWorldPos, Quaternion.identity);

            var hc = hammer.GetComponent<HammerSwingController>();
            if (hc != null)
            {
                // localOffset을 Anchor 로컬 좌표로 전달 (spawnOffset + hammerLocalOffset)
                Vector2 localOffsetForAnchor = hammerLocalOffset;
                if (ownerTransform != null)
                {
                    localOffsetForAnchor = new Vector2(spawnOffset.x + hammerLocalOffset.x, spawnOffset.y + hammerLocalOffset.y);
                }

                // swingAngle / speedCurve 결정 ( PlayerCombat 값 우선, 실패 시 Guardian 값 사용 )
                float swingAngleToUse = this.swingAngle;
                AnimationCurve speedCurveToUse = this.swingCurve;
                if (owner != null)
                {
                    try
                    {
                        FieldInfo fi = owner.GetType().GetField("hammerSwingAngle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (fi != null)
                        {
                            object val = fi.GetValue(owner);
                            if (val is float f) swingAngleToUse = f;
                        }

                        FieldInfo fiCurve = owner.GetType().GetField("hammerSwingCurve", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (fiCurve != null)
                        {
                            object cv = fiCurve.GetValue(owner);
                            if (cv is AnimationCurve ac) speedCurveToUse = ac;
                        }
                    }
                    catch
                    {
                        // 실패 시 Guardian의 설정 사용
                    }
                }

                // Initialize 호출: ownerTransform으로 '고정 Anchor' 전달(없으면 원 ownerTransform 전달)
                Transform anchorTransformToUse = aimAnchorGO != null ? aimAnchorGO.transform : ownerTransform;
                hc.Initialize(
                    owner: owner,
                    ownerTransform: anchorTransformToUse,
                    damage: damage,
                    knockback: knockbackForce,
                    swingAngle: swingAngleToUse,
                    swingDuration: swingDuration,
                    executeHealAmount: executeHealAmount,
                    localOffset: localOffsetForAnchor,
                    speedCurve: speedCurveToUse,
                    enableExecution: true
                );

                // Initialize 후 baseLocalAngle 고정(초기 마우스 위치 기준) — 기존 구현 유지
                try
                {
                    System.Type hcType = hc.GetType();

                    // invertSwingDirection 읽기 (private field)
                    bool invert = false;
                    FieldInfo fiInvert = hcType.GetField("invertSwingDirection", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (fiInvert != null)
                    {
                        object invVal = fiInvert.GetValue(hc);
                        if (invVal is bool b) invert = b;
                    }

                    float ownerWorldAngle = (anchorTransformToUse != null) ? anchorTransformToUse.eulerAngles.z : 0f;
                    Vector2 toMouse = (initialMouseWorld - (anchorTransformToUse != null ? anchorTransformToUse.position : (Vector3)Vector2.zero));
                    float desiredWorldAngle = (toMouse.sqrMagnitude > 0.0001f) ? Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg : ownerWorldAngle;
                    float baseLocal = Mathf.DeltaAngle(ownerWorldAngle, desiredWorldAngle);

                    if (invert) baseLocal += 180f;

                    float swingCenterOffset = (swingAngleToUse - 180f) * 0.5f;
                    if (invert) baseLocal -= swingCenterOffset;
                    else baseLocal += swingCenterOffset;

                    FieldInfo fiBase = hcType.GetField("baseLocalAngle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (fiBase != null)
                    {
                        fiBase.SetValue(hc, baseLocal);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[GuardianSkillController] baseLocalAngle 고정 실패: {ex.Message}");
                }

                // 플레이어와 해머 콜라이더 충돌 무시
                var hammerCols = hammer.GetComponentsInChildren<Collider2D>();
                var playerCols = ownerTransform.GetComponentsInChildren<Collider2D>();
                foreach (var hcCol in hammerCols)
                    foreach (var pc in playerCols)
                        if (hcCol != null && pc != null) Physics2D.IgnoreCollision(hcCol, pc, true);
            }
            else
            {
                Debug.LogWarning("[GuardianSkillController] hammerPrefab에 HammerSwingController가 없습니다.");
            }

            // === TrailRenderer 생성 및 관리 ===
            if (enableHammerTrail && hammer != null)
            {
                hammerTrailObj = new GameObject("HammerTrail");
                Transform attachTransform = null;
                if (!string.IsNullOrEmpty(trailAttachPointName))
                {
                    attachTransform = hammer.transform.Find(trailAttachPointName);
                    if (attachTransform == null)
                    {
                        Debug.LogWarning($"[GuardianSkillController] trailAttachPointName '{trailAttachPointName}' 을(를) 해머에서 찾지 못했습니다. 해머 루트에 붙입니다.");
                    }
                }

                if (attachTransform != null)
                {
                    hammerTrailObj.transform.SetParent(attachTransform, false);
                    hammerTrailObj.transform.localPosition = Vector3.zero;
                }
                else
                {
                    hammerTrailObj.transform.SetParent(hammer.transform, false);
                    hammerTrailObj.transform.localPosition = trailLocalOffset;
                }

                TrailRenderer tr = hammerTrailObj.AddComponent<TrailRenderer>();
                tr.time = Mathf.Max(0.01f, trailTime);
                tr.startWidth = Mathf.Max(0f, trailStartWidth);
                tr.endWidth = Mathf.Max(0f, trailEndWidth);
                tr.minVertexDistance = Mathf.Max(0.001f, trailMinVertexDistance);

                if (trailMaterial != null)
                    tr.material = trailMaterial;
                else
                {
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    mat.SetColor("_Color", trailColor);
                    tr.material = mat;
                }

                Gradient g = new Gradient();
                g.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(trailColor, 0.0f), new GradientColorKey(trailColor, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(trailColor.a, 0.0f), new GradientAlphaKey(0f, 1.0f) }
                );
                tr.colorGradient = g;

                StartCoroutine(HammerTrailLifecycle(hammer.transform, hammerTrailObj, tr.time));
            }
        }

        // ------------------------------------------------
        // Phase 4: 대기 (공격 모션/유지 시간)
        // ------------------------------------------------
        float waitTime = Mathf.Max(swingDuration, lingerDuration);
        float waited = 0f;
        while (waited < waitTime)
        {
            if (owner == null && !enableDebugKey) break;
            waited += Time.deltaTime;
            yield return null;
        }

        // ------------------------------------------------
        // Phase 5: 페이드 아웃 및 카메라 복구
        // - Guardian 자체에 SpriteRenderer가 있으면 페이드 처리 (옵션)
        // ------------------------------------------------
        SpriteRenderer sr = guardian.GetComponentInChildren<SpriteRenderer>();
        Color startColor = sr != null ? sr.color : Color.white;
        elapsed = 0f;
        while (elapsed < Mathf.Max(0.0001f, fadeOutDuration))
        {
            if (owner == null && !enableDebugKey) break;
            float t = elapsed / fadeOutDuration;
            if (sr != null) sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
            if (cam != null)
            {
                if (camIsOrtho) cam.orthographicSize = Mathf.Lerp(targetCamSize, originalCamValue, t);
                else cam.fieldOfView = Mathf.Lerp(targetCamSize, originalCamValue, t);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        RestoreCamera(cam, camIsOrtho, originalCamValue);

        // Guardian은 소멸. 해머를 직접 Destroy 하기 전에 트레일이 해머 자식이면 분리하여 남김
        if (hammerTrailObj != null && hammer != null && hammerTrailObj.transform.IsChildOf(hammer.transform))
        {
            hammerTrailObj.transform.SetParent(null);
            hammerTrailObj = null;
        }

        if (hammer != null) Object.Destroy(hammer);
        if (guardian != null) Object.Destroy(guardian);

        // Anchor 정리: Anchor는 해머의 부모였으나 고정(움직이지 않음). 해머가 파괴되면 Anchor도 정리.
        if (aimAnchorGO != null) Object.Destroy(aimAnchorGO);

        isSkillActive = false;
        Debug.Log("[GuardianSkillController] 스킬 종료");
    }

    private IEnumerator HammerTrailLifecycle(Transform hammerTransform, GameObject trailObject, float trailDuration)
    {
        // 해머가 살아있을 동안 대기
        while (hammerTransform != null)
        {
            yield return null;
        }

        // 해머가 파괴되면 트레일을 부모에서 분리(혹은 이미 분리됨)
        if (trailObject != null)
        {
            trailObject.transform.SetParent(null);
            yield return new WaitForSeconds(Mathf.Max(0f, trailDuration));
            if (trailObject != null) Object.Destroy(trailObject);
        }
    }

    private void RestoreCamera(Camera cam, bool camIsOrtho, float value)
    {
        if (cam == null) return;
        if (camIsOrtho) cam.orthographicSize = value;
        else cam.fieldOfView = value;
    }
}