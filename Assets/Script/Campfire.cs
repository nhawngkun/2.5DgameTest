using System.Collections;
using UnityEngine;

public class Campfire : MonoBehaviour
{
    [Header("References")]
    public Animator _Animator;
    [Tooltip("Prefab gỗ để làm hiệu ứng bay (ví dụ: dùng chính prefab WoodLoot hoặc một bản đơn giản)")]
    public GameObject _WoodVisualPrefab;

    [Header("Settings")]
    public string _AnimatorBoolName = "ISFire";
    public float _BurnDuration = 10f;
    public float _FlyDuration = 0.6f;
    [Tooltip("Số tiền cộng thêm mỗi giây khi đang cháy")]
    public float _MoneyPerSecond = 5f;

    private float _burnTimer = 0f;
    private bool _isBurning = false;
    private Coroutine _burnCoroutine = null;

    public float BurnTimer => _burnTimer;
    public bool IsBurningVal => _isBurning;

    private void Awake()
    {
        if (_Animator == null)
        {
            _Animator = GetComponent<Animator>();
            if (_Animator == null)
            {
                _Animator = GetComponentInChildren<Animator>();
            }
        }
    }

    
    public void LoadState(bool isBurning, float burnTimer)
    {
        _isBurning = isBurning;
        _burnTimer = burnTimer;

        if (_burnCoroutine != null)
        {
            StopCoroutine(_burnCoroutine);
            _burnCoroutine = null;
        }

        if (_isBurning && _burnTimer > 0f)
        {
            _burnCoroutine = StartCoroutine(BurnSequence(resumeFromTimer: true));
        }
        else
        {
            _isBurning = false;
            _burnTimer = 0f;
            if (_Animator != null)
            {
                _Animator.SetBool(_AnimatorBoolName, false);
            }
        }
    }

    
    public void FeedWood(Vector3 playerPosition)
    {
        StartCoroutine(FlyWoodSequence(playerPosition));
    }

    private IEnumerator FlyWoodSequence(Vector3 startPos)
    {
        if (_WoodVisualPrefab != null)
        {
            GameObject woodVisual = Instantiate(_WoodVisualPrefab, startPos + Vector3.up * 0.5f, Quaternion.Euler(45f, 0f, 0f));
            
            Collider col = woodVisual.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            WoodLoot wl = woodVisual.GetComponent<WoodLoot>();
            if (wl != null) wl.enabled = false;

            float elapsed = 0f;
            Vector3 targetPos = transform.position + Vector3.up * 0.2f; 
            Vector3 startPosition = woodVisual.transform.position;

            while (elapsed < _FlyDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _FlyDuration;
                
                Vector3 currentXZ = Vector3.Lerp(startPosition, targetPos, t);
                float height = Mathf.Sin(t * Mathf.PI) * 1.5f;
                float currentY = Mathf.Lerp(startPosition.y, targetPos.y, t) + height;

                woodVisual.transform.position = new Vector3(currentXZ.x, currentY, currentXZ.z);
                yield return null;
            }

            Destroy(woodVisual);
        }

        StartBurning();
    }

    private void StartBurning()
    {
        if (_burnCoroutine != null)
        {
            StopCoroutine(_burnCoroutine);
        }
        _burnCoroutine = StartCoroutine(BurnSequence(resumeFromTimer: false));

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save();
        }
    }

    private IEnumerator BurnSequence(bool resumeFromTimer = false)
    {
        _isBurning = true;
        if (_Animator != null)
        {
            _Animator.SetBool(_AnimatorBoolName, true);
        }

        if (!resumeFromTimer)
        {
            _burnTimer = _BurnDuration;
        }

        float moneyAccumulator = 0f;
        while (_burnTimer > 0f)
        {
            _burnTimer -= Time.deltaTime;

            moneyAccumulator += _MoneyPerSecond * Time.deltaTime;
            if (moneyAccumulator >= 1f)
            {
                int intMoney = Mathf.FloorToInt(moneyAccumulator);
                moneyAccumulator -= intMoney;
                
                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null && player._InventoryData != null)
                {
                    player._InventoryData.AddMoney(intMoney);
                }
            }

            yield return null;
        }

        _isBurning = false;
        if (_Animator != null)
        {
            _Animator.SetBool(_AnimatorBoolName, false);
        }
        Debug.Log("[Campfire] Tắt lửa (ISFire = false)!");
        _burnCoroutine = null;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save();
        }
    }

    public bool IsBurning()
    {
        return _isBurning;
    }
}
