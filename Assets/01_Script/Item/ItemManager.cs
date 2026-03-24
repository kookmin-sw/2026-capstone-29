using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//아이템의 인터페이스를 받아 관리. 저장받을 때 유효시간을 받고(_Available), 타이머로(_Timer)로 유효 시간이 경과한 것을 확인하면 아이템을 제거한다.
public class ItemManager : NetworkBehaviour
{
    // 인터페이스는 SyncVar 불가 → 서버에서만 관리
    [HideInInspector] public IWeapon weapon;
    [HideInInspector] public IActive active;
    [HideInInspector] public IPassive passive;

    // bool 상태는 SyncVar로 클라이언트에 동기화 (UI 표시 등에 활용)
    [SyncVar] private bool hasWeapon = false;
    [SyncVar] private bool hasActive = false;
    [SyncVar] private bool hasPassive = false;

    [SyncVar] public float weaponAvailable;
    [SyncVar] public float activeAvailable;

    [SerializeField] float weaponTimer;
    [SerializeField] float activeTimer;

    void Update()
    {
        // 타이머 관리는 서버에서만 처리
        if (!isServer) return;

        //각 아이템들의 인터페이스가 null이 아닌 경우에, 각 아이템의 유효시간 만료 여부를 검사하는 타이머 작동.(코루틴으로 리펙토링할 예정)
        if (hasWeapon)
        {
            weaponTimer += Time.deltaTime;
            if (weaponAvailable <= weaponTimer)
            {
                Debug.Log("아이템 해제.");
                hasWeapon = false;
                weapon = null;
                weaponTimer = 0;
                weaponAvailable = 0;

                // 클라이언트에도 해제 알림
                RpcOnWeaponRemoved();
            }
            else
            {
                //Debug.Log(weapon != null);
            }
        }
        if (hasActive) 
        {
            activeTimer += Time.deltaTime;
            if (activeAvailable <= activeTimer)
            {
                hasActive = false;
                active = null;
                activeTimer = 0;
                activeAvailable = 0;

                //클라이언트에 전달
                RpcOnActiveRemoved();
            }
        }
        if (hasPassive)
        {
            //패시브는 얻는 즉시 효과를 발동한 후, 제거해버린다.
            passive.Apply();
            hasPassive = false;
            passive = null;

            //클라이언트에 전달.
            RpcOnPassiveUsed();
        }
        

    }

    public void ApplyWeaponTimer(float available)
    {
        weaponAvailable = available;
    }
    public void ApplyActiveTimer(float available)
    {
        activeAvailable = available;
    }

    public bool HasWeapon()
    {
        return hasWeapon;
    }

    public void GetWeapon()
    {
        hasWeapon = true;
    }

    public bool HasActive() 
    {
        return hasActive;
    }
    public void GetActive()
    {
        hasActive = true;
    }

    public bool HasPassive()
    {
        return hasPassive;
    }
    public void GetPassive()
    {
        hasPassive = true;
    }

    [ClientRpc]
    void RpcOnWeaponRemoved()
    {
        Debug.Log("무기 아이템 해제됨.");
        //  UI 갱신 (무기 아이콘 제거 등)등의 작업 추가
    }

    [ClientRpc]
    void RpcOnActiveRemoved()
    {
        Debug.Log("액티브 아이템 해제됨.");
        // UI 갱신 등의 작업 추가
    }

    [ClientRpc]
    void RpcOnPassiveUsed()
    {
        Debug.Log("패시브 아이템 사용됨.");
        // TODO: UI 갱신, 이펙트 재생 등 작업 추가
    }

}
