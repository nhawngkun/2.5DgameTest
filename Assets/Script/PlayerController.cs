using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Animator _Animator;
    [Tooltip("Joystick di động (không bắt buộc)")]
    public VariableJoystick_SilkyWoods _Joystick;

    [Header("Speed Settings")]
    [Tooltip("Tốc độ tối thiểu khi bắt đầu di chuyển")]
    public float _MinSpeed = 2f;

    [Tooltip("Tốc độ tối đa (khi đạt → chuyển Run)")]
    public float _MaxSpeed = 6f;

    [Tooltip("Gia tốc tăng tốc (units/s²)")]
    public float _Acceleration = 8f;

    [Tooltip("Gia tốc giảm tốc khi thả phím (units/s²)")]
    public float _Deceleration = 12f;

    [Tooltip("% MaxSpeed để tính là đang Run (0.95 = 95%)")]
    [Range(0.5f, 1f)]
    public float _RunThreshold = 0.95f;

    [Header("Chopping Settings")]
    [Tooltip("Khoảng cách tối đa để chặt trúng cây")]
    public float _ChopRange = 1.6f;

    [Tooltip("Góc quét hình nón trước mặt Player (độ)")]
    [Range(0f, 360f)]
    public float _ChopAngle = 120f;

    [Tooltip("Thời gian trễ từ khi ấn K tới khi chém trúng cây (giây)")]
    public float _ChopDamageDelay = 0.25f;

    [Header("Inventory Settings")]
    public InventoryData _InventoryData;
    [Tooltip("Khoảng cách tối đa để hiện nút nhặt gỗ")]
    public float _CollectRange = 2f;
    private WoodLoot _closestLoot = null;

    [Header("Campfire Settings")]
    [Tooltip("Khoảng cách tối đa để hiện nút bật lửa")]
    public float _CampfireRange = 2.5f;
    private Campfire _closestCampfire = null;

    private MapPortal _closestPortal = null;

    [Header("Debug (Read Only)")]
    [SerializeField] private bool _IsWalk;
    [SerializeField] private bool _IsRun;
    [SerializeField] private bool _IsCut;
    [SerializeField] private float _CurrentSpeed;
    [SerializeField] private Vector2 _InputDirection;
    [SerializeField] private Vector2 _LastMoveDirection;

    private Rigidbody _Rigidbody;

    private void OnValidate()
    {
        if (_Animator == null)
            _Animator = GetComponentInChildren<Animator>();

        _MinSpeed = Mathf.Min(_MinSpeed, _MaxSpeed);
    }

    private void Awake()
    {
        _Rigidbody = GetComponent<Rigidbody>();

        _Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _Rigidbody.freezeRotation = true;

        if (_Animator == null)
            _Animator = GetComponentInChildren<Animator>();

        if (_Joystick == null)
            _Joystick = FindAnyObjectByType<VariableJoystick_SilkyWoods>();

        _LastMoveDirection = Vector2.down;
    }

    private void Update()
    {
        ReadInput();
        UpdateSpeed();
        UpdateState();
        SetAnimation();
        UpdateCollectPrompt();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void ReadInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _InputDirection = new Vector2(h, v);

        if (_InputDirection.sqrMagnitude == 0f && _Joystick != null)
        {
            _InputDirection = _Joystick.Direction;
        }

      
        if (_InputDirection.sqrMagnitude > 0f)
            _LastMoveDirection = _InputDirection.normalized;

        if (_IsCut) return;

        if (Input.GetKeyDown(KeyCode.K))
        {
            _IsCut = true;
           
            UpdateSpeed();
            UpdateState();
            SetAnimation();
            
            var token = this.GetCancellationTokenOnDestroy();
            WaitForCutAnimationAsync(token).Forget();
            PerformChopCheckAsync(_ChopDamageDelay, token).Forget();
        }
    }

    private async UniTaskVoid WaitForCutAnimationAsync(System.Threading.CancellationToken cancellationToken)
    {
        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        while (_Animator.IsInTransition(0))
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        float clipLength = _Animator.GetCurrentAnimatorStateInfo(0).length;

        await UniTask.Delay(System.TimeSpan.FromSeconds(clipLength * 0.75f), cancellationToken: cancellationToken);

        _IsCut = false;
        UpdateSpeed();
        UpdateState();
        SetAnimation();
    }

    private async UniTaskVoid PerformChopCheckAsync(float delay, System.Threading.CancellationToken cancellationToken)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);

        Vector3 faceDir = new Vector3(_LastMoveDirection.x, 0f, _LastMoveDirection.y).normalized;
        if (faceDir == Vector3.zero)
            faceDir = Vector3.back;

        Collider[] hits = Physics.OverlapSphere(transform.position, _ChopRange);

        ChoppableTree closestTree = null;
        float closestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            ChoppableTree tree = hit.GetComponent<ChoppableTree>();
            if (tree == null)
                tree = hit.GetComponentInParent<ChoppableTree>();

            if (tree != null)
            {
                Vector3 toTree = (tree.transform.position - transform.position);
                toTree.y = 0f;

                float distance = toTree.magnitude;

                if (distance == 0f)
                {
                    closestTree = tree;
                    break;
                }

                Vector3 dirToTree = toTree / distance;
                float angle = Vector3.Angle(faceDir, dirToTree);

                if (angle <= _ChopAngle * 0.5f)
                {
                    if (distance < closestDist)
                    {
                        closestDist = distance;
                        closestTree = tree;
                    }
                }
            }
        }

        if (closestTree != null)
            closestTree.Chop(transform.position);
    }

    private void UpdateSpeed()
    {
        bool hasInput = _InputDirection.sqrMagnitude > 0f;

        if (hasInput)
        {
            if (_CurrentSpeed < _MinSpeed)
                _CurrentSpeed = _MinSpeed;

            float targetMaxSpeed = _MaxSpeed;
            if (_Joystick != null && Input.GetAxisRaw("Horizontal") == 0f && Input.GetAxisRaw("Vertical") == 0f)
            {
                targetMaxSpeed = _MaxSpeed * Mathf.Clamp01(_InputDirection.magnitude);
                if (targetMaxSpeed < _MinSpeed && _InputDirection.sqrMagnitude > 0f)
                {
                    targetMaxSpeed = _MinSpeed;
                }
            }

            _CurrentSpeed = Mathf.MoveTowards(_CurrentSpeed, targetMaxSpeed, _Acceleration * Time.deltaTime);
        }
        else
        {
            _CurrentSpeed = Mathf.MoveTowards(_CurrentSpeed, 0f, _Deceleration * Time.deltaTime);
        }
    }

    private void UpdateState()
    {
        bool hasInput = _InputDirection.sqrMagnitude > 0f;
        _IsWalk = hasInput;
        _IsRun = hasInput && _CurrentSpeed >= _MaxSpeed * _RunThreshold;
    }

    private void MovePlayer()
    {
        if (_Rigidbody == null) return;

        if (_IsCut)
        {
            _Rigidbody.linearVelocity = new Vector3(0f, _Rigidbody.linearVelocity.y, 0f);
            return;
        }

        if (_InputDirection.sqrMagnitude > 0f)
        {
            Vector3 moveDir = new Vector3(_InputDirection.normalized.x, 0f, _InputDirection.normalized.y);
            _Rigidbody.linearVelocity = new Vector3(
                moveDir.x * _CurrentSpeed,
                _Rigidbody.linearVelocity.y,
                moveDir.z * _CurrentSpeed
            );
        }
        else
        {
            _Rigidbody.linearVelocity = new Vector3(0f, _Rigidbody.linearVelocity.y, 0f);
        }
    }

    public void SetAnimation()
    {
        if (_Animator == null || !_Animator.gameObject.activeSelf) return;

        Vector2 blendDir = (_InputDirection.sqrMagnitude > 0f && !_IsCut)
            ? _InputDirection.normalized
            : _LastMoveDirection;

        
        _Animator.SetFloat(AnimatorParameters.MOVE_X, blendDir.x);
        _Animator.SetFloat(AnimatorParameters.MOVE_Y, blendDir.y);

        _Animator.SetBool(AnimatorParameters.IS_MOVE, _IsWalk);
        _Animator.SetBool(AnimatorParameters.IS_RUN, _IsRun);
        _Animator.SetBool(AnimatorParameters.IS_CUT, _IsCut);

       
        _Animator.Update(0f);

        
        _Animator.SetFloat(AnimatorParameters.MOVE_X, blendDir.x);
        _Animator.SetFloat(AnimatorParameters.MOVE_Y, blendDir.y);
    }

    private void UpdateCollectPrompt()
    {
        
        WoodLoot[] allLoot = FindObjectsByType<WoodLoot>(FindObjectsInactive.Exclude);
        WoodLoot newClosestLoot = null;
        float closestLootDist = _CollectRange;

        foreach (WoodLoot loot in allLoot)
        {
            if (loot.CanBeCollected())
            {
                float dist = Vector3.Distance(transform.position, loot.transform.position);
                if (dist < closestLootDist)
                {
                    closestLootDist = dist;
                    newClosestLoot = loot;
                }
            }
        }
        _closestLoot = newClosestLoot;

        
        Campfire[] allCampfires = FindObjectsByType<Campfire>(FindObjectsInactive.Exclude);
        Campfire newClosestCampfire = null;
        float closestCampfireDist = _CampfireRange;

        foreach (Campfire campfire in allCampfires)
        {
            float dist = Vector3.Distance(transform.position, campfire.transform.position);
            if (dist < closestCampfireDist)
            {
                closestCampfireDist = dist;
                newClosestCampfire = campfire;
            }
        }
        _closestCampfire = newClosestCampfire;

        
        MapPortal newClosestPortal = null;
        float closestPortalDist = float.MaxValue;

        foreach (MapPortal portal in MapPortal.AllPortals)
        {
            float dist = Vector3.Distance(transform.position, portal.transform.position);
            if (dist < portal._InteractRange && dist < closestPortalDist)
            {
                closestPortalDist = dist;
                newClosestPortal = portal;
            }
        }
        _closestPortal = newClosestPortal;

        
        UIGameplay gameplayUI = UIManager_SSMB.Instance != null ? UIManager_SSMB.Instance.GetUI<UIGameplay>() : null;
        if (gameplayUI != null)
        {
            if (_closestPortal != null)
            {
                gameplayUI.ShowCollectPrompt(true, $"Nhấn L để sang {_closestPortal._PortalName}");
            }
            else if (_closestLoot != null)
            {
                gameplayUI.ShowCollectPrompt(true, "Nhấn J để nhặt");
            }
            else if (_closestCampfire != null)
            {
                gameplayUI.ShowCollectPrompt(true, "Nhấn H để bật lửa");
            }
            else
            {
                gameplayUI.ShowCollectPrompt(false);
            }
        }

        if (_closestPortal != null && Input.GetKeyDown(KeyCode.L))
        {
            TryInteractWithPortal();
        }

        if (_closestLoot != null && Input.GetKeyDown(KeyCode.J))
        {
            TryCollectClosestLoot();
        }

        if (_closestCampfire != null && Input.GetKeyDown(KeyCode.H))
        {
            TryFeedCampfire();
        }
    }

    public void TryInteractWithPortal()
    {
        if (_closestPortal != null)
        {
            MapManager.Instance.TransitionToPortalAsync(_closestPortal).Forget();
            _closestPortal = null;
        }
    }

    public void TryCollectClosestLoot()
    {
        if (_closestLoot != null)
        {
          
            _closestLoot.StartMagnetize(transform);
            _closestLoot = null;
        }
    }

  
    public void TryInteractWithPrompt()
    {
        if (_closestPortal != null)
        {
            TryInteractWithPortal();
        }
        else if (_closestLoot != null)
        {
            TryCollectClosestLoot();
        }
        else if (_closestCampfire != null)
        {
            TryFeedCampfire();
        }
    }

    public void TryFeedCampfire()
    {
        if (_closestCampfire != null)
        {
            if (_InventoryData != null)
            {
                var woodItem = _InventoryData.items.Find(i => i.type == ItemType.Wood);
                if (woodItem != null && woodItem.quantity > 0)
                {
                   
                    _InventoryData.AddItem(ItemType.Wood, -1);

                 
                    if (SaveManager.Instance != null)
                    {
                        SaveManager.Instance.Save();
                    }

                      _closestCampfire.FeedWood(transform.position);
                   
                }
                else
                {
                  
                    UIGameplay gameplayUI = UIManager_SSMB.Instance != null ? UIManager_SSMB.Instance.GetUI<UIGameplay>() : null;
                    gameplayUI?.PlayCollectFeedback("Cần có Gỗ để bật lửa!");
                }
            }
            else
            {
                Debug.LogError("[PlayerController] InventoryData là NULL!");
            }
        }
    }

    public void AddWoodToInventory(int qty)
    {
        if (_InventoryData != null)
        {
            _InventoryData.AddItem(ItemType.Wood, qty);
           

          
            UIGameplay gameplayUI = UIManager_SSMB.Instance != null ? UIManager_SSMB.Instance.GetUI<UIGameplay>() : null;
            gameplayUI?.PlayCollectFeedback($"+{qty} Gỗ");
        }
        else
        {
            Debug.LogError("[PlayerController] InventoryData là NULL! Kéo InventoryData asset vào Inspector của PlayerController.");
        }
    }
}

public static class AnimatorParameters
{
    public static readonly int IS_MOVE = Animator.StringToHash("IsWalk");
    public static readonly int IS_RUN = Animator.StringToHash("IsRun");
    public static readonly int IS_CUT = Animator.StringToHash("IsCut");
    public static readonly int MOVE_X = Animator.StringToHash("MoveX");
    public static readonly int MOVE_Y = Animator.StringToHash("MoveY");
}