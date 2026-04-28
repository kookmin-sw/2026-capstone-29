using System.Collections;
using UnityEngine;

public class UltimateSkillController : MonoBehaviour
{
    [Header("참조 (비워두면 자동 검색)")]
    public Animator animator;
    public Transform target;              // 궁극기 대상 (런타임에 TriggerUltimate(t)로 전달 권장)
    public Camera mainCamera;

    [Header("모션 시퀀스 설정")]
    public MotionStep[] motionSteps;      // Inspector에서 순서대로 추가

    [Header("전역 설정")]
    public float introSlowMotionScale = 0.15f;   // 발동 시 슬로우
    public float introDuration = 0.4f;            // 발동 연출 시간
    public float defaultFOV = 60f;

    // 내부 상태
    private bool isPlayingUltimate = false;
    private Vector3 camOriginalPos;
    private Quaternion camOriginalRot;
    private float camOriginalFOV;
    private Coroutine ultimateCoroutine;

    // Cinemachine Brain (2.x/3.x 무관, 타입 이름으로 매칭 → 패키지 의존성 없음)
    private Behaviour cinemachineBrain;
    private bool brainWasEnabled;

    // ─────────────────────────────────────────────────────────
    //  프리팹 안전 초기화
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 컴포넌트가 처음 추가될 때 에디터에서 자동 채움.
    /// 프리팹 작업 시 Animator를 손으로 끌어다놓지 않아도 됨.
    /// </summary>
    private void Reset()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        // 프리팹 인스턴스화 후에도 안전하게 자동 검색
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null)
            camOriginalFOV = mainCamera.fieldOfView;
        else
            camOriginalFOV = defaultFOV;

        CacheCinemachineBrain();
    }

    private void OnDisable()
    {
        // 얼티밋 도중 프리팹 비활성화/파괴 시 TimeScale 원복 + Brain 복원 (안전장치)
        if (isPlayingUltimate)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            RestoreCinemachineBrain();
            isPlayingUltimate = false;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Cinemachine Brain 제어
    //  - 얼티밋 중에만 Brain을 끄고, 끝나면 다시 켬.
    //  - Cinemachine 패키지가 없어도 컴파일 가능하도록 타입명으로 검색.
    // ─────────────────────────────────────────────────────────

    private void CacheCinemachineBrain()
    {
        if (mainCamera == null) return;

        Behaviour[] components = mainCamera.GetComponents<Behaviour>();
        foreach (Behaviour b in components)
        {
            if (b == null) continue;
            if (b.GetType().Name == "CinemachineBrain")
            {
                cinemachineBrain = b;
                return;
            }
        }
    }

    private void DisableCinemachineBrain()
    {
        if (cinemachineBrain == null) CacheCinemachineBrain();
        if (cinemachineBrain == null) return;

        brainWasEnabled = cinemachineBrain.enabled;
        cinemachineBrain.enabled = false;
    }

    private void RestoreCinemachineBrain()
    {
        if (cinemachineBrain == null) return;
        cinemachineBrain.enabled = brainWasEnabled;
    }

    // ─────────────────────────────────────────────────────────
    //  외부 호출 API
    // ─────────────────────────────────────────────────────────

    // 외부에서 호출 (버튼, Input 등)
    public void TriggerUltimate(Transform t)
    {
        target = t;
        if (isPlayingUltimate) return;
        if (!ValidateReferences()) return;

        ultimateCoroutine = StartCoroutine(PlayUltimateSequence());
    }

    /// <summary>발동 전 미리 target만 설정해두기</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>현재 얼티밋 재생 중인지</summary>
    public bool IsPlaying => isPlayingUltimate;

    private bool ValidateReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("[UltimateSkillController] Animator를 찾을 수 없습니다.", this);
                return false;
            }
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[UltimateSkillController] 메인 카메라를 찾을 수 없습니다. (MainCamera 태그 확인)", this);
                return false;
            }
        }

        return true;
    }

    private IEnumerator PlayUltimateSequence()
    {
        isPlayingUltimate = true;

        // 카메라 원래 상태 저장
        camOriginalPos = mainCamera.transform.position;
        camOriginalRot = mainCamera.transform.rotation;
        camOriginalFOV = mainCamera.fieldOfView;

        // Cinemachine Brain OFF → 직접 카메라 조작 가능
        DisableCinemachineBrain();

        // 1. 발동 연출: 슬로우 + 카메라 줌인
        yield return StartCoroutine(PlayIntro());

        // 2. 모션 스텝 순차 실행
        if (motionSteps != null)
        {
            foreach (MotionStep step in motionSteps)
            {
                if (step == null) continue;
                yield return StartCoroutine(PlayMotionStep(step));
            }
        }

        // 3. 복원
        yield return StartCoroutine(RestoreNormal());

        // Cinemachine Brain ON → 평상시 카메라로 복귀
        RestoreCinemachineBrain();

        isPlayingUltimate = false;
    }

    // ── 발동 연출 ──────────────────────────────────────────────
    private IEnumerator PlayIntro()
    {
        Time.timeScale = introSlowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        float elapsed = 0f;
        Vector3 introPos = target != null
            ? target.position + Vector3.up * 2f - mainCamera.transform.forward * 5f
            : mainCamera.transform.position;

        while (elapsed < introDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / introDuration;

            // FOV 살짝 좁히기 (임팩트 강조)
            mainCamera.fieldOfView = Mathf.Lerp(camOriginalFOV, camOriginalFOV - 10f, t);

            if (target != null)
                mainCamera.transform.position = Vector3.Lerp(camOriginalPos, introPos, t * 0.4f);

            yield return null;
        }

        // 슬로우 해제
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    // ── 개별 모션 스텝 ─────────────────────────────────────────
    private IEnumerator PlayMotionStep(MotionStep step)
    {
        // 애니메이션 트리거
        if (animator != null && !string.IsNullOrEmpty(step.animationTrigger))
            animator.CrossFade(step.animationTrigger, step.crossfadeTime);

        // 슬로우모션
        if (step.cameraEffect.slowMotion)
        {
            Time.timeScale = step.cameraEffect.timeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }

        float elapsed = 0f;
        Vector3 stepStartPos = transform.position;
        Vector3 stepTargetPos = step.moveToTarget && target != null
            ? target.position - transform.forward * 1.5f
            : transform.position + transform.TransformDirection(step.moveOffset);

        // 카메라 셰이크 코루틴 (비동기)
        if (step.cameraEffect.shake)
            StartCoroutine(CameraShake(step.cameraEffect.shakeIntensity,
                                        step.cameraEffect.shakeDuration));

        while (elapsed < step.duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / step.duration;

            // 캐릭터 이동
            if (step.moveCharacter)
                transform.position = Vector3.Lerp(stepStartPos, stepTargetPos,
                                                   EaseInOut(t) * step.moveSpeed * Time.unscaledDeltaTime);

            // FOV 변경
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView,
                                                  step.cameraEffect.targetFOV,
                                                  step.cameraEffect.fovTransitionSpeed * Time.unscaledDeltaTime);

            // 카메라 오빗 - target 없으면 자기 자신을 pivot으로 사용 (제자리 시전 대응)
            if (step.cameraEffect.orbitTarget)
            {
                Transform pivot = target != null ? target : transform;
                OrbitCamera(pivot, step.cameraEffect, elapsed);
            }

            yield return null;
        }

        // 슬로우 복원
        if (step.cameraEffect.slowMotion)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    // ── 카메라 오빗 ────────────────────────────────────────────
    private void OrbitCamera(Transform pivot, CameraEffect effect, float time)
    {
        float angle = effect.orbitAngle + time * effect.orbitSpeed;
        float rad = angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Sin(rad) * effect.orbitDistance,
            effect.orbitHeight,
            Mathf.Cos(rad) * effect.orbitDistance
        );

        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            pivot.position + offset,
            effect.orbitSpeed * Time.unscaledDeltaTime
        );
        mainCamera.transform.LookAt(pivot.position + Vector3.up * 1f);
    }

    // ── 카메라 셰이크 ──────────────────────────────────────────
    private IEnumerator CameraShake(float intensity, float duration)
    {
        Vector3 originalPos = mainCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float remaining = 1f - (elapsed / duration);

            mainCamera.transform.localPosition = originalPos + (Vector3)Random.insideUnitCircle
                                                  * intensity * remaining;
            yield return null;
        }

        mainCamera.transform.localPosition = originalPos;
    }

    // ── 전체 복원 ──────────────────────────────────────────────
    private IEnumerator RestoreNormal()
    {
        float elapsed = 0f;
        float restoreDuration = 0.5f;

        Vector3 fromPos = mainCamera.transform.position;
        Quaternion fromRot = mainCamera.transform.rotation;
        float fromFOV = mainCamera.fieldOfView;

        while (elapsed < restoreDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseInOut(elapsed / restoreDuration);

            mainCamera.transform.position = Vector3.Lerp(fromPos, camOriginalPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(fromRot, camOriginalRot, t);
            mainCamera.fieldOfView = Mathf.Lerp(fromFOV, camOriginalFOV, t);

            yield return null;
        }

        // 확실히 원위치
        mainCamera.transform.position = camOriginalPos;
        mainCamera.transform.rotation = camOriginalRot;
        mainCamera.fieldOfView = camOriginalFOV;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    // ── 유틸 ───────────────────────────────────────────────────
    private float EaseInOut(float t) => t * t * (3f - 2f * t);
}
