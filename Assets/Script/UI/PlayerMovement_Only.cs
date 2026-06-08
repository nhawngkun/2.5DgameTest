using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

[RequireComponent(typeof(CharacterController))]
public class PlayerControllers : MonoBehaviour
{
    [Header("Movement")]
    public float _MinSpeed     = 3f;
    public float _MaxSpeed     = 6f;
    public float _Acceleration = 10f;
    public float _Gravity      = -25f;

    [Header("Ground Check")]
    public float     _GroundCheckRadius = 0.25f;
    public float     _GroundCheckOffset = 0.05f;
    public LayerMask _GroundLayer       = ~0;

    [Header("Camera")]
    public Transform _CameraTransform;

    [HideInInspector] public bool _IsMoves;
    [HideInInspector] public int  _MoveIDs;
    [HideInInspector] public bool _IsGrounded;

    private CharacterController _CC;
    private float _CurrentSpeed     = 0f;
    private float _VerticalVelocity = 0f;
    public Animator _Animator;
 

    private void Awake()
    {
        _CC = GetComponent<CharacterController>();
        if (_CameraTransform == null && Camera.main != null)
            _CameraTransform = Camera.main.transform;
        if (_Animator == null) _Animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        CheckGround();
        HandleMovement();
        if (_Animator == null) return;

        _Animator.SetBool(AnimatorParameterss.IS_MOVE,_IsMoves);
        _Animator.SetFloat(AnimatorParameterss.BLEND___MOVE__ID,_MoveIDs, 0f, Time.deltaTime);
    }

    private void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.down * _GroundCheckOffset;
        _IsGrounded = Physics.CheckSphere(origin, _GroundCheckRadius, _GroundLayer, QueryTriggerInteraction.Ignore);

        if (_IsGrounded && _VerticalVelocity < 0f)
            _VerticalVelocity = -2f;
        else
            _VerticalVelocity += _Gravity * Time.deltaTime;
    }

    private void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 camForward = _CameraTransform ? _CameraTransform.forward : Vector3.forward;
        Vector3 camRight   = _CameraTransform ? _CameraTransform.right   : Vector3.right;
        camForward.y = 0f; camForward.Normalize();
        camRight.y   = 0f; camRight.Normalize();

        Vector3 moveDir  = camForward * v + camRight * h;
        bool    hasInput = moveDir.sqrMagnitude > 0.01f;

        if (hasInput)
        {
            moveDir.Normalize();
            if (_CurrentSpeed < _MinSpeed) _CurrentSpeed = _MinSpeed;
            _CurrentSpeed = Mathf.MoveTowards(_CurrentSpeed, _MaxSpeed, _Acceleration * Time.deltaTime);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(new Vector3(moveDir.x, 0f, moveDir.z)),
                720f * Time.deltaTime);

            _IsMoves = true;
            _MoveIDs = (_CurrentSpeed >= _MaxSpeed - 0.15f) ? 1 : 0;
        }
        else
        {
            _IsMoves       = false;
            _MoveIDs       = 0;
            _CurrentSpeed = 0f;
        }

        Vector3 move = (hasInput ? moveDir : Vector3.zero) * _CurrentSpeed;
        move.y = _VerticalVelocity;
        _CC.Move(move * Time.deltaTime);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

public static class AnimatorParameterss
{
    public static readonly int IS_MOVE          = Animator.StringToHash("IsMove");
    public static readonly int BLEND___MOVE__ID = Animator.StringToHash("MoveID");
}

// ─────────────────────────────────────────────────────────────────────────────

