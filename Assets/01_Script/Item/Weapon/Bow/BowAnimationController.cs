using UnityEngine;

public class BowAnimationController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("활 오브젝트의 Animator. 비워두면 자동으로 찾음.")]
    [SerializeField] private Animator bowAnimator;

    // Animator 파라미터 이름을 해시로 캐싱 (문자열 비교보다 빠름)
    private static readonly int PullHash = Animator.StringToHash("Pull");

    private bool isPulling;

    private void Awake()
    {
        if (bowAnimator == null)
            bowAnimator = GetComponent<Animator>();

        if (bowAnimator == null)
            bowAnimator = GetComponentInChildren<Animator>();

        if (bowAnimator == null)
            Debug.LogError($"[BowAnimationController] {gameObject.name} 없음.");
    }

    private void Update()
    {
        if (bowAnimator == null) return;

        HandleInput();
    }

    private void HandleInput()
    {
        // 좌클릭 누르면 당기기 시작
        if (Input.GetMouseButtonDown(0) && !isPulling)
        {
            StartPull();
        }
        // 좌클릭 떼면 발사
        else if (Input.GetMouseButtonUp(0) && isPulling)
        {
            ReleasePull();
        }
    }

    // 시위 당기기 시작. idle → pull_start → pull_loop 전환.

    public void StartPull()
    {
        isPulling = true;
        bowAnimator.SetBool(PullHash, true);
    }

    // 시위 놓기. pull_loop → pull_end → idle 전환.
    public void ReleasePull()
    {
        isPulling = false;
        bowAnimator.SetBool(PullHash, false);
    }

    // 현재 시위를 당기고 있는지 여부. 외부에서 참조할 때 사용.
    // (예: 화살 발사 타이밍 체크, UI 표시 등)
    public bool IsPulling => isPulling;
}