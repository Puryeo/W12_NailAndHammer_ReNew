using UnityEngine;

/// <summary>
/// 투사체 상태 머신을 관리하는 컨트롤러
/// AttackProjectile에 포함되어 상태 전환 및 로직 처리
/// </summary>
public class ProjectileStateController
{
    private AttackProjectile projectile;
    private ProjectileState currentState = ProjectileState.Inactive;

    [SerializeField] private bool showDebugLogs = false;

    public ProjectileState CurrentState => currentState;

    public ProjectileStateController(AttackProjectile proj, bool debugLogs = false)
    {
        this.projectile = proj;
        this.showDebugLogs = debugLogs;
    }

    /// <summary>
    /// 상태 전환
    /// </summary>
    public void ChangeState(ProjectileState newState)
    {
        if (currentState == newState) return;

        // 이전 상태 종료
        OnExitState(currentState);

        ProjectileState prevState = currentState;
        currentState = newState;

        if (showDebugLogs)
            Debug.Log($"ProjectileStateController: {prevState} → {newState}");

        // 새 상태 진입
        OnEnterState(newState, prevState);
    }

    private void OnExitState(ProjectileState state)
    {
        switch (state)
        {
            case ProjectileState.Flying:
                // 비행 종료 처리
                break;
            case ProjectileState.Impaling:
                // 꿰뚫기 종료 처리
                break;
            case ProjectileState.Returning:
                // 회수 종료 처리
                break;
        }
    }

    private void OnEnterState(ProjectileState state, ProjectileState prevState)
    {
        switch (state)
        {
            case ProjectileState.Launching:
                projectile.OnStateLaunching();
                break;

            case ProjectileState.Flying:
                projectile.OnStateFlying();
                break;

            case ProjectileState.Impaling:
                projectile.OnStateImpaling(prevState);
                break;

            case ProjectileState.Stuck:
                projectile.OnStateStuck();
                break;

            case ProjectileState.Returning:
                projectile.OnStateReturning();
                break;

            case ProjectileState.Collected:
                projectile.OnStateCollected();
                break;
        }
    }

    /// <summary>
    /// 매 프레임 상태별 업데이트
    /// </summary>
    public void UpdateState()
    {
        switch (currentState)
        {
            case ProjectileState.Flying:
                projectile.UpdateFlying();
                break;

            case ProjectileState.Impaling:
                projectile.UpdateImpaling();
                break;

            case ProjectileState.Returning:
                projectile.UpdateReturning();
                break;
        }
    }
}
