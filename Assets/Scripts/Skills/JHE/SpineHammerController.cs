using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SpineHammerController (통합본)
/// - 망치 이동(내려찍기) 제어
/// - 타격 판정 및 결과 처리 (일반 타격 vs 가시 처형 분기)
/// </summary>
public class SpineHammerController : MonoBehaviour
{
    [Header("Motion Settings")]
    [Tooltip("스윙 속도 곡선")]
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("스윙 시작 각도 (목표 각도 기준 뒤로 몇 도?)")]
    [SerializeField] private float swingRange = 120f;
    [Tooltip("스프라이트 회전 보정값 (-90: 위쪽이 앞인 스프라이트)")]
    [SerializeField] private float spriteRotationOffset = -90f;

    [Header("Behaviour")]
    [SerializeField] private float quickStun = 0.12f; // 일반 타격 경직 시간

    // 내부 변수
    private PlayerCombat ownerCombat;
    private float damage;
    private float knockback;
    private float swingDuration;
    private float hitRadius;

    // 가시 스킬 데이터
    private GameObject spineWavePrefab;

    private float targetZAngle;
    private bool isSwinging = false;

    // 초기화 (SpineSkill에서 호출)
    public void Initialize(PlayerCombat owner, float damage, float knockback, float swingDuration, float hitRadius,
                           GameObject spinePrefab, int spineCount, float spineRadius, float targetAngle)
    {
        this.ownerCombat = owner;
        this.damage = damage;
        this.knockback = knockback;
        this.swingDuration = Mathf.Max(0.01f, swingDuration);
        this.hitRadius = hitRadius;

        this.spineWavePrefab = spinePrefab;
        this.targetZAngle = targetAngle;

        StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        isSwinging = true;

        // 1. 각도 계산
        float finalAngle = targetZAngle + spriteRotationOffset;
        float startAngle = finalAngle + swingRange;
        float elapsed = 0f;

        // 2. 스윙 애니메이션
        while (elapsed < swingDuration)
        {
            float t = Mathf.Clamp01(elapsed / swingDuration);
            float curveValue = speedCurve.Evaluate(t);
            float currentAngle = Mathf.Lerp(startAngle, finalAngle, curveValue);

            transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. 최종 위치 확정
        transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);

        // 4. 타격 판정 실행 (여기서 일반/처형이 갈림)
        CheckImpact();

        // 5. 잠시 후 삭제
        Destroy(gameObject, 0.1f);
        isSwinging = false;
    }

    /// <summary>
    /// 타격 판정 로직
    /// </summary>
    private void CheckImpact()
    {
        // 타격점: 해머 머리 부분
        Vector2 impactPoint = transform.TransformPoint(new Vector3(1.5f, 0, 0));

        // 범위 내 적 검출
        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPoint, hitRadius);
        HashSet<int> alreadyHitIds = new HashSet<int>();

        bool anyExecution = false; // 처형 성공 여부

        foreach (var c in hits)
        {
            if (!c.CompareTag("Enemy")) continue;

            var enemyCtrl = c.GetComponent<EnemyController>() ?? c.GetComponentInParent<EnemyController>();
            if (enemyCtrl == null) continue;

            // 중복 타격 방지
            int id = enemyCtrl.GetInstanceID();
            if (alreadyHitIds.Contains(id)) continue;
            alreadyHitIds.Add(id);

            //그로기 상태인가?
            if (enemyCtrl.IsGroggy())
            {
                // 가시 소환 
                ProcessGroggyHit(enemyCtrl, c.transform.position);
                anyExecution = true;
            }
            else
            {
                // 가시 소환
                ProcessNormalHit(enemyCtrl, c.transform.position, impactPoint);
            }
        }

        // (선택) 처형 성공 시 카메라 쉐이크 등을 여기서 추가로 줄 수도 있음
    }

    // -----------------------------------------------------------------------
    // [처형 로직] - 가시 소환 포함
    // -----------------------------------------------------------------------
    private void ProcessGroggyHit(EnemyController enemy, Vector3 hitPos)
    {
        // 말뚝 회수 및 처형 마킹
        enemy.ConsumeStacks(true, true, ownerCombat);
        enemy.MarkExecuted();

        // 가시 소환
        enemy.ApplyImpale(3.0f); // 속박

        SpawnSpineWave(hitPos);

        // 처형 이펙트 및 사망 처리
        var enemyHealth = enemy.GetComponent<HealthSystem>();
        if (enemyHealth != null)
        {
            var he = enemyHealth.GetComponent<HitEffect>();
            if (he != null) he.PlayExecuteEffect();

            var hpe = enemyHealth.GetComponent<HitParticleEffect>();
            if (hpe != null) hpe.PlayExecuteParticle(hitPos);

            enemyHealth.ForceDieWithFade(1f);
        }

        // 강한 타격감
        HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Strong, EShakeStrength.Strong, hitPos);

        Debug.Log($"[SpineHammer] {enemy.name} 처형 성공 (가시 발동)");
    }

    //일반 타격
    private void ProcessNormalHit(EnemyController enemy, Vector3 hitPos, Vector2 sourcePos)
    {
        var health = enemy.GetComponent<HealthSystem>();
        var rb = enemy.GetComponent<Rigidbody2D>();

        // 데미지
        if (health != null) health.TakeDamage(damage);

        // 경직 (Stun)
        if (quickStun > 0f) enemy.ApplyStun(quickStun);

        // 넉백 (Knockback)
        if (rb != null)
        {
            Vector2 dir = (hitPos - (Vector3)sourcePos).normalized;
            // 내려찍기지만 살짝 퍼지도록
            rb.AddForce(dir * knockback, ForceMode2D.Impulse);
        }

        // 타격 이펙트 (일반 강도)
        HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Weak, EShakeStrength.Weak, hitPos);

        // 말뚝 회수 및 히트 등록
        enemy.RegisterHit(1, ownerCombat.transform);
        enemy.ConsumeStacks(true, true, ownerCombat);

        Debug.Log($"[SpineHammer] {enemy.name} 일반 타격 (가시 미발동)");
    }

    private void SpawnSpineWave(Vector3 spawnPos)
    {
        if (spineWavePrefab == null) return;

        GameObject waveObj = Instantiate(spineWavePrefab, spawnPos, Quaternion.identity);
        bool isLookingLeft = Mathf.Abs(targetZAngle) > 90f;

        if (isLookingLeft)
        {
            Vector3 scale = waveObj.transform.localScale;
            scale.x = -Mathf.Abs(scale.x);
            waveObj.transform.localScale = scale;
        }
        else
        {
            Vector3 scale = waveObj.transform.localScale;
            scale.x = Mathf.Abs(scale.x);
            waveObj.transform.localScale = scale;
        }
    }

    private void OnDrawGizmos()
    {
        if (isSwinging)
        {
            Gizmos.color = Color.red;
            Vector2 impactPoint = transform.TransformPoint(new Vector3(1.5f, 0, 0));
            Gizmos.DrawWireSphere(impactPoint, hitRadius);
        }
    }
}