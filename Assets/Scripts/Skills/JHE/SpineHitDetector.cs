using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 런타임에 망치 오브젝트에 추가되어, 그로기 상태의 적을 별도로 감지하는 스크립트
/// SpineSkill에서 AddComponent로 붙여줍니다.
/// </summary>
public class SpineHitDetector : MonoBehaviour
{
    private PlayerCombat owner;
    private Transform ownerTransform;
    private GameObject spinePrefab;
    private Vector2 checkOffset;
    private float checkRadius;
    private float spineRadius;
    private int spineCount;

    private float lifeTime;
    private bool isSkillTriggered = false; // 스킬이 한 번 발동했는지 체크

    // 한 번 감지된 적은 다시 감지하지 않도록 ID 저장
    private HashSet<int> hitEnemies = new HashSet<int>();

    // 초기화 함수 (SpineSkill에서 호출)
    public void Setup(PlayerCombat owner, Transform ownerTransform, GameObject spinePrefab,
                      Vector2 checkOffset, float checkRadius, float spineRadius, int spineCount, float duration)
    {
        this.owner = owner;
        this.ownerTransform = ownerTransform;
        this.spinePrefab = spinePrefab;
        this.checkOffset = checkOffset;
        this.checkRadius = checkRadius;
        this.spineRadius = spineRadius;
        this.spineCount = spineCount;
        this.lifeTime = duration;
    }

    private void Update()
    {
        // 망치가 사라지기 전까지만 동작
        if (lifeTime <= 0) return;
        lifeTime -= Time.deltaTime;

        // 이미 스킬이 발동했다면(가시가 나갔다면) 더 이상 감지 안 함
        if (isSkillTriggered) return;

        DetectGroggyEnemy();
    }

    private void DetectGroggyEnemy()
    {
        if (ownerTransform == null) return;

        // 망치 헤드의 현재 월드 위치 계산 (플레이어 회전 반영)
        Vector2 worldCenter = ownerTransform.TransformPoint(checkOffset);

        // 범위 내의 적 검출 (Enemy 태그 사용)
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldCenter, checkRadius);

        foreach (var col in hits)
        {
            if (!col.CompareTag("Enemy")) continue;

            var enemy = col.GetComponent<EnemyController>();
            // 부모에 있을 수도 있으니 체크
            if (enemy == null) enemy = col.GetComponentInParent<EnemyController>();
            if (enemy == null) continue;

            // 이미 체크한 적이면 패스
            if (hitEnemies.Contains(enemy.GetInstanceID())) continue;
            hitEnemies.Add(enemy.GetInstanceID());

            // ★ 그로기 상태인지 확인 ★
            if (enemy.IsGroggy())
            {
                TriggerSpineSkill(enemy, col.transform.position);
                isSkillTriggered = true; // 스킬 발동 완료 플래그
                break; // 한 번에 한 명만 트리거 (원하면 제거 가능)
            }
        }
    }

    private void TriggerSpineSkill(EnemyController target, Vector3 hitPos)
    {
        // 1. 적에게 꽂혀있는 말뚝 회수 (보상 지급)
        target.ConsumeStacks(true, true, owner);

        // 2. 가시 소환
        SpawnSpines(hitPos);

        // 3. 타겟 적은 즉시 속박 (가시 생성 딜레이 보완)
        // EnemyController 수정 없이 기존 메서드 활용 (3초 정지/기절)
        target.ApplyMovementStop(3.0f, 0f);
        target.ApplyStun(3.0f);

        // 로그
        Debug.Log($"[SpineHitDetector] 그로기 적 발견! 가시 스킬 발동 -> {target.name}");
    }

    private void SpawnSpines(Vector3 centerPos)
    {
        // 중앙 가시
        if (spinePrefab != null)
            Instantiate(spinePrefab, centerPos, Quaternion.identity);

        // 주변 원형 가시
        for (int i = 0; i < spineCount; i++)
        {
            float angle = i * (360f / spineCount);
            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            Vector3 spawnPos = centerPos + dir * spineRadius;

            if (spinePrefab != null)
                Instantiate(spinePrefab, spawnPos, Quaternion.identity);
        }
    }

    // 에디터에서 감지 범위 눈으로 확인용
    private void OnDrawGizmos()
    {
        if (ownerTransform != null)
        {
            Gizmos.color = Color.cyan;
            Vector2 worldCenter = ownerTransform.TransformPoint(checkOffset);
            Gizmos.DrawWireSphere(worldCenter, checkRadius);
        }
    }
}