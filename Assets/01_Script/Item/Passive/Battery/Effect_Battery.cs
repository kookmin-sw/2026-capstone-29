using UnityEngine;

[CreateAssetMenu(menuName = "Item/Passive/Battery/Effect")]

public class Effect_Battery : ScriptableObject, IPassive
{
    public void Apply()
    {
        Debug.Log("배터리 효과 적용!");
    }
}
