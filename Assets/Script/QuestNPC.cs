using UnityEngine;

public class QuestNPC : MonoBehaviour
{
    [Header("Movement")]
    public float MoveSpeed = 3.5f;
    public float StoppingDistance = 1.6f;

    [Header("Visual Customization")]
    public Color NpcColorTint = new Color(1f, 0.92f, 0.65f); 

    [Header("Quest Configuration")]
    [Tooltip("If empty, default to active Map ID")]
    [SerializeField] private string mapIdForQuest;

    private enum NPCState
    {
        RunningToPlayer,
        TalkingToPlayer,
        WalkingToPortal,
        StandingStill
    }

    private NPCState _currentState = NPCState.RunningToPlayer;
    private Vector3 _originalPosition;
    private Transform _playerTransform;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private bool _hasTriggeredUI = false;
    private bool _reachedPlayer = false;

    private bool _initialized = false;
    private bool _hasLoadedPosition = false;
    private Vector3 _loadedPosition;

    private void Start()
    {
        _originalPosition = transform.position;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            _playerTransform = player.transform;
        }

        _animator = GetComponentInChildren<Animator>();
        if (_animator == null)
            _animator = GetComponent<Animator>();

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = NpcColorTint;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.isKinematic = true; 
        }

        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
    }

    private void OnEnable()
    {
        _initialized = false;
        _hasTriggeredUI = false;
        _reachedPlayer = false;
    }

    public void LoadPosition(Vector3 position)
    {
        _loadedPosition = position;
        _hasLoadedPosition = true;
    }

    private void InitializeNPC()
    {
        _initialized = true;

        string targetMapId = string.IsNullOrEmpty(mapIdForQuest) && MapManager.Instance != null
            ? MapManager.Instance.CurrentMapId
            : mapIdForQuest;

        if (QuestManager.Instance != null && !string.IsNullOrEmpty(targetMapId))
        {
            QuestManager.Instance.InitializeQuestForMap(targetMapId);
        }

        bool accepted = QuestManager.Instance != null && QuestManager.Instance.IsQuestAccepted(targetMapId);
        bool claimed = QuestManager.Instance != null && QuestManager.Instance.GetQuestForMap(targetMapId) != null && QuestManager.Instance.GetQuestForMap(targetMapId).RewardClaimed;

        if (_hasLoadedPosition)
        {
            transform.position = _loadedPosition;
            _hasLoadedPosition = false; // Reset

            if (accepted || claimed)
            {
                _currentState = NPCState.StandingStill;
                _reachedPlayer = true;
            }
            else
            {
                _currentState = NPCState.RunningToPlayer;
                _reachedPlayer = false;
            }
            _hasTriggeredUI = false;
            if (_animator != null)
            {
                _animator.SetBool("IsWalk", false);
                _animator.SetBool("IsRun", false);
            }
        }
        else if (accepted || claimed)
        {
            transform.position = GetPortalStandPosition();
            _currentState = NPCState.StandingStill;
            _hasTriggeredUI = false;
            _reachedPlayer = true;
            if (_animator != null)
            {
                _animator.SetBool("IsWalk", false);
                _animator.SetBool("IsRun", false);
            }
        }
        else
        {
            transform.position = _originalPosition;
            _currentState = NPCState.RunningToPlayer;
            _hasTriggeredUI = false;
            _reachedPlayer = false;
        }
    }

    private Vector3 GetPortalStandPosition()
    {
        MapPortal portal = FindAnyObjectByType<MapPortal>();
        if (portal != null)
        {
            return portal.transform.position + new Vector3(1.8f, 0f, 0f);
        }
        return _originalPosition;
    }

    private void Update()
    {
        if (MapManager.Instance != null && MapManager.Instance.IsTransitioning)
        {
            return;
        }

        if (!_initialized)
        {
            InitializeNPC();
            return;
        }

        if (_playerTransform == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null) _playerTransform = player.transform;
            return;
        }

        Vector3 playerPosXZ = new Vector3(_playerTransform.position.x, transform.position.y, _playerTransform.position.z);
        float distanceToPlayer = Vector3.Distance(transform.position, playerPosXZ);

        switch (_currentState)
        {
            case NPCState.RunningToPlayer:
                if (distanceToPlayer > StoppingDistance)
                {
                    _reachedPlayer = false;
                    Vector3 targetDir = (playerPosXZ - transform.position).normalized;
                    transform.position = Vector3.MoveTowards(transform.position, playerPosXZ, MoveSpeed * Time.deltaTime);

                    if (_animator != null)
                    {
                        _animator.SetFloat("MoveX", targetDir.x);
                        _animator.SetFloat("MoveY", targetDir.z);
                        _animator.SetBool("IsWalk", true);
                        _animator.SetBool("IsRun", false);
                    }
                }
                else
                {
                    if (!_reachedPlayer)
                    {
                        _reachedPlayer = true;
                        if (_animator != null)
                        {
                            _animator.SetBool("IsWalk", false);
                            _animator.SetBool("IsRun", false);
                            Vector3 faceDir = (playerPosXZ - transform.position).normalized;
                            _animator.SetFloat("MoveX", faceDir.x);
                            _animator.SetFloat("MoveY", faceDir.z);
                        }
                    }

                    if (!_hasTriggeredUI)
                    {
                        _hasTriggeredUI = true;
                        TriggerQuestUI();
                        _currentState = NPCState.TalkingToPlayer;
                    }
                }
                break;

            case NPCState.TalkingToPlayer:
                bool uiOpened = UIManager_SSMB.Instance != null && UIManager_SSMB.Instance.IsUIOpened<QuestUI>();
                if (!uiOpened)
                {
                    string targetMapId = string.IsNullOrEmpty(mapIdForQuest) && MapManager.Instance != null
                        ? MapManager.Instance.CurrentMapId
                        : mapIdForQuest;

                    bool accepted = QuestManager.Instance != null && QuestManager.Instance.IsQuestAccepted(targetMapId);
                    if (accepted)
                    {
                        _currentState = NPCState.WalkingToPortal;
                    }
                    else
                    {
                        _hasTriggeredUI = false;
                        _currentState = NPCState.StandingStill;
                    }
                }
                break;

            case NPCState.WalkingToPortal:
                Vector3 portalStandPos = GetPortalStandPosition();
                float distToPortal = Vector3.Distance(transform.position, portalStandPos);

                if (distToPortal > 0.3f)
                {
                    Vector3 walkDir = (portalStandPos - transform.position).normalized;
                    transform.position = Vector3.MoveTowards(transform.position, portalStandPos, MoveSpeed * Time.deltaTime);

                    if (_animator != null)
                    {
                        _animator.SetFloat("MoveX", walkDir.x);
                        _animator.SetFloat("MoveY", walkDir.z);
                        _animator.SetBool("IsWalk", true);
                        _animator.SetBool("IsRun", false);
                    }
                }
                else
                {
                    if (_animator != null)
                    {
                        _animator.SetBool("IsWalk", false);
                        _animator.SetBool("IsRun", false);
                    }
                    _hasTriggeredUI = false;
                    _currentState = NPCState.StandingStill;
                }
                break;

            case NPCState.StandingStill:
                {
                    string targetMapId = string.IsNullOrEmpty(mapIdForQuest) && MapManager.Instance != null
                        ? MapManager.Instance.CurrentMapId
                        : mapIdForQuest;
                    bool accepted = QuestManager.Instance != null && QuestManager.Instance.IsQuestAccepted(targetMapId);
                    if (!accepted && distanceToPlayer > StoppingDistance + 0.5f)
                    {
                        _currentState = NPCState.RunningToPlayer;
                        _hasTriggeredUI = false;
                        _reachedPlayer = false;
                        break;
                    }
                }

                if (distanceToPlayer <= StoppingDistance)
                {
                    if (!_hasTriggeredUI)
                    {
                        _hasTriggeredUI = true;
                        if (_animator != null)
                        {
                            Vector3 faceDir = (playerPosXZ - transform.position).normalized;
                            _animator.SetFloat("MoveX", faceDir.x);
                            _animator.SetFloat("MoveY", faceDir.z);
                        }
                        TriggerQuestUI();
                        _currentState = NPCState.TalkingToPlayer; 
                    }
                }
                else if (distanceToPlayer > StoppingDistance + 1.5f)
                {
                    _hasTriggeredUI = false;
                }
                break;
        }
    }

    private void TriggerQuestUI()
    {
        
        if (QuestManager.Instance != null)
        {
            string targetMapId = string.IsNullOrEmpty(mapIdForQuest) && MapManager.Instance != null
                ? MapManager.Instance.CurrentMapId
                : mapIdForQuest;

            QuestManager.Instance.InitializeQuestForMap(targetMapId);
        }

        if (UIManager_SSMB.Instance != null)
        {
            UIManager_SSMB.Instance.EnableQuestUI(true);
        }
    }
}
