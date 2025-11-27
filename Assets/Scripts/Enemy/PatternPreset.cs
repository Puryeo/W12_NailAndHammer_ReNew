using UnityEngine;

/// <summary>
/// PatternPreset - 몬스터 공격 패턴 데이터 (PoC 최소 필드)
/// CreateAssetMenu으로 에셋 생성 가능하도록 제공합니다.
/// </summary>
[CreateAssetMenu(menuName = "Enemy/Pattern Preset", fileName = "PatternPreset")]
public class PatternPreset : ScriptableObject
{
    [Header("Common")]
    public string patternName = "New Pattern";
    public EAttackType attackType = EAttackType.Projectile; // 기존 프로젝트의 EAttackType 사용
    public float damage = 10f;
    public float cooldown = 2f;

    [Header("Telegraph")]
    public bool useTelegraph = false;
    public GameObject telegraphPrefab;
    public float telegraphDelay = 0.8f;
    public float telegraphSize = 1.5f;
    [Tooltip("텔레그래프 색상(알파 포함). 인디케이터 스프라이트가 있으면 이 색이 적용됩니다)")]
    public Color telegraphColor = new Color(1f, 0f, 0f, 0.45f);

    [Header("Melee (Hitbox)")]
    public EHitboxType hitboxType = EHitboxType.Box;
    public Vector2 hitboxOffset = Vector2.zero;
    public Vector2 hitboxSize = Vector2.one;
    public float hitboxRadius = 1f;

    [Header("Projectile")]
    public float projectileSpeed = 10f;
    public float projectileLifetime = 5f;
    [Tooltip("풀 키(또는 프리팹 식별자). PoC에서는 문자열 키로 관리")]
    public string projectilePrefabKey = "";

    [Header("Suicide")]
    public float fuseTime = 1f;
    public float aoeRadius = 1.5f;
    public float aoeDamage = 20f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 간단한 편의 검사 (에디터 시 로깅)
        if (string.IsNullOrEmpty(patternName)) patternName = name;
        if (cooldown < 0f) cooldown = 0f;
        if (projectileSpeed < 0f) projectileSpeed = 0f;
        if (projectileLifetime < 0f) projectileLifetime = 0f;
        telegraphColor.a = Mathf.Clamp01(telegraphColor.a);
    }
#endif
}