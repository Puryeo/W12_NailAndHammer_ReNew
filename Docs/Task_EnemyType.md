# Task: 일반 몬스터 유형 정리 (Boss 제외)

변경 요약 (중요)
- 단기 PoC/현재 결정: EnemyCombat이 모든 공격 파라미터와 실행 흐름을 단독으로 관리합니다. __EnemyPatternController로 위임하지 않습니다.__
- Pattern SO (PatternPreset 등)는 당분간 사용하지 않으며, 패턴 데이터는 __EnemyCombat의 Inspector 필드__로 관리합니다.
- 위 결정은 PoC 속도와 기존 레거시 호환성(빠른 디버그/디자이너 반복)을 우선하기 위한 것입니다. 장기적으로 Pattern SO 기반 아키텍처로 전환할 수 있으나 팀 합의가 필요합니다.

목적
- 기존 소스 파일을 광범위하게 변경하지 않으면서(가능하면 최소 변경), 디자이너가 인스펙터에서 편하게 공격 파라미터를 조정할 수 있는 구조를 PoC로 도입합니다.
- 우선 대상: 근접, 투사체(원거리), 자폭 — 보스는 별도 단계로 보류.
- 핵심: 단기 PoC에서는 __EnemyCombat 중심__(인스펙터 기반)으로 운영하고, 필요 시 향후 Strategy/Behavior 패턴(EnemyPatternController + PatternPreset)으로 점진 전환합니다.

핵심 결정 사항 (요약)
- 단기(현재)
  - EnemyCombat 컴포넌트의 Inspector 필드로 모든 공격 파라미터(타입, 데미지, 쿨다운, 텔레그래프, 히트박스 등)를 관리합니다.
  - PatternPreset(SO) 및 EnemyPatternController 위임 경로는 사용하지 않으며, 관련 코드/에셋은 보존하되 활성 경로로 사용하지 않습니다.
- 중장기(옵션)
  - 향후 필요 시 PatternPreset(SO) + EnemyPatternController(Behavior 인스턴스) 아키텍처로 전환 가능(팀 합의 필요).
- 공통 권장
  - 텔레그래프·임시 히트박스·적 전용 투사체는 풀링(FXPool/ProjectilePool)으로 관리.
  - 플레이어용 말뚝(Stake) 등 기존 시스템과 충돌하지 않도록 책임 분리.

아키텍처 개요 (현재 PoC 기준)
- EnemyCombat (MonoBehaviour, 프리팹에 추가) — 핵심(현재)
  - Inspector: attackType, damage, cooldown, useTelegraph, telegraphPrefab, telegraphDelay, telegraphSize, hitboxType, hitboxOffset, hitboxSize, hitboxRadius, hitboxLife, projectilePrefab, projectileSpeed 등
  - 런타임: TryAttack(target) → ExecuteAttack(target) 내부에서 텔레그래프 처리(코루틴), 임시 히트박스 생성(부모=Enemy), 투사체 발사 등을 직접 수행
  - 책임: 모든 공격 파라미터 관리, 텔레그래프/히트박스 생성·정리, 쿨다운 관리, 중복 생성 방지, 임시 오브젝트 정리
- (옵션) EnemyPatternController / IEnemyAttackBehavior
  - 기존 설계상 존재하던 컴포넌트/인터페이스는 레포지토리에 보존하되, 현재 PoC 경로에서는 사용하지 않습니다.
  - 향후 패턴화 전환 시 재활용 가능하나, SO 기반 패턴은 상태 공유 문제 등으로 설계 주의 필요.
- Pools
  - 텔레그래프/임시 히트박스/적 프로젝타일은 풀링으로 운영 권장.

EnemyCombat (Inspector) 권장 필드
- EAttackType attackType (Melee, Projectile, Suicide 등)
- float damage
- float cooldown
- bool useTelegraph
- GameObject telegraphPrefab
- Color telegraphColor
- Color telegraphHitColor
- float telegraphDelay
- float telegraphSize
- EHitboxType hitboxType (Box, Circle)
- Vector2 hitboxOffset (로컬 기준; 적의 정면이 +X)
- Vector2 hitboxSize
- float hitboxRadius
- float hitboxLife
- GameObject projectilePrefab
- float projectileSpeed
- float projectileLifetime
- float telegraphPersistAfterHit (히트 후 인디케이터 추가 유지 시간)

주의사항 / 구현 팁
- 히트박스 위치는 로컬 기준(hitboxOffset)을 transform.TransformPoint로 월드 좌표 변환하거나, 임시 히트박스를 Enemy의 자식으로 생성하여 로컬 위치로 관리하면 회전 일관성이 확보됩니다.
- 인디케이터(telegraph) 스프라이트는 pivot=center, 스프라이트 PPU 통일, SpriteRenderer의 Shader=Sprites/Default 권장.
- 한 공격당 인디케이터 1개, 히트박스 1개만 생성되도록 재진입/중복 방지(플래그 + activeIndicator 참조) 구현 권장.
- 동일 공격의 중복 데미지 방지를 위해 TemporaryHitbox에 처리 대상 집합(HashSet) 유지 권장.
- 풀링(FXPool/ProjectilePool) 적용으로 Instantiate/Destroy 비용을 줄이세요.

테스트 체크리스트 (PoC 우선순위)
1. 텔레그래프: 인디케이터가 히트박스 크기에 맞춰 스케일되고, telegraphColor/telegraphHitColor(알파 포함)가 적용되는지 확인.
2. Melee: 히트박스 로컬 오프셋·크기 정확성, 플레이어가 한 공격 주기당 한 번만 데미지 받는지 검증.
3. Projectile: 발사 각도·속도·수명, 플레이어 충돌 데미지 정상 동작.
4. 중복 생성 방지: 공격 중 재진입 시 인디케이터·히트박스가 추가로 생성되지 않음.
5. 퍼포먼스: 다수 적 동시(50~100)에서 GC/프레임 영향 측정, 풀링 적용 여부 판단.

마이그레이션·운영 권고
- 단기: 기존 프리팹을 Pattern SO 기반으로 전환하지 말고 EnemyCombat 인스펙터로 한시적으로 운영합니다. 팀에게 변경 의도를 문서화하고 PR에 명확히 표기하세요.
- 장기: 패턴화(Behavior 분리)를 도입할 경우 다음을 권장
  - Behavior는 인스턴스/컴포넌트로 구현(전역 상태 공유 방지)
  - PatternPreset(SO)는 디자이너 편의용 메타데이터로만 사용하거나, SO 사용 시 상태 공유 문제를 해결하는 안전장치를 도입
  - 폴링·ResetForPool 규약을 엄격히 정의

변경 이력 (요약)
- 기존 문서의 PatternPreset/EnemyPatternController 우선 권고를 단기 PoC 운영 정책으로 변경: __현재는 EnemyCombat 중심 운영, Pattern SO는 사용하지 않음.__
- 위 변경은 빠른 디버깅·디자이너 반복을 우선하기 위한 결정이며, 문서 상단에 명시했습니다.

다음 단계 제안
- EnemyCombat Inspector 필드 목록을 기준으로 샘플 프리팹 1개를 설정하고 기본 동작(telegraph→hitbox→cleanup) 검증.
- 필요 시 제가 EnemyCombat용 PR 템플릿(변경 파일·테스트 체크리스트·리뷰 포인트)을 생성해 드리겠습니다.
