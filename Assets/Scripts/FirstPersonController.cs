using System;
using UnityEngine;
using Random = UnityEngine.Random;
using Unity.Mathematics;
using static Unity.Mathematics.math;
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class FirstPersonController : MonoBehaviour
{
    [SerializeField] private float m_WalkSpeed;
    [SerializeField] private float m_RunSpeed;
    [SerializeField] [Range(0f, 1f)] private float m_RunstepLenghten;
    [SerializeField] private float m_JumpSpeed;
    [SerializeField] private float m_StickToGroundForce;
    [SerializeField] private float m_GravityMultiplier;
    [SerializeField] private float m_StepInterval;
    [SerializeField] private AudioClip[] m_FootstepSounds;    // an array of footstep sounds that will be randomly selected from.
    [SerializeField] private AudioClip m_JumpSound;           // the sound played when character leaves the ground.
    [SerializeField] private AudioClip m_LandSound;           // the sound played when character touches back on ground.

    public FirstPersonCamera m_FirstPersonCamera;
    private Camera m_Camera;
    private bool m_Jump;
    private float m_YRotation;
    private Vector2 m_Input;
    private float2 m_OriginInput = 0;
    private Vector3 m_MoveDir = Vector3.zero;
    private CharacterController m_CharacterController;
    private CollisionFlags m_CollisionFlags;
    private bool m_PreviouslyGrounded;
    private Vector3 m_OriginalCameraPosition;
    private float m_StepCycle;
    private float m_NextStep;
    private bool m_Jumping;
    private AudioSource m_AudioSource;
    public AnimationCurve speedCurve;
    public float accelerateSpeed = 3;
    public float stopSpeed = 5;

    // Use this for initialization
    private void Start()
    {
        m_CharacterController = GetComponent<CharacterController>();
        m_Camera = m_FirstPersonCamera.GetComponent<Camera>();
        m_OriginalCameraPosition = m_Camera.transform.localPosition;
        m_StepCycle = 0f;
        m_NextStep = m_StepCycle / 2f;
        m_Jumping = false;
        m_AudioSource = GetComponent<AudioSource>();
    }


    // Update is called once per frame
    private void Update()
    {
        // the jump state needs to read here to make sure it is not missed
        //记录是否跳起的状态值（如果是跳起状态跳过赋值，即跳起状态不能持续叠加）
        if (!m_Jump)
        {
            m_Jump = Input.GetButtonDown("Jump");
        }

        //判定跳起后落地的状态（前一帧为未落地，当前帧落地，此刻即为落地状态）
        if (!m_PreviouslyGrounded && m_CharacterController.isGrounded)
        {
            PlayLandingSound();
            m_MoveDir.y = 0f;
            m_Jumping = false;
        }

        //角色控制器（实际上是Collider）脚下没有碰撞体，非跳起状态，前一帧在地面
        //此处判断为角色从高处跌落的瞬间
        if (!m_CharacterController.isGrounded && !m_Jumping && m_PreviouslyGrounded)
        {
            m_MoveDir.y = 0f;
        }

        //将当前帧的落地状态赋值给前一帧落地状态
        m_PreviouslyGrounded = m_CharacterController.isGrounded;
    }

    private void PlayJumpSound()
    {
        if (!m_JumpSound) return;
        m_AudioSource.clip = m_JumpSound;
        m_AudioSource.Play();
    }
    private void PlayLandingSound()
    {
        if (!m_LandSound) return;
        m_AudioSource.clip = m_LandSound;
        m_AudioSource.Play();
        m_NextStep = m_StepCycle + .5f;
    }


    private void FixedUpdate()
    {
        Vector3 euler = transform.eulerAngles;
        euler.y = m_FirstPersonCamera.transform.eulerAngles.y;
        transform.eulerAngles = euler;
        euler = m_FirstPersonCamera.transform.localEulerAngles;
        euler.y = 0;
        m_FirstPersonCamera.transform.localEulerAngles = euler;
        float speed;
        //获取输入并判断采取 步行速度/跑动速度
        GetInput(out speed);
        // always move along the camera forward as it is the direction that it being aimed at
        // 角色当前移动的目标矢量
        Vector3 desiredMove = transform.forward * m_Input.y + transform.right * m_Input.x;

        // get a normal for the surface that is being touched to move along it
        // 检测角色接触的平面，并将移动的目标矢量投影在该平面上，适用于遇到斜面时，速度会减小
        // 同时角色的y轴就会有速度 
        RaycastHit hitInfo;
        Physics.SphereCast(transform.position, m_CharacterController.radius, Vector3.down, out hitInfo,
                           m_CharacterController.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        desiredMove = Vector3.ProjectOnPlane(desiredMove, hitInfo.normal).normalized;

        m_MoveDir.x = desiredMove.x * speed;
        m_MoveDir.z = desiredMove.z * speed;

        // 前一帧如果在地面上
        if (m_CharacterController.isGrounded)
        {
            // 未触发跳跃时，y方向上的值
            m_MoveDir.y = -m_StickToGroundForce;
            // 触发跳跃后，y方向重新赋值
            if (m_Jump)
            {
                m_MoveDir.y = m_JumpSpeed;
                PlayJumpSound();
                m_Jump = false;
                m_Jumping = true;
            }

        }
        //如果不在地面上（跳起或是跌落）
        else
        {
            //施加重力
            m_MoveDir += Physics.gravity * m_GravityMultiplier * Time.fixedDeltaTime;
        }
        m_CollisionFlags = m_CharacterController.Move(m_MoveDir * Time.fixedDeltaTime);

        ProgressStepCycle(speed);
        //TODO
        //Update Pos
    }

    // 处理脚步的周期，主要用来处理播放脚本声音的计时
    private void ProgressStepCycle(float speed)
    {
        // 计算行走距离
        if (m_CharacterController.velocity.sqrMagnitude > 0 && (m_Input.x != 0 || m_Input.y != 0))
        {
            m_StepCycle += (m_CharacterController.velocity.magnitude + speed) *
                         Time.fixedDeltaTime;
        }

        // 如果行走距离没有到达下一步的距离，退出
        if (!(m_StepCycle > m_NextStep))
        {
            return;
        }

        // 否则更新一下步的距离，同时播放脚步声音
        m_NextStep = m_StepCycle + m_StepInterval;

        PlayFootStepAudio();
    }


    private void PlayFootStepAudio()
    {
        if (!m_CharacterController.isGrounded || m_FootstepSounds.Length <= 0)
        {
            return;
        }
        // 随机播放脚步声音
        // pick & play a random footstep sound from the array,
        // excluding sound at index 0
        int n = Random.Range(1, m_FootstepSounds.Length);
        m_AudioSource.clip = m_FootstepSounds[n];
        m_AudioSource.PlayOneShot(m_AudioSource.clip);
        // move picked sound to index 0 so it's not picked next time
        m_FootstepSounds[n] = m_FootstepSounds[0];
        m_FootstepSounds[0] = m_AudioSource.clip;
    }
    //获取输入并判断采取 步行速度/跑动速度
    private void GetInput(out float speed)
    {
        // Read input
        float horizontal = 0;
        if (Input.GetKey(KeyCode.A)) horizontal--;
        if (Input.GetKey(KeyCode.D)) horizontal++;
        float vertical = 0;
        if (Input.GetKey(KeyCode.W)) vertical++;
        if (Input.GetKey(KeyCode.S)) vertical--;
        float lerpWalkValue(float originValue, float targetValue)
        {
            float spd = accelerateSpeed;
            if (abs(originValue - targetValue) > 1.1f) spd = stopSpeed * 2;
            else if (abs(targetValue) < 1e-4f) spd = stopSpeed;
            return originValue + ( targetValue > originValue ? min(Time.deltaTime * spd, targetValue - originValue) : -min(Time.deltaTime * spd, originValue - targetValue));
        }
        // set the desired speed to be walking or running
        speed = m_WalkSpeed;
        float2 target = float2(horizontal, vertical);
        m_OriginInput.x = lerpWalkValue(m_OriginInput.x, target.x);
        m_OriginInput.y = lerpWalkValue(m_OriginInput.y, target.y);
        m_OriginInput = clamp(m_OriginInput, -1, 1);
        m_Input.x = speedCurve.Evaluate(m_OriginInput.x);
        m_Input.y = speedCurve.Evaluate(m_OriginInput.y);
        float maxValue = max(abs(m_Input.x), abs(m_Input.y));
        m_Input = maxValue * sign(m_Input);
        // normalize input if it exceeds 1 in combined length:
        // 如果输入的方向矢量长度大于1 归一化
        if (m_Input.sqrMagnitude > 1)
        {
            m_Input.Normalize();
        }
    }

    // 处理角色碰到刚体时
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        //dont move the rigidbody if the character is on top of it
        if (m_CollisionFlags == CollisionFlags.Below)
        {
            return;
        }

        if (body == null || body.isKinematic)
        {
            return;
        }
        body.AddForceAtPosition(m_CharacterController.velocity * 0.1f, hit.point, ForceMode.Impulse);
    }
}
