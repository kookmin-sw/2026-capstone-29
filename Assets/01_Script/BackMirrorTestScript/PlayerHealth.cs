using Mirror;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    public const int maxHealth = 100;

    // [SyncVar]: 이 변수가 서버에서 변하면 모든 클라이언트의 화면에서도 자동으로 변한다
    [SyncVar]
    public int currentHealth = maxHealth;


    private SendDataToServer sendData; // 체력과 점수를 서버로 넘기기 위한 스크립트 부착

    private void Start()
    {
        sendData = GetComponent<SendDataToServer>();
    }

    // 데미지 처리는 중요한 로직이므로 반드시 [Server]에서만 실행
    public void TakeDamage(int amount)
    {
        if (!isServer) return; // 서버가 아니면 무시

        currentHealth -= amount;
        Debug.Log($"현재 체력: {currentHealth}");
        sendData.UpdateCurHealth(currentHealth); // IngameData에 현재 체력 전달


        if (currentHealth <= 0)
        {
            // 상대방에게 점수 추가 (내가 호스트면 클라이언트에게 점수)
            sendData.UpdateScore();

            currentHealth = maxHealth; // 체력 초기화

            // 체력 초기화도 IngameData에 반영
            sendData.UpdateCurHealth(currentHealth);

            RpcRespawn(); // "리스폰 위치로 이동해라"라고 클라이언트에게 명령
        }

    }

    // [ClientRpc]: 서버가 호출하고, 실행은 모든 클라이언트(특히 당사자)의 컴퓨터에서 된다
    [ClientRpc]
    void RpcRespawn()
    {
        // 시작 지점(0, 0, 0)으로 이동
        transform.position = new Vector3(0, 1, 0);
    }
}