using Mirror;
using UnityEngine;

public class IngameData : NetworkBehaviour
{
    [Header("게임 설정")]
    [SerializeField] private float timeLimit = 300f;

    [Header("UI")]
    [SerializeField] private ShowCurrentGame ui;

    [Header("점수")]
    [SyncVar(hook = nameof(OnHostScoreChanged))]
    private int score_host;

    [SyncVar(hook = nameof(OnClientScoreChanged))]
    private int score_client;

    [Header("체력")]
    [SyncVar(hook = nameof(OnHostHealthChanged))]
    private int curHealth_host;

    [SyncVar(hook = nameof(OnClientHealthChanged))]
    private int curHealth_client;

    [Header("시간")]
    [SyncVar(hook = nameof(OnPlayTimeChanged))]
    private float playTime = 0f;

    private bool gameEnded = false;

    public int ScoreHost => score_host;
    public int ScoreClient => score_client;
    public int HealthHost => curHealth_host;
    public int HealthClient => curHealth_client;
    public float PlayTime => playTime;
    public float TimeLimit => timeLimit;
    public float RemainingTime => Mathf.Max(0f, timeLimit - playTime);

    void Start()
    {
        // Inspector에서 연결 안 했을 경우 자동 탐색
        if (ui == null)
            ui = FindObjectOfType<ShowCurrentGame>();
    }

    void Update()
    {
        if (!isServer) return;
        if (gameEnded) return;

        playTime += Time.deltaTime;

        if (playTime >= timeLimit)
        {
            gameEnded = true;
            RpcGameEnd();
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdAddScore(bool isHost)
    {
        if (isHost)
            score_host++;
        else
            score_client++;
    }

    [Command(requiresAuthority = false)]
    public void CmdChangeCurHealth(bool isHost, int curHealth)
    {
        if (isHost)
            curHealth_host = curHealth;
        else
            curHealth_client = curHealth;
    }

    // === Hook 콜백: SyncVar 값 변경 시 모든 클라이언트에서 UI 갱신 ===

    void OnHostScoreChanged(int oldValue, int newValue)
    {
        if (ui != null) ui.ChangeHostScore(newValue);
    }

    void OnClientScoreChanged(int oldValue, int newValue)
    {
        if (ui != null) ui.ChangeClientScore(newValue);
    }

    void OnHostHealthChanged(int oldValue, int newValue)
    {
        if (ui != null) ui.ChangeHostHealth(newValue);
    }

    void OnClientHealthChanged(int oldValue, int newValue)
    {
        if (ui != null) ui.ChangeClientHealth(newValue);
    }

    void OnPlayTimeChanged(float oldValue, float newValue)
    {
        if (ui != null) ui.ChangeTimeLeft(Mathf.Max(0f, timeLimit - newValue));
    }

    [ClientRpc]
    void RpcGameEnd()
    {
        Debug.Log("게임 종료!");
        // TODO: 결과 화면 표시
    }
}