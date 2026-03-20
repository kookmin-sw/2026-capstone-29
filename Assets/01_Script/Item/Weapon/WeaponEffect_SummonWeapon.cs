using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(menuName ="Item/Weapon/SummonWeapon")]
public class WeaponEffect_SummonWeapon : ScriptableObject, IWeapon
{
    [SerializeField] GameObject weapon;
    public float availableTime = 100f;
    public void SummonWeapon(Vector3 pos, Quaternion qt)
    {
        GameObject.Instantiate(weapon, pos, qt);
    }

    public float AvailableTime()
    {
        return availableTime;
    }
}
