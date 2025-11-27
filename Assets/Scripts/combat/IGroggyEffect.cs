/// <summary>
/// 그로기 상태에서 실행될 추가 효과 인터페이스
/// MonoBehaviour 컴포넌트에서 구현하여 EnemyController에 추가할 수 있습니다.
/// </summary>
public interface IGroggyEffect
{
    /// <summary>
    /// 그로기 진입 시 호출됩니다.
    /// </summary>
    /// <param name="controller">그로기 상태에 진입한 EnemyController</param>
    void OnGroggyEnter(EnemyController controller);

    /// <summary>
    /// 그로기 회복/타이머 완료 시 호출됩니다.
    /// 부활 시점에 특정 효과를 발동하고 싶을 때 사용하세요.
    /// </summary>
    /// <param name="controller">그로기에서 회복하는 EnemyController</param>
    void OnGroggyComplete(EnemyController controller);

    /// <summary>
    /// 그로기가 처형 등으로 중단될 때 정리 작업을 수행합니다.
    /// 코루틴 중단, 타이머 정리 등에 사용하세요.
    /// </summary>
    void Cleanup();
}
