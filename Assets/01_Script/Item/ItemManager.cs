using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//아이템의 인터페이스를 받아 관리. 저장받을 때 유효시간을 받고(_Available), 타이머로(_Timer)로 유효 시간이 경과한 것을 확인하면 아이템을 제거한다.
public class ItemManager : MonoBehaviour
{

    [SerializeField] public IWeapon weapon;
    [SerializeField] public IActive active;
    [SerializeField] public IPassive passive;

    private bool hasWeapon = false;
    private bool hasActive = false;
    private bool hasPassive = false;

    [SerializeField] public float weaponAvailable;
    [SerializeField] public float activeAvailable;

    [SerializeField] float weaponTimer;
    [SerializeField] float activeTimer;

    void Update()
    {
        //각 아이템들의 인터페이스가 null이 아닌 경우에, 각 아이템의 유효시간 만료 여부를 검사하는 타이머 작동.(코루틴으로 리펙토링할 예정)
        if (hasWeapon)
        {
            weaponTimer += Time.deltaTime;
            if (weaponAvailable <= weaponTimer)
            {
                hasWeapon = false;
                weapon = null;
                weaponTimer = 0;
                weaponAvailable = 0;
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
            }
        }
        if (hasPassive)
        {
            //패시브는 얻는 즉시 효과를 발동한 후, 제거해버린다.
            passive.Apply();
            hasPassive = false;
            passive = null;
        }

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
}
