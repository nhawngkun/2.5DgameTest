using UnityEngine;


[RequireComponent(typeof(BoxCollider))]
public class MapBounds : MonoBehaviour
{
    private BoxCollider _col;

    private void Awake()
    {
        _col = GetComponent<BoxCollider>();
        _col.isTrigger = true;
    }

    public Bounds GetWorldBounds()
    {
        if (_col == null) _col = GetComponent<BoxCollider>();
        return _col.bounds;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(col.center, col.size);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(col.center, col.size);

        Gizmos.matrix = Matrix4x4.identity;

        UnityEditor.Handles.Label(
            transform.TransformPoint(col.center + Vector3.up * (col.size.y * 0.5f + 0.5f)),
            $"MapBounds\n{col.size.x:F1} x {col.size.z:F1}",
            new GUIStyle { normal = { textColor = Color.cyan }, fontSize = 11, fontStyle = FontStyle.Bold }
        );
    }
#endif
}
