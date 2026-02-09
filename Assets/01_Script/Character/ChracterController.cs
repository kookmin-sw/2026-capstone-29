using UnityEngine;

public class CharacterControl : MonoBehaviour
{
    [Header("필수 컴포넌트")]
    private CharacterModel model;
    private CharacterView view;
    private Rigidbody rb;
    private Collider col;

    [Header("조작 키")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode punchKey = KeyCode.Z;
    public KeyCode chargeKey = KeyCode.X;

    [Header("이동 설정")]
    public float moveSpeed = 5f;   // 이동 속도
    public float turnSpeed = 10f;  // 회전 속도
    public float jumpForce = 5f;

     private Vector3 inputDir;

    [Header("밸런스 설정")]
    public float comboResetTime = 1.0f;
    public float chargeTimeNeeded = 2.0f;

    private float lastAttackTime;
    private float chargeStartTime;

    void Awake()
    {
        model = GetComponent<CharacterModel>();
        view = GetComponent<CharacterView>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    void Update()
    {
        if (model.IsDead) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        inputDir = new Vector3(h, 0, v).normalized; // 대각선 이동 속도 일정하게

        ProcessActionInput();
        CheckComboTimer();

        if (model.IsDead) 
        {
            rb.velocity = Vector3.zero; // 미끄러짐 방지 (확실하게 멈춤)
            return; 
        }
        
        if (Input.GetKeyDown(KeyCode.H))
        {
            model.TakeDamage(10);
            Debug.Log("(체력: " + model.CurrentHealth + ")");
        }
    }

    void FixedUpdate()
    {
        if (model.IsDead) return;

        Move();
        
        // 뷰 갱신
        if (view != null)
        {
            view.UpdateMovementAnimation(inputDir.magnitude); // 0이면 정지, 1이면 이동
            view.UpdatePhysicsAnimation(rb.velocity.y, IsGrounded());
        }
    }

    void Move()
    {
        if (inputDir.magnitude >= 0.1f)
        {
            // 이동
            Vector3 targetVelocity = inputDir * moveSpeed;
            rb.velocity = new Vector3(targetVelocity.x, rb.velocity.y, targetVelocity.z);

            // 회전
            Quaternion targetRotation = Quaternion.LookRotation(inputDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
        }
        else
        {
            rb.velocity = new Vector3(0, rb.velocity.y, 0);
        }
    }

    void ProcessActionInput()
    {
        // 점프 
        if (Input.GetKeyDown(jumpKey) && IsGrounded())
        {
            rb.velocity = new Vector3(rb.velocity.x, jumpForce, rb.velocity.z);
        }

        // 펀치
        if (Input.GetKeyDown(punchKey) && !model.IsCharging)
        {
            lastAttackTime = Time.time;
            model.NextCombo();
        }

        // 차지 공격
        if (Input.GetKeyDown(chargeKey))
        {
            chargeStartTime = Time.time;
            model.SetCharging(true);
        }

        if (Input.GetKey(chargeKey))
        {
            float duration = Time.time - chargeStartTime;
            bool isReady = (duration >= chargeTimeNeeded);
            
            view.UpdateChargeEffect(true, isReady);
        }


        if (Input.GetKeyUp(chargeKey))
        {
            model.SetCharging(false);
            view.UpdateChargeEffect(false, false); 
            if (Time.time - chargeStartTime >= chargeTimeNeeded)
            {
                view.PlayStrongAttackEffect();
            }
        }
    }

    void CheckComboTimer()
    {
        if (model.ComboCount > 0 && Time.time > lastAttackTime + comboResetTime)
        {
            model.ResetCombo();
        }
    }

    // 3D 바닥 체크
    bool IsGrounded()
    {
        float distToGround = col.bounds.extents.y;
        
        return Physics.Raycast(col.bounds.center, Vector3.down, distToGround + 0.1f);
    }
}