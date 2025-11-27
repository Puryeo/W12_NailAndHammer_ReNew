using System.Collections;
using UnityEngine;

/// <summary>
/// PoC MeleeBehavior - IEnemyAttackBehavior 구현
/// - telegraph(옵션) 후 즉시 히트박스 검사(OverlapBox / OverlapCircle)
/// - Player 태그 대상의 HealthSystem.TakeDamage 호출
/// - ResetForPool/Cancel에서 코루틴 및 임시 오브젝트 정리
/// - telegraphPrefab의 SpriteRenderer(또는 자식 SpriteRenderer)에 preset.telegraphColor를 적용하고
///   히트박스 크기에 맞춰 스케일을 조정합니다.
/// - telegraph/히트 위치는 로컬 오프셋을 owner.TransformPoint로 변환하여 계산합니다.
/// </summary>
public class MeleeBehavior : IEnemyAttackBehavior
{
    private EnemyPatternController owner;
    private PatternPreset preset;
    private Coroutine runningCoroutine;
    private GameObject telegraphInstance;

    public void Initialize(EnemyPatternController owner, PatternPreset preset)
    {
        // 안전 초기화: 이전 실행 취소
        Cancel();
        this.owner = owner;
        this.preset = preset;
    }

    public void Execute(Transform target)
    {
        if (owner == null || preset == null) return;

        // PoC: 대상 태그는 Player로 고정 (향후 파라미터화)
        if (preset.useTelegraph && preset.telegraphDelay > 0f)
        {
            runningCoroutine = owner.StartCoroutine(TelegraphRoutine(target));
        }
        else
        {
            DoHitCheck(target);
        }
    }

    private IEnumerator TelegraphRoutine(Transform target)
    {
        // telegraph 프리팹이 있으면 인스턴스(풀 미구현 PoC)
        if (preset.telegraphPrefab != null)
        {
            try
            {
                // parent로 붙여서 Enemy의 회전/이동을 따르게 함
                telegraphInstance = Object.Instantiate(preset.telegraphPrefab, owner.transform);
                // 로컬 기준 오フ셋을 그대로 적용하면 "적의 앞"에 배치됨
                telegraphInstance.transform.localPosition = (Vector3)preset.hitboxOffset;

                // 스케일: 히트박스 크기와 일치하도록 설정 (telegraphSize는 추가 배수로 사용)
                Vector3 targetScale = Vector3.one;
                if (preset.hitboxType == EHitboxType.Box)
                {
                    targetScale = new Vector3(preset.hitboxSize.x, preset.hitboxSize.y, 1f) * preset.telegraphSize;
                }
                else // Circle
                {
                    float diameter = preset.hitboxRadius * 2f * preset.telegraphSize;
                    targetScale = new Vector3(diameter, diameter, 1f);
                }
                telegraphInstance.transform.localScale = targetScale;

                // 색 적용: SpriteRenderer가 있으면 색을 덮어씀
                var sr = telegraphInstance.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = preset.telegraphColor;
                }
            }
            catch
            {
                // 인스턴스 생성 실패 시 안전하게 무시
                telegraphInstance = null;
            }
        }

        float delay = Mathf.Max(0f, preset.telegraphDelay);
        yield return new WaitForSeconds(delay);

        DoHitCheck(target);

        if (telegraphInstance != null)
        {
            Object.Destroy(telegraphInstance);
            telegraphInstance = null;
        }

        runningCoroutine = null;
    }

    private void DoHitCheck(Transform target)
    {
        if (preset == null || owner == null) return;

        // 로컬 오프셋을 월드 위치로 변환하여 검사(적의 회전을 반영)
        Vector2 origin = (Vector2)owner.transform.TransformPoint((Vector3)preset.hitboxOffset);
        Collider2D[] hits;

        if (preset.hitboxType == EHitboxType.Box)
        {
            hits = Physics2D.OverlapBoxAll(origin, preset.hitboxSize, owner.transform.eulerAngles.z);
        }
        else
        {
            hits = Physics2D.OverlapCircleAll(origin, preset.hitboxRadius);
        }

        if (hits == null || hits.Length == 0) return;

        foreach (var c in hits)
        {
            if (c == null) continue;
            if (!c.CompareTag("Player")) continue;

            var hs = c.GetComponent<HealthSystem>() ?? c.GetComponentInParent<HealthSystem>();
            if (hs != null)
            {
                hs.TakeDamage(preset.damage);
            }
        }
    }

    public void Cancel()
    {
        if (runningCoroutine != null && owner != null)
        {
            owner.StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }

        if (telegraphInstance != null)
        {
            Object.Destroy(telegraphInstance);
            telegraphInstance = null;
        }
    }

    public void ResetForPool()
    {
        Cancel();
        owner = null;
        preset = null;
    }
}