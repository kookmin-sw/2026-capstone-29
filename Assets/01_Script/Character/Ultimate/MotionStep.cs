using UnityEngine;

[System.Serializable]
public class MotionStep
{
    [Header("애니메이션")]
    public string animationTrigger;       // Animator 트리거 이름
    public float duration = 0.5f;         // 이 스텝의 지속 시간
    public float crossfadeTime = 0.15f;   // 크로스페이드 전환 시간

    [Header("캐릭터 이동")]
    public bool moveCharacter = false;
    public Vector3 moveOffset;            // 캐릭터 이동 오프셋 (로컬 기준)
    public bool moveToTarget = false;     // 타겟 방향으로 이동 여부
    public float moveSpeed = 10f;

    [Header("카메라 연출")]
    public CameraEffect cameraEffect;
}

[System.Serializable]
public class CameraEffect
{
    public float targetFOV = 60f;
    public float fovTransitionSpeed = 5f;

    public bool shake = false;
    public float shakeIntensity = 0.3f;
    public float shakeDuration = 0.2f;

    public bool orbitTarget = false;
    public float orbitAngle = 45f;        // 타겟 기준 궤도 각도
    public float orbitHeight = 2f;
    public float orbitDistance = 4f;
    public float orbitSpeed = 8f;

    public bool slowMotion = false;
    public float timeScale = 0.2f;
    public float slowMotionDuration = 0.5f;
}