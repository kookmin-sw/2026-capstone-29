using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
    [SerializeField] private float EffectShow = 3f;
    [SerializeField] private float RemoveColliderTime = 1f;
    private bool hasColider = true;
    private float timer;

    private ParticleSystem ps;
    private Color startColor;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps != null)
        {
            startColor = ps.main.startColor.color;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= RemoveColliderTime)
        {
            RemoveCollider();
        }

        if (ps != null)
        {
            float alpha = Mathf.Lerp(1f, 0f, timer / EffectShow);
            var main = ps.main;
            Color newColor = startColor;
            newColor.a = alpha;
            main.startColor = newColor;
        }

        if (timer > EffectShow)
        {
            Destroy(gameObject);
        }
    }

    private void RemoveCollider()
    {
        if (hasColider)
        {
            SphereCollider col = gameObject.GetComponent<SphereCollider>();
            col.enabled = false;
            hasColider = false;
        }
    }
}