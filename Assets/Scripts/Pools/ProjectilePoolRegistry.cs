using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ProjectilePoolRegistry - 프로젝트 전역에서 투사체 프리팹별 공유 GameObjectPool을 관리합니다.
/// - 디버깅을 위해 생성/해제 로그(옵션)를 남기고, Spawn/Release 래퍼 제공.
/// - 풀 인스턴스는 내부 PoolRef 컴포넌트를 통해 반환을 라우팅합니다.
/// </summary>
[DisallowMultipleComponent]
public class ProjectilePoolRegistry : MonoBehaviour
{
    public static ProjectilePoolRegistry Instance { get; private set; }

    [Tooltip("프리팹별 기본 풀 크기")]
    public int defaultPoolSize = 8;

    [Tooltip("풀 오브젝트들을 부모로 둘 Transform (비워두면 이 오브젝트가 부모가 됩니다)")]
    public Transform poolRoot;

    [Tooltip("로그 출력 여부 (생성/반환 등)")]
    public bool showDebugLogs = false;

    // prefab -> pool
    private readonly Dictionary<GameObject, GameObjectPool> pools = new Dictionary<GameObject, GameObjectPool>(ReferenceEqualityComparer.Instance);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (poolRoot == null)
        {
            poolRoot = this.transform;
        }
    }

    /// <summary>
    /// 지정한 prefab에 대한 GameObjectPool을 반환(없으면 생성).
    /// initialSize > 0이면 그 값으로 생성, 아니면 defaultPoolSize 사용.
    /// </summary>
    public GameObjectPool GetPool(GameObject prefab, int initialSize = -1)
    {
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));

        if (pools.TryGetValue(prefab, out var existing))
            return existing;

        int size = (initialSize > 0) ? initialSize : Mathf.Max(1, defaultPoolSize);
        var parent = poolRoot ?? this.transform;

        var pool = new GameObjectPool(prefab, size, parent);
        pools[prefab] = pool;

        if (showDebugLogs) Debug.Log($"ProjectilePoolRegistry: Created pool for '{prefab.name}' size={size}");
        return pool;
    }

    /// <summary>
    /// 간편 생성: prefab에 대응하는 풀에서 인스턴스 획득하고, PoolRef를 설정하여 이후 Release 호출이 가능하도록 합니다.
    /// tint가 지정되면 SpriteRenderer(있으면) 색상을 설정합니다.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, int initialSize = -1, Color? tint = null)
    {
        if (prefab == null) return null;
        var pool = GetPool(prefab, initialSize);
        var go = pool.Get(position, rotation);
        if (go == null) return null;

        // Attach or set PoolRef for safe Release routing
        var pr = go.GetComponent<PoolRef>();
        if (pr == null) pr = go.AddComponent<PoolRef>();
        pr.pool = pool;

        // Apply tint if possible (SpriteRenderer on root or children)
        if (tint.HasValue)
        {
            var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                if (sr != null) sr.color = tint.Value;
            }
        }

        if (showDebugLogs) Debug.Log($"ProjectilePoolRegistry: Spawn '{prefab.name}' -> id={go.GetInstanceID()} pos={position}");
        return go;
    }

    /// <summary>
    /// 안전한 반환: PoolRef가 있으면 해당 풀에 반환, 없으면 Destroy.
    /// </summary>
    public void Release(GameObject go)
    {
        if (go == null) return;
        var pr = go.GetComponent<PoolRef>();
        if (pr != null && pr.pool != null)
        {
            try
            {
                pr.pool.Release(go);
                if (showDebugLogs) Debug.Log($"ProjectilePoolRegistry: Released id={go.GetInstanceID()}");
                return;
            }
            catch (Exception ex)
            {
                if (showDebugLogs) Debug.LogWarning($"ProjectilePoolRegistry: Release failed -> {ex.Message}");
            }
        }

        // Fallback
        Destroy(go);
        if (showDebugLogs) Debug.Log($"ProjectilePoolRegistry: Destroyed id={go.GetInstanceID()} (no pool)");
    }

    /// <summary>
    /// Pool 소유를 표기하는 내부 컴포넌트 (풀 반환 라우팅용).
    /// </summary>
    private class PoolRef : MonoBehaviour
    {
        public GameObjectPool pool;
    }

    /// <summary>
    /// Reference equality comparer for GameObject dictionary keys (ensure prefab identity).
    /// </summary>
    private class ReferenceEqualityComparer : IEqualityComparer<GameObject>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
        public bool Equals(GameObject x, GameObject y) => ReferenceEquals(x, y);
        public int GetHashCode(GameObject obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}