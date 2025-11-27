using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그로기 시스템 설정 및 관리 클래스
/// - 기본 부활 로직 포함
/// - IGroggyEffect를 통한 추가 효과 확장 가능
/// </summary>
[Serializable]
public class GroggySettings
{
    [Header("Groggy Enable")]
    [Tooltip("그로기 상태 진입 가능 여부. false면 HP 0 시 즉시 사망")]
    public bool enableGroggy = true;

    [Header("Groggy Trigger")]
    [Tooltip("HP가 이 비율 이하로 떨어지면 그로기 진입 (0~1)")]
    [Range(0f, 1f)]
    public float groggyHpPercent = 0.25f;

    [Tooltip("그로기 상태일 때 적용할 색상")]
    public Color groggyColor = new Color(1f, 0.6f, 0.6f, 1f);

    [Header("Recovery Settings")]
    [Tooltip("그로기 후 부활 가능 여부. false면 영구 그로기 상태")]
    public bool enableRecovery = true;

    [Tooltip("그로기 진입 후 몇 초 뒤 부활할지 (초 단위)")]
    [Min(0f)]
    public float recoveryDelay = 5f;

    [Tooltip("부활 시 회복할 체력 비율 (0~1)")]
    [Range(0f, 1f)]
    public float recoveryHealthPercent = 0.25f;

    [Header("Additional Effects")]
    [Tooltip("그로기 상태에서 실행할 추가 효과들 (IGroggyEffect를 구현한 컴포넌트)")]
    public List<MonoBehaviour> additionalEffects = new List<MonoBehaviour>();

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // 내부 상태
    private Coroutine recoveryCoroutine;
    private EnemyController ownerController;

    /// <summary>
    /// 그로기 진입 처리
    /// - 추가 효과 실행
    /// - 부활 타이머 시작 (enableRecovery=true일 때)
    /// </summary>
    public void OnEnterGroggy(EnemyController controller)
    {
        ownerController = controller;

        if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: OnEnterGroggy - enableRecovery={enableRecovery}, delay={recoveryDelay}");

        // 추가 효과 실행
        foreach (var effect in additionalEffects)
        {
            if (effect is IGroggyEffect groggyEffect)
            {
                try
                {
                    groggyEffect.OnGroggyEnter(controller);
                    if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: IGroggyEffect.OnGroggyEnter 호출 - {effect.GetType().Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"GroggySettings [{controller.name}]: IGroggyEffect.OnGroggyEnter 실행 중 오류 - {effect.GetType().Name}: {ex.Message}");
                }
            }
        }

        // 부활 타이머 시작
        if (enableRecovery && recoveryDelay > 0f)
        {
            if (recoveryCoroutine != null)
            {
                controller.StopCoroutine(recoveryCoroutine);
                recoveryCoroutine = null;
            }

            recoveryCoroutine = controller.StartCoroutine(RecoveryCoroutine(controller));
            if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: 부활 타이머 시작 ({recoveryDelay}초)");
        }
        else if (!enableRecovery)
        {
            if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: 부활 비활성화 - 영구 그로기 상태");
        }
    }

    /// <summary>
    /// 그로기 탈출 처리 (처형 등으로 중단될 때)
    /// - 부활 타이머 중단
    /// - 추가 효과 정리
    /// </summary>
    public void OnExitGroggy(EnemyController controller)
    {
        if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: OnExitGroggy");

        // 타이머 중단
        if (recoveryCoroutine != null)
        {
            controller.StopCoroutine(recoveryCoroutine);
            recoveryCoroutine = null;
            if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: 부활 타이머 중단");
        }

        // 추가 효과 정리
        foreach (var effect in additionalEffects)
        {
            if (effect is IGroggyEffect groggyEffect)
            {
                try
                {
                    groggyEffect.Cleanup();
                    if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: IGroggyEffect.Cleanup 호출 - {effect.GetType().Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"GroggySettings [{controller.name}]: IGroggyEffect.Cleanup 실행 중 오류 - {effect.GetType().Name}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 부활 코루틴 - recoveryDelay 초 후 자동 부활
    /// </summary>
    private IEnumerator RecoveryCoroutine(EnemyController controller)
    {
        if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: RecoveryCoroutine 시작 - {recoveryDelay}초 대기");

        yield return new WaitForSeconds(recoveryDelay);

        if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: RecoveryCoroutine - 타이머 완료, 부활 처리 시작");

        // 부활 처리 (컨트롤러가 여전히 그로기 상태인지 확인)
        if (controller != null && controller.IsGroggy())
        {
            // 체력 회복
            var healthSystem = controller.GetComponent<HealthSystem>();
            if (healthSystem != null && !healthSystem.IsDead())
            {
                float recoverAmount = healthSystem.GetMaxHealth() * recoveryHealthPercent;
                healthSystem.Heal(recoverAmount);

                if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: 체력 회복 - {recoverAmount} HP ({recoveryHealthPercent * 100}%)");
            }

            // 추가 효과 완료 콜백
            foreach (var effect in additionalEffects)
            {
                if (effect is IGroggyEffect groggyEffect)
                {
                    try
                    {
                        groggyEffect.OnGroggyComplete(controller);
                        if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: IGroggyEffect.OnGroggyComplete 호출 - {effect.GetType().Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"GroggySettings [{controller.name}]: IGroggyEffect.OnGroggyComplete 실행 중 오류 - {effect.GetType().Name}: {ex.Message}");
                    }
                }
            }

            // 그로기 탈출
            controller.ExitGroggy();

            if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: 부활 완료!");
        }
        else
        {
            if (showDebugLogs) Debug.Log($"GroggySettings [{controller.name}]: RecoveryCoroutine - 컨트롤러가 더 이상 그로기 상태가 아님 (이미 처형되었을 가능성)");
        }

        recoveryCoroutine = null;
    }
}
