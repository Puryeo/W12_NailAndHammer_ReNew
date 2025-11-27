using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject normalEnemyPrefab; // 일반 적
    [SerializeField] private GameObject strongEnemyPrefab; // 강한 적

    [Header("Spawn Settings")]
    [SerializeField] private int maxNormalEnemies = 10;
    [SerializeField] private int maxStrongEnemies = 3;

    [SerializeField] private float normalSpawnCooldown = 2f; // 일반 적 스폰 쿨타임
    [SerializeField] private float strongSpawnCooldown = 5f; // 강한 적 스폰 쿨타임

    [Header("Spawn Area (2D)")]
    [SerializeField] private Vector2 spawnAreaCenter = Vector2.zero; // 스폰 구역 중심
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(10f, 5f); // 스폰 구역 크기

    [SerializeField] private Transform enemiesParent; // 생성된 적을 담을 컨테이너 (인스펙터에 할당하면 편함)

    private float normalSpawnTimer = 0f;
    private float strongSpawnTimer = 0f;

    private int currentNormalEnemies = 0;
    private int currentStrongEnemies = 0;

    void Update()
    {
        normalSpawnTimer += Time.deltaTime;
        strongSpawnTimer += Time.deltaTime;

        // 일반 적 스폰
        if (normalSpawnTimer >= normalSpawnCooldown && currentNormalEnemies < maxNormalEnemies)
        {
            SpawnEnemy(normalEnemyPrefab);
            currentNormalEnemies++;
            normalSpawnTimer = 0f;
        }

        // 강한 적 스폰
        if (strongSpawnTimer >= strongSpawnCooldown && currentStrongEnemies < maxStrongEnemies)
        {
            SpawnEnemy(strongEnemyPrefab);
            currentStrongEnemies++;
            strongSpawnTimer = 0f;
        }
    }

    private void Awake()
    {
        // spawnAreaCenter가 (0,0)일 때 스포너 위치를 기본으로 사용하도록 안전화
        if (spawnAreaSize == Vector2.zero) spawnAreaSize = new Vector2(1f, 1f);
        spawnAreaCenter = transform.position;
        if (enemiesParent == null)
        {
            GameObject go = new GameObject("Enemies");
            enemiesParent = go.transform;
        }
    }

    private void SpawnEnemy(GameObject enemyPrefab)
    {
        // 2D용 랜덤 위치 (x, y)
        Vector2 randomPosition = new Vector2(
            Random.Range(spawnAreaCenter.x - spawnAreaSize.x / 2, spawnAreaCenter.x + spawnAreaSize.x / 2),
            Random.Range(spawnAreaCenter.y - spawnAreaSize.y / 2, spawnAreaCenter.y + spawnAreaSize.y / 2)
        );

        Instantiate(enemyPrefab, randomPosition, Quaternion.identity, enemiesParent);
    }

    // 적이 죽을 때 호출할 수 있도록
    public void OnEnemyDestroyed(bool isStrongEnemy)
    {
        if (isStrongEnemy)
            currentStrongEnemies--;
        else
            currentNormalEnemies--;
    }

    // 씬 뷰에서 스폰 구역을 시각적으로 보기 쉽게
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
    }
}
