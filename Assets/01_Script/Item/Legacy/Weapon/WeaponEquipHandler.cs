using UnityEngine;

public class WeaponEquipHandler : MonoBehaviour
{
    [Header("장착 슬롯")]
    [SerializeField] private WeaponSlot attachSlot = WeaponSlot.RightHand;

    [Tooltip("장착 중 해당 슬롯의 기본 무기를 비활성화할지")]
    [SerializeField] private bool hideDefaultOnAttachSlot = true;

    [Header("추적 오프셋")]
    public Vector3 positionOffset = new Vector3(-0.084f, 0.053f, 0.058f);
    public Vector3 rotationOffset = Vector3.zero;

    [Header("히트박스")]
    [SerializeField] private CharacterHitBox weaponHitbox;

    private Transform followTarget;
    private UnifiedCharacterView cachedView;
    private CharacterHitBox originalRightHitbox;
    private GameObject hiddenWeaponObj;
    private bool isEquipped;

    public void Equip(GameObject owner)
    {
        if (owner == null) return;

        // 소켓 탐색
        var attacher = owner.GetComponent<WeaponAttacher>();
        if (attacher == null)
            attacher = owner.GetComponentInChildren<WeaponAttacher>();
        if (attacher == null)
        {
            Debug.LogWarning($"[{name}] WeaponAttacher 를 찾지 못함.");
            return;
        }

        // 부착 소켓
        followTarget = attacher.GetSocket(attachSlot);
        if (followTarget == null)
        {
            Debug.LogWarning($"[{name}] 슬롯 {attachSlot} 의 본이 세팅되지 않음.");
            return;
        }
        Debug.Log($"[{name}] {followTarget.name} 에 장착!");

        // 캐릭터 뷰 참조
        cachedView = owner.GetComponent<UnifiedCharacterView>();
        if (cachedView == null)
            cachedView = owner.GetComponentInChildren<UnifiedCharacterView>();

        // 히트박스 교체
        if (cachedView != null)
        {
            originalRightHitbox = cachedView.rightHandHitbox;
            if (weaponHitbox == null)
                weaponHitbox = GetComponentInChildren<CharacterHitBox>(true);

            if (weaponHitbox != null)
            {
                cachedView.rightHandHitbox = weaponHitbox;
                weaponHitbox.DisableHitbox();
            }
        }

        // 기본 무기 숨김
        if (hideDefaultOnAttachSlot)
        {
            hiddenWeaponObj = attacher.GetDefaultWeapon(attachSlot);
            if (hiddenWeaponObj != null && hiddenWeaponObj != gameObject)
                hiddenWeaponObj.SetActive(false);
            else
                hiddenWeaponObj = null;
        }

        isEquipped = true;
    }

    public void Unequip()
    {
        if (!isEquipped) return;

        if (cachedView != null && originalRightHitbox != null)
            cachedView.rightHandHitbox = originalRightHitbox;

        if (hiddenWeaponObj != null)
            hiddenWeaponObj.SetActive(true);

        followTarget = null;
        cachedView = null;
        originalRightHitbox = null;
        hiddenWeaponObj = null;
        isEquipped = false;
    }

    private void LateUpdate()
    {
        if (followTarget == null) return;
        transform.position = followTarget.position
            + followTarget.TransformDirection(positionOffset);
        transform.rotation = followTarget.rotation
            * Quaternion.Euler(rotationOffset);
    }

    private void OnDestroy()
    {
        if (isEquipped) Unequip();
    }
}