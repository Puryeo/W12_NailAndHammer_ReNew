using UnityEngine;

/// <summary>
/// 카드 데이터
/// - 각 카드는 하나의 스킬(SecondaryChargedAttackType)에 매핑됨
/// </summary>
[System.Serializable]
public class Card
{
    [Tooltip("이 카드와 연결된 스킬 타입")]
    public SecondaryChargedAttackType skillType;

    [Tooltip("카드 이름 (UI 표시용)")]
    public string cardName;

    [Tooltip("카드 아이콘 (UI 표시용)")]
    public Sprite cardIcon;

    public Card(SecondaryChargedAttackType skillType, string cardName = "", Sprite cardIcon = null)
    {
        this.skillType = skillType;
        this.cardName = cardName;
        this.cardIcon = cardIcon;
    }

    public override string ToString()
    {
        return $"Card[{cardName}({skillType})]";
    }
}
