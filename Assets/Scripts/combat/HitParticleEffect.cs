using UnityEngine;

/// <summary>
/// HitParticleEffect - 피격 시 파티클 효과 생성 및 재생
/// - 기본(Basic) 이펙트와 Execute(처형) 이펙트를 분리하여 재생 가능하게 확장
/// </summary>
public class HitParticleEffect : MonoBehaviour
{
    [Header("Basic Particle Settings")]
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private Color particleColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private int particleCount = 10;
    [SerializeField] private float particleSize = 0.1f;
    [SerializeField] private float particleSpeed = 3f;
    [SerializeField] private float particleLifetime = 0.3f;

    [Header("Execute Particle Settings")]
    [SerializeField] private Color executeColor = new Color(1f, 0.9f, 0.6f);
    [SerializeField] private int executeCount = 22;
    [SerializeField] private float executeSize = 0.18f;
    [SerializeField] private float executeSpeed = 4.5f;
    [SerializeField] private float executeLifetime = 0.6f;

    private void Awake()
    {
        if (particlePrefab == null)
        {
            particlePrefab = CreateDefaultParticlePrefab();
        }
    }

    public void PlayHitParticle() => PlayHitParticle(transform.position);

    public void PlayHitParticle(Vector3 worldPosition)
    {
        PlayParticleInternal(worldPosition, particleColor, particleCount, particleSize, particleSpeed, particleLifetime);
    }

    /// <summary>
    /// 처형 전용 파티클 재생 (더 크고 느리게 퍼지는 이펙트)
    /// </summary>
    public void PlayExecuteParticle(Vector3 worldPosition)
    {
        PlayParticleInternal(worldPosition, executeColor, executeCount, executeSize, executeSpeed, executeLifetime);
    }

    private void PlayParticleInternal(Vector3 worldPosition, Color color, int count, float size, float speed, float lifetime)
    {
        if (particlePrefab == null) return;

        GameObject particleObj = Instantiate(particlePrefab, worldPosition, Quaternion.identity);
        particleObj.SetActive(true);

        ParticleSystem ps = particleObj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.startColor = color;
            main.startSize = size;
            main.startSpeed = speed;
            main.startLifetime = lifetime;
            main.maxParticles = count;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

            // 중요한 부분: 파티클이 Time.timeScale에 영향을 받지 않도록 시뮬레이션에 unscaled time 사용 (Unity 버전에서 지원하면)
            #if UNITY_2019_3_OR_NEWER
            main.useUnscaledTime = true;
            #endif

            ps.Play();
        }

        Destroy(particleObj, lifetime + 0.5f);
    }

    private GameObject CreateDefaultParticlePrefab()
    {
        GameObject prefab = new GameObject("HitParticle_Default");
        prefab.hideFlags = HideFlags.DontSave;

        ParticleSystem ps = prefab.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startColor = particleColor;
        main.startSize = particleSize;
        main.startSpeed = particleSpeed;
        main.startLifetime = particleLifetime;
        main.maxParticles = particleCount;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)particleCount) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;
        shape.radiusThickness = 1f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.radial = new ParticleSystem.MinMaxCurve(particleSpeed);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(particleColor, 0f), new GradientColorKey(particleColor, 1f) },
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
        renderer.material.color = particleColor;

        prefab.SetActive(false);
        Debug.Log("HitParticleEffect: 기본 파티클 프리팹 자동 생성 완료 (비활성화됨)");

        return prefab;
    }

    public void SetParticleColor(Color color) { particleColor = color; }
    public void SetParticleCount(int count) { particleCount = count; }
    public void SetParticleSize(float size) { particleSize = size; }
    public void SetParticleSpeed(float speed) { particleSpeed = speed; }
}