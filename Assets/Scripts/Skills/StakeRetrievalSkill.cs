using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StakeRetrievalSkill : MonoBehaviour
{
    [Header("Retrieval Parameters")]
    [SerializeField] private float pullDistance = 0.6f;
    [SerializeField] private float hitStopSeconds = 0.1f;
    [SerializeField] private float moveToPlayerDuration = 0.25f;
    [Tooltip("각 말뚝 애니메이션 시작 사이 간격 (0 = 동시에)")]
    [SerializeField] private float spawnInterval = 0.04f;

    [Header("Visuals")]
    [Tooltip("선 렌더러 프리팹 (옵션)")]
    [SerializeField] private LineRenderer linePrefab;
    [Tooltip("끝 장식(끝자락에 표시할 프리팹, 옵션)")]
    [SerializeField] private GameObject endDecoratorPrefab;

    [Header("Hit Feedback")]
    [SerializeField] private global::EHitStopStrength hitStop = global::EHitStopStrength.Weak;
    [SerializeField] private global::EShakeStrength shake = global::EShakeStrength.Weak;

    [Header("Cooldown")]
    [Tooltip("R키 회수 스킬의 쿨타임 (초)")]
    [SerializeField] private float retrievalCooldown = 1.5f; // 인스펙터에서 조정 가능
    private float cooldownTimer = 0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // 적 시각화 파라미터
    [SerializeField] private float enemyVisualPullAmount = 0.8f; // 시각적으로 당길 거리
    [SerializeField] private float enemyVisualDuration = 0.6f; // 한 말뚝당 연출 시간

    public void Activate()
    {
        // 쿨다운 중이면 동작 차단
        if (cooldownTimer > 0f)
        {
            if (showDebugLogs) Debug.Log($"StakeRetrievalSkill: on cooldown {cooldownTimer:F2}s");
            return;
        }

        StartCoroutine(RunRetrieval());
        // 스킬 사용 직후 쿨다운 시작
        cooldownTimer = Mathf.Max(0f, retrievalCooldown);
        if (showDebugLogs) Debug.Log($"StakeRetrievalSkill: Activated, cooldown set to {cooldownTimer:F2}s");
    }

    private void Update()
    {
        // 쿨다운 타이머 감소
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                cooldownTimer = 0f;
                if (showDebugLogs) Debug.Log("StakeRetrievalSkill: cooldown finished");
            }
        }
    }

    // 쿨다운 상태 조회 API (UI에서 사용 가능)
    public bool IsOnCooldown() => cooldownTimer > 0f;
    public float GetCooldownRemaining() => cooldownTimer;

    private IEnumerator RunRetrieval()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning("StakeRetrievalSkill: Player not found (tag='Player')");
            yield break;
        }
        Transform playerT = playerObj.transform;
        PlayerCombat playerCombat = playerObj.GetComponent<PlayerCombat>();

        var all = FindObjectsOfType<AttackProjectile>();
        if (all == null || all.Length == 0)
        {
            if (showDebugLogs) Debug.Log("StakeRetrievalSkill: No AttackProjectile found in scene");
            yield break;
        }

        // 그룹화: 적에 붙은 말뚝 목록과 벽(또는 호스트 없음) 리스트
        var enemyMap = new Dictionary<EnemyController, List<AttackProjectile>>();
        var wallStakes = new List<AttackProjectile>();

        foreach (var stake in all)
        {
            if (stake == null) continue;
            var host = stake.GetHostEnemy();
            if (host != null)
            {
                if (!enemyMap.TryGetValue(host, out var list)) { list = new List<AttackProjectile>(); enemyMap[host] = list; }
                list.Add(stake);
            }
            else
            {
                wallStakes.Add(stake);
            }
        }

        if (showDebugLogs) Debug.Log($"StakeRetrievalSkill: {enemyMap.Count} enemies with stakes, {wallStakes.Count} wall-stakes");

        int visualCount = 0;
        int immediateConsumed = 0;

        // 1) 적에 붙은 말뚝: 원본을 즉시 제거하고 위치 스냅샷으로 비주얼만 재생
        foreach (var kv in enemyMap)
        {
            var stakes = kv.Value;
            foreach (var stake in stakes)
            {
                if (stake == null) continue;

                // 스냅샷 위치/회전(방향)
                Vector3 snapPos = stake.transform.position;
                Vector3 snapDir = (playerT.position - snapPos).normalized;
                if (snapDir == Vector3.zero) snapDir = Vector3.right;
                float snapAngle = Mathf.Atan2(snapDir.y, snapDir.x) * Mathf.Rad2Deg;

                // 1-a) 원본 말뚝 안전 제거: Enemy의 리스트에서 제거 + ForceDetach + 풀 반환(or Destroy)
                var host = stake.GetHostEnemy();
                if (host != null)
                {
                    try
                    {
                        // Enemy에 등록된 리스트에서 제거
                        host.RemoveStuckProjectile(stake);
                    }
                    catch { }

                }

                try
                {
                    stake.ForceDetachFromHost();
                }
                catch { }

                try
                {
                    if (AttackManager.Instance != null)
                        AttackManager.Instance.ReleaseStake(stake.gameObject);
                    else
                        Destroy(stake.gameObject);
                }
                catch
                {
                    try { Destroy(stake.gameObject); } catch { }
                }

                immediateConsumed++;

                // 1-b) 비주얼 재생 (스냅샷 포지션 기준)
                visualCount++;
                if (showDebugLogs) Debug.Log($"StakeRetrievalSkill: start visual (snapshot) for stake {stake.name} on enemy {kv.Key.name}");
                StartCoroutine(VisualPullRoutineSnapshot(snapPos, snapAngle, playerT, enemyVisualPullAmount, hitStopSeconds, enemyVisualDuration, linePrefab, endDecoratorPrefab));
                if (spawnInterval > 0f) yield return new WaitForSeconds(spawnInterval);
            }
        }

        // 2) 벽에 박힌 말뚝은 기존 RetrievalAnimationRoutine 호출 (말뚝 내부 로직에서 보상/풀 처리)
        foreach (var ws in wallStakes)
        {
            if (ws == null) continue;
            try
            {
                StartCoroutine(
                    ws.RetrievalAnimationRoutine(
                        collector: playerT,
                        pullDistance: pullDistance,
                        hitStopSeconds: hitStopSeconds,
                        moveToPlayerDuration: moveToPlayerDuration,
                        linePrefab: linePrefab,
                        hitStop: hitStop,
                        shake: shake,
                        endDecorator: endDecoratorPrefab // end decorator 전달
                    )
                );
                if (showDebugLogs) Debug.Log($"StakeRetrievalSkill: started wall retrieval for {ws.name}");
            }
            catch (System.Exception ex)
            {
                if (showDebugLogs) Debug.LogWarning($"StakeRetrievalSkill: failed to start retrieval routine on wall-stake {ws.name} -> {ex.Message}");
            }
            if (spawnInterval > 0f) yield return new WaitForSeconds(spawnInterval);
        }

        // 3) 대기: 비주얼이 끝날 시간을 대략 대기
        float waitTime = Mathf.Max(enemyVisualDuration, moveToPlayerDuration) + 0.1f + spawnInterval * visualCount;
        if (showDebugLogs) Debug.Log($"StakeRetrievalSkill: waiting {waitTime:F2}s for visuals to finish");
        yield return new WaitForSeconds(waitTime);

        // 4) 즉시 제거한 말뚝 수 만큼 플레이어에게 보상
        if (immediateConsumed > 0)
        {
            if (playerCombat != null)
            {
                playerCombat.RecoverAmmo(immediateConsumed);
                if (showDebugLogs) Debug.Log($"StakeRetrievalSkill: Awarded {immediateConsumed} ammo to {playerCombat.name} (immediate destroyed stakes)");
            }
            else
            {
                if (showDebugLogs) Debug.LogWarning("StakeRetrievalSkill: PlayerCombat not found - cannot award ammo for immediate stakes");
            }
        }

        if (showDebugLogs) Debug.Log("StakeRetrievalSkill: RunRetrieval finished");
        yield break;
    }

    // 스냅샷 기반 비주얼 루틴: stake 오리지널이 제거되어도 스냅샷으로만 연출
    private IEnumerator VisualPullRoutineSnapshot(
        Vector3 snapshotPos,
        float snapshotAngleDeg,
        Transform collector,
        float pullAmount,
        float hitStopSeconds,
        float totalDuration,
        LineRenderer linePrefabInstance,
        GameObject endDecorator)
    {
        if (collector == null) yield break;

        // line 생성 (Skill 소유)
        GameObject lrGo = null;
        LineRenderer lr = null;
        if (linePrefabInstance != null)
        {
            lr = Instantiate(linePrefabInstance, transform);
            lrGo = lr.gameObject;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
        }
        else
        {
            lrGo = new GameObject($"RetrievalLine_Snap");
            lr = lrGo.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.positionCount = 2;
            lr.startWidth = lr.endWidth = 0.05f;
            lr.numCapVertices = 4;
            lr.useWorldSpace = true;
            lr.sortingOrder = 1000;
        }

        // end decorator (스냅샷 위치/회전)
        GameObject endDec = null;
        if (endDecorator != null)
        {
            endDec = Instantiate(endDecorator, snapshotPos, Quaternion.Euler(0f, 0f, snapshotAngleDeg));
        }

        // 초기 세팅
        lr.SetPosition(0, collector.position);
        lr.SetPosition(1, snapshotPos);
        if (endDec != null) endDec.transform.position = snapshotPos;

        // 1) 앞당김 연출 (시각용)
        float t = 0f;
        float stepDur = totalDuration * 0.45f;
        Vector3 pullPos = snapshotPos + ((collector.position - snapshotPos).normalized * Mathf.Min(pullAmount, (collector.position - snapshotPos).magnitude * 0.5f));

        while (t < stepDur)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / stepDur);
            Vector3 pos = Vector3.Lerp(snapshotPos, pullPos, f);

            lr.SetPosition(0, collector.position);
            lr.SetPosition(1, pos);
            if (endDec != null) endDec.transform.position = pos;
            yield return null;
        }

        // 2) 히트 이펙트 (시각)
        try { HitEffectManager.PlayHitEffect(EHitSource.Stake, hitStop, shake, pullPos); } catch { }
        if (hitStopSeconds > 0f) yield return new WaitForSecondsRealtime(hitStopSeconds);

        // 3) 흡수(시각)
        t = 0f;
        float moveDur = totalDuration * 0.55f;
        Vector3 start = pullPos;
        Vector3 end = collector.position;
        while (t < moveDur)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / moveDur);
            Vector3 pos = Vector3.Lerp(start, end, f);

            lr.SetPosition(0, collector.position);
            lr.SetPosition(1, pos);
            if (endDec != null) endDec.transform.position = pos;
            yield return null;
        }

        // 정리
        if (lrGo != null) Destroy(lrGo);
        if (endDec != null) Destroy(endDec);
        yield break;
    }
}