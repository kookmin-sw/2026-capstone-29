using UnityEngine;

public class ObjectRotator : MonoBehaviour
{
    [Header("회전 설정")]
    [Tooltip("초당 회전할 속도입니다.")]
    public Vector3 rotationSpeed = new Vector3(0, 50, 0);

    [Tooltip("월드 좌표 기준인지, 오브젝트 자체 좌표 기준인지 설정합니다.")]
    public Space rotationSpace = Space.Self;

    void Update()
    {
        // 시간을 곱해줘야 프레임에 상관없이 일정한 속도로 회전합니다.
        transform.Rotate(rotationSpeed * Time.deltaTime, rotationSpace);
    }
}