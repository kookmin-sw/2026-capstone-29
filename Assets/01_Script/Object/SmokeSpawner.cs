using UnityEngine;

// 테스트용: 키를 누르면 연막 오브젝트 생성.

public class SmokeSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public KeyCode spawnKey = KeyCode.G;
    public Vector3 spawnOffset = new Vector3(0, 0, 3f);
    public Color smokeColor = Color.white;
    public float radius = 5f;
    public float lifetime = 10f;

    void Update()
    {
        if (Input.GetKeyDown(spawnKey))
        {
            SpawnSmoke();
        }
    }

    public void SpawnSmoke()
    {
        Vector3 pos = transform.position + transform.forward * spawnOffset.z
                     + transform.up * spawnOffset.y
                     + transform.right * spawnOffset.x;

        GameObject smokeObj = new GameObject("SmokeGrenade");
        smokeObj.transform.position = pos;

        SmokeGrenade smoke = smokeObj.AddComponent<SmokeGrenade>();
        smoke.smokeColor = smokeColor;
        smoke.maxRadius = radius;
        smoke.lifetime = lifetime;
    }
}
