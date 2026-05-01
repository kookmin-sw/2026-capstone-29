// UnifiedItemPickUp.cs에서 해당 아이템 스포너가 아이템을 스폰했는지 확인하기 위한 장치. 마커라고 부르자.

using UnityEngine;

public class SpawnedItemOrigin : MonoBehaviour
{
    [Tooltip("이 아이템을 만든 스포너. 픽업 시 NotifyItemConsumed 를 호출할 대상.")]
    public ItemSpawner spawner;
}