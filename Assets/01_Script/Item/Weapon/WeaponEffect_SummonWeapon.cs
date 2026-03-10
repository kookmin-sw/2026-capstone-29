using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName ="Item/Weapon/SummonWeapon")]
public class WeaponEffect_SummonWeapon : ScriptableObject, IWeapon
{
    [SerializeField] GameObject weapon;
    public float availableTime = 100f;
    public void Attack()
    {
        GameObject.Instantiate(weapon);
    }

    public float AvailableTime()
    {
        return availableTime;
    }
}
