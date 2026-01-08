using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class TempDynamicObj : MonoBehaviour
{
    private float amplitude = 0.3f;
    [SerializeField] public float speed = 8f;
    [SerializeField] float lifeTime = 8f;
    private float timer = 0;

    private Vector2 startPos;

    private void Start()
    {
        startPos = this.transform.position;
    }
    void Update()
    {
        float newY = startPos.y + Mathf.Sin(Time.time * speed) * amplitude;
        transform.position = new Vector2(startPos.x, newY + 0.15f);
        timer += Time.deltaTime;
        if (timer > lifeTime)
        {
            Destroy(gameObject);
            return;
        }

    }
}
