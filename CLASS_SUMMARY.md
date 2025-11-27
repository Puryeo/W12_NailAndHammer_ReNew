# Class Role Summary

## Player, Weapons, Core Combat
- `Assets/Scripts/PlayerController.cs` – Handles player movement/aim/dash input and forwards attack inputs to `PlayerCombat`.
- `Assets/Scripts/PlayerCombat.cs` – Manages ammo, firing wood/blood stakes, charged shots, hammer swings, stake retrieval, and secondary charged attack strategies.
- `Assets/Scripts/AttackManager.cs` – Central attack service: pools/instantiates stakes, fires normal/charged projectiles, runs hammer hitboxes, and executes generic attack patterns.
- `Assets/Scripts/Weapons/HammerSwingController.cs` – Spawns and animates the swinging hammer hitbox, applies damage/execution/knockback, and optional gizmo previews.
- `Assets/Scripts/AttackPatternData.cs` – Runtime container for attack parameters (hitboxes, damage, stun, projectile speed/lifetime, retrievable flag).
- `Assets/Scripts/GameObjectPool.cs` – Lightweight object pool for reusable prefabs (resets physics/collider/sprite state on get/release).
- `Assets/Scripts/ProjectileTestController.cs` – Debug hotkeys/UI to swap projectile configs for testing.
- `Assets/Scripts/SimpleStakeRetrieval.cs` – Simple R-key retrieval: triggers each `AttackProjectile` to run its configured retrieval behavior with cooldown display helpers.
- `Assets/Scripts/Skills/StakeRetrievalSkill.cs` – Skill that recalls embedded stakes (visualizes and rewards ammo) with cooldown and line rendering options.
- `Assets/Scripts/GameOverUIHandler.cs` – UI buttons to restart current/target scene or quit the application.
- `Assets/Scripts/PlayerGameOverHandler.cs` – Listens to player `HealthSystem.OnZeroHealth` to show a game-over canvas and disable player controls.
- `Assets/Scripts/AttackProjectile.cs` – Stateful stake projectile with config-driven collision/retrieval strategies, impaling support, hit feedback, and pooling awareness.
- `Assets/Scripts/AttackProjectile.cs.backup` – Legacy version of `AttackProjectile` kept as a backup reference.

## Camera
- `Assets/Scripts/camera/TopDownCamera.cs` – Smoothly follows the player with optional camera shake presets and unscaled-time smoothing.
- `Assets/Scripts/camera/CameraShake.cs` – Generates 2D shake offsets with strength presets/curves and exposes the current offset for the camera to apply.

## Combat Systems & Effects
- `Assets/Scripts/combat/CombatContracts.cs` – Common combat interfaces (`IDamageable`, `IStunnable`) and enums (attack type, hitbox type, knockback, hit stop/shake, hit source).
- `Assets/Scripts/combat/HealthSystem.cs` – Generic health/death logic with hit feedback, hit-stop/camera shake hooks, ragdoll/fade-out, events, and ammo reward delegation.
- `Assets/Scripts/combat/HitEffect.cs` – Sprite flash and squash/stretch feedback for hits and executions.
- `Assets/Scripts/combat/HitParticleEffect.cs` – Spawns particle effects for normal hit vs execution impacts.
- `Assets/Scripts/combat/HitEffectManager.cs` – Static helper to trigger hit-stop and camera shake based on hit source/strength.
- `Assets/Scripts/combat/HitStopManager.cs` – Singleton that applies time-scale hit-stop with weak/medium/strong presets.
- `Assets/Scripts/combat/KnockbackRunner.cs` – Temporary component that delays and applies knockback (and optional shake) to its host rigidbody.
- `Assets/Scripts/combat/GroggySettings.cs` – Configures enemy groggy thresholds, recovery timing, health restore, and extra effects.
- `Assets/Scripts/combat/IGroggyEffect.cs` – Interface for groggy state side-effects.
- `Assets/Scripts/combat/GroggyUIEffect.cs` – Spawns and updates groggy UI above enemies using `GroggyUIView`.
- `Assets/Scripts/combat/GroggyUIView.cs` – World-space UI view for groggy timer/skull visuals with fill animation.
- `Assets/Scripts/combat/ExplosionGroggyEffect.cs` – Groggy effect that triggers an AoE explosion on enter or completion.
- `Assets/Scripts/combat/ISecondaryChargedAttack.cs` – Strategy interface for right-click charged attacks used by `PlayerCombat`.
- `Assets/Scripts/combat/SecondaryAttacks/SecondaryChargedAttackA.cs` – ScriptableObject hammer swing preset (executes with execution enabled).
- `Assets/Scripts/combat/SecondaryAttacks/SecondaryChargedAttackB.cs` – Alternate ScriptableObject hammer swing preset with higher damage/sweep.
- `Assets/Scripts/combat/SecondaryAttacks/SecondaryChargedAttackAComponent.cs` – MonoBehaviour version of charged attack A.
- `Assets/Scripts/combat/SecondaryAttacks/SecondaryChargedAttackBComponent.cs` – MonoBehaviour version of charged attack B.

## Projectiles: Config, State, Behaviors
- `Assets/Scripts/combat/Core/ProjectileConfig.cs` – ScriptableObject defining projectile damage/speed/lifetime, collision/retrieval types, impaling and retrieval visuals/feedback.
- `Assets/Scripts/combat/Core/ProjectileState.cs` – Enums for projectile lifecycle plus collision/retrieval behavior types.
- `Assets/Scripts/combat/Core/ProjectileStateController.cs` – State machine driving `AttackProjectile` callbacks per state.
- `Assets/Scripts/combat/Behaviors/IProjectileCollisionBehavior.cs` – Collision strategy interface.
- `Assets/Scripts/combat/Behaviors/Collision/StickToEnemyBehavior.cs` – Default collision: damages, sticks to enemy/wall, switches to Stuck state.
- `Assets/Scripts/combat/Behaviors/Collision/ImpaleEnemyBehavior.cs` – Impaling collision: threads enemies through projectile, accelerates, and handles wall impact damage/stun.
- `Assets/Scripts/combat/Behaviors/IProjectileRetrievalBehavior.cs` – Retrieval strategy interface.
- `Assets/Scripts/combat/Behaviors/Retrieval/SimpleRetrievalBehavior.cs` – Straight return to player with optional line/decorator and hit feedback.
- `Assets/Scripts/combat/Behaviors/Retrieval/BindingRetrievalBehavior.cs` – Return path damages and applies slow via `BindingTrigger`/`BindingEffect`; also handles stuck host immediately.
- `Assets/Scripts/combat/Behaviors/Retrieval/PullRetrievalBehavior.cs` – Return path damages then applies `PullEffect` via `PullTrigger` to drag enemies toward the player, damaging on wall hit.
- `Assets/Scripts/combat/Behaviors/Retrieval/StuckEnemyPullRetrievalBehavior.cs` – If stuck to an enemy, drags that enemy with the projectile toward the player with wall detection (`WallDetector`) and timed detach.
- `Assets/Scripts/combat/Utilities/ProjectileLineRendererUtil.cs` – Shared utilities to create/update/cleanup retrieval line renderers and apply hit feedback.
- `Assets/Scripts/combat/Components/ImpaledEnemyManager.cs` – Manages enemies impaled on a projectile: stores originals, repositions/spaces them, applies damage/stun on wall impact, and releases/restores.

## Enemy AI & Patterns
- `Assets/Scripts/Enemy/EnemyController.cs` – Core enemy AI: movement/rotation toward player, groggy handling, stun, stuck stake tracking/consumption, ammo rewards, and death bookkeeping.
- `Assets/Scripts/Enemy/EnemyCombat.cs` – Inspector-driven enemy attacks (melee/hitbox/projectile/suicide) with telegraphing, temporary hitboxes, projectile spawning, and suicide VFX; includes `TemporaryHitbox` helper.
- `Assets/Scripts/Enemy/EnemyPatternController.cs` – Wraps pattern presets and delegates execution to `IEnemyAttackBehavior` (currently melee/no-op behaviors) with cooldown and cancel/reset support.
- `Assets/Scripts/Enemy/IEnemyAttackBehavior.cs` – Interface for pattern-driven enemy attack behaviors.
- `Assets/Scripts/Enemy/MeleeBehavior.cs` – Pattern behavior that optionally telegraphs then performs hitbox checks versus the player.
- `Assets/Scripts/Enemy/PatternPreset.cs` – ScriptableObject describing pattern data (attack type, cooldown, telegraph visuals, hitbox/projectile/suicide parameters).
- `Assets/Scripts/Enemy/EnemyProjectile.cs` – Lightweight pooled enemy projectile that moves toward a direction, damages targets with a tag, and returns to its pool.
- `Assets/Scripts/Enemy/EnemySpawner.cs` – Spawns normal/strong enemies within an area on cooldown and tracks counts.
- `Assets/Scripts/Enemy/PatternPreset` (above) pairs with `EnemyPatternController`; `EnemyPatternController` also contains a simple `NoOpBehavior` fallback.

## Pools & Registries
- `Assets/Scripts/Pools/ProjectilePoolRegistry.cs` – Global registry mapping projectile prefabs to shared `GameObjectPool`s, provides spawn/release wrappers and internal `PoolRef`/`ReferenceEqualityComparer`.

## UI & HUD
- `Assets/Scripts/UI/AimingUI.cs` – Displays mouse reticle and charge UI based on `PlayerCombat` charge state; can center or follow cursor.
- `Assets/Scripts/UI/CooldownRUI.cs` – Shows retrieval skill cooldown (fill + timer text) using unscaled time with color cues.
- `Assets/Scripts/UI/PlayerHUD.cs` – Updates player HP bar/text and ammo text with smoothing.
- `Assets/Scripts/UI/PanelCloseResumeHandler.cs` – Empty placeholder (no implementation).

## Skills & Selection UI
- `Assets/Scripts/Selection/HealthSystemExtensions.cs` – Extension methods to modify max health via reflection while clamping current health.
- `Assets/Scripts/Selection/ISkillEffect.cs` – Skill effect interface for both MonoBehaviour and ScriptableObject skills.
- `Assets/Scripts/Selection/ISkillReceiver.cs` – Interface for objects that can receive/apply a `SkillData`.
- `Assets/Scripts/Selection/PanelCloser.cs` – Closes a linked panel and resumes time scale.
- `Assets/Scripts/Selection/PlayerHealthSkill.cs` – MonoBehaviour skill to adjust player health (max and optional heal).
- `Assets/Scripts/Selection/PlayerHealthSkillSO.cs` – ScriptableObject skill to adjust player health.
- `Assets/Scripts/Selection/PlayerSkillManager.cs` – Test receiver applying hardcoded skill effects to player health.
- `Assets/Scripts/Selection/RelicItem.cs` – On pickup, rolls two random skills and opens the selection UI.
- `Assets/Scripts/Selection/SimpleRelicTest.cs` – Trigger that opens a panel on player contact (pauses time) and destroys itself.
- `Assets/Scripts/Selection/SimpleSkillApplier.cs` – Buttons to trigger indexed `SkillEffectSO` skills and close the panel.
- `Assets/Scripts/Selection/SkillButton.cs` – Binds `SkillData` to UI button visuals and notifies `SkillSelectionUI` on click.
- `Assets/Scripts/Selection/SkillData.cs` – ScriptableObject holding skill metadata (id, name, icon, description).
- `Assets/Scripts/Selection/SkillEffectSO.cs` – Abstract ScriptableObject base implementing `ISkillEffect`.
- `Assets/Scripts/Selection/SkillSelectionUI.cs` – Singleton selection panel that populates two skill options and sends selection to the connector.
- `Assets/Scripts/Selection/SkillSystemConnector.cs` – Bridges selected skills to the player via an `ISkillReceiver`.

## Selection/Skill Assets
- `Assets/Scripts/Selection/SkillS/*.asset` – Sample ScriptableObject skill assets referenced by the selection system.

## Projectiles & Retrieval Utilities
- `Assets/Scripts/combat/Behaviors/Collision/StickToEnemyBehavior.cs` / `ImpaleEnemyBehavior.cs` – See projectile behaviors above.
- `Assets/Scripts/combat/Behaviors/Retrieval/BindingRetrievalBehavior.cs` – Contains helper classes `BindingTrigger` and `BindingEffect` for return-path status effects.
- `Assets/Scripts/combat/Behaviors/Retrieval/PullRetrievalBehavior.cs` – Contains helper classes `PullTrigger` and `PullEffect` for dragging enemies.
- `Assets/Scripts/combat/Behaviors/Retrieval/StuckEnemyPullRetrievalBehavior.cs` – Includes `WallDetector` to flag wall collisions during drag.
- `Assets/Scripts/combat/Enemy/EnemyCombat.cs` – Contains helper class `TemporaryHitbox` for transient hit colliders.

## Misc & Legacy
- `Assets/Scripts/Archive/CombatEnums.cs` – Empty placeholder file.
- `Assets/Scripts/UI/PanelCloseResumeHandler.cs` – Empty placeholder file (duplicate mention for clarity).

## Editor Utilities
- `Assets/Scripts/Editor/ProjectileConfigCreator.cs` – Editor menu commands to auto-create/delete sample `ProjectileConfig` assets under `Assets/ScriptableObjects/ProjectileConfigs`.

## Third-Party Plugins (summary)
- `Assets/Plugins/CodeStage/Maintainer/*` – Large editor-only suite for project maintenance (references finder, issues finder, project cleaner), including core map/scan/detector classes, UI windows/menus/tree views, settings, and demo scripts (`Maintainer`, `MaintainerWindow`, `IssuesFinder`, `ProjectCleaner`, `ReferencesFinder`, numerous `*Detector`, `*Record`, `*TreeView`, and storage/map helpers).
- `Assets/Plugins/Sirenix/Odin Inspector/Modules/Unity.Mathematics/MathematicsDrawers.cs` – Custom Odin Inspector property drawers for Unity.Mathematics types.

