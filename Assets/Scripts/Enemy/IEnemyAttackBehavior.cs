using UnityEngine;

/// <summary>
/// IEnemyAttackBehavior - EnemyPatternController가 위임할 행동 계약
/// - Initialize: owner 및 패턴 초기화
/// - Execute: 대상에 대해 행동 트리거
/// - Cancel: 진행중인 행동 정리
/// - ResetForPool: 풀 반환 시 상태 초기화
/// </summary>
public interface IEnemyAttackBehavior
{
    void Initialize(EnemyPatternController owner, PatternPreset preset);
    void Execute(Transform target);
    void Cancel();
    void ResetForPool();
}