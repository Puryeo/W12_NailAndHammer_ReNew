using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// HammerSwingControllerWithCallback
/// - HammerSwingController의 복사본으로, 처형 시 콜백 기능 추가
/// - 차징 공격 전용으로 사용하여 일반 공격과 분리
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class HammerSwingControllerWithCallback : MonoBehaviour
{
    // 처형 콜백 델리게이트 (추가)
    public delegate void OnExecutionCallback(Vector2 playerPos, Vector2 enemyPos, EnemyController executedEnemy);
    private OnExecutionCallback onExecutionCallback;

    private PlayerCombat ownerCombat;
    private Transform ownerTransform;
    private float damage;
    private float knockback;
    private float swingAngle;
    private float swingDuration;
    private float executeHealAmount;
    private bool isSwinging = false;
    private Collider2D col;
    private SpriteRenderer sr;

    [SerializeField] private float hitQueueDelay = 0.05f;
    private Queue<Collider2D> hitQueue;
    private Coroutine hitQueueCoroutine;

    private Vector2 localOffset = Vector2.zero;
    private AnimationCurve speedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    private float baseLocalAngle = 0f;
    private float midSweep = 0f;
    private CameraShake cameraShake;

    [Header("Options")]
    [SerializeField] private bool invertSwingDirection = true;

    [Header("Behaviour")]
    [SerializeField] private float quickStun = 0.12f;

    [Header("Execution")]
    [Tooltip("처형 기능 활성화 여부")]
    [SerializeField] private bool enableExecution = true;
    [Tooltip("처형 시 적용할 넉백 강도 (임펄스)")]
    [SerializeField] private float executeKnockbackForce = 12f;

    public enum HitDetectionMode
    {
        ManualOnly,
        ColliderOnly,
        Both
    }

    [Header("Hit Detection")]
    [Tooltip("Hit detection 모드 선택: ManualOnly(권장), ColliderOnly, Both")]
    [SerializeField] private HitDetectionMode detectionMode = HitDetectionMode.ManualOnly;
    [Tooltip("Hit detection에서 사용할 레이어 마스크 (Enemy 레이어 지정 권장)")]
    [SerializeField] private LayerMask hitLayer = 0;
    [Tooltip("오버랩 반경 (플레이어 기준의 해머 도달 거리에 맞춰 조정)")]
    [SerializeField] private float hitRadius = 1.2f;
    [Tooltip("한 프레임에 수집할 최대 콜라이더 수 (성능/정확도 균형)")]
    [SerializeField] private int maxOverlapResults = 32;
    [Tooltip("스윙 진행(0..1) 기준으로 히트가 발생할 타이밍")]
    [Range(0f, 1f)]
    [SerializeField] private float hitTiming = 0.5f;
    [Tooltip("히트 시 콜라이더를 일시적으로 활성화할 길이(초). ManualOnly일 때는 사용되지 않음.")]
    [SerializeField] private float hitActiveDuration = 0.06f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    [Header("Gizmo (Scene preview)")]
    [Tooltip("씬에서 히트 범위 기즈모 표시 (애니메이션 각도 시각화용)")]
    [SerializeField] private bool showGizmo = true;
    [Tooltip("프리팹 배치 시 미리보기용 스윙 각도 (시각화 전용)")]
    [SerializeField] private float previewSwingAngle = 120f;
    [Tooltip("프리팹 배치 시 미리보기용 반경")]
    [SerializeField] private float previewRadius = 1.2f;
    [Tooltip("기즈모 색상")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.25f);
    [Tooltip("기즈모 아크 분할 수 (시각화 정밀도)")]
    [SerializeField, Range(6, 64)] private int gizmoSegments = 24;

    private Collider2D[] overlapBuffer;
    private HashSet<int> alreadyHitIds;
    private bool hitProcessed = false;

    // Initialize 메서드 - 콜백 파라미터 추가
    public void Initialize(PlayerCombat owner, Transform ownerTransform, float damage, float knockback,
                          float swingAngle, float swingDuration, float executeHealAmount,
                          Vector2 localOffset, AnimationCurve speedCurve, bool enableExecution = true,
                          OnExecutionCallback executionCallback = null)
    {
        this.ownerCombat = owner;
        this.ownerTransform = ownerTransform;
        this.damage = damage;
        this.knockback = knockback;
        this.swingAngle = swingAngle;
        this.swingDuration = Mathf.Max(0.01f, swingDuration);
        this.executeHealAmount = executeHealAmount;
        this.localOffset = localOffset;
        this.enableExecution = enableExecution;
        this.onExecutionCallback = executionCallback; // 콜백 저장
        if (speedCurve != null) this.speedCurve = speedCurve;

        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        if (col != null) col.enabled = false;
        if (Camera.main != null) cameraShake = Camera.main.GetComponent<CameraShake>();

        overlapBuffer = new Collider2D[Mathf.Max(1, maxOverlapResults)];
        alreadyHitIds = new HashSet<int>(32);
        hitProcessed = false;

        if (ownerTransform != null)
        {
            Vector3 mouseWorld = Vector3.zero;
            if (Camera.main != null)
            {
                Vector3 m = Input.mousePosition;
                m.z = Mathf.Abs(Camera.main.transform.position.z - ownerTransform.position.z);
                mouseWorld = Camera.main.ScreenToWorldPoint(m);
                mouseWorld.z = ownerTransform.position.z;
            }
            else
            {
                mouseWorld = ownerTransform.position + ownerTransform.right;
            }

            Vector2 toMouse = (mouseWorld - ownerTransform.position);
            if (toMouse.sqrMagnitude > 0.0001f)
            {
                float desiredWorldAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
                float ownerWorldAngle = ownerTransform.eulerAngles.z;
                baseLocalAngle = Mathf.DeltaAngle(ownerWorldAngle, desiredWorldAngle);
                if (invertSwingDirection) baseLocalAngle += 180f;
            }
            else
            {
                baseLocalAngle = 0f;
                if (invertSwingDirection) baseLocalAngle += 180f;
            }
        }

        float swingCenterOffset = (this.swingAngle - 180f) * 0.5f;
        if (invertSwingDirection)
            baseLocalAngle -= swingCenterOffset;
        else
            baseLocalAngle += swingCenterOffset;

        if (showDebugLogs)
        {
            Debug.Log($"[Hammer DBG] InitAngles owner={ownerTransform?.eulerAngles.z:F1} baseLocal={baseLocalAngle:F1} midSweep={midSweep:F2} sr.localRot={(sr?sr.transform.localEulerAngles.z:0f):F1}");
        }

        if (showDebugLogs)
        {
            float halfDebug = swingAngle * 0.5f;
            float startSweepDebug = Mathf.Lerp(-halfDebug, halfDebug, speedCurve.Evaluate(0f)) - midSweep;
            float endSweepDebug = Mathf.Lerp(-halfDebug, halfDebug, speedCurve.Evaluate(1f)) - midSweep;
            Debug.Log($"[Hammer Init] invert={invertSwingDirection} swingAngle={swingAngle} centerOffset={swingCenterOffset:F1} baseLocalAngle={baseLocalAngle:F1} start={baseLocalAngle+startSweepDebug:F1} end={baseLocalAngle+endSweepDebug:F1} hitTiming={hitTiming:F2} hitDur={hitActiveDuration:F3}");
        }

        float half = swingAngle * 0.5f;
        midSweep = Mathf.Lerp(-half, half, this.speedCurve.Evaluate(0.5f));

        hitQueue = new Queue<Collider2D>();

        StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        isSwinging = true;
        alreadyHitIds.Clear();
        hitProcessed = false;

        if (ownerTransform != null)
        {
            transform.SetParent(ownerTransform, false);
            transform.localPosition = localOffset;
            float half = swingAngle * 0.5f;
            float startSweep = Mathf.Lerp(-half, half, speedCurve.Evaluate(0f)) - midSweep;
            transform.localRotation = Quaternion.Euler(0f, 0f, baseLocalAngle - startSweep);

            if (sr != null)
            {
                sr.transform.localRotation = Quaternion.identity;
            }

            if (sr != null)
            {
                float ownerWorld = ownerTransform != null ? ownerTransform.eulerAngles.z : 0f;
                float expectedWorldStart = ownerWorld + (baseLocalAngle - startSweep);
                float visualWorld = sr.transform.eulerAngles.z;
                float delta = Mathf.DeltaAngle(visualWorld, expectedWorldStart);

                Vector3 localEuler = sr.transform.localEulerAngles;
                float newLocalZ = Mathf.DeltaAngle(0f, localEuler.z) - delta;
                sr.transform.localEulerAngles = new Vector3(localEuler.x, localEuler.y, newLocalZ);

                if (showDebugLogs)
                    Debug.Log($"[Hammer Align] expectedWorld={expectedWorldStart:F1} visualWorldBefore={visualWorld:F1} delta={delta:F1} newLocalZ={newLocalZ:F1}");
            }
        }

        float halfAngle = swingAngle * 0.5f;
        float elapsed = 0f;

        bool windowActive = false;
        float halfWindow = hitActiveDuration * 0.5f;

        while (elapsed < swingDuration)
        {
            float t = Mathf.Clamp01(elapsed / swingDuration);
            float eased = speedCurve.Evaluate(t);
            float sweep = Mathf.Lerp(-halfAngle, halfAngle, eased) - midSweep;
            float total = baseLocalAngle - sweep;
            transform.localRotation = Quaternion.Euler(0f, 0f, total);

            bool enable = t >= 0.25f && t <= 0.75f;
            if (col != null && col.enabled != enable)
            {
                if (detectionMode != HitDetectionMode.ManualOnly)
                {
                    col.enabled = enable;
                    if (showDebugLogs) Debug.Log($"Hammer [{gameObject.name}] collider {(enable ? "enabled" : "disabled")} t={t:F2}");
                }
                else
                {
                    if (col != null && col.enabled) col.enabled = false;
                }
            }

            bool isInHitWindow = (t >= (hitTiming - halfWindow)) && (t <= (hitTiming + halfWindow));

            if (isInHitWindow && !windowActive)
            {
                windowActive = true;
                if (detectionMode != HitDetectionMode.ManualOnly && col != null && hitActiveDuration > 0f)
                {
                    StartCoroutine(TemporarilyEnableCollider(hitActiveDuration));
                }
            }
            else if (!isInHitWindow && windowActive)
            {
                windowActive = false;
            }

            if (isInHitWindow && detectionMode != HitDetectionMode.ColliderOnly)
            {
                PerformSweepHitDetection(total, halfAngle);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        float finalSweep = Mathf.Lerp(-halfAngle, halfAngle, speedCurve.Evaluate(1f)) - midSweep;
        float finalTotal = baseLocalAngle - finalSweep;
        transform.localRotation = Quaternion.Euler(0f, 0f, finalTotal);
        if (col != null) col.enabled = false;

        float waitTimeout = 1.0f;
        float waited = 0f;
        while ((hitQueue != null && hitQueue.Count > 0) && waited < waitTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        transform.SetParent(null);
        Destroy(gameObject, 0.01f);

        isSwinging = false;
    }

    private IEnumerator TemporarilyEnableCollider(float duration)
    {
        if (col == null) yield break;
        bool prev = col.enabled;
        col.enabled = true;
        yield return new WaitForSeconds(duration);
        if (col != null) col.enabled = prev;
    }

    private void PerformSingleHitDetection(float currentLocalTotalAngle, float halfAngle)
    {
        if (ownerTransform == null) return;

        Vector2 worldCenter = (ownerTransform != null) ? (Vector2)ownerTransform.TransformPoint(localOffset) : (Vector2)transform.position;
        int layerMask = (hitLayer == 0) ? ~0 : (int)hitLayer;
        int count = Physics2D.OverlapCircleNonAlloc(worldCenter, hitRadius, overlapBuffer, layerMask);
        if (count <= 0)
        {
            if (showDebugLogs) Debug.Log($"Hammer SingleHit -> none ({count})");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var c = overlapBuffer[i];
            if (c == null) continue;

            if (ownerTransform != null && (c.transform == ownerTransform || c.transform.IsChildOf(ownerTransform))) continue;

            var enemyCtrl = c.GetComponent<EnemyController>() ?? c.GetComponentInParent<EnemyController>();
            var enemyHealth = c.GetComponent<HealthSystem>() ?? c.GetComponentInParent<HealthSystem>();
            if (enemyCtrl == null) continue;

            int id = enemyCtrl.GetInstanceID();
            if (alreadyHitIds.Contains(id)) continue;

            alreadyHitIds.Add(id);
            if (enableExecution && enemyCtrl.IsGroggy())
            {
                int stackReward = enemyCtrl.ConsumeStacks(true, true, ownerCombat);
                enemyCtrl.MarkExecuted();
                if (ownerCombat != null) ownerCombat.OnExecutionSuccess(executeHealAmount, 0);

                Rigidbody2D targetRb = c.GetComponent<Rigidbody2D>() ?? c.GetComponentInParent<Rigidbody2D>();
                if (targetRb != null && ownerTransform != null)
                {
                    Vector2 push = ((Vector2)c.transform.position - worldCenter).normalized;
                    targetRb.AddForce(push * executeKnockbackForce, ForceMode2D.Impulse);
                }

                if (enemyHealth != null)
                {
                    var he = enemyHealth.GetComponent<HitEffect>();
                    if (he != null) he.PlayExecuteEffect();

                    var hpe = enemyHealth.GetComponent<HitParticleEffect>();
                    if (hpe != null) hpe.PlayExecuteParticle(c.transform.position);
                }

                HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Strong, EShakeStrength.Strong, c.transform.position);

                if (enemyHealth != null)
                {
                    enemyHealth.ForceDieWithFade(1f);
                }

                // 콜백 호출 (추가)
                if (onExecutionCallback != null && ownerTransform != null)
                {
                    onExecutionCallback.Invoke(ownerTransform.position, c.transform.position, enemyCtrl);
                }
            }
            else
            {
                if (enemyHealth != null) enemyHealth.TakeDamage(damage);

                if (quickStun > 0f) enemyCtrl.ApplyStun(quickStun);

                Rigidbody2D rb = c.GetComponent<Rigidbody2D>() ?? c.GetComponentInParent<Rigidbody2D>();
                if (rb != null && ownerTransform != null)
                {
                    Vector2 push = ((Vector2)c.transform.position - worldCenter).normalized;
                    rb.AddForce(push * knockback, ForceMode2D.Impulse);
                }

                HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Weak, EShakeStrength.Weak, c.transform.position);

                enemyCtrl.RegisterHit(1, ownerTransform);
                enemyCtrl.ConsumeStacks(true, true, ownerCombat);
            }

            if (showDebugLogs) Debug.Log($"Hammer SingleHit -> hit {c.name} id={c.GetInstanceID()}");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (detectionMode == HitDetectionMode.ManualOnly) return;

        if (!isSwinging) return;
        if (other == null) return;
        if (!other.CompareTag("Enemy")) return;

        var enemyCtrl = other.GetComponent<EnemyController>() ?? other.GetComponentInParent<EnemyController>();
        if (enemyCtrl != null)
        {
            if (alreadyHitIds != null && alreadyHitIds.Contains(enemyCtrl.GetInstanceID()))
            {
                if (showDebugLogs) Debug.Log($"Hammer OnTriggerEnter2D ignored (alreadyHit) -> {other.name}");
                return;
            }
        }

        if (showDebugLogs) Debug.Log($"Hammer [{gameObject.name}] OnTriggerEnter2D -> {other.name}");

        if (ownerTransform != null && other.transform == ownerTransform) return;

        var enemyHealth = other.GetComponent<HealthSystem>() ?? other.GetComponentInParent<HealthSystem>();

        if (enableExecution && enemyCtrl != null && enemyCtrl.IsGroggy())
        {
            int stackReward = enemyCtrl.ConsumeStacks(true, true, ownerCombat);
            enemyCtrl.MarkExecuted();
            if (ownerCombat != null) ownerCombat.OnExecutionSuccess(executeHealAmount, 0);

            Rigidbody2D targetRb = other.GetComponent<Rigidbody2D>() ?? other.GetComponentInParent<Rigidbody2D>();
            if (targetRb != null && ownerTransform != null)
            {
                Vector2 dir = (other.transform.position - ownerTransform.position).normalized;
                targetRb.AddForce(dir * executeKnockbackForce, ForceMode2D.Impulse);
            }

            if (enemyHealth != null)
            {
                var he = enemyHealth.GetComponent<HitEffect>();
                if (he != null) he.PlayExecuteEffect();

                var hpe = enemyHealth.GetComponent<HitParticleEffect>();
                if (hpe != null) hpe.PlayExecuteParticle(other.transform.position);
            }

            HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Strong, EShakeStrength.Strong, other.transform.position);

            if (enemyHealth != null)
            {
                enemyHealth.ForceDieWithFade(1f);
            }

            if (enemyCtrl != null && alreadyHitIds != null) alreadyHitIds.Add(enemyCtrl.GetInstanceID());

            // 콜백 호출 (추가)
            if (onExecutionCallback != null && ownerTransform != null)
            {
                onExecutionCallback.Invoke(ownerTransform.position, other.transform.position, enemyCtrl);
            }

            return;
        }
        else
        {
            if (enemyHealth != null) enemyHealth.TakeDamage(damage);

            if (enemyCtrl != null && quickStun > 0f)
            {
                enemyCtrl.ApplyStun(quickStun);
                if (showDebugLogs) Debug.Log($"Applied quickStun={quickStun} to {other.name}");
            }

            Rigidbody2D rb = other.GetComponent<Rigidbody2D>() ?? other.GetComponentInParent<Rigidbody2D>();
            if (rb != null && ownerTransform != null)
            {
                Vector2 dir = (other.transform.position - ownerTransform.position).normalized;
                float force = knockback;
                rb.AddForce(dir * force, ForceMode2D.Impulse);
                if (showDebugLogs) Debug.Log($"Applied knockback force={force} dir={dir} to {other.name}");
            }

            HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Weak, EShakeStrength.Weak, other.transform.position);

            if (enemyCtrl != null)
            {
                enemyCtrl.RegisterHit(1, ownerTransform);
                enemyCtrl.ConsumeStacks(true, true, ownerCombat);
                if (alreadyHitIds != null) alreadyHitIds.Add(enemyCtrl.GetInstanceID());
                if (showDebugLogs) Debug.Log($"Hammer [{gameObject.name}]: Consumed stacks from {enemyCtrl.name} on hit (non-execute)");
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        float drawAngle = (Application.isPlaying && swingAngle > 0f) ? swingAngle : previewSwingAngle;
        float drawRadius = (Application.isPlaying && hitRadius > 0f) ? hitRadius : previewRadius;

        Vector3 center = (ownerTransform != null) ? (Vector3)ownerTransform.position + (Vector3)localOffset : transform.position;
        float ownerAngle = (ownerTransform != null) ? ownerTransform.eulerAngles.z : transform.eulerAngles.z;

        float half = drawAngle * 0.5f;
        float startSweep = Mathf.Lerp(-half, half, speedCurve.Evaluate(0f)) - midSweep;
        float endSweep = Mathf.Lerp(-half, half, speedCurve.Evaluate(1f)) - midSweep;

        float startWorld = ownerAngle + (baseLocalAngle - startSweep);
        float endWorld = ownerAngle + (baseLocalAngle - endSweep);

        float sweepSigned = Mathf.DeltaAngle(startWorld, endWorld);
        float sweepAbs = Mathf.Abs(sweepSigned);

        Gizmos.color = gizmoColor;
        Vector3 startDir = DegreeToVector3(startWorld);
        DrawWireArc(center, startWorld + sweepAbs * 0.5f, sweepAbs, drawRadius, gizmoSegments);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            float curWorld = (ownerTransform != null) ? ownerTransform.eulerAngles.z + transform.localEulerAngles.z : transform.eulerAngles.z;
            Vector3 dir = DegreeToVector3(curWorld);
            Gizmos.DrawLine(center, center + dir * drawRadius);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;

#if UNITY_EDITOR
    float drawAngle = (Application.isPlaying && swingAngle > 0f) ? swingAngle : previewSwingAngle;
    float drawRadius = (Application.isPlaying && hitRadius > 0f) ? hitRadius : previewRadius;

    Vector3 center = (ownerTransform != null) ? (Vector3)ownerTransform.position + (Vector3)localOffset : transform.position;
    float ownerAngle = (ownerTransform != null) ? ownerTransform.eulerAngles.z : transform.eulerAngles.z;

    float half = drawAngle * 0.5f;
    float startSweep = Mathf.Lerp(-half, half, speedCurve.Evaluate(0f)) - midSweep;
    float endSweep = Mathf.Lerp(-half, half, speedCurve.Evaluate(1f)) - midSweep;

    float startWorld = ownerAngle + (baseLocalAngle - startSweep);
    float endWorld = ownerAngle + (baseLocalAngle - endSweep);

    float sweepSigned = Mathf.DeltaAngle(startWorld, endWorld);
    float sweepAbs = Mathf.Abs(sweepSigned);

    Color prev = Handles.color;
    Handles.color = gizmoColor;
    Vector3 normal = Vector3.forward;
    Vector3 startDir = DegreeToVector3(startWorld);
    Handles.DrawSolidArc(center, normal, startDir, sweepAbs, drawRadius);
    Handles.color = prev;

    Vector3 labelPos = center + DegreeToVector3(startWorld + sweepSigned * 0.5f) * (drawRadius + 0.25f);
    Handles.Label(labelPos, $"start={startWorld:F1}\nend={endWorld:F1}\nmid={baseLocalAngle:F1}");
#endif
    }

    private void DrawWireArc(Vector3 center, float centerAngleDeg, float angleDeg, float radius, int segments)
    {
        if (segments <= 0) segments = 24;
        float half = angleDeg * 0.5f;
        float start = centerAngleDeg - half;
        Vector3 prev = center + DegreeToVector3(start) * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = start + (angleDeg * i / segments);
            Vector3 curr = center + DegreeToVector3(a) * radius;
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
        Vector3 p1 = center + DegreeToVector3(start) * radius;
        Vector3 p2 = center + DegreeToVector3(start + angleDeg) * radius;
        Gizmos.DrawLine(center, p1);
        Gizmos.DrawLine(center, p2);
    }

    private Vector3 DegreeToVector3(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
    }

    private void PerformSweepHitDetection(float currentLocalTotalAngle, float halfAngle)
    {
        if (ownerTransform == null) return;
        if (overlapBuffer == null || overlapBuffer.Length != Mathf.Max(1, maxOverlapResults))
            overlapBuffer = new Collider2D[Mathf.Max(1, maxOverlapResults)];

        Vector2 worldCenter = (ownerTransform != null) ? (Vector2)ownerTransform.TransformPoint(localOffset) : (Vector2)transform.position;
        int layerMask = (hitLayer == 0) ? ~0 : (int)hitLayer;
        int count = Physics2D.OverlapCircleNonAlloc(worldCenter, hitRadius, overlapBuffer, layerMask);
        if (count <= 0)
        {
            if (showDebugLogs) Debug.Log($"Hammer SweepDetect -> none center={worldCenter} r={hitRadius}");
            return;
        }

        float worldAngleDeg = ownerTransform.eulerAngles.z + currentLocalTotalAngle;
        float worldAngleRad = worldAngleDeg * Mathf.Deg2Rad;
        Vector2 swingDir = new Vector2(Mathf.Cos(worldAngleRad), Mathf.Sin(worldAngleRad));
        const float angleTolerance = 5f;

        for (int i = 0; i < count; i++)
        {
            var c = overlapBuffer[i];
            if (c == null) continue;

            if (ownerTransform != null && (c.transform == ownerTransform || c.transform.IsChildOf(ownerTransform))) continue;

            var enemyCtrl = c.GetComponent<EnemyController>() ?? c.GetComponentInParent<EnemyController>();
            var enemyHealth = c.GetComponent<HealthSystem>() ?? c.GetComponentInParent<HealthSystem>();
            if (enemyCtrl == null) continue;

            int id = enemyCtrl.GetInstanceID();
            if (alreadyHitIds.Contains(id)) continue;

            Vector2 toTarget = ((Vector2)c.transform.position - worldCenter);
            if (toTarget.sqrMagnitude >= 0.0001f)
            {
                Vector2 targetDir = toTarget.normalized;
                float angleBetween = Vector2.Angle(swingDir, targetDir);
                if (angleBetween > (halfAngle + angleTolerance)) continue;
            }

            EnqueueHit(c);
            if (showDebugLogs) Debug.Log($"Hammer SweepDetect -> enqueued {c.name} id={id}");
        }
    }

    private void EnqueueHit(Collider2D c)
    {
        if (c == null) return;
        var enemyCtrl = c.GetComponent<EnemyController>() ?? c.GetComponentInParent<EnemyController>();
        int id = (enemyCtrl != null) ? enemyCtrl.GetInstanceID() : c.GetInstanceID();
        if (alreadyHitIds == null) alreadyHitIds = new HashSet<int>();
        if (alreadyHitIds.Contains(id)) return;

        alreadyHitIds.Add(id);
        if (hitQueue == null) hitQueue = new Queue<Collider2D>();
        hitQueue.Enqueue(c);

        if (hitQueueCoroutine == null)
        {
            hitQueueCoroutine = StartCoroutine(ProcessHitQueue());
        }
    }

    private IEnumerator ProcessHitQueue()
    {
        while (hitQueue != null && hitQueue.Count > 0)
        {
            Collider2D c = hitQueue.Dequeue();
            if (c != null)
            {
                ApplyHitImmediate(c);
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, hitQueueDelay));
        }

        hitQueueCoroutine = null;
        yield break;
    }

    private void ApplyHitImmediate(Collider2D target)
    {
        if (target == null) return;
        if (ownerTransform != null && (target.transform == ownerTransform || target.transform.IsChildOf(ownerTransform))) return;

        var enemyCtrl = target.GetComponent<EnemyController>() ?? target.GetComponentInParent<EnemyController>();
        var enemyHealth = target.GetComponent<HealthSystem>() ?? target.GetComponentInParent<HealthSystem>();
        var targetRb = target.GetComponent<Rigidbody2D>() ?? target.GetComponentInParent<Rigidbody2D>();
        Vector2 worldCenter = ownerTransform != null ? (Vector2)ownerTransform.TransformPoint(localOffset) : (Vector2)transform.position;
        Vector2 hitDir = ((Vector2)target.transform.position - worldCenter).normalized;

        if (enableExecution && enemyCtrl != null && enemyCtrl.IsGroggy())
        {
            enemyCtrl.ConsumeStacks(true, true, ownerCombat);
            enemyCtrl.MarkExecuted();
            if (ownerCombat != null) ownerCombat.OnExecutionSuccess(executeHealAmount, 0);

            if (targetRb != null) targetRb.AddForce(hitDir * executeKnockbackForce, ForceMode2D.Impulse);

            if (enemyHealth != null)
            {
                var he = enemyHealth.GetComponent<HitEffect>();
                if (he != null) he.PlayExecuteEffect();
                var hpe = enemyHealth.GetComponent<HitParticleEffect>();
                if (hpe != null) hpe.PlayExecuteParticle(target.transform.position);

                enemyHealth.ForceDieWithFade(1f);
            }

            HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Strong, EShakeStrength.Strong, target.transform.position);

            // 콜백 호출 (추가)
            if (onExecutionCallback != null && ownerTransform != null)
            {
                onExecutionCallback.Invoke(ownerTransform.position, target.transform.position, enemyCtrl);
            }
        }
        else
        {
            if (enemyHealth != null) enemyHealth.TakeDamage(damage);
            if (enemyCtrl != null && quickStun > 0f) enemyCtrl.ApplyStun(quickStun);
            if (targetRb != null) targetRb.AddForce(hitDir * knockback, ForceMode2D.Impulse);

            HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Weak, EShakeStrength.Weak, target.transform.position);

            if (enemyCtrl != null)
            {
                enemyCtrl.RegisterHit(1, ownerTransform);
                enemyCtrl.ConsumeStacks(true, true, ownerCombat);
            }
        }

        if (showDebugLogs) Debug.Log($"Hammer QueueProcessed -> hit {target.name} id={target.GetInstanceID()}");
    }
}
