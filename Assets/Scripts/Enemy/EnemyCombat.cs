using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyCombat - 적의 공격 로직과 쿨타임 관리 (Pattern SO/Controller 미사용, Inspector 기반 설정)
/// - 단기 PoC: 모든 공격 파라미터는 이 컴포넌트의 Inspector에서 조정
/// - Melee/Hitbox: telegraph -> 임시 콜라이더(부모=Enemy) 생성 -> 자동 파괴
/// - Projectile: telegraph(옵션) 후 간단한 임시 히트박스(레거시 동작 유지, 풀 연동 권장)
/// - Suicide: 플레이어에 돌진(디자이너가 moveSpeed로 조정) -> stopDistance 내 진입 시 폭발 카운트다운 -> 폭발
/// </summary>
[DisallowMultipleComponent]
public class EnemyCombat : MonoBehaviour
{
    [Header("Attack Settings (Inspector-controlled, Pattern SO unused)")]
    public EAttackType attackType = EAttackType.MeleeSlash;

    [Tooltip("공격 데미지")]
    public float damage = 10f;

    [Tooltip("공격 쿨타임 (초)")]
    public float attackCooldown = 2f;

    [Header("Telegraph (visual)")]
    public bool useTelegraph = false;
    public GameObject telegraphPrefab;
    public Color telegraphColor = new Color(1f, 0f, 0f, 0.45f);
    [Tooltip("공격 시(히트 시) 적용할 컬러(알파 포함). 인디케이터의 시각적 강조에 사용)")]
    public Color telegraphHitColor = new Color(1f, 0f, 0f, 0.9f);
    public float telegraphDelay = 0.8f;
    [Tooltip("스케일 배수: hitbox 크기에 곱해 적용")]
    public float telegraphSize = 1.0f;
    [Tooltip("히트 시점(또는 히트박스 소멸 시) 이후 인디케이터를 추가로 유지할 시간 (초)")]
    public float telegraphPersistAfterHit = 0.5f;

    [Header("Melee / Hitbox")]
    public EHitboxType hitboxType = EHitboxType.Box;
    [Tooltip("로컬 기준 오프셋 (적의 앞쪽이 +X)")]
    public Vector2 hitboxOffset = Vector2.zero;
    public Vector2 hitboxSize = Vector2.one;
    public float hitboxRadius = 1f;
    [Tooltip("임시 히트박스 생존 시간(초)")]
    public float hitboxLife = 0.05f;

    [Header("Projectile (basic PoC)")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 5f;
    [Tooltip("발사체 색상 (인스펙터에서 지정)")]
    public Color projectileColor = Color.white;
    [Tooltip("풀 초기 크기 (레지스트리에서 풀 생성시 사용)")]
    public int projectilePoolInitialSize = 4;

    [Header("Suicide (Self-destruct)")]
    [Tooltip("폭발 카운트다운(초). 색/원/스케일 변화가 이 시간 동안 진행됩니다.")]
    public float bombCountDown = 1.0f;
    [Tooltip("카운트다운이 끝났을 때 적용될 스프라이트 색상")]
    public Color bombTargetColor = Color.red;
    [Tooltip("폭발 반경 (월드 단위)")]
    public float suicideRadius = 2.0f;
    [Tooltip("스프라이트가 카운트다운 동안 증가할 배수: finalScale = original * (1 + scaleIncrease)")]
    public float scaleIncrease = 0.5f;
    [Tooltip("멈출 거리(플레이어와의 거리) — ExecuteAttack는 이 값 이하일 때 폭발 루틴을 시작합니다.")]
    public float stopDistance = 1.5f;

    [Tooltip("폭발 시 재생할 VFX prefab (비워두면 임시 기본 VFX를 동적으로 생성하여 사용합니다).")]
    public GameObject explosionVfxPrefab;
    [Tooltip("폭발 VFX 삭제 대기 시간(초)")]
    public float explosionVfxDuration = 2.0f;
    [Tooltip("폭발 시 데미지를 받을 대상 태그 (기본: Player)")]
    public string explosionTargetTag = "Player";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // 내부 상태
    private float lastAttackTime = -999f;
    private AttackManager attackManager;

    // 중복 생성/재진입 방지
    private GameObject activeIndicator = null;
    private bool attackInProgress = false;

    // 캐시: 동적 기본 VFX 프리팹
    private GameObject cachedDefaultExplosionPrefab = null;

    // 추가: Suicide 퓨즈 중 유지할 이동 속도 (인스펙터에서 조정 가능)
    [Tooltip("퓨즈(카운트다운) 동안 적이 유지할 이동 속도 (기본: 0 = 완전 정지)")]
    public float suicideFuseMoveSpeed = 0f;

    private void Start()
    {
        attackManager = FindFirstObjectByType<AttackManager>();
        if (attackManager == null)
        {
            Debug.LogError($"EnemyCombat [{gameObject.name}]: AttackManager를 찾을 수 없습니다!");
        }

        if (showDebugLogs) Debug.Log($"EnemyCombat [{gameObject.name}]: 초기화 완료 (쿨타임: {attackCooldown}초, 타입: {attackType})");
    }

    /// <summary>
    /// 외부에서 호출: 타겟(보통 플레이어)에게 공격 시도
    /// </summary>
    public void TryAttack(Transform target)
    {
        if (Time.time < lastAttackTime + attackCooldown)
        {
            if (showDebugLogs)
            {
                float remaining = (lastAttackTime + attackCooldown) - Time.time;
                Debug.Log($"EnemyCombat [{gameObject.name}]: 쿨타임 중 (남은 시간: {remaining:F2}s)");
            }
            return;
        }

        // 단순화: Controller / Pattern SO 사용하지 않음 — 이 컴포넌트의 필드로 모든 동작 제어
        ExecuteAttack(target);
    }

    private void ExecuteAttack(Transform target)
    {
        // 재진입 방지: 이미 진행중이면 무시
        if (attackInProgress)
        {
            if (showDebugLogs) Debug.Log($"EnemyCombat [{gameObject.name}]: 이미 공격 진행 중이므로 무시");
            return;
        }

        attackInProgress = true; // 시작

        Vector2 direction = (target.position - transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (showDebugLogs) Debug.Log($"EnemyCombat [{gameObject.name}]: 공격 시작 (타입={attackType}, 각도={angle:F1}°)");

        if (attackType == EAttackType.Projectile)
        {
            // Projectile 타입은 인디케이터(텔레그래프)를 표시하지 않음.
            FireProjectile(direction);
            lastAttackTime = Time.time;
            attackInProgress = false;
        }
        else if (attackType == EAttackType.Suicide)
        {
            // Suicide: start only if within stopDistance
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= stopDistance)
            {
                StartCoroutine(SuicideRoutine(target));
            }
            else
            {
                // 아직 멀다 — 아무 작업하지 않음. (EnemyController가 추적을 계속하도록 두세요)
                if (showDebugLogs) Debug.Log($"EnemyCombat [{gameObject.name}]: Suicide - 대상이 stopDistance 밖({dist:F2} > {stopDistance})");
                attackInProgress = false;
            }
        }
        else // MeleeSlash / Hitbox
        {
            if (useTelegraph)
            {
                StartCoroutine(TelegraphThenMeleeRoutine());
            }
            else
            {
                DoMeleeHit();
                lastAttackTime = Time.time;
                attackInProgress = false;
            }
        }
    }

    // ----------------------
    // Suicide routine
    // ----------------------
    private IEnumerator SuicideRoutine(Transform target)
    {
        // 1) 변경: EnemyController가 있다면 '완전 정지' 대신 임시 속도 감소를 적용
        var enemyCtrl = GetComponent<EnemyController>();
        if (enemyCtrl != null)
        {
            // ApplyStun 제거 — 완전 정지하지 않음.
            // 대신 퓨즈 동안 유지할 이동 속도를 지정하여 완전 정지 대신 느리게 이동하도록 함.
            enemyCtrl.ApplyTemporaryMoveSpeed(bombCountDown, suicideFuseMoveSpeed);
        }
        else
        {
            // EnemyController가 없으면 기존처럼 Rigidbody 정지는 유지 (호스트가 없다면 fallback으로 정지)
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        // 2) 준비: 캐시 원본값
        var sr = GetComponent<SpriteRenderer>();
        Color originalColor = sr != null ? sr.color : Color.white;
        Vector3 originalScale = transform.localScale;

        // 변경: 인디케이터를 Enemy의 자식으로 생성하여 밀림/외력에 따라 같이 움직이도록 함
        GameObject indicator = null;
        SpriteRenderer[] indicatorSrs = null;
        if (telegraphPrefab != null)
        {
            // instantiate as child so it follows this enemy
            indicator = Instantiate(telegraphPrefab, transform);
            indicator.transform.localPosition = (Vector3)hitboxOffset;
            indicator.transform.localRotation = Quaternion.identity;
            indicatorSrs = indicator.GetComponentsInChildren<SpriteRenderer>(true);
            indicator.transform.localScale = Vector3.zero;
        }

        float elapsed = 0f;
        while (elapsed < bombCountDown)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, bombCountDown));

            if (sr != null)
            {
                sr.color = Color.Lerp(originalColor, bombTargetColor, t);
            }

            transform.localScale = Vector3.Lerp(originalScale, originalScale * (1f + scaleIncrease), t);

            if (indicator != null && indicatorSrs != null)
            {
                float targetScale = suicideRadius * 2f;
                // For child indicator we set localScale to approximate target diameter.
                indicator.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * targetScale, t);

                foreach (var isr in indicatorSrs)
                {
                    if (isr == null) continue;
                    var c = isr.color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    isr.color = c;
                }
            }

            yield return null;
        }

        // 4) 폭발: VFX 생성(동적 기본 프리팹 자동 생성 지원) 및 범위 내 Tag == explosionTargetTag 에 고정 데미지 적용
        GameObject vfxPrefabToUse = explosionVfxPrefab;
        if (vfxPrefabToUse == null)
        {
            if (cachedDefaultExplosionPrefab == null)
                cachedDefaultExplosionPrefab = CreateDefaultExplosionPrefab();
            vfxPrefabToUse = cachedDefaultExplosionPrefab;
        }

        if (vfxPrefabToUse != null)
        {
            var vfx = Instantiate(vfxPrefabToUse, transform.position, Quaternion.identity);
            vfx.SetActive(true);
            Destroy(vfx, explosionVfxDuration);
        }

        // Apply fixed damage to targets in radius
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, suicideRadius);
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (!hit.CompareTag(explosionTargetTag)) continue;

            var hs = hit.GetComponent<HealthSystem>() ?? hit.GetComponentInParent<HealthSystem>();
            if (hs != null)
            {
                hs.TakeDamage(damage);
            }
        }

        // cleanup indicator
        if (indicator != null) Destroy(indicator);

        // Destroy or Release this enemy
        Destroy(this.gameObject);
        yield break;
    }

    // ----------------------
    // Telegraph + Melee flow
    // ----------------------
    private IEnumerator TelegraphThenMeleeRoutine()
    {
        // Pass actual hitbox dimensions so indicator matches hitbox precisely
        // SpawnTelegraphIndicator returns existing activeIndicator if already present
        GameObject indicator = SpawnTelegraphIndicator(hitboxType, hitboxSize, hitboxRadius);
        yield return new WaitForSeconds(Mathf.Max(0f, telegraphDelay));

        // 강조 색(히트 시) 적용 — 투명도 포함
        if (indicator != null) ApplyColorToIndicator(indicator, telegraphHitColor);

        // 실제 히트 수행
        DoMeleeHit();

        // 히트박스(TemporaryHitbox)의 생존시간(hitboxLife) + 추가 유지 시간 만큼 대기
        yield return new WaitForSeconds(Mathf.Max(0f, hitboxLife + telegraphPersistAfterHit));

        // 정리
        if (indicator != null)
        {
            Destroy(indicator);
            if (activeIndicator == indicator) activeIndicator = null;
        }

        lastAttackTime = Time.time;
        attackInProgress = false;
    }

    private IEnumerator TelegraphThenProjectileRoutine(Vector2 direction)
    {
        // For projectile we don't have a box size; use telegraphSize as desired visual diameter
        GameObject indicator = SpawnTelegraphIndicator(EHitboxType.Circle, Vector2.one * telegraphSize, telegraphSize * 0.5f);
        yield return new WaitForSeconds(Mathf.Max(0f, telegraphDelay));

        // 강조 색(히트 시) 적용 — 투명도 포함
        if (indicator != null) ApplyColorToIndicator(indicator, telegraphHitColor);

        FireProjectile(direction);

        // 프로젝트일 때: 사용한 히트박스가 fallback이면 hitboxLife를, 실제 projectile 프리팹 발사라면 기본 persist 값만 적용
        float persist = telegraphPersistAfterHit;
        if (projectilePrefab == null)
            persist = hitboxLife + telegraphPersistAfterHit;
        // wait for persist duration
        yield return new WaitForSeconds(Mathf.Max(0f, persist));

        if (indicator != null)
        {
            Destroy(indicator);
            if (activeIndicator == indicator) activeIndicator = null;
        }

        lastAttackTime = Time.time;
        attackInProgress = false;
    }

    /// <summary>
    /// 인디케이터를 히트박스 크기에 맞춰 생성.
    /// - hitboxType: Box / Circle
    /// - providedBoxSize: for Box => (width,height) in world units; for Circle this parameter can be ignored.
    /// - providedRadius: for Circle => radius in world units.
    /// </summary>
    private GameObject SpawnTelegraphIndicator(EHitboxType hitboxTypeParam, Vector2 providedBoxSize, float providedRadius)
    {
        if (telegraphPrefab == null) return null;

        // 이미 인디케이터가 존재하면 재사용 (중복 생성 방지)
        if (activeIndicator != null)
        {
            if (showDebugLogs) Debug.Log($"EnemyCombat: activeIndicator already exists - reusing");
            return activeIndicator;
        }

        // parent로 붙여서 Enemy 회전/이동을 따르도록 함
        GameObject inst = Instantiate(telegraphPrefab, transform);
        // 로컬 위치는 hitboxOffset (로컬 기준)
        inst.transform.localPosition = (Vector3)hitboxOffset;
        inst.transform.localRotation = Quaternion.identity;

        // determine desired world size from provided values (don't read fields directly)
        Vector3 desiredWorld = Vector3.one;
        if (hitboxTypeParam == EHitboxType.Box)
        {
            desiredWorld = new Vector3(providedBoxSize.x, providedBoxSize.y, 1f) * telegraphSize;
        }
        else
        {
            float diameter = providedRadius * 2f * telegraphSize;
            desiredWorld = new Vector3(diameter, diameter, 1f);
        }

        // Find all SpriteRenderers in the prefab and apply color/alpha to all of them.
        var srs = inst.GetComponentsInChildren<SpriteRenderer>(true);

        // Choose a representative SpriteRenderer for size-based scaling (prefer root or first valid)
        SpriteRenderer baseSr = null;
        foreach (var sr in srs)
        {
            if (sr != null && sr.sprite != null)
            {
                baseSr = sr;
                break;
            }
        }

        if (baseSr != null && baseSr.sprite != null)
        {
            // sprite.bounds.size is in local units (unscaled). compute current world size.
            Vector3 spriteLocalSize = baseSr.sprite.bounds.size;
            Vector3 currentWorldSize = new Vector3(
                spriteLocalSize.x * baseSr.transform.lossyScale.x,
                spriteLocalSize.y * baseSr.transform.lossyScale.y,
                1f);

            // avoid zero
            if (currentWorldSize.x <= 0.0001f) currentWorldSize.x = 1f;
            if (currentWorldSize.y <= 0.0001f) currentWorldSize.y = 1f;

            Vector3 scale = new Vector3(
                desiredWorld.x / currentWorldSize.x,
                desiredWorld.y / currentWorldSize.y,
                1f);

            // set localScale relative to current transform.localScale
            inst.transform.localScale = Vector3.Scale(inst.transform.localScale, scale);
        }
        else
        {
            // fallback: direct localScale assignment (may be coarse)
            inst.transform.localScale = desiredWorld;
        }

        // Apply color (including alpha) to all SpriteRenderers found.
        // If the prefab's material uses a shader that ignores color alpha, force a default sprite material (PoC).
        foreach (var sr in srs)
        {
            if (sr == null) continue;

            // Ensure material supports tinting with alpha. If shader is not Sprites/Default, create a temporary material.
            // Note: creating new Material at runtime allocates memory — acceptable for PoC, replace with pooled/shared material later.
            var shaderName = sr.sharedMaterial != null ? sr.sharedMaterial.shader.name : "";
            if (!string.IsNullOrEmpty(shaderName) && shaderName != "Sprites/Default")
            {
                try
                {
                    sr.material = new Material(Shader.Find("Sprites/Default"));
                }
                catch
                {
                    // ignore shader find failure — still try to set color
                }
            }

            sr.color = telegraphColor;
        }

        activeIndicator = inst; // 등록
        if (showDebugLogs)
        {
            Debug.Log($"EnemyCombat: SpawnTelegraphIndicator created '{inst.name}' localPos={inst.transform.localPosition} localScale={inst.transform.localScale} srCount={srs.Length} color={telegraphColor}");
        }

        return inst;
    }

    /// <summary>
    /// 인디케이터(생성된 인스턴스)의 모든 SpriteRenderer에 지정 컬러를 적용합니다.
    /// </summary>
    private void ApplyColorToIndicator(GameObject inst, Color color)
    {
        if (inst == null) return;
        var srs = inst.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            if (sr == null) continue;
            var shaderName = sr.sharedMaterial != null ? sr.sharedMaterial.shader.name : "";
            if (!string.IsNullOrEmpty(shaderName) && shaderName != "Sprites/Default")
            {
                try
                {
                    sr.material = new Material(Shader.Find("Sprites/Default"));
                }
                catch { }
            }
            sr.color = color;
        }
    }

    // ----------------------
    // Melee / Hit handling
    // ----------------------
    private void DoMeleeHit()
    {
        // Spawn a temporary hitbox GameObject as a child so rotation/position follow enemy
        GameObject hitboxGo = new GameObject("Enemy_MeleeHitbox");
        hitboxGo.transform.SetParent(transform, worldPositionStays: false);
        // Use local position so hitboxOffset is local
        hitboxGo.transform.localPosition = (Vector3)hitboxOffset;
        hitboxGo.transform.localRotation = Quaternion.identity;
        hitboxGo.transform.localScale = Vector3.one;

        if (hitboxType == EHitboxType.Box)
        {
            var box = hitboxGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = hitboxSize;
        }
        else
        {
            var circ = hitboxGo.AddComponent<CircleCollider2D>();
            circ.isTrigger = true;
            circ.radius = hitboxRadius;
        }

        var tb = hitboxGo.AddComponent<TemporaryHitbox>();
        tb.Setup(damage, hitboxLife, destroyOnHit: false);
    }

    // ----------------------
    // Projectile (PoC -> shared pooled via ProjectilePoolRegistry)
    // ----------------------
    private void FireProjectile(Vector2 direction)
    {
        if (projectilePrefab != null)
        {
            GameObject go = null;

            // Registry가 있으면 공유 풀에서 Spawn 사용 (pool 생성/관리은 레지스트리가 담당)
            if (ProjectilePoolRegistry.Instance != null)
            {
                go = ProjectilePoolRegistry.Instance.Spawn(projectilePrefab, transform.position, Quaternion.identity, projectilePoolInitialSize, projectileColor);
            }
            else
            {
                // 레지스트리가 없으면 로컬 Instantiate(호환성 유지)
                go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
                // tint 시도
                if (projectileColor != Color.white)
                {
                    var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
                    foreach (var sr in srs) if (sr != null) sr.color = projectileColor;
                }
            }

            if (go == null)
            {
                if (showDebugLogs) Debug.LogWarning($"EnemyCombat [{gameObject.name}]: projectile 인스턴스 생성 실패");
                return;
            }

            // Ensure projectile is not parented to this Enemy — leave it in world / pool root
            go.transform.SetParent(null);

            // Enemy 전용 컴포넌트가 있으면 Initialize 사용 (선택적)
            var enemyProj = go.GetComponent<EnemyProjectile>();
            if (enemyProj != null)
            {
                enemyProj.Initialize(direction, projectileSpeed, projectileLifetime, damage, projectileColor, null, transform);
            }
            else
            {
                // 레거시/외부 프리팹: Rigidbody2D velocity 설정 + fallback Destory(레지스트리 Spawn은 풀 내부에서 생성이므로 Release에 의존)
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = direction.normalized * projectileSpeed;
                }

                // 레지스트리로 Spawn했으면 레지스트리에서 풀 관리 -> Destroy 호출하지 않음.
                if (ProjectilePoolRegistry.Instance == null)
                {
                    Destroy(go, projectileLifetime);
                }
            }
        }
        else
        {
            // fallback: short-lived hitbox in front
            GameObject hb = new GameObject("Projectile_FallbackHitbox");
            // Do NOT parent to enemy — place in world at the hitboxOffset position
            hb.transform.SetParent(null);
            hb.transform.position = transform.TransformPoint(hitboxOffset); 
            hb.transform.rotation = Quaternion.identity;
            hb.transform.localScale = Vector3.one;

            var box = hb.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(telegraphSize, telegraphSize);

            var tb = hb.AddComponent<TemporaryHitbox>();
            // Projectile (fallback) : destroy on first collision
            tb.Setup(damage, 0.05f, destroyOnHit: true);
        }
    }

    // ----------------------
    // Utility
    // ----------------------
    public bool CanAttack() => Time.time >= lastAttackTime + attackCooldown;

    public float GetRemainingCooldown()
    {
        float r = (lastAttackTime + attackCooldown) - Time.time;
        return Mathf.Max(0f, r);
    }

    // Dynamically create a default explosion prefab similar to HitParticleEffect.CreateDefaultParticlePrefab()
    private GameObject CreateDefaultExplosionPrefab()
    {
        GameObject prefab = new GameObject("Explosion_DefaultVFX");
        prefab.hideFlags = HideFlags.DontSave;

        ParticleSystem ps = prefab.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startColor = bombTargetColor;
        main.startSize = Mathf.Max(0.2f, suicideRadius * 0.2f);
        main.startSpeed = Mathf.Max(1f, suicideRadius * 2f);
        main.startLifetime = 0.6f;
        main.maxParticles = 40;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)30) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.1f, suicideRadius * 0.3f);
        shape.radiusThickness = 1f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.radial = new ParticleSystem.MinMaxCurve(main.startSpeed.constant);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(bombTargetColor, 0f), new GradientColorKey(bombTargetColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = bombTargetColor;

        prefab.SetActive(false);
        if (showDebugLogs) Debug.Log("EnemyCombat: 기본 Explosion VFX 프리팹 자동 생성 완료 (비활성화됨)");
        return prefab;
    }
}

/// <summary>
/// TemporaryHitbox - 임시 콜라이더: 같은 공격에서 대상 중복 데미지 방지
/// 변경: melee(box)용과 projectile(fallback)용 동작을 분리 — destroyOnHit 플래그로 제어
/// </summary>
public class TemporaryHitbox : MonoBehaviour
{
    private float dmg;
    private float lifetime;
    private bool destroyOnHit = false;
    private HashSet<int> hitIds = new HashSet<int>();

    public void Setup(float damage, float life, bool destroyOnHit = false)
    {
        this.dmg = damage;
        this.lifetime = Mathf.Max(0f, life);
        this.destroyOnHit = destroyOnHit;
        StartCoroutine(LifeRoutine());
    }

    private IEnumerator LifeRoutine()
    {
        // lifetime 후 자동 제거 (폴백)
        yield return new WaitForSeconds(lifetime);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // Enemy 태그와의 충돌은 무시: 파괴하지 않고 통과시키기(친화 처리)
        if (other.CompareTag("Enemy"))
        {
            return;
        }

        // 플레이어일 경우 데미지 적용 (중복 방지)
        if (other.CompareTag("Player"))
        {
            int id = other.GetInstanceID();
            if (!hitIds.Contains(id))
            {
                hitIds.Add(id);
                var hs = other.GetComponent<HealthSystem>() ?? other.GetComponentInParent<HealthSystem>();
                if (hs != null)
                {
                    hs.TakeDamage(dmg);
                }
            }
        }

        // destroyOnHit이 참인 경우(주로 projectile fallback) 충돌 즉시 제거
        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }
}
