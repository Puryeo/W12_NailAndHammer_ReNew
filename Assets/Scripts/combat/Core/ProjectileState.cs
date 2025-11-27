/// <summary>
/// 투사체의 생명주기 상태
/// </summary>
public enum ProjectileState
{
    Inactive,       // 비활성 (풀에서 대기 중)
    Launching,      // 발사 시작
    Flying,         // 일반 비행 중
    Impaling,       // 몬스터를 꿰뚫고 끌고 가는 중
    Stuck,          // 박힌 상태 (벽 또는 적)
    Returning,      // 회수 중
    Collected       // 플레이어에게 복귀 완료
}

/// <summary>
/// 충돌 처리 방식
/// </summary>
public enum CollisionBehaviorType
{
    None,
    StickToEnemy,      // 적에 바로 박힘
    ImpaleAndCarry,    // 적을 꿰뚫고 끌고 감
    Bounce             // 튕겨냄
}

/// <summary>
/// 회수 처리 방식
/// </summary>
public enum RetrievalBehaviorType
{
    None,              // 회수 불가
    Simple,            // 단순 회수 (탄약만 회복)
    Binding,           // 경로 몬스터 속박 (옵션 A)
    Pull,              // 경로 몬스터 끌어오기 (옵션 B)
    StuckEnemyPull,    // 박힌 적만 끌어오기 (옵션 B-2)
    Chain              // 연쇄 효과
}
