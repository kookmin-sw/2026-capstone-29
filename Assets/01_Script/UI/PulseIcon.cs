using UnityEngine;

public class PulseIcon : MonoBehaviour
{
    [Header("Scaling Settings")]
    public float pulseSpeed = 3f;    // 크기 변화 속도
    public float minScale = 0.5f;    // 가장 작을 때의 배율
    public float maxScale = 1.0f;    // 가장 클 때의 배율

    private Vector3 initialScale;

    void Start()
    {
        initialScale = transform.localScale;
    }

    void Update()
    {
        float lerpStep = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;

        float currentScale = Mathf.Lerp(minScale, maxScale, lerpStep);

        transform.localScale = initialScale * currentScale;
    }
}