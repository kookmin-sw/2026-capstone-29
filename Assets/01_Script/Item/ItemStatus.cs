using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName = "Item/Status")]
public class ItemStatus : ScriptableObject
{
    [SerializeField] public float availableTime; // 사용 가능 시간
    [SerializeField] public float useableTime; // 사용 가능 횟수

    [SerializeField] public float damage; // 대미지
    [SerializeField] public float range; // 거리. 이는 원거리 발사체에 대해 적용
    [SerializeField] public float projectileDrop; // 낙차
    [SerializeField] public float startUpDelay; // 선딜
    [SerializeField] public float RecoveryDelay; // 후딜

    [SerializeField] public bool electroShock; // 감전 cc
    [SerializeField] public bool knockBack; // 넉백 cc
    [SerializeField] public bool stop; // 저지 cc



}
