using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName = "Item/Status")]
public class ItemStatus : ScriptableObject
{
    [Header("아이템 유효기간")]
    [Tooltip("availableTime을 참조하여 플레이어가 아이템을 소유할 수 있는 시간을 정한다")]
    [SerializeField] public float availableTime;

    [Header("아이템 사용 가능 횟수")]
    [Tooltip("useableTime을 참조하여 아이템을 일정 횟수만 사용할 수 있게끔 조정")]
    [SerializeField] public float useableTime; // 사용 가능 횟수

    [SerializeField] public float damage; // 대미지

    [SerializeField] public float range; // 거리. 이는 원거리 발사체에 대해 적용
    [SerializeField] public float projectileDrop; // 낙차
    [SerializeField] public float startUpDelay; // 선딜

    [Header("아이템 쿨타임")]
    [Tooltip("RecoveryDelay를 참조하여 아이템을 사용한 후 일정 시간 뒤에 사용할 수 있게끔 조정")]
    [SerializeField] public float RecoveryDelay; // 후딜

    [SerializeField] public bool Stun; // 감전 cc 및 스턴에 적용
    [SerializeField] public bool knockBack; // 넉백 cc
    [SerializeField] public bool stop; // 저지 cc



}
