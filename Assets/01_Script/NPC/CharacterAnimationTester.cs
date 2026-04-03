using UnityEngine;
 
/// <summary>
/// Inspector에서 bool 체크박스로 애니메이션을 테스트하는 스크립트
/// CharacterLocalView와 같은 오브젝트에 붙여서 사용
/// </summary>
public class CharacterAnimationTester : MonoBehaviour
{
    private CharacterLocalView localView;
    private CharacterModel model;
    private Animator anim;
 
    [Header("─── 콤보 공격 테스트 ───")]
    public bool testCombo1 = false;
    public bool testCombo2 = false;
    public bool testCombo3 = false;
    public bool testCombo4 = false;
 
    [Header("─── 강공격 테스트 ───")]
    public bool testStrongAttack = false;
 
    [Header("─── 차지 공격 테스트 ───")]
    public bool testChargeStart = false;   // 차징 시작
    public bool testChargeReady = false;   // 차지 완료 상태
    public bool testChargeRelease = false; // 차지 해제
 
    [Header("─── 피격 테스트 ───")]
    public bool testGetHit = false;
 
    [Header("─── 사망 / 부활 테스트 ───")]
    public bool testDie = false;
    public bool testRespawn = false;


    [SerializeField] private float verticalSpeed = -2f;
 
    // 이전 프레임 값 저장 (엣지 감지용)
    private bool _prevCombo1, _prevCombo2, _prevCombo3, _prevCombo4;
    private bool _prevStrongAttack;
    private bool _prevChargeStart, _prevChargeReady, _prevChargeRelease;
    private bool _prevGetHit;
    private bool _prevDie, _prevRespawn;

    private void Awake()
    {
        localView = GetComponent<CharacterLocalView>();
        model = GetComponent<CharacterModel>();
        anim = GetComponent<Animator>();
    }
 
    private void Update()
    {
        // ── 콤보 공격 ──
        if (testCombo1 && !_prevCombo1)   { TriggerCombo(1); testCombo1 = false; }
        if (testCombo2 && !_prevCombo2)   { TriggerCombo(2); testCombo2 = false; }
        if (testCombo3 && !_prevCombo3)   { TriggerCombo(3); testCombo3 = false; }
        if (testCombo4 && !_prevCombo4)   { TriggerCombo(4); testCombo4 = false; }
 
        // ── 강공격 ──
        if (testStrongAttack && !_prevStrongAttack)
        {
            localView?.PlayStrongAttackEffect();
            testStrongAttack = false;
        }
 
        // ── 차지 공격 ──
        if (testChargeStart && !_prevChargeStart)
        {
            localView?.UpdateChargeEffect(true, false);
            testChargeStart = false;
        }
        if (testChargeReady && !_prevChargeReady)
        {
            localView?.UpdateChargeEffect(true, true);
            testChargeReady = false;
        }
        if (testChargeRelease && !_prevChargeRelease)
        {
            localView?.UpdateChargeEffect(false, false);
            testChargeRelease = false;
        }
 
        // ── 피격 ──
        if (testGetHit && !_prevGetHit)
        {
            anim?.SetTrigger("GetHit");
            testGetHit = false;
        }
 
        // ── 사망 ──
        if (testDie && !_prevDie)
        {
            anim?.SetBool("Die", true);
            testDie = false;
        }
 
        // ── 부활 ──
        if (testRespawn && !_prevRespawn)
        {
            anim?.SetBool("Die", false);
            testRespawn = false;
        }
        anim?.SetFloat("VerticalSpeed", verticalSpeed);
        if (verticalSpeed < 0) 
            anim?.SetBool("Grounded", true);
        
 
        // 이전 프레임 값 갱신
            _prevCombo1       = testCombo1;
        _prevCombo2       = testCombo2;
        _prevCombo3       = testCombo3;
        _prevCombo4       = testCombo4;
        _prevStrongAttack = testStrongAttack;
        _prevChargeStart  = testChargeStart;
        _prevChargeReady  = testChargeReady;
        _prevChargeRelease= testChargeRelease;
        _prevGetHit       = testGetHit;
        _prevDie          = testDie;
        _prevRespawn      = testRespawn;
    }
 
    private void TriggerCombo(int step)
    {
        if (anim == null) return;
        anim.ResetTrigger("AttackTrigger");
        anim.SetInteger("ComboStep", step);
        anim.SetTrigger("AttackTrigger");
        Debug.Log($"[AnimTester] Combo {step} 실행");
    }
}