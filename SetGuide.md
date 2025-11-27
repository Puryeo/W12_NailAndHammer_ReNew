# Scene Setup Guide (A → Z) — UI(Canvas) + Aiming 기능 포함

간단명료, 단계별 씬 구성 가이드입니다. 2D Top-down 기준이며 기존 스크립트(AttackManager, PlayerController, PlayerCombat, AttackProjectile, HealthSystem, AimingUI 등)가 존재한다고 가정합니다.

목표 요약
- 플레이어/적/말뚝 풀링/카메라/회수 흐름을 동작시키고, 화면용 Canvas와 AimingUI(조준경 + 점선)를 함께 세팅합니다.

사전 준비
- Unity 에디터 열기, Project 뷰와 Inspector 준비
- 모든 스크립트 컴파일 성공 확인

1) 태그 / 레이어
- Tags: Player, Enemy, Wall
- Layers: (선택) ProjectileLayer, Environment 등
- __Edit > Project Settings > Physics 2D__ 에서 충돌 매트릭스 확인

2) AttackManager
- 빈 GameObject 생성: `AttackManager` (씬 당 1개)
- 컴포넌트: AttackManager 스크립트 추가
- Inspector: stakePrefab(Assets/Prefabs/Stake) 연결, pool size 등 설정

3) Stake Prefab (말뚝)
- Prefab: `StakePrefab` 생성
  - SpriteRenderer, Rigidbody2D (GravityScale=0), Collider2D, AttackProjectile 컴포넌트
  - Collider는 초기 isTrigger true로 관리될 수 있으므로 Prefab에서는 보통 일반 Collider로 두고 초기화 시 설정해도 됨
- Assets/Prefabs/Stake.prefab로 저장
- AttackManager.stakePrefab에 연결

4) Player 세팅
- GameObject: `Player`
  - Tag = Player
  - 컴포넌트: SpriteRenderer, Rigidbody2D(BodyType=Dynamic, GravityScale=0), Collider2D
  - 스크립트: PlayerController, PlayerCombat, HealthSystem
  - PlayerCombat 인스펙터: maxAmmo, chargeTimeRequired, retrieveRange, projectileLayer 설정
  - HealthSystem: maxHealth, autoDieOnZeroHealth=false(차지샷은 ForceDie로 즉사)

5) Enemy prefab
- Tag = Enemy
- 컴포넌트: SpriteRenderer, Rigidbody2D, Collider2D, EnemyController, EnemyCombat, HealthSystem
- Prefab 저장

6) 월드(벽)
- Wall 오브젝트: Tag = Wall, Collider2D 추가

7) 카메라
- Main Camera: TopDownCamera(있다면), CameraShake 컴포넌트 추가
- Camera의 Projection = Orthographic 권장

8) Canvas + EventSystem (UI)
- Canvas 생성: GameObject → UI → Canvas
  - __Canvas.renderMode__:
    - 권장: __Screen Space - Overlay__ (간단). Screen Space - Camera 사용 시 Main Camera 할당.
  - Canvas 컴포넌트:
    - Render Mode: Screen Space - Overlay (또는 Screen Space - Camera)
    - Pixel Perfect: 필요 시 활성
  - Canvas Scaler:
    - UI Scale Mode: Scale With Screen Size
    - Reference Resolution: 1920 x 1080 (프로젝트 해상도에 맞게)
    - Screen Match Mode: Match Width Or Height (0.5 권장)
  - Graphic Raycaster: 기본 포함
- EventSystem 생성(없으면 자동 생성). UI 입력(클릭) 필요 시 필수.

9) AimingUI 구성 (Canvas 하위)
- 빈 UI GameObject 생성: `AimingUI` (Canvas의 자식)
  - 컴포넌트: AimingUI 스크립트 추가
- Reticule (조준경)
  - UI → Image 생성: 이름 `Reticule`
  - RectTransform: Anchor = MiddleCenter, Size 적당(예: 32x32)
  - Sprite: 조준 이미지를 할당
  - Reticule은 Canvas의 자식이고 AimingUI.reticuleRect에 연결
  - Reticule 오브젝트의 Canvas Renderer/Sorting: Canvas 계층에서 렌더링됨
- Dotted Line (월드-공간 라인)
  - AimingUI 게임오브젝트에 LineRenderer 컴포넌트 추가(또는 별도 GameObject)
  - LineRenderer 설정:
    - Material: Sprites/Default (또는 점선용 텍스처)
    - textureMode = Tile
    - positionCount = 2
    - startWidth/endWidth: 0.03~0.08
    - sortingLayerName = "UI", sortingOrder = 100 (항상 위)
  - AimingUI.dottedLineRenderer에 연결
- AimingUI 설정:
  - playerTransform: Player 오브젝트의 Transform 할당(또는 자동 탐색 허용)
  - showReticule / showDottedLine 옵션 확인

10) AimingUI - 주요 동작 포인트
- Reticule 위치: ScreenPoint → Canvas local 좌표로 변환 (RectTransformUtility 사용). AimingUI.GetMouseWorldPosition2D 제공.
- 점선: LineRenderer는 월드 좌표를 사용. 시작점 = playerTransform.position, 끝점 = Camera.ScreenToWorldPoint(mouse).
- Main Camera가 null이면 AimingUI는 비활성화/로그 처리

11) Input 연결 확인
- PlayerController는 Input.GetAxis 및 GetMouseButton을 사용
- Canvas가 Screen Space - Camera일 때 AimingUI에서 RectTransformUtility에 Camera 인자 전달

12) 풀/회수 연동
- AttackProjectile.StartReturn()는 박힌 상태에서만 호출 가능
- PlayerCombat.TryRetrieveStake()는 projectileLayer 및 OverlapCircleAll로 말뚝 콜라이더를 찾음 → StartReturn 호출
- AttackProjectile.CompleteRetrieval()는 AttackManager.ReleaseStake(gameObject) 호출(풀 반환) — AttackManager.Instance가 반드시 존재

13) 런타임 체크리스트
- [ ] AttackManager.Instance null 아님
- [ ] stakePrefab 설정
- [ ] Player Tag/PlayerController/PlayerCombat/HealthSystem 연결
- [ ] Canvas, EventSystem 존재
- [ ] AimingUI.reticuleRect, playerTransform, dottedLineRenderer 연결
- [ ] LineRenderer Material/Texture 설정(점선 텍스처 필요하면 추가)
- [ ] Physics2D 충돌 매트릭스 확인

14) 디버그 팁
- Reticule이 화면 밖에 보이면 RectTransform anchor/scale 확인
- LineRenderer가 보이지 않으면 Material이 올바른지와 sortingLayer/Order 확인
- Screen Space - Camera 사용 시 AimingUI 에서 RectTransformUtility에 camera 인자 null이 아닌지 확인
- 말뚝(Projectile) 레이어/태그가 projectileLayer와 일치하는지 확인

15) 권장 후처리(성능/안정)
- AttackProjectile이 풀로 반환될 때 상태 리셋 필요:
  - transform.SetParent(null); isStuck=false; isReturning=false; col2D.isTrigger=true; spriteRenderer.color = Color.white; rb2D.bodyType = RigidbodyType2D.Static 또는 Kinematic 등 초기화
  - AttackManager.ReleaseStake에서 Reset 처리 권장
- AimingUI 최적: reticule 이미지는 atlas/sprite sheet 사용, LineRenderer는 점선 텍스처 타일링으로 성능 유지

끝 — 빠른 요약
- Canvas: Screen Space - Overlay, Canvas Scaler: Scale With Screen Size(1920x1080 권장), EventSystem 필요
- AimingUI: Reticule(RectTransform) + LineRenderer(월드 좌표), AimingUI 스크립트 필드 연결
- AttackManager / Stake Prefab / Player / Enemy / Wall / Physics2D 충돌 세팅 확인
- 풀 반환 시 상태 리셋 및 AttackManager.Instance 존재 확인

추가로 원하는 것
- Aiming reticule용 권장 Sprite(예: 32x32)와 LineRenderer 점선 텍스처 예시 파일을 생성해 드릴까요?
- 또는 AttackManager.ReleaseStake에 풀 반환 시 Reset 패치(코드)를 적용해 드릴까요?