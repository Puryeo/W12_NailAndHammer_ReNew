using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 간단한 GameObject 풀 구현
/// </summary>
public class GameObjectPool
{
    private readonly GameObject prefab;
    private readonly Queue<GameObject> queue = new Queue<GameObject>();
    private readonly Transform parent;
    private readonly Vector3 prefabLocalScale;

    public GameObjectPool(GameObject prefab, int initialSize = 8, Transform parent = null)
    {
        this.prefab = prefab;
        this.parent = parent;
        this.prefabLocalScale = prefab != null ? prefab.transform.localScale : Vector3.one;

        for (int i = 0; i < initialSize; i++)
        {
            var go = CreateNew();
            go.SetActive(false);
            queue.Enqueue(go);
        }
    }

    private GameObject CreateNew()
    {
        var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        go.SetActive(false);
        return go;
    }

    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject go;
        if (queue.Count > 0)
        {
            go = queue.Dequeue();
            if (go == null)
            {
                go = CreateNew();
            }
        }
        else
        {
            go = CreateNew();
        }

        if (parent != null)
            go.transform.SetParent(parent, false);
        else
            go.transform.SetParent(null);

        go.transform.localScale = prefabLocalScale;
        go.transform.SetPositionAndRotation(position, rotation);

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        var col = go.GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = true;
            col.isTrigger = true;
        }

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.white;
        }

        // 풀 오브젝트에 남아 있을 수 있는 attacker/상태 초기화
        var ap = go.GetComponent<AttackProjectile>();
        if (ap != null)
        {
            ap.ClearAttacker();
        }

        go.SetActive(true);
        return go;
    }

    // Release 메서드 안전 처리: Rigidbody가 Static이면 linear/angular 세팅을 건너뜁니다.
    public void Release(GameObject go)
    {
        if (go == null) return;

        if (parent != null)
            go.transform.SetParent(parent, false);
        else
            go.transform.SetParent(null);

        go.transform.localScale = prefabLocalScale;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localPosition = Vector3.zero;

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Static 바디에 대해 linear/angular 접근을 시도하면 Unity 에러 발생 -> 체크
            if (rb.bodyType != RigidbodyType2D.Static)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        var col = go.GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = true;
            col.isTrigger = true;
        }

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.white;
        }

        // AttackProjectile 소유자 초기화 (중요)
        var ap = go.GetComponent<AttackProjectile>();
        if (ap != null)
        {
            ap.ClearAttacker();
        }

        go.SetActive(false);
        queue.Enqueue(go);
    }
}