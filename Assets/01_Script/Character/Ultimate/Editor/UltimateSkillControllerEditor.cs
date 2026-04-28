using UnityEditor;
using UnityEngine;

/// <summary>
/// UltimateSkillController 전용 커스텀 인스펙터.
/// 모션 스텝 프리셋을 한 번에 자동 설정해주는 버튼을 제공합니다.
/// (Editor 전용 — 빌드에는 포함되지 않음)
/// </summary>
[CustomEditor(typeof(UltimateSkillController))]
public class UltimateSkillControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 기본 인스펙터 그대로 그리기
        DrawDefaultInspector();

        UltimateSkillController controller = (UltimateSkillController)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("━━ 프리셋 ━━", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "5스텝 근거리 연속 베기 프리셋을 자동으로 채워 넣습니다.\n" +
            "Animator State 이름은 placeholder(Ultimate_Intro/Slash1~3/Finisher) 사용.",
            MessageType.Info);

        GUI.backgroundColor = new Color(0.6f, 0.9f, 1f);
        if (GUILayout.Button("5스텝 근거리 연속 베기 프리셋 적용", GUILayout.Height(32)))
        {
            ApplySlashComboPreset(controller);
        }

        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("모션 스텝 모두 비우기", GUILayout.Height(22)))
        {
            if (EditorUtility.DisplayDialog(
                    "모션 스텝 초기화",
                    "현재 설정된 모든 모션 스텝을 삭제하시겠습니까?",
                    "삭제", "취소"))
            {
                Undo.RecordObject(controller, "Clear Motion Steps");
                controller.motionSteps = new MotionStep[0];
                EditorUtility.SetDirty(controller);
            }
        }

        GUI.backgroundColor = Color.white;
    }

    // ═══════════════════════════════════════════════════════════════
    // 5스텝 근거리 연속 베기 프리셋
    // ═══════════════════════════════════════════════════════════════
    private void ApplySlashComboPreset(UltimateSkillController controller)
    {
        Undo.RecordObject(controller, "Apply Slash Combo Preset");

        // ── 전역 설정 ────────────────────────────────────────────
        controller.introSlowMotionScale = 0.2f;
        controller.introDuration        = 0.5f;
        controller.defaultFOV           = 60f;

        // ── 5개 스텝 생성 ────────────────────────────────────────
        controller.motionSteps = new MotionStep[5];

        // Element 0 ── 발동 (자세 잡기)
        controller.motionSteps[0] = new MotionStep
        {
            animationTrigger = "Ultimate_Intro",
            duration         = 0.6f,
            crossfadeTime    = 0.15f,
            moveCharacter    = false,
            moveToTarget     = false,
            moveOffset       = Vector3.zero,
            moveSpeed        = 10f,
            cameraEffect = new CameraEffect
            {
                targetFOV           = 50f,
                fovTransitionSpeed  = 6f,
                shake               = false,
                shakeIntensity      = 0f,
                shakeDuration       = 0f,
                orbitTarget         = true,
                orbitAngle          = 30f,
                orbitHeight         = 1.8f,
                orbitDistance       = 4.5f,
                orbitSpeed          = 20f,
                slowMotion          = true,
                timeScale           = 0.4f,
                slowMotionDuration  = 0.6f
            }
        };

        // Element 1 ── 1차 베기 (횡베기)
        controller.motionSteps[1] = new MotionStep
        {
            animationTrigger = "Ultimate_Slash1",
            duration         = 0.35f,
            crossfadeTime    = 0.10f,
            moveCharacter    = false,
            moveToTarget     = false,
            moveOffset       = Vector3.zero,
            moveSpeed        = 10f,
            cameraEffect = new CameraEffect
            {
                targetFOV           = 55f,
                fovTransitionSpeed  = 8f,
                shake               = true,
                shakeIntensity      = 0.20f,
                shakeDuration       = 0.15f,
                orbitTarget         = true,
                orbitAngle          = 90f,
                orbitHeight         = 1.6f,
                orbitDistance       = 3.8f,
                orbitSpeed          = 35f,
                slowMotion          = false,
                timeScale           = 1f,
                slowMotionDuration  = 0f
            }
        };

        // Element 2 ── 2차 베기 (반대 사선)
        controller.motionSteps[2] = new MotionStep
        {
            animationTrigger = "Ultimate_Slash2",
            duration         = 0.35f,
            crossfadeTime    = 0.08f,
            moveCharacter    = false,
            moveToTarget     = false,
            moveOffset       = Vector3.zero,
            moveSpeed        = 10f,
            cameraEffect = new CameraEffect
            {
                targetFOV           = 55f,
                fovTransitionSpeed  = 8f,
                shake               = true,
                shakeIntensity      = 0.25f,
                shakeDuration       = 0.15f,
                orbitTarget         = true,
                orbitAngle          = 200f,
                orbitHeight         = 1.6f,
                orbitDistance       = 3.8f,
                orbitSpeed          = 40f,
                slowMotion          = false,
                timeScale           = 1f,
                slowMotionDuration  = 0f
            }
        };

        // Element 3 ── 3차 강타 (임팩트 슬로우)
        controller.motionSteps[3] = new MotionStep
        {
            animationTrigger = "Ultimate_Slash3",
            duration         = 0.55f,
            crossfadeTime    = 0.10f,
            moveCharacter    = false,
            moveToTarget     = false,
            moveOffset       = Vector3.zero,
            moveSpeed        = 10f,
            cameraEffect = new CameraEffect
            {
                targetFOV           = 45f,
                fovTransitionSpeed  = 10f,
                shake               = true,
                shakeIntensity      = 0.40f,
                shakeDuration       = 0.30f,
                orbitTarget         = true,
                orbitAngle          = 320f,
                orbitHeight         = 2.2f,
                orbitDistance       = 3.2f,
                orbitSpeed          = 15f,
                slowMotion          = true,
                timeScale           = 0.35f,
                slowMotionDuration  = 0.4f
            }
        };

        // Element 4 ── 피니시 (마무리 포즈)
        controller.motionSteps[4] = new MotionStep
        {
            animationTrigger = "Ultimate_Finisher",
            duration         = 0.9f,
            crossfadeTime    = 0.20f,
            moveCharacter    = false,
            moveToTarget     = false,
            moveOffset       = Vector3.zero,
            moveSpeed        = 10f,
            cameraEffect = new CameraEffect
            {
                targetFOV           = 65f,
                fovTransitionSpeed  = 4f,
                shake               = false,
                shakeIntensity      = 0f,
                shakeDuration       = 0f,
                orbitTarget         = true,
                orbitAngle          = 45f,
                orbitHeight         = 2.5f,
                orbitDistance       = 5.5f,
                orbitSpeed          = 8f,
                slowMotion          = true,
                timeScale           = 0.25f,
                slowMotionDuration  = 0.6f
            }
        };

        EditorUtility.SetDirty(controller);
        Debug.Log("[UltimateSkillController] 5스텝 근거리 연속 베기 프리셋이 적용되었습니다.");
    }
}
