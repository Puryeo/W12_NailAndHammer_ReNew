using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// ProjectileConfig 테스트 에셋을 자동 생성하는 에디터 스크립트
/// 메뉴: Tools/Combat/Create Test Projectile Configs
/// </summary>
public class ProjectileConfigCreator : Editor
{
    private const string CONFIG_FOLDER = "Assets/ScriptableObjects/ProjectileConfigs";

    [MenuItem("Tools/Combat/Create Test Projectile Configs")]
    public static void CreateTestConfigs()
    {
        // 폴더 생성
        if (!AssetDatabase.IsValidFolder(CONFIG_FOLDER))
        {
            string[] folders = CONFIG_FOLDER.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = newPath;
            }
        }

        // Config 1: 기본 말뚝
        CreateConfig1_Basic();

        // Config 2: 끌어오기 회수
        CreateConfig2_Pull();

        // Config 3: 꿰뚫기 + 속박
        CreateConfig3_ImpaleBinding();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ProjectileConfigCreator] 테스트용 Config 3개 생성 완료! ({CONFIG_FOLDER})");
        EditorUtility.RevealInFinder(CONFIG_FOLDER);
    }

    private static void CreateConfig1_Basic()
    {
        var config = ScriptableObject.CreateInstance<ProjectileConfig>();

        // Basic
        config.damage = 10f;
        config.speed = 14f;
        config.lifetime = 5f;
        config.isRetrievable = true;

        // Collision: StickToEnemy
        config.collisionType = CollisionBehaviorType.StickToEnemy;
        config.canImpale = false;

        // Retrieval: Simple
        config.retrievalType = RetrievalBehaviorType.Simple;
        config.returnSpeed = 20f;
        config.returnDamageRatio = 0.5f;

        string path = $"{CONFIG_FOLDER}/TestConfig1_Basic.asset";
        AssetDatabase.CreateAsset(config, path);
        Debug.Log($"[Created] {path}");
    }

    private static void CreateConfig2_Pull()
    {
        var config = ScriptableObject.CreateInstance<ProjectileConfig>();

        // Basic
        config.damage = 12f;
        config.speed = 14f;
        config.lifetime = 5f;
        config.isRetrievable = true;

        // Collision: StickToEnemy (꿰뚫기 없음)
        config.collisionType = CollisionBehaviorType.StickToEnemy;
        config.canImpale = false;

        // Retrieval: Pull (끌어오기)
        config.retrievalType = RetrievalBehaviorType.Pull;
        config.returnSpeed = 20f;
        config.returnDamageRatio = 0.5f;
        config.pullForce = 15f;
        config.pullWallImpactDamage = 30f;

        string path = $"{CONFIG_FOLDER}/TestConfig2_Pull.asset";
        AssetDatabase.CreateAsset(config, path);
        Debug.Log($"[Created] {path}");
    }

    private static void CreateConfig3_ImpaleBinding()
    {
        var config = ScriptableObject.CreateInstance<ProjectileConfig>();

        // Basic
        config.damage = 15f;
        config.speed = 16f;
        config.lifetime = 5f;
        config.isRetrievable = true;

        // Collision: ImpaleAndCarry (꿰뚫기)
        config.collisionType = CollisionBehaviorType.ImpaleAndCarry;
        config.canImpale = true;
        config.maxImpaleCount = 5;
        config.enemySpacing = 0.3f;
        config.accelerateOnImpale = true;
        config.impaleSpeedMultiplier = 1.3f;
        config.maxImpalingDistance = 10f;

        // Wall Impact
        config.wallImpactDamage = 20f;
        config.applyStunOnWallImpact = true;
        config.wallImpactStunDuration = 2f;

        // Retrieval: Binding (속박)
        config.retrievalType = RetrievalBehaviorType.Binding;
        config.returnSpeed = 20f;
        config.returnDamageRatio = 0.5f;
        config.bindingDuration = 3f;
        config.bindingSlowAmount = 0.5f;

        string path = $"{CONFIG_FOLDER}/TestConfig3_ImpaleBinding.asset";
        AssetDatabase.CreateAsset(config, path);
        Debug.Log($"[Created] {path}");
    }

    [MenuItem("Tools/Combat/Delete Test Projectile Configs")]
    public static void DeleteTestConfigs()
    {
        string[] configPaths = new string[]
        {
            $"{CONFIG_FOLDER}/TestConfig1_Basic.asset",
            $"{CONFIG_FOLDER}/TestConfig2_Pull.asset",
            $"{CONFIG_FOLDER}/TestConfig3_ImpaleBinding.asset"
        };

        int deletedCount = 0;
        foreach (var path in configPaths)
        {
            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
                deletedCount++;
                Debug.Log($"[Deleted] {path}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ProjectileConfigCreator] 테스트 Config {deletedCount}개 삭제 완료!");
    }
}
