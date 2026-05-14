using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 피격 시 로컬 플레이어에게 두 가지 피드백을 동시에 발생시키는 컨트롤러.
///   1) 카메라 흔들림  : Cinemachine Impulse 시스템을 통해 발동
///   2) 붉은 화면 효과 : Screen-Space Overlay UI Image 의 알파를 페이드 인/아웃
///
/// 플레이어 프리팹에 부착하며, <see cref="UnifiedCharacterModel.OnHealthChanged"/>
/// 이벤트를 구독해 HP가 감소했을 때만 효과를 발동한다. 네트워크 모드든 오프라인
/// 모드든 동일하게 동작하며, 로컬 플레이어가 아닌 캐릭터에선 스스로 비활성화된다.
///
/// 에디터 셋업:
///   - 같은 GameObject(또는 자식)에 <see cref="CinemachineImpulseSource"/> 부착
///   - 씬의 <see cref="CinemachineVirtualCamera"/> 에 CinemachineImpulseListener Extension 추가
///   - Screen-Space Overlay Canvas 안에 풀스크린 빨간 Image(α=0, Raycast Target off) 를
///     만들어서 <see cref="redOverlay"/> 에 연결
/// </summary>
[DisallowMultipleComponent]
public class HitFeedbackController : MonoBehaviour
{
    // ============================================================
    // Inspector
    // ============================================================
    [Header("Source (auto-find if null)")]
    [Tooltip("HP 변화를 구독할 캐릭터 모델. 비워두면 같은 GameObject 에서 자동 검색.")]
    [SerializeField] private UnifiedCharacterModel characterModel;

    [Header("Camera Shake")]
    [Tooltip("Cinemachine Impulse 를 발생시키는 소스. 비워두면 같은 GameObject 에서 자동 검색.")]
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Tooltip("기본 흔들림 강도(피해량 1 기준). 피해량에 비례해 스케일됨.")]
    [SerializeField] private float shakeForce = 0.4f;

    [Tooltip("최대 흔들림 강도 클램프.")]
    [SerializeField] private float maxShakeForce = 2.0f;

    [Tooltip("true 면 피해량에 비례해 흔들림이 강해진다. false 면 항상 shakeForce 만큼 흔들림.")]
    [SerializeField] private bool scaleByDamage = true;

    [Tooltip("scaleByDamage 가 true 일 때 사용. (피해량 * 이 값) 이 최종 강도.")]
    [SerializeField] private float damageToShakeRatio = 0.04f;

    [Header("Red Overlay")]
    [Tooltip("풀스크린 붉은 UI Image. 비워두면 붉은 효과는 동작하지 않음.")]
    [SerializeField] private Image redOverlay;

    [Tooltip("피격 시 도달할 최대 알파(0~1).")]
    [Range(0f, 1f)]
    [SerializeField] private float flashMaxAlpha = 0.55f;

    [Tooltip("페이드 인 시간(초). 짧을수록 충격이 강하게 느껴짐.")]
    [SerializeField] private float fadeInDuration = 0.05f;

    [Tooltip("최대 알파를 유지하는 시간(초).")]
    [SerializeField] private float holdDuration = 0.08f;

    [Tooltip("페이드 아웃 시간(초).")]
    [SerializeField] private float fadeOutDuration = 0.35f;

    [Tooltip("true 면 큰 피해일수록 더 진한 붉은색이 깔린다.")]
    [SerializeField] private bool scaleAlphaByDamage = true;

    [Tooltip("scaleAlphaByDamage 가 true 일 때 사용. (피해량 * 이 값) 이 알파.")]
    [SerializeField] private float damageToAlphaRatio = 0.02f;

    // ============================================================
    // 내부 상태
    // ============================================================
    private float lastHealth;
    private bool subscribed;
    private Coroutine flashRoutine;

    // ============================================================
    // 라이프사이클
    // ============================================================
    private void Awake()
    {
        if (characterModel == null) characterModel = GetComponent<UnifiedCharacterModel>();
        if (impulseSource == null)  impulseSource  = GetComponent<CinemachineImpulseSource>();
    }

    private void OnEnable()
    {
        // 로컬에서 조작하는 캐릭터에만 피드백을 띄운다.
        // 네트워크 모드라면 isLocalPlayer 가 true 가 된 이후에야 의미가 있으므로,
        // 한 프레임 뒤에 다시 확인하는 코루틴을 돌린다.
        StartCoroutine(InitWhenLocal());
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        if (redOverlay != null) SetOverlayAlpha(0f);
    }

    private IEnumerator InitWhenLocal()
    {
        // 네트워크 스폰 → isLocalPlayer 가 true 가 되기까지 한두 프레임 걸릴 수 있음.
        // 최대 2초까지 기다린다.
        float t = 0f;
        while (t < 2f)
        {
            if (AuthorityGuard.IsLocallyControlled(gameObject))
            {
                Subscribe();
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }
        // 로컬 플레이어가 아니면 그냥 종료. 다른 캐릭터의 피격은 화면 연출을 띄우지 않음.
    }

    private void Subscribe()
    {
        if (subscribed || characterModel == null) return;
        lastHealth = characterModel.CurrentHealth;
        characterModel.OnHealthChanged += HandleHealthChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || characterModel == null) return;
        characterModel.OnHealthChanged -= HandleHealthChanged;
        subscribed = false;
    }

    // ============================================================
    // 이벤트 핸들러
    // ============================================================
    private void HandleHealthChanged(float newHealth)
    {
        float delta = lastHealth - newHealth; // 양수면 피해를 입은 것
        lastHealth = newHealth;

        // 회복이거나 동일 → 무시
        if (delta <= 0.0001f) return;

        TriggerShake(delta);
        TriggerRedFlash(delta);
    }

    // ============================================================
    // 카메라 흔들림
    // ============================================================
    private void TriggerShake(float damage)
    {
        if (impulseSource == null) return;

        float force = scaleByDamage ? damage * damageToShakeRatio : shakeForce;
        force = Mathf.Clamp(force, 0f, maxShakeForce);
        if (force <= 0f) return;

        // 방향은 랜덤. 화면 전체가 살짝 진동하는 느낌.
        Vector3 dir = Random.insideUnitSphere;
        impulseSource.GenerateImpulseWithVelocity(dir * force);
    }

    // ============================================================
    // 붉은 화면 효과
    // ============================================================
    private void TriggerRedFlash(float damage)
    {
        if (redOverlay == null) return;

        float targetAlpha = scaleAlphaByDamage
            ? Mathf.Clamp(damage * damageToAlphaRatio, 0f, flashMaxAlpha)
            : flashMaxAlpha;

        if (targetAlpha <= 0.001f) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(targetAlpha));
    }

    private IEnumerator FlashRoutine(float targetAlpha)
    {
        // 페이드 인
        float startAlpha = redOverlay.color.a;
        float t = 0f;
        if (fadeInDuration > 0f)
        {
            while (t < fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                SetOverlayAlpha(Mathf.Lerp(startAlpha, targetAlpha, t / fadeInDuration));
                yield return null;
            }
        }
        SetOverlayAlpha(targetAlpha);

        // 유지
        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        // 페이드 아웃
        t = 0f;
        if (fadeOutDuration > 0f)
        {
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                SetOverlayAlpha(Mathf.Lerp(targetAlpha, 0f, t / fadeOutDuration));
                yield return null;
            }
        }
        SetOverlayAlpha(0f);
        flashRoutine = null;
    }

    private void SetOverlayAlpha(float a)
    {
        if (redOverlay == null) return;
        Color c = redOverlay.color;
        c.a = a;
        redOverlay.color = c;
    }

    // ============================================================
    // 외부에서 직접 트리거하고 싶을 때(예: 폭발 데미지 외 폴존 등)
    // ============================================================
    public void PlayHitFeedback(float damage)
    {
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
        TriggerShake(damage);
        TriggerRedFlash(damage);
    }
}
