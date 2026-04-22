using UnityEngine;

public enum WeaponSlot
{
    RightHand,
    LeftHand,
}

public class WeaponAttacher : MonoBehaviour
{
    [Header("붙일 본 이름")]
    public string rightHandBoneName = "hand_r";
    public string leftHandBoneName = "hand_l";

    [Header("기본 무기 오브젝트")]
    public Transform addWeaponR;
    public Transform addWeaponL;

    // 스폰 시 찾은 본과 기본 무기를 슬롯별로 캐시
    private Transform rightHandBone;
    private Transform leftHandBone;

    private void Start()
    {
        rightHandBone = AttachToBone(addWeaponR, rightHandBoneName);
        leftHandBone = AttachToBone(addWeaponL, leftHandBoneName);
    }

    /// 슬롯에 해당하는 본 Transform 반환, 없으면 null.
    public Transform GetSocket(WeaponSlot slot)
    {
        switch (slot)
        {
            case WeaponSlot.RightHand: return rightHandBone;
            case WeaponSlot.LeftHand: return leftHandBone;
        }
        return null;
    }

    // 슬롯에 붙어 있던 기본 무기 GameObject 반환. 없으면 null
    public GameObject GetDefaultWeapon(WeaponSlot slot)
    {
        switch (slot)
        {
            case WeaponSlot.RightHand: return addWeaponR != null ? addWeaponR.gameObject : null;
            case WeaponSlot.LeftHand: return addWeaponL != null ? addWeaponL.gameObject : null;
        }
        return null;
    }

    private Transform AttachToBone(Transform weapon, string boneName)
    {
        if (weapon == null) return null;

        Transform bone = FindBoneRecursive(transform, boneName);
        if (bone == null)
        {
            Debug.LogWarning($"본을 찾을 수 없음: {boneName}");
            return null;
        }

        Quaternion originalRotation = weapon.rotation;
        Vector3 originalPosition = weapon.position;

        weapon.SetParent(bone, worldPositionStays: true);
        weapon.rotation = originalRotation;
        weapon.position = originalPosition;

        return bone;
    }

    private Transform FindBoneRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindBoneRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }
}