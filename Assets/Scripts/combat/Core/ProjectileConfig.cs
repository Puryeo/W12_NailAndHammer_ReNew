using UnityEngine;

/// <summary>
/// 투사체의 모든 설정을 담는 ScriptableObject
/// Unity 에디터에서 생성 가능: Assets/Create/Combat/Projectile Config
/// </summary>
[CreateAssetMenu(fileName = "New Projectile Config", menuName = "Combat/Projectile Config", order = 0)]
public class ProjectileConfig : ScriptableObject
{
    [Header("Basic")]
    public float damage = 10f;
    public float speed = 14f;
    public float lifetime = 5f;
    public bool isRetrievable = true;

    [Header("Collision Behavior")]
    public CollisionBehaviorType collisionType = CollisionBehaviorType.StickToEnemy;

    [Header("Retrieval Behavior")]
    public RetrievalBehaviorType retrievalType = RetrievalBehaviorType.Simple;
    public float returnSpeed = 20f;
    public float returnDamageRatio = 0.5f;

    // ========== Impaling 전용 설정 ==========
    [Header("Impaling Settings")]
    [Tooltip("꿰뚫기 활성화 여부")]
    public bool canImpale = false;

    [Tooltip("최대 꿰뚫을 수 있는 적 수")]
    [Range(1, 10)]
    public int maxImpaleCount = 5;

    [Tooltip("꿰뚫린 적들 사이 간격 (월드 단위)")]
    public float enemySpacing = 0.3f;

    [Tooltip("꿰뚫었을 때 투사체 속도 증가 여부")]
    public bool accelerateOnImpale = true;

    [Tooltip("꿰뚫었을 때 속도 배수")]
    public float impaleSpeedMultiplier = 1.3f;

    [Tooltip("벽 없이 날아갈 최대 거리")]
    public float maxImpalingDistance = 10f;

    [Header("Wall Impact (Impaling)")]
    [Tooltip("벽 충돌 시 꿰뚫린 적들에게 추가 데미지")]
    public float wallImpactDamage = 20f;

    [Tooltip("벽 충돌 시 스턴 적용 여부")]
    public bool applyStunOnWallImpact = true;

    [Tooltip("벽 충돌 스턴 지속 시간")]
    public float wallImpactStunDuration = 2f;

    // ========== Retrieval A (Binding) 전용 ==========
    [Header("Binding Retrieval Settings")]
    [Tooltip("속박 지속 시간")]
    public float bindingDuration = 3f;

    [Tooltip("속박 시 이동 속도 감소 비율 (0.5 = 50% 감속)")]
    [Range(0f, 1f)]
    public float bindingSlowAmount = 0.5f;

    // ========== Retrieval B (Pull) 전용 ==========
    [Header("Pull Retrieval Settings")]
    [Tooltip("몬스터를 끌어오는 힘")]
    public float pullForce = 15f;

    [Tooltip("끌려온 몬스터가 벽 충돌 시 받는 데미지")]
    public float pullWallImpactDamage = 30f;

    [Tooltip("플레이어 앞 몇 유닛까지만 끌어올 것인지 (0 = 플레이어 위치까지)")]
    public float pullStopDistance = 1.5f;

    // ========== Retrieval C (StuckEnemyPull) 전용 ==========
    [Header("StuckEnemyPull Retrieval Settings")]
    [Tooltip("몬스터를 끌어오는 전체 시간 (초) - 애니메이션 커브 적용 시간이자 타임아웃")]
    public float pullDuration = 1.0f;

    [Tooltip("몬스터 분리 거리 (플레이어로부터) - 이 거리에 도달하면 몬스터를 분리하고 투사체만 회수")]
    public float pullDetachDistance = 2.0f;

    // ========== 공통 회수 비주얼 설정 (모든 Retrieval에 적용 가능) ==========
    [Header("Retrieval Visual Settings (All Modes)")]
    [Tooltip("라인렌더러 활성화 여부")]
    public bool enableLineRenderer = false;

    [Tooltip("LineRenderer 프리팹 (null이면 기본 LineRenderer 생성)")]
    public GameObject linePrefab;

    [Tooltip("말뚝 끝에 표시할 장식 프리팹 (옵션)")]
    public GameObject endDecoratorPrefab;

    // ========== 공통 회수 파라미터 (모든 Retrieval에 적용 가능) ==========
    [Header("Retrieval Animation Parameters (All Modes)")]
    [Tooltip("앞당김 거리")]
    public float pullDistance = 0.6f;

    [Tooltip("히트스톱 지속 시간 (초)")]
    public float hitStopSeconds = 0.1f;

    [Tooltip("플레이어에게 이동하는 애니메이션 시간 (초)")]
    public float moveToPlayerDuration = 0.25f;

    [Tooltip("각 말뚝 애니메이션 시작 간격 (순차 연출용, 초)")]
    public float spawnInterval = 0.04f;

    [Tooltip("회수 속도 커브 (0~1 시간 → 0~1 진행도)")]
    public AnimationCurve retrievalCurve = AnimationCurve.Linear(0, 0, 1, 1);

    // ========== 공통 히트 피드백 (모든 Retrieval에 적용 가능) ==========
    [Header("Retrieval Hit Feedback (All Modes)")]
    [Tooltip("히트스톱 강도")]
    public EHitStopStrength hitStop = EHitStopStrength.None;

    [Tooltip("카메라 셰이크 강도")]
    public EShakeStrength shake = EShakeStrength.None;
}
