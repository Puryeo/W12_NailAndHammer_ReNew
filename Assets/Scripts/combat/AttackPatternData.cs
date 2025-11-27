using UnityEngine;
using System;

/// <summary>
/// AttackPatternData - ScriptableObject 대신 런타임에서 생성해 사용하는 구성 클래스
/// 필요하면 필드를 추가하세요.
/// </summary>
[Serializable]
public class AttackPatternData
{
    public EAttackType attackType = EAttackType.Projectile;

    // 시각/물리
    public Sprite attackSprite;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 5f;
    public float attackDuration = 0.3f;

    // 히트박스
    public EHitboxType hitboxType = EHitboxType.Box;
    public Vector2 hitboxOffset = Vector2.zero;
    public Vector2 hitboxSize = Vector2.one;
    public float hitboxRadius = 1f;

    // 전투 파라미터
    public float damage = 10f;
    public float stunDuration = 0f;
    public EKnockbackStrength knockbackStrength = EKnockbackStrength.None;
    public float knockbackDistance = 1f;

    // 기타
    public bool isRetrievable = true;
}