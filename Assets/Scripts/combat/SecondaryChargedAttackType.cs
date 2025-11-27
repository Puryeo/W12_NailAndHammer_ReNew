/// <summary>
/// 우클릭 차징공격 스킬 타입
/// - None: 타입 미지정 또는 기타 스킬
/// - Windmill: 윈드밀 (플레이어 중심 360도 회전 공격)
/// - Thorns: 가시소환 (내려찍기 후 주변에 가시 생성)
/// - Guardian: 가디언 (큰 망치 소환)
/// - Sector: 부채꼴 (타격 시 부채꼴 범위 공격)
/// </summary>
public enum SecondaryChargedAttackType
{
    None,
    Windmill,
    Thorns,
    Guardian,
    Sector
}
