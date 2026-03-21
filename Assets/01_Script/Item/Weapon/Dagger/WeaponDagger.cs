using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponDagger : MonoBehaviour
{

    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    private float lifeTimer;
    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer > itemStat.availableTime)
        {
            Destroy(this.gameObject);
            return;
        }

        if (Input.GetKeyDown("V"))
        {
            ThrowDagger();
        }
    }

    private void ThrowDagger()
    {
        Debug.Log("단검 던지기!");
    }
}
