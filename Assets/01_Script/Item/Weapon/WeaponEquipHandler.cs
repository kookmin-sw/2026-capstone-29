using UnityEngine;
/*  
    1. LateUpdate에서 플레이어 소켓의 위치/회전을 추적 (SetParent 없이)
    2. 장착 시 CharacterView의 히트박스를 무기의 것으로 교체, 기존 무기 비활성화
    3. 해제 시 원래 히트박스 복원, 기존 무기 재활성화
*/
public class WeaponEquipHandler : MonoBehaviour
{
    [Header("추적 설정")]
    [Tooltip("소켓 기준 위치 오프셋")]
    public Vector3 positionOffset = new Vector3(-0.084f, 0.053f, 0.058f);

    [Tooltip("소켓 기준 회전 오프셋 (Euler)")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("히트박스")]
    [Tooltip("이 무기의 히트박스. 비워두면 자식에서 자동 탐색.")]
    [SerializeField] private CharacterHitBox weaponHitbox;

    // 추적 대상-오브젝트의 포지션
    [SerializeField] private Transform followTarget;

    // 백업
    private CharacterView cachedView;
    private CharacterHitBox originalRightHitbox;
    private GameObject originalWeaponObj;
    private bool isEquipped;

    // 무기 장착. 소켓 추적 시작 + 히트박스 교체 + 기존 무기 비활성화.
    public void Equip(GameObject owner, string socketPath)
    {
        if (owner == null) return;

        //소켓 탐색
        followTarget = owner.transform.Find(socketPath);

        Debug.Log($"{followTarget.name}에 장착 시도!");
        if (followTarget == null)
        {
            Debug.LogWarning($"[WeaponEquipHandler] 소켓을 찾을 수 없습니다: {socketPath}");
            return;
        }
        Debug.Log($"{followTarget.name}에 장착!");

        
        //CharacterView 참조
        cachedView = owner.GetComponent<CharacterView>();
        if (cachedView == null) return;

        //히트박스 교체
        originalRightHitbox = cachedView.rightHandHitbox;

        if (weaponHitbox == null)
            weaponHitbox = GetComponentInChildren<CharacterHitBox>();

        if (weaponHitbox != null)
        {
            cachedView.rightHandHitbox = weaponHitbox;
            weaponHitbox.DisableHitbox();
        }

        //기존 무기 비활성화
        // 소켓 하위에서 자신이 아닌 활성 오브젝트를 찾아 비활성화
        foreach (Transform child in followTarget)
        {
            if (child.gameObject != gameObject && child.gameObject.activeSelf)
            {
                originalWeaponObj = child.gameObject;
                originalWeaponObj.SetActive(false);
                break;
            }
        }

        isEquipped = true;
    }

    // 무기 해제. 히트박스 복원 + 기존 무기 재활성화 + 추적 해제.
    public void Unequip()
    {
        if (!isEquipped) return;

        //히트박스 복원
        if (cachedView != null && originalRightHitbox != null)
        {
            cachedView.rightHandHitbox = originalRightHitbox;
        }

        //기존 무기 복원
        if (originalWeaponObj != null)
        {
            originalWeaponObj.SetActive(true);
        }

        // 참조 정리
        followTarget = null;
        cachedView = null;
        originalRightHitbox = null;
        originalWeaponObj = null;
        isEquipped = false;
    }

    // Animator 적용 이후에 소켓 위치를 추적한다.
    private void LateUpdate()
    {
        if (followTarget == null) return;

        transform.position = followTarget.position
            + followTarget.TransformDirection(positionOffset);
        transform.rotation = followTarget.rotation
            * Quaternion.Euler(rotationOffset);
    }

    // 예외 상황(씬 전환 등)에서도 복원을 보장한다.
    private void OnDestroy()
    {
        if (isEquipped)
            Unequip();
    }
}