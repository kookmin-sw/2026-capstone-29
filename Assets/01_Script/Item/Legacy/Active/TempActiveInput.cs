// 별도 입력 스크립트 사용. StartAssetsInput에 이런 식으로 입력 받으면 되는데, 현재는 반영 안해서 따로 빼둠. StartAssetsInput 연동 이후 제거해야함.

using UnityEngine;
using Mirror;

public class ActiveItemInput : NetworkBehaviour
{
    [SerializeField] private KeyCode useActiveKey = KeyCode.Q;
    private ItemManager itemManager;

    void Awake()
    {
        itemManager = GetComponent<ItemManager>();
    }

    void Update()
    {
        // 로컬 플레이어만 입력 처리
        if (!isLocalPlayer) return;
        if (itemManager == null) return;

        if (Input.GetKeyDown(useActiveKey))
        {
            itemManager.RequestUseActive();
        }
    }
}