using UnityEngine;

/// <summary>
/// 우클릭 차징공격 전략 인터페이스
/// - HammerSwingController를 사용하되, 각 공격마다 다른 설정 적용
/// </summary>
public interface ISecondaryChargedAttack
{
    /// <summary>
    /// 차징공격 실행 (망치 생성 및 초기화)
    /// </summary>
    /// <param name="owner">플레이어 PlayerCombat</param>
    /// <param name="ownerTransform">플레이어 Transform</param>
    void Execute(PlayerCombat owner, Transform ownerTransform);

    /// <summary>
    /// 공격 이름 (디버그/UI용)
    /// </summary>a
    string GetAttackName();
}
