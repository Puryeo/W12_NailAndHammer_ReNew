using System.Collections;
using UnityEngine;

/// <summary>
/// EnemyPatternController - 패턴 프리셋을 보유하고 IEnemyAttackBehavior로 실행을 위임하는 스켈레톤
/// PR #1에서는 구체 행동(Behavior들)은 별도 PR에서 구현됩니다.
/// 기본 동작: 패턴 설정(Configure), Execute/TryTrigger, ForceCancel, ResetForPool 제공.
/// </summary>
[DisallowMultipleComponent]
public class EnemyPatternController : MonoBehaviour
{
    [Header("Inspector (PoC)")]
    [Tooltip("패턴 프리셋. Assets > Create > Enemy > Pattern Preset 로 생성")]
    [SerializeField] private PatternPreset patternPreset;

    [Tooltip("Inspector에서 임시로 타입을 오버라이드할 때 사용")]
    [SerializeField] private EAttackType inspectorAttackType = EAttackType.Projectile;

    // 런타임
    private IEnemyAttackBehavior currentBehavior;
    private float lastTriggerTime = -999f;

    private void OnDisable()
    {
        ForceCancel();
    }

    private void OnDestroy()
    {
        ForceCancel();
    }

    /// <summary>
    /// Configure: 외부에서 패턴을 할당하여 컨트롤러를 재설정합니다.
    /// </summary>
    public void Configure(PatternPreset preset)
    {
        patternPreset = preset;
        if (preset != null) inspectorAttackType = preset.attackType;
        InitializeBehavior();
    }

    /// <summary>
    /// TryTrigger: 쿨타임 체크 후 행동을 실행. 실행되면 true 반환.
    /// </summary>
    public bool TryTrigger(Transform target)
    {
        // 필수: 패턴이 없으면 동작하지 않음
        if (patternPreset == null)
        {
            Debug.LogWarning($"EnemyPatternController [{gameObject.name}]: PatternPreset이 할당되지 않아 TryTrigger를 수행할 수 없습니다.");
            return false;
        }

        float cooldown = patternPreset != null ? patternPreset.cooldown : 0f;
        if (Time.time < lastTriggerTime + cooldown) return false;

        EnsureBehaviorInitialized();
        currentBehavior.Initialize(this, patternPreset);
        currentBehavior.Execute(target);
        lastTriggerTime = Time.time;
        return true;
    }

    /// <summary>
    /// Execute: EnemyCombat 같은 외부에서 직접 호출하기 위한 편의 API
    /// </summary>
    public void Execute(Transform target)
    {
        TryTrigger(target);
    }

    /// <summary>
    /// ForceCancel: 현재 진행중인 행동이 있으면 취소
    /// </summary>
    public void ForceCancel()
    {
        currentBehavior?.Cancel();
    }

    /// <summary>
    /// ResetForPool: 풀에 반환되기 전 상태 초기화
    /// </summary>
    public void ResetForPool()
    {
        currentBehavior?.ResetForPool();
        currentBehavior = null;
        patternPreset = null;
        lastTriggerTime = -999f;
    }

    private void EnsureBehaviorInitialized()
    {
        if (currentBehavior != null) return;
        InitializeBehavior();
    }

    private void InitializeBehavior()
    {
        // PR #2: MeleeBehavior PoC 지원 추가
        // 안전: enum 멤버 직접 참조로 인한 컴파일 문제를 피하기 위해 ToString 검사 사용
        string typeName = patternPreset != null ? patternPreset.attackType.ToString() : inspectorAttackType.ToString();

        if (!string.IsNullOrEmpty(typeName) && typeName.Contains("Melee"))
        {
            currentBehavior = new MeleeBehavior();
            return;
        }

        // PR #3 이후에 Projectile, Suicide 등 추가 예정
        currentBehavior = new NoOpBehavior();
    }

    // 간단한 기본 No-op 구현 (컴파일 안전성 확보용)
    private class NoOpBehavior : IEnemyAttackBehavior
    {
        public void Initialize(EnemyPatternController owner, PatternPreset preset) { /* no-op */ }
        public void Execute(Transform target) { /* no-op */ }
        public void Cancel() { /* no-op */ }
        public void ResetForPool() { /* no-op */ }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 패턴이 할당되어 있으면 타입 미리 동기화
        if (patternPreset != null) inspectorAttackType = patternPreset.attackType;
    }
#endif
}