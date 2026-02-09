using Mirror;
using UnityEngine;

public class BulletScript : NetworkBehaviour
{
    public GameObject owner;

    // 시작하자마자 서버에서 실행됨
    public override void OnStartServer()
    {
        // 2초 뒤에 스스로 파괴되도록 예약 (못 맞춰도 사라져야 하니까)
        Invoke(nameof(DestroySelf), 2.0f);
    }

    // [ServerCallback]: 물리 충돌 감지는 서버에서만 처리
    [ServerCallback] 
    void OnCollisionEnter(Collision collision)
    {
        // 본인은 무시
        if(collision.gameObject == owner) return;

        // 부딪힌 물체에서 'PlayerHealth' 스크립트를 찾기
        GameObject hitObject = collision.gameObject;
        PlayerHealth health = hitObject.GetComponent<PlayerHealth>();

        // 만약 플레이어라면 데미지를 준다
        if (health != null)
        {
            health.TakeDamage(10); // 10만큼 아프게
        }

        // 맞췄으니 총알은 사라진다
        DestroySelf();
    }

    [Server]
    void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }
}