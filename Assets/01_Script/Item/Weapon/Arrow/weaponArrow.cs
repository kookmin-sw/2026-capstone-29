using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponArrow : MonoBehaviour
{
    [SerializeField] public ItemStatus itemStat;
    private float timer;
    
    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= itemStat.availableTime)
        {
            Destroy(this.gameObject);
            return;
        }
    }
}
