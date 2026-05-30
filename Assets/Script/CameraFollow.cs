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

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            smoothSpeed * Time.deltaTime
        );
    }
}