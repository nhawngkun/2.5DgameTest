using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Transform của Player")]
    public Transform target;

    [Header("Offset")]
    [Tooltip("Khoảng cách camera so với player (x=ngang, y=cao, z=xa)")]
    public Vector3 offset = new Vector3(0f, 5f, -8f);

    [Header("Smoothing")]
    [Tooltip("Độ mượt khi follow (càng lớn càng mượt hơn)")]
    [Range(1f, 20f)]
    public float smoothSpeed = 8f;

    [Header("Bounds Clamping")]
    [Tooltip("Bật/tắt giới hạn phạm vi camera theo bounds của map")]
    public bool useBoundsClamping = true;

    private Bounds _currentBounds;
    private bool _hasBounds = false;

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position + offset;

        if (useBoundsClamping && _hasBounds)
        {
            desiredPos = ClampCameraPosition(desiredPos);
        }

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            smoothSpeed * Time.deltaTime
        );
    }

    
    public void SetBounds(MapBounds mapBounds)
    {
        if (mapBounds != null)
        {
            _currentBounds = mapBounds.GetWorldBounds();
            _hasBounds = true;
        }
        else
        {
            _hasBounds = false;
        }
    }

    
    public void ClearBounds()
    {
        _hasBounds = false;
    }

    private Vector3 ClampCameraPosition(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, _currentBounds.min.x, _currentBounds.max.x);
        pos.z = Mathf.Clamp(pos.z, _currentBounds.min.z, _currentBounds.max.z);
       
        return pos;
    }
}