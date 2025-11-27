using System.Collections;
using UnityEngine;

/// <summary>
/// KnockbackRunner - 타깃에 붙여 지연된 넉백을 실행하는 임시 컴포넌트
/// 생성 후 Initialize()로 설정하며, 작업 완료 시 자신만 Destroy 함.
/// </summary>
public class KnockbackRunner : MonoBehaviour
{
    private Vector2 dir;
    private float force;
    private float delay;
    private CameraShake cameraShake;
    private EKnockbackStrength strength;

    public void Initialize(Vector2 dir, float force, float delay, CameraShake cameraShake, EKnockbackStrength strength)
    {
        this.dir = dir;
        this.force = force;
        this.delay = delay;
        this.cameraShake = cameraShake;
        this.strength = strength;

        // 시작
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(delay);

        // 대상이 여전히 유효한지 확인
        if (this == null || gameObject == null) yield break;
        if (!gameObject.activeInHierarchy) yield break;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.AddForce(dir * force, ForceMode2D.Impulse);
        }

        // CameraShake 호출 (넉백 시점에 맞춤)
        if (cameraShake != null)
        {
            if (strength == EKnockbackStrength.Weak) cameraShake.ShakeWeak();
            else if (strength == EKnockbackStrength.Medium) cameraShake.ShakeMedium();
            else if (strength == EKnockbackStrength.Strong) cameraShake.ShakeStrong();
        }

        // 작업 완료: 컴포넌트만 제거
        Destroy(this);
    }
}