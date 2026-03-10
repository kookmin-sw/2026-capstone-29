using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponBow : MonoBehaviour
{
    [SerializeField] public ItemStatus itemStat;
    [SerializeField] GameObject arrow;

    private void Start()
    {
        GameObject.Instantiate(arrow);
    }
}
