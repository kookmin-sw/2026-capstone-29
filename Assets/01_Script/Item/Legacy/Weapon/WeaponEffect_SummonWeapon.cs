using UnityEngine;

[CreateAssetMenu(menuName = "Item/Weapon/SummonWeapon")]
public class WeaponEffect_SummonWeapon : ScriptableObject, IWeapon
{
    [SerializeField] GameObject weapon;
    public float availableTime = 100f;

    [Header("UI")]
    public Sprite uiSprite;


    public GameObject SummonWeapon(Vector3 pos, Quaternion qt)
    {
        // Instantiate만 하고 반환
        return GameObject.Instantiate(weapon, pos, qt);
    }

    public float AvailableTime()
    {
        return availableTime;
    }

    public Sprite UISprite => uiSprite;
}