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
    [HideInInspector] public IField field;

    // bool 상태는 SyncVar로 클라이언트에 동기화 (UI 표시 등에 활용)
    [SyncVar] private bool hasWeapon = false;
    [SyncVar] private bool hasActive = false;
    [SyncVar] private bool activeUsed = false;
    [SyncVar] private bool hasPassive = false;
    [SyncVar] private bool passiveUsed = false;
    [SyncVar] private bool hasField = false;     
    [SyncVar] private bool fieldUsed = false;

    [SyncVar] public float weaponAvailable;
    [SyncVar] public float activeAvailable;
    [SyncVar] public float passiveAvailable;
    [SyncVar] public float fieldAvailable;

    [SerializeField] float weaponTimer;
    [SerializeField] float activeTimer;
    [SerializeField] float passiveTimer;
    [SerializeField] float fieldTimer;

    private Coroutine activeRoutine;
    private Coroutine passiveRoutine;
    private Coroutine fieldRoutine;

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
        if (activeUsed)
        {
            activeTimer += Time.deltaTime;
            if (activeAvailable <= activeTimer)
            {
                // 코루틴이 아직 돌고 있으면 멈추고 원복 호출
                if (activeRoutine != null)
                {
                    StopCoroutine(activeRoutine);
                    activeRoutine = null;
                    active?.OnDeactivate(gameObject);
                }

                activeUsed = false;
                hasActive = false;
                active = null;
                activeTimer = 0;
                activeAvailable = 0;
                RpcOnActiveRemoved();
            }
        }
        
        // 패시브는 장착 즉시 자동 발동: hasPassive가 되면 다음 프레임에 자동으로 코루틴 시작
        if (hasPassive && !passiveUsed && passive != null)
        {
            passiveUsed = true;
            passiveTimer = 0f;
            passiveRoutine = StartCoroutine(PassiveRoutineWrapper(passive.Activate(gameObject)));
            RpcOnPassiveActivated();
        }

        if (passiveUsed)
        {
            passiveTimer += Time.deltaTime;
            if (passiveAvailable <= passiveTimer)
            {
                if (passiveRoutine != null)
                {
                    StopCoroutine(passiveRoutine);
                    passiveRoutine = null;
                    passive?.OnDeactivate(gameObject);
                }

                passiveUsed = false;
                hasPassive = false;
                passive = null;
                passiveTimer = 0;
                passiveAvailable = 0;
                RpcOnPassiveRemoved();
            }
        }

        if (hasField && !fieldUsed && field != null)
        {
            fieldUsed = true;
            fieldTimer = 0f;
            fieldRoutine = StartCoroutine(FieldRoutineWrapper(field.Activate()));
            RpcOnFieldActivated();
        }

        if (fieldUsed)
        {
            fieldTimer += Time.deltaTime;
            if (fieldAvailable <= fieldTimer)
            {
                if (fieldRoutine != null)
                {
                    StopCoroutine(fieldRoutine);
                    fieldRoutine = null;
                    field?.OnDeactivate();
                }

                fieldUsed = false;
                hasField = false;
                field = null;
                fieldTimer = 0;
                fieldAvailable = 0;
                RpcOnFieldRemoved();
            }
        }


    }

    public void ApplyWeaponTimer(float available) { weaponAvailable = available; }
    public void ApplyActiveTimer(float available) { activeAvailable = available; }
    public void ApplyPassiveTimer(float available) { passiveAvailable = available; }
    public void ApplyFieldTimer(float available) { fieldAvailable = available; }


    public bool HasWeapon() => hasWeapon;
    public void GetWeapon() { hasWeapon = true; }

    public bool HasActive() => hasActive;
    public bool IsActiveRunning() => activeUsed;
    public void GetActive() { hasActive = true; }

    public bool HasPassive() => hasPassive;
    public bool IsPassiveRunning() => passiveUsed;
    public void GetPassive() { hasPassive = true; }

    public bool HasField() => hasField;
    public bool IsFieldRunning() => fieldUsed;
    public void GetField() { hasField = true; }

    public void RequestUseActive()
    {
        // 보유하지 않았거나 이미 사용 중이면 무시
        if (!hasActive || activeUsed) return;
        CmdUseActive();
    }

    [Command]
    private void CmdUseActive()
    {
        UseActive();
    }

    [Server]
    public void UseActive()
    {
        if (!hasActive || activeUsed || active == null) return;

        activeUsed = true;
        activeTimer = 0f;

        // IActive는 인터페이스라 StartCoroutine을 직접 못 가짐 → ItemManager가 대신 실행
        activeRoutine = StartCoroutine(active.Activate(gameObject));

        RpcOnActiveUsed();
    }

    private IEnumerator ActiveRoutineWrapper(IEnumerator inner)
    {
        yield return inner;
        activeRoutine = null;
    }

    private IEnumerator PassiveRoutineWrapper(IEnumerator inner)
    {
        yield return inner;
        passiveRoutine = null;
    }

    private IEnumerator FieldRoutineWrapper(IEnumerator inner)
    {
        yield return inner;
        fieldRoutine = null;
    }

    [ClientRpc]
    void RpcOnWeaponRemoved()
    {
        Debug.Log("무기 아이템 해제됨.");
    }

    [ClientRpc]
    void RpcOnActiveUsed()
    {
        Debug.Log("액티브 아이템 사용 시작.");
        // UI: 지속시간 게이지 시작 등
    }

    [ClientRpc]
    void RpcOnActiveRemoved()
    {
        Debug.Log("액티브 아이템 해제됨.");
    }
    
    [ClientRpc] 
    void RpcOnPassiveActivated() 
    { 
        Debug.Log("패시브 아이템 발동.");
    }
    
    [ClientRpc] 
    void RpcOnPassiveRemoved() 
    { 
        Debug.Log("패시브 아이템 해제됨."); 
    }

    [ClientRpc]
    void RpcOnFieldActivated()
    {
        Debug.Log("필드 아이템 발동 (장판 스폰).");
    }

    [ClientRpc]
    void RpcOnFieldRemoved()
    {
        Debug.Log("필드 아이템 해제됨.");
    }
}
