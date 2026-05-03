using UnityEngine;


// 검집 오브젝트의 위치 오프셋을 부여하는 스크립트.
[DefaultExecutionOrder(100)] // 애니메이션이 본을 갱신한 뒤 실행되도록 하기 위한 장치.
public class Sheath : MonoBehaviour
{
    [Header("소속 캐릭터의 WeaponAttacher")]
    [Tooltip("비워두면 부모 계층에서 자동으로 찾는다.")]
    public WeaponAttacher attacher;

    [Header("따라갈 슬롯")]
    public WeaponSlot followSlot = WeaponSlot.LeftHand;

    [Header("오프셋 (본 기준 로컬 좌표)")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;

    [Header("디버그")]
    [Tooltip("Scene 뷰에서 본 위치를 시각화.")]
    public bool drawDebugGizmo = false;

    private Transform targetBone;

    private void Awake()
    {
        if (attacher == null)
            attacher = GetComponentInParent<WeaponAttacher>();
    }

    private void Start()
    {
        ResolveBone();
    }

    private void ResolveBone()
    {
        if (attacher == null)
        {
            Debug.LogWarning("[ScabbardFollower] WeaponAttacher 참조 없음");
            return;
        }

        targetBone = attacher.GetSocket(followSlot);

        if (targetBone == null)
            Debug.LogWarning($"[ScabbardFollower] 슬롯 본을 찾을 수 없음: {followSlot}");
    }

    // 애니메이션 시스템이 본을 업데이트한 후에 LateUpdate에서 본 좌표를 추적.
    private void LateUpdate()
    {
        if (targetBone == null)
        {
            ResolveBone();
            if (targetBone == null) return;
        }

        Quaternion worldRotation = targetBone.rotation * Quaternion.Euler(rotationOffset);
        Vector3 worldPosition = targetBone.position + targetBone.rotation * positionOffset;

        transform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    //참조하는 본 변경시 호출한다.
    public void SetFollowSlot(WeaponSlot slot)
    {
        followSlot = slot;
        ResolveBone();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmo || targetBone == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetBone.position, 0.03f);
        Gizmos.DrawLine(targetBone.position, transform.position);
    }
}