using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpineHammerController : MonoBehaviour
{
    [Header("Motion Settings")]
    [Tooltip("스윙 속도 곡선 (EaseIn: 천천히 시작해서 빠르게 쾅)")]
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("해머가 시작될 때 목표 지점보다 뒤로 가있는 각도 (예: 120도 뒤에서 시작)")]
    [SerializeField] private float swingRange = 120f;

    [Tooltip("스프라이트 보정값: 이미지가 위(↑)를 보면 -90, 오른쪽(→)이면 0")]
    [SerializeField] private float spriteRotationOffset = -90f;

    [Header("Behaviour")]
    [SerializeField] private float quickStun = 0.12f;

    // 내부 변수 (HammerSwingController 스타일)
    private PlayerCombat ownerCombat;
    private float damage;
    private float knockback;
    private float swingDuration;
    private float hitRadius;

    // 가시 스킬 데이터
    private GameObject spinePrefab;
    private int spineCount;
    private float spineRadius;

    private float targetZAngle; // 목표(마우스) 각도
    private bool isSwinging = false;

    // 초기화
    public void Initialize(PlayerCombat owner, float damage, float knockback, float swingDuration, float hitRadius,
                           GameObject spinePrefab, int spineCount, float spineRadius, float targetAngle)
    {
        this.ownerCombat = owner;
        this.damage = damage;
        this.knockback = knockback;
        this.swingDuration = Mathf.Max(0.01f, swingDuration); // 0 방지
        this.hitRadius = hitRadius;

        this.spinePrefab = spinePrefab;
        this.spineCount = spineCount;
        this.spineRadius = spineRadius;

        this.targetZAngle = targetAngle;

        StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        isSwinging = true;

        // 1. 각도 계산
        // 목표 각도 (마우스 방향 + 스프라이트 보정)
        float finalAngle = targetZAngle + spriteRotationOffset;
        // 시작 각도 (목표 각도 + 스윙 범위만큼 뒤로)
        float startAngle = finalAngle + swingRange;

        float elapsed = 0f;

        while (elapsed < swingDuration)
        {
            // 진행도 (0 ~ 1)
            float t = Mathf.Clamp01(elapsed / swingDuration);
            // 커브 적용 (속도감 조절)
            float curveValue = speedCurve.Evaluate(t);

            // 현재 각도 계산 (Lerp로 부드럽게)
            float currentAngle = Mathf.Lerp(startAngle, finalAngle, curveValue);

            // 회전 적용
            transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2. 최종 위치 확정 (쾅!)
        transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);

        // 3. 타격 판정 (Trigger 대신 직접 체크)
        CheckImpact();

        // 4. 잠시 후 삭제
        Destroy(gameObject, 0.1f);
        isSwinging = false;
    }

    private void CheckImpact()
    {
        // 타격 중심점: 해머의 머리 부분 (로컬 X축으로 길이만큼 이동)
        // (1.5f는 해머 길이에 따라 조절하세요)
        Vector2 impactPoint = transform.TransformPoint(new Vector3(1.5f, 0, 0));

        // 범위 내 적 검출
        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPoint, hitRadius);
        HashSet<int> alreadyHitIds = new HashSet<int>();

        foreach (var c in hits)
        {
            if (!c.CompareTag("Enemy")) continue;

            var enemyCtrl = c.GetComponent<EnemyController>() ?? c.GetComponentInParent<EnemyController>();
            if (enemyCtrl == null) continue;

            // 중복 타격 방지
            int id = enemyCtrl.GetInstanceID();
            if (alreadyHitIds.Contains(id)) continue;
            alreadyHitIds.Add(id);

            // 상태 분기
            if (enemyCtrl.IsGroggy())
            {
                ProcessGroggyHit(enemyCtrl, c.transform.position);
            }
            else
            {
                ProcessNormalHit(enemyCtrl, c.transform.position, impactPoint);
            }
        }
    }

    // [처형 로직]
    private void ProcessGroggyHit(EnemyController enemy, Vector3 hitPos)
    {
        // 1. 말뚝 회수 및 처형 마킹
        enemy.ConsumeStacks(true, true, ownerCombat);
        enemy.MarkExecuted();

        // 2. 처형 보상 (PlayerCombat의 OnExecutionSuccess 호출이 필요하면 여기서 직접 호출하거나 델리게이트 사용)
        // 여기서는 SpineHammer는 힐 보상이 없다고 가정했으나, 필요하면 아래 주석 해제
        // ownerCombat.OnExecutionSuccess(20f, 0); 

        // 3. 속박 및 가시 소환
        enemy.ApplyImpale(3.0f);
        SpawnSpines(hitPos);

        // 4. 이펙트 및 사망 처리
        var enemyHealth = enemy.GetComponent<HealthSystem>();
        if (enemyHealth != null)
        {
            var he = enemyHealth.GetComponent<HitEffect>();
            if (he != null) he.PlayExecuteEffect();

            enemyHealth.ForceDieWithFade(1f);
        }

        HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Strong, EShakeStrength.Strong, enemy.transform.position);
    }

    // [일반 타격 로직]
    private void ProcessNormalHit(EnemyController enemy, Vector3 hitPos, Vector2 sourcePos)
    {
        var health = enemy.GetComponent<HealthSystem>();

        // 데미지
        if (health != null)
        {
            health.TakeDamage(damage);
            if (quickStun > 0f) enemy.ApplyStun(quickStun);
        }

        // 넉백
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 dir = (hitPos - (Vector3)sourcePos).normalized;
            // 살짝 위로 띄우기
            dir += Vector2.up * 0.2f;
            rb.AddForce(dir.normalized * knockback, ForceMode2D.Impulse);
        }

        // 이펙트
        HitEffectManager.PlayHitEffect(EHitSource.Hammer, EHitStopStrength.Weak, EShakeStrength.Weak, hitPos);

        // 일반 타격 등록 (말뚝 회수)
        enemy.RegisterHit(1, ownerCombat.transform);
        enemy.ConsumeStacks(true, true, ownerCombat);
    }

    private void SpawnSpines(Vector3 centerPos)
    {
        if (spinePrefab == null) return;

        Instantiate(spinePrefab, centerPos, Quaternion.identity);

        for (int i = 0; i < spineCount; i++)
        {
            float angle = i * (360f / spineCount);
            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            Vector3 spawnPos = centerPos + dir * spineRadius;
            Instantiate(spinePrefab, spawnPos, Quaternion.identity);
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