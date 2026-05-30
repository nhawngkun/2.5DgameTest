using System.Collections;
using UnityEngine;

public class WoodLoot : MonoBehaviour
{
    [Header("Flight Settings")]
    public float flyDuration = 0.8f;
    public float arcHeight = 2.0f;
    public float minSpinSpeed = 180f;
    public float maxSpinSpeed = 360f;
    [Tooltip("Độ lệch chiều cao để gỗ không bị lún sâu xuống sàn (nếu pivot của gỗ nằm ở tâm)")]
    public float groundYOffset = 0.15f;

    [Header("Bounce Settings")]
    public bool enableBounce = true;
    public float bounceHeightFactor = 0.3f;
    public float bounceDuration = 0.25f;

    [Header("Magnet Settings")]
    public float magnetRadius = 3f;
    public float magnetStartSpeed = 2f;
    public float magnetAcceleration = 15f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 spinAxis;
    private float spinSpeed;
    private float elapsed;
    private bool isFlying = false;
    private bool isCollectible = false;
    private bool isMagnetized = false;
    private float currentMagnetSpeed;
    private Transform playerTransform;

    private void Awake()
    {
        currentMagnetSpeed = magnetStartSpeed;
    }

   
    public void Initialize(Vector3 start, Vector3 target)
    {
        startPosition = start;
        targetPosition = target;
        targetPosition.y += groundYOffset;

        transform.position = start;
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        
        elapsed = 0f;
        isFlying = true;
        isCollectible = false;
        isMagnetized = false;
    }

   
    public void InitializeLanded(Vector3 pos)
    {
        transform.position = pos;
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        isFlying = false;
        isCollectible = true;
        isMagnetized = false;
    }

    private void Update()
    {
        if (isFlying)
        {
            UpdateFlight();
        }
        else if (isCollectible)
        {
            UpdateMagnet();
        }
    }

    private void UpdateFlight()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / flyDuration;

        if (t >= 1f)
        {
            t = 1f;
            isFlying = false;
            
            if (enableBounce)
            {
                StartCoroutine(BounceSequence());
            }
            else
            {
                OnLanded();
            }
            return;
        }

        Vector3 currentXZ = Vector3.Lerp(startPosition, targetPosition, t);
        
        float height = Mathf.Sin(t * Mathf.PI) * arcHeight;
        float currentY = Mathf.Lerp(startPosition.y, targetPosition.y, t) + height;

        Vector3 nextPosition = new Vector3(currentXZ.x, currentY, currentXZ.z);

        if (CheckAndHandleWallCollision(nextPosition))
        {
            return;
        }

        transform.position = nextPosition;
        
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);
    }

    private IEnumerator BounceSequence()
    {
        Quaternion targetRotation = Quaternion.Euler(45f, 0f, 0f);

        Vector3 bounceStart = targetPosition;
        Vector3 bounceOffset = (targetPosition - startPosition).normalized * 0.4f;
        bounceOffset.y = 0;
        Vector3 bounceEnd = targetPosition + bounceOffset;

        float bounceHeight = arcHeight * bounceHeightFactor;
        float bElapsed = 0f;

        while (bElapsed < bounceDuration)
        {
            bElapsed += Time.deltaTime;
            float t = bElapsed / bounceDuration;

            Vector3 currentXZ = Vector3.Lerp(bounceStart, bounceEnd, t);
            float height = Mathf.Sin(t * Mathf.PI) * bounceHeight;
            float currentY = Mathf.Lerp(bounceStart.y, bounceEnd.y, t) + height;

            Vector3 nextPosition = new Vector3(currentXZ.x, currentY, currentXZ.z);

            if (CheckAndHandleWallCollision(nextPosition))
            {
                yield break;
            }

            transform.position = nextPosition;
            
            transform.rotation = targetRotation;
            yield return null;
        }

        transform.position = bounceEnd;
        transform.rotation = targetRotation;

        Vector3 originalScale = transform.localScale;
        float squashDuration = 0.1f;
        float squashElapsed = 0f;

        while (squashElapsed < squashDuration)
        {
            squashElapsed += Time.deltaTime;
            float ratio = squashElapsed / squashDuration;
            transform.localScale = new Vector3(
                originalScale.x * Mathf.Lerp(1f, 1.25f, ratio),
                originalScale.y * Mathf.Lerp(1f, 0.7f, ratio),
                originalScale.z * Mathf.Lerp(1f, 1.25f, ratio)
            );
            yield return null;
        }

        float stretchElapsed = 0f;
        while (stretchElapsed < squashDuration)
        {
            stretchElapsed += Time.deltaTime;
            float ratio = stretchElapsed / squashDuration;
            transform.localScale = new Vector3(
                originalScale.x * Mathf.Lerp(1.25f, 1f, ratio),
                originalScale.y * Mathf.Lerp(0.7f, 1f, ratio),
                originalScale.z * Mathf.Lerp(1.25f, 1f, ratio)
            );
            yield return null;
        }

        transform.localScale = originalScale;
        OnLanded();
    }

    private void OnLanded()
    {
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        isCollectible = true;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save();
        }
    }

    private bool CheckAndHandleWallCollision(Vector3 nextPosition)
    {
        Vector3 movement = nextPosition - transform.position;
        float distance = movement.magnitude;
        if (distance > 0.0001f)
        {
            Vector3 direction = movement / distance;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, distance + 0.15f))
            {
                if (IsValidWallCollision(hit))
                {
                    isFlying = false;
                    StopAllCoroutines();

                    Vector3 contactPoint = hit.point + hit.normal * 0.15f;
                    
                    float groundY = targetPosition.y;
                    RaycastHit groundHit;
                    if (Physics.Raycast(contactPoint, Vector3.down, out groundHit, 15f))
                    {
                        if (!groundHit.collider.isTrigger)
                        {
                            groundY = groundHit.point.y + groundYOffset;
                        }
                    }

                    StartCoroutine(FallSequence(contactPoint, groundY));
                    return true;
                }
            }
        }
        return false;
    }

    private bool IsValidWallCollision(RaycastHit hit)
    {
        if (hit.collider.isTrigger) return false;

        if (Vector3.Angle(hit.normal, Vector3.up) < 45f) return false;

        if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerController>() != null) return false;

        if (hit.collider.GetComponent<WoodLoot>() != null || hit.collider.GetComponentInParent<WoodLoot>() != null) return false;

        if (hit.collider.GetComponent<ChoppableTree>() != null || hit.collider.GetComponentInParent<ChoppableTree>() != null) return false;

        return true;
    }

    private IEnumerator FallSequence(Vector3 startFallPos, float targetY)
    {
        isFlying = false;
        isCollectible = false;

        float elapsedFall = 0f;
        float fallDuration = 0.35f; 

        while (elapsedFall < fallDuration)
        {
            elapsedFall += Time.deltaTime;
            float t = elapsedFall / fallDuration;
            float currentY = Mathf.Lerp(startFallPos.y, targetY, t * t);
            
            transform.position = new Vector3(startFallPos.x, currentY, startFallPos.z);
            transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            yield return null;
        }

        transform.position = new Vector3(startFallPos.x, targetY, startFallPos.z);
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);

        Vector3 originalScale = transform.localScale;
        float squashDuration = 0.1f;
        float squashElapsed = 0f;

        
        while (squashElapsed < squashDuration)
        {
            squashElapsed += Time.deltaTime;
            float ratio = squashElapsed / squashDuration;
            transform.localScale = new Vector3(
                originalScale.x * Mathf.Lerp(1f, 1.25f, ratio),
                originalScale.y * Mathf.Lerp(1f, 0.7f, ratio),
                originalScale.z * Mathf.Lerp(1f, 1.25f, ratio)
            );
            yield return null;
        }

        float stretchElapsed = 0f;
        while (stretchElapsed < squashDuration)
        {
            stretchElapsed += Time.deltaTime;
            float ratio = stretchElapsed / squashDuration;
            transform.localScale = new Vector3(
                originalScale.x * Mathf.Lerp(1.25f, 1f, ratio),
                originalScale.y * Mathf.Lerp(0.7f, 1f, ratio),
                originalScale.z * Mathf.Lerp(1.25f, 1f, ratio)
            );
            yield return null;
        }

        transform.localScale = originalScale;
        OnLanded();
    }

    public bool CanBeCollected()
    {
        return isCollectible && !isMagnetized;
    }

    public void StartMagnetize(Transform player)
    {
        playerTransform = player;
        isMagnetized = true;
    }

    private void UpdateMagnet()
    {
        if (!isMagnetized) return;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (playerTransform == null) return;

        Vector3 targetPoint = playerTransform.position + Vector3.up * 0.5f;
        currentMagnetSpeed += magnetAcceleration * Time.deltaTime;
        
        transform.position = Vector3.MoveTowards(transform.position, targetPoint, currentMagnetSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPoint) < 0.35f)
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (playerTransform != null)
        {
            PlayerController pc = playerTransform.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.AddWoodToInventory(1);
            }
        }

        if (WoodPool.Instance != null)
        {
            WoodPool.Instance.Recycle(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}
