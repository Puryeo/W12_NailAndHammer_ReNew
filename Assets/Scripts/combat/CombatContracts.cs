using UnityEngine;

/// <summary>
/// CombatContracts - 전투 관련 소규모 계약(인터페이스)과 공용 타입을 한곳에 모음
/// - 기존 전역 타입 이름(IDamageable, IStunnable 등)을 그대로 유지하여 호환성 보장
/// - 필요시 여기에서 확장(이벤트 델리게이트, 공통 구조체 등)하세요.
/// </summary>
public static class CombatContracts
{
    // 의도적으로 비어있는 컨테이너형 클래스: 문서 목적
}

/// <summary>
/// 대미지 가능한 객체 계약
/// </summary>
public interface IDamageable
{
    /// <summary>기본 대미지 처리</summary>
    void TakeDamage(float damage);
}

/// <summary>
/// 경직(stun) 인터페이스
/// </summary>
public interface IStunnable
{
    /// <summary>지정 시간 동안 경직 적용</summary>
    void ApplyStun(float duration);

    /// <summary>경직 여부 조회</summary>
    bool IsStunned();
}

/// <summary>
/// 전투 관련 공용 열거형 (작은 규모일 경우 여기서 함께 관리)
/// 필요시 별도 파일로 분리 가능
/// </summary>
public enum EAttackType
{
    Projectile,
    MeleeSlash,
    Hitbox,
    Suicide
}

public enum EHitboxType
{
    Box,
    Circle
}

// 변경: 일부 코드에서 'Weak' 항목을 사용하므로 Weak를 추가했습니다.
public enum EKnockbackStrength
{
    None = 0,
    Weak = 1,
    Small = 2,
    Medium = 3,
    Strong = 4
}

// ----------------- 히트 이펙트 공용 타입 추가 -----------------
public enum EHitStopStrength
{
    None = 0,
    Weak = 1,
    Medium = 2,
    Strong = 3
}

public enum EShakeStrength
{
    None = 0,
    Weak = 1,
    Medium = 2,
    Strong = 3
}

public enum EHitSource
{
    Unknown = 0,
    Stake = 1,
    Hammer = 2,
    ReturnProjectile = 3
}