using System.Collections.Generic;
using UnityEngine;

public class MapPortal : MonoBehaviour
{
    public static List<MapPortal> AllPortals = new List<MapPortal>();

    [Header("Portal Settings")]
    [Tooltip("ID duy nhất của cổng này (ví dụ: Map1_PortalA)")]
    public string _PortalId;

    [Tooltip("ID của Map đích cần chuyển đến (ví dụ: Map2)")]
    public string _TargetMapId;

    [Tooltip("ID của cổng đích nơi Player sẽ xuất hiện (ví dụ: Map2_PortalA)")]
    public string _TargetPortalId;

    [Tooltip("Điểm xuất hiện của Player khi dịch chuyển đến cổng này")]
    public Transform _SpawnPoint;

    [Tooltip("Tên hiển thị của cổng (ví dụ: Cổng sang Map 2)")]
    public string _PortalName = "Cổng chuyển map";

    [Tooltip("Khoảng cách kích hoạt tương tác")]
    public float _InteractRange = 2f;

    private void OnEnable()
    {
        if (!AllPortals.Contains(this))
            AllPortals.Add(this);
    }

    private void OnDisable()
    {
        AllPortals.Remove(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _InteractRange);
        if (_SpawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_SpawnPoint.position, 0.3f);
            Gizmos.DrawLine(transform.position, _SpawnPoint.position);
        }
    }
}
