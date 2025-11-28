/// <summary>
/// 카드 사용 후 처리 방식
/// </summary>
public enum CardUsageMode
{
    /// <summary>
    /// 사용한 카드를 덱 맨 아래로 반환 (로그라이크 스타일)
    /// </summary>
    ReturnToDeckBottom,

    /// <summary>
    /// 사용한 카드를 게임에서 제거 (1회용)
    /// </summary>
    RemoveFromGame,

    /// <summary>
    /// 사용한 카드를 별도 Discard Pile로 이동
    /// </summary>
    MoveToDiscardPile
}
