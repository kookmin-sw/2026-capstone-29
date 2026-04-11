using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class LocalDagger : MonoBehaviour, IWeaponHitBox, IPlayerWeapon
{

    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("히트박스")]
    [Tooltip("단검의 히트박스. 비워두면 자신에서 자동 탐색.")]
    [SerializeField] private CharacterHitBox weaponHitbox;

    [SerializeField] private GameObject owner;

    //던지기 관련 변수
    private bool thrown = false;
    private Vector3 flyDirection;
    private float flySpeed;


    private float lifeTimer;

    private void Awake()
    {
        if (weaponHitbox == null)
        {
            weaponHitbox = GetComponent<CharacterHitBox>();
        }

        if (weaponHitbox != null)
        {
            weaponHitbox.damage = itemStat.damage;
            weaponHitbox.EnableHitbox();
        }
        flySpeed = itemStat.speed;
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer > itemStat.availableTime)
        {
            Destroy(this.gameObject);
            return;
        }

        //던짐 상태가 되었을 경우에만 앞으로 나아감.
        if (thrown)
        {
            transform.position += flyDirection * flySpeed * Time.deltaTime;
            return;
        }
    }

    public void SetThrowDagger(Vector3 direction)
    {
        thrown = true;
        flyDirection = direction.normalized;
    }

    public void SetOwner(GameObject user) { owner = user; }
    public GameObject GetOwner() { return owner; }


    public void SetUser(GameObject user)
    {
        owner = user;
        WeaponEquipHandler handler = GetComponent<WeaponEquipHandler>();
        if (handler != null)
        {
            handler.Equip(user, "CombatGirls_Sword_Shield/root/add_weapon_r");
        }
    }
    public void ThrowWeapon()
    {
        // 던질 방향: 소유자의 전방
        Vector3 direction = owner != null
            ? owner.transform.forward
            : transform.forward;
        SetThrowDagger (direction);
    }
}
