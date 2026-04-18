using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponAttacher : MonoBehaviour
{
    [Header("붙일 본 이름")]
    public string rightHandBoneName = "hand_r";
    public string leftHandBoneName  = "hand_l";

    [Header("무기 오브젝트")]
    public Transform addWeaponR;
    public Transform addWeaponL;

    private void Start()
    {
        AttachTobone(addWeaponR, rightHandBoneName);
        AttachTobone(addWeaponL, leftHandBoneName);
    }

    private void AttachTobone(Transform weapon, string boneName)
    {
        if (weapon == null) return;

        // 전체 자식 본에서 이름으로 검색
        Transform bone = FindBoneRecursive(transform, boneName);
        if (bone == null)
        {
            Debug.LogWarning($"본을 찾을 수 없음: {boneName}");
            return;
        }
        Quaternion originalRotation = weapon.rotation;
        Vector3 originalPosition = weapon.position;

        // 월드 포지션 유지하면서 부모 변경
        weapon.SetParent(bone, worldPositionStays: true);
        weapon.rotation = originalRotation;
        weapon.position = originalPosition;
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