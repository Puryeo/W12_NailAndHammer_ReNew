using System.Collections;
using UnityEngine;

/// <summary>
/// PlayerController (뱀파이어 헌터 Ver.)
/// 역할: WASD 이동, 마우스 회전, 대시(Dash), 그리고 플레이어의 입력(Input)을 받아 Combat 스크립트로 전달
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Dash Settings")]
    [Tooltip("대시 거리")]
    [SerializeField] private float dashDistance = 4f;
    [Tooltip("대시 속도 배수 (기본 이동 속도 대비)")]
    [SerializeField] private float dashSpeedMultiplier = 4f;
    [Tooltip("대시 쿨타임")]
    [SerializeField] private float dashCooldown = 0.8f;
    [Tooltip("대시 중 무적 판정 여부")]
    [SerializeField] private bool isInvulnerableWhileDashing = true;

    [Header("Visual Settings")]
    [Tooltip("마우스 방향에 따라 스프라이트 좌우 반전")]
    [SerializeField] private bool useFlipX = true;
    [SerializeField] private bool showDirectionIndicator = true;

    [Header("Dash Visual")]
    [SerializeField] private bool useDashTrail = true;
    [SerializeField] private float dashTrailTime = 0.2f;
    [SerializeField] private Color dashTrailColor = Color.white;

    // 컴포넌트 참조
    private Rigidbody2D rb2D;
    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private TrailRenderer dashTrail;

    // 전투 컴포넌트 참조
    private PlayerCombat playerCombat;

    // 상태 변수
    private Vector2 moveInput;
    private Vector2 lookDirection = Vector2.right;
    private bool isDashing = false;
    private float dashCooldownTimer = 0f;

    // rotation: Update에서 계산하여 FixedUpdate에서 물리 회전 적용
    private float desiredAngle = 0f;

    // 방향 인디케이터
    private GameObject directionIndicator;

    private void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        // 권장: Rigidbody2D 보간 강제 적용(이미 세팅되어 있어도 안전)
        if (rb2D != null) rb2D.interpolation = RigidbodyInterpolation2D.Interpolate;

        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;

        // 전투 컴포넌트 가져오기
        playerCombat = GetComponent<PlayerCombat>();

        // 대시 트레일 설정
        if (useDashTrail)
        {
            SetupDashTrail();
        }

        // 방향 인디케이터 설정
        if (showDirectionIndicator)
        {
            CreateDirectionIndicator();
        }
    }

    private void Update()
    {
        // 1. 쿨타임 갱신
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        // 2. 입력 처리 (대시 중에는 이동 입력만 차단, 조준은 가능)
        HandleInput();

        // 3. 시각적 요소 갱신
        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        // 4. 물리 이동 (대시 중이 아닐 때만)
        if (!isDashing)
        {
            Move();
        }

        // 물리 스텝에서 회전 적용: transform 직접 변경 대신 Rigidbody2D.MoveRotation으로 보간된 회전 보장
        if (rb2D != null)
        {
            // MoveRotation expects degrees for Rigidbody2D
            rb2D.MoveRotation(desiredAngle);
        }
    }

    /// <summary>
    /// 사용자 입력 처리
    /// - LMB down/up -> PlayerCombat 차지/발사 API 호출
    /// - RMB down -> 망치 휘두르기
    /// </summary>
    private void HandleInput()
    {
        // 이동 입력 (WASD)
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(x, y).normalized;

        // 마우스 조준 (항상 갱신) — 회전은 Update에서 계산만 하고 FixedUpdate에서 적용
        AimAtMouse();

        // --- 전투 입력 매핑 (기획서 기준) ---

        // 좌클릭: 누름/뗌 처리 (차지와 탭 구분은 PlayerCombat에서)
        if (Input.GetMouseButtonDown(0))
        {
            if (playerCombat != null) playerCombat.OnPrimaryDown();
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (playerCombat != null) playerCombat.OnPrimaryUp();
        }

        // Shift + 좌클릭 (또는 별도 키): 피 말뚝 사격 (Gambit)
        if (Input.GetKeyDown(KeyCode.LeftShift) && Input.GetMouseButtonDown(0))
        {
            if (playerCombat != null) playerCombat.TryFireBloodStake();
        }

        // 우클릭 (또는 E): 망치 처형
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E))
        {
            if (playerCombat != null) playerCombat.TrySwingHammer();
        }

        // R키: 사슬 회수 (기획 변경으로 사용하지 않는 경우 주석 처리 가능)
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (playerCombat != null) playerCombat.TryRetrieveStake();
        }

        // Space: 대시
        if (Input.GetKeyDown(KeyCode.Space) && !isDashing && dashCooldownTimer <= 0)
        {
            StartCoroutine(DashRoutine());
        }
    }

    /// <summary>
    /// 물리 이동 처리
    /// </summary>
    private void Move()
    {
        if (rb2D != null)
            rb2D.linearVelocity = moveInput * moveSpeed;
    }

    /// <summary>
    /// 마우스 방향 바라보기
    /// - Update에서 각도만 계산하고, 실제 회전은 FixedUpdate에서 Rigidbody2D.MoveRotation으로 적용.
    ///   이렇게 하면 물리 보간과 일관되어 카메라/플레이어 간 jitter가 줄어듭니다.
    /// </summary>
    private void AimAtMouse()
    {
        if (mainCamera == null) return;

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(mainCamera.transform.position.z);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);

        Vector2 dir = (worldPos - transform.position);
        if (dir.sqrMagnitude > 0.0001f)
        {
            lookDirection = dir.normalized;

            // 원하는 회전 각도만 계산 (transform.rotation 직접 변경 금지)
            desiredAngle = Mathf.Atan2(lookDirection.y, lookDirection.x) * Mathf.Rad2Deg;

            // 기존 좌우 반전은 스프라이트에 따라 유지(선택)
            if (useFlipX && spriteRenderer != null)
            {
                spriteRenderer.flipX = (worldPos.x < transform.position.x);
            }
        }
    }

    /// <summary>
    /// 대시 코루틴
    /// </summary>
    private IEnumerator DashRoutine()
    {
        isDashing = true;
        dashCooldownTimer = dashCooldown;

        Vector2 dashDir = moveInput.sqrMagnitude > 0 ? moveInput : lookDirection;

        if (dashTrail != null) dashTrail.emitting = true;

        float duration = dashDistance / (moveSpeed * dashSpeedMultiplier);
        float startTime = Time.time;

        while (Time.time < startTime + duration)
        {
            rb2D.linearVelocity = dashDir * (moveSpeed * dashSpeedMultiplier);
            yield return null;
        }

        rb2D.linearVelocity = Vector2.zero;
        if (dashTrail != null) dashTrail.emitting = false;

        isDashing = false;
    }

    private void UpdateVisuals()
    {
        if (directionIndicator != null)
        {
            float angle = desiredAngle;
            directionIndicator.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
            directionIndicator.transform.position = transform.position + (Vector3)lookDirection * 1.0f;
            directionIndicator.SetActive(showDirectionIndicator);
        }
    }

    private void SetupDashTrail()
    {
        dashTrail = GetComponent<TrailRenderer>();
        if (dashTrail == null) dashTrail = gameObject.AddComponent<TrailRenderer>();

        dashTrail.material = new Material(Shader.Find("Sprites/Default"));
        dashTrail.time = dashTrailTime;
        dashTrail.startWidth = 0.5f;
        dashTrail.endWidth = 0f;
        dashTrail.startColor = new Color(1, 1, 1, 0.5f);
        dashTrail.endColor = new Color(1, 1, 1, 0f);
        dashTrail.emitting = false;
        dashTrail.sortingOrder = -1;
    }

    private void CreateDirectionIndicator()
    {
        directionIndicator = new GameObject("DirectionIndicator");
        directionIndicator.transform.SetParent(transform);

        LineRenderer lr = directionIndicator.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.positionCount = 3;
        lr.SetPositions(new Vector3[] { new Vector3(-0.2f, -0.2f, 0), new Vector3(0, 0.3f, 0), new Vector3(0.2f, -0.2f, 0) });
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.yellow;
        lr.endColor = Color.yellow;

        directionIndicator.SetActive(false);
    }
}