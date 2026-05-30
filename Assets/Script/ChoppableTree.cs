using System.Collections;
using UnityEngine;

public class ChoppableTree : MonoBehaviour
{
    [Header("Tree Stats")]
    [Tooltip("Số phát chặt để cây đổ/biến mất")]
    public int maxHits = 3;
    
    [Header("Wood Drops")]
    [Tooltip("Prefab của khúc gỗ rơi ra")]
    public GameObject woodPrefab;
    
    [Tooltip("Số lượng khúc gỗ tối thiểu")]
    public int minWoodCount = 3;
    
    [Tooltip("Số lượng khúc gỗ tối đa")]
    public int maxWoodCount = 5;

    [Tooltip("Khoảng cách bay xa tối thiểu")]
    public float scatterDistanceMin = 0.5f;
    
    [Tooltip("Khoảng cách bay xa tối đa")]
    public float scatterDistanceMax = 1.2f;

    [Header("Visual Feedback (Wobble)")]
    [Tooltip("Thời gian rung lắc mỗi khi bị chặt trúng")]
    public float wobbleDuration = 0.25f;
    
    [Tooltip("Góc lắc tối đa (độ)")]
    public float wobbleAngle = 8f;

    private int currentHits;
    private bool isChopped = false;
    private Coroutine wobbleCoroutine;

    public int CurrentHits => currentHits;
    public bool IsChopped => isChopped;

    private bool _isInitialized = false;

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (_isInitialized) return;
        currentHits = maxHits;
        _isInitialized = true;
    }

    public void LoadState(int hits, bool chopped)
    {
        InitializeIfNeeded();
        this.currentHits = hits;
        this.isChopped = chopped;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = !chopped;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            r.enabled = !chopped;
        }
    }

    
    public void Chop(Vector3 playerPosition)
    {
        if (isChopped) return;

        currentHits--;

        if (wobbleCoroutine != null)
        {
            StopCoroutine(wobbleCoroutine);
        }
        wobbleCoroutine = StartCoroutine(WobbleSequence(playerPosition));


        if (currentHits <= 0)
        {
            TreeDestroyed();
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save();
        }
    }

    private IEnumerator WobbleSequence(Vector3 playerPosition)
    {
        Vector3 originalRotation = transform.localEulerAngles;
        float baseAngleX = originalRotation.x;
        float baseAngleY = originalRotation.y;
        float baseAngleZ = originalRotation.z;

        float directionFactor = 1f;
        if (playerPosition.x > transform.position.x)
        {
            directionFactor = -1f; 
        }

        float elapsed = 0f;
        while (elapsed < wobbleDuration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / wobbleDuration;
            
            float angleOffset = Mathf.Sin(percent * Mathf.PI * 4f) * wobbleAngle * (1f - percent) * directionFactor;
            
            transform.localEulerAngles = new Vector3(baseAngleX, baseAngleY, baseAngleZ + angleOffset);
            yield return null;
        }

        transform.localEulerAngles = new Vector3(baseAngleX, baseAngleY, baseAngleZ);
        wobbleCoroutine = null;
    }

    private void TreeDestroyed()
    {
        isChopped = true;

        if (wobbleCoroutine != null)
        {
            StopCoroutine(wobbleCoroutine);
        }

        SpawnWoodLoot();

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            r.enabled = false;
        }

     
    }

    private void SpawnWoodLoot()
    {
        if (woodPrefab == null)
        {
            return;
        }

        int woodCount = Random.Range(minWoodCount, maxWoodCount + 1);

        float startAngle = Random.Range(0f, 360f);
        float angleStep = 360f / woodCount;

        for (int i = 0; i < woodCount; i++)
        {
            float angleDeg = startAngle + (i * angleStep) + Random.Range(-15f, 15f);
            float angleRad = angleDeg * Mathf.Deg2Rad;

            Vector3 flyDirection = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
            float flyDistance = Random.Range(scatterDistanceMin, scatterDistanceMax);

            Vector3 startPos = transform.position + Vector3.up * 0.5f;
            Vector3 targetPos = transform.position + flyDirection * flyDistance;
            targetPos.y = transform.position.y;

            GameObject woodObj = null;
            if (WoodPool.Instance != null && WoodPool.Instance._WoodPrefab != null)
            {
                woodObj = WoodPool.Instance.Spawn(startPos, Quaternion.Euler(45f, 0f, 0f));
            }
            else
            {
                woodObj = Instantiate(woodPrefab, startPos, Quaternion.Euler(45f, 0f, 0f));
            }

            if (woodObj != null)
            {
                woodObj.transform.SetParent(transform.parent);
            }
            
            WoodLoot woodLoot = woodObj.GetComponent<WoodLoot>();
            if (woodLoot == null)
            {
                woodLoot = woodObj.AddComponent<WoodLoot>();
            }
            
            woodLoot.Initialize(startPos, targetPos);
        }
    }
}
