using UnityEngine;

/// <summary>
/// 투사체 회수 시 LineRenderer 그리기를 위한 유틸리티 클래스
/// 모든 Retrieval Behavior에서 공통으로 사용 가능
/// </summary>
public class ProjectileLineRendererUtil
{
    /// <summary>
    /// LineRenderer와 EndDecorator를 생성하고 초기화합니다.
    /// </summary>
    /// <param name="projectile">투사체</param>
    /// <param name="config">ProjectileConfig</param>
    /// <param name="outLineRenderer">생성된 LineRenderer (출력)</param>
    /// <param name="outDecorator">생성된 EndDecorator (출력, null 가능)</param>
    public static void CreateLineRenderer(
        AttackProjectile projectile,
        ProjectileConfig config,
        out LineRenderer outLineRenderer,
        out GameObject outDecorator)
    {
        outLineRenderer = null;
        outDecorator = null;

        if (!config.enableLineRenderer)
            return;

        GameObject lrGo = new GameObject($"LineRenderer_{projectile.name}");
        lrGo.transform.SetParent(projectile.transform);
        lrGo.transform.localPosition = Vector3.zero;

        // LineRenderer 생성
        if (config.linePrefab != null)
        {
            // 프리팹이 있으면 Instantiate
            var linePrefabInstance = Object.Instantiate(config.linePrefab, lrGo.transform);
            outLineRenderer = linePrefabInstance.GetComponent<LineRenderer>();

            if (outLineRenderer != null)
            {
                outLineRenderer.positionCount = 2;
                outLineRenderer.useWorldSpace = true;
            }
            else
            {
                Debug.LogWarning($"ProjectileLineRendererUtil: linePrefab에 LineRenderer가 없습니다. 기본 LineRenderer 생성.");
                outLineRenderer = CreateDefaultLineRenderer(lrGo);
            }
        }
        else
        {
            // 프리팹이 없으면 기본 LineRenderer 생성
            outLineRenderer = CreateDefaultLineRenderer(lrGo);
        }

        // EndDecorator 생성
        if (config.endDecoratorPrefab != null)
        {
            Vector3 projectilePos = projectile.transform.position;
            float angleDeg = Mathf.Atan2(
                projectilePos.y - projectile.Attacker.position.y,
                projectilePos.x - projectile.Attacker.position.x
            ) * Mathf.Rad2Deg;

            outDecorator = Object.Instantiate(
                config.endDecoratorPrefab,
                projectilePos,
                Quaternion.Euler(0f, 0f, angleDeg)
            );
        }
    }

    /// <summary>
    /// 기본 LineRenderer를 생성합니다.
    /// </summary>
    private static LineRenderer CreateDefaultLineRenderer(GameObject parent)
    {
        var lr = parent.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.cyan;
        lr.endColor = Color.white;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.sortingOrder = 1000;
        return lr;
    }

    /// <summary>
    /// LineRenderer의 위치를 업데이트합니다.
    /// </summary>
    /// <param name="lineRenderer">LineRenderer</param>
    /// <param name="startPos">시작 위치 (플레이어)</param>
    /// <param name="endPos">끝 위치 (투사체)</param>
    public static void UpdateLineRenderer(LineRenderer lineRenderer, Vector3 startPos, Vector3 endPos)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);
    }

    /// <summary>
    /// EndDecorator의 위치를 업데이트합니다.
    /// </summary>
    /// <param name="decorator">EndDecorator GameObject</param>
    /// <param name="position">위치</param>
    public static void UpdateDecorator(GameObject decorator, Vector3 position)
    {
        if (decorator != null)
        {
            decorator.transform.position = position;
        }
    }

    /// <summary>
    /// LineRenderer와 EndDecorator를 정리합니다.
    /// </summary>
    /// <param name="lineRenderer">LineRenderer</param>
    /// <param name="decorator">EndDecorator GameObject</param>
    public static void Cleanup(LineRenderer lineRenderer, GameObject decorator)
    {
        if (lineRenderer != null)
        {
            Object.Destroy(lineRenderer.gameObject);
        }

        if (decorator != null)
        {
            Object.Destroy(decorator);
        }
    }

    /// <summary>
    /// 히트 피드백을 적용합니다 (히트스톱, 카메라 셰이크).
    /// </summary>
    /// <param name="config">ProjectileConfig</param>
    public static void ApplyHitFeedback(ProjectileConfig config)
    {
        // 히트스톱 적용
        if (config.hitStop != EHitStopStrength.None)
        {
            var hitStopManager = HitStopManager.Instance;
            if (hitStopManager != null)
            {
                switch (config.hitStop)
                {
                    case EHitStopStrength.Weak:
                        hitStopManager.StopWeak();
                        break;
                    case EHitStopStrength.Medium:
                        hitStopManager.StopMedium();
                        break;
                    case EHitStopStrength.Strong:
                        hitStopManager.StopStrong();
                        break;
                }
            }
        }

        // 카메라 셰이크 적용
        if (config.shake != EShakeStrength.None)
        {
            var cameraShake = Object.FindFirstObjectByType<CameraShake>();
            if (cameraShake != null)
            {
                switch (config.shake)
                {
                    case EShakeStrength.Weak:
                        cameraShake.ShakeWeak();
                        break;
                    case EShakeStrength.Medium:
                        cameraShake.ShakeMedium();
                        break;
                    case EShakeStrength.Strong:
                        cameraShake.ShakeStrong();
                        break;
                }
            }
        }
    }
}
