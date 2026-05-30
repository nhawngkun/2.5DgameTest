using System.Linq;
using Common.Scripts;
using UnityEditor;
using UnityEngine;

namespace Common.Editor
{
    [CustomEditor(typeof(EditableColliderTool))]
    public sealed class ColliderToolEditor : ShapeToolEditor
    {
        #region Properties

        private EditableColliderTool ColliderShape => Shape as EditableColliderTool;

        private MeshCollider MeshCollider => Shape.GetComponent<MeshCollider>();

        #endregion


        #region ShapeToolEditor

        public override void Bake()
        {
            // --- Validate ---
            if (Shape.points.Count < 2)
            {
                Debug.LogError("[ColliderTool] Cần ít nhất 2 điểm để bake.");
                return;
            }

            Vector3 topOffset = new Vector3(0, ColliderShape.height, 0);
            int pointsCount = Shape.points.Count;

            Vector3[] orderedPoints = Shape.points.ToArray();
            if (ColliderShape.reverseTriangles)
                orderedPoints = orderedPoints.Reverse().ToArray();

            // Số segment thực sự có triangle (closed = tất cả, open = bỏ segment cuối)
            int segmentCount = Shape.isClosedShape ? pointsCount : pointsCount - 1;

            Vector3[] vertices = new Vector3[pointsCount * 2];

            // Mỗi segment = 2 triangle (1 quad) × 2 mặt (double-sided) × 3 index = 12
            int[] tris = new int[segmentCount * 12];

            for (int i = 0; i < pointsCount; i++)
            {
                int currentIndex = i * 2;
                int currentIndexTop = currentIndex + 1;

                vertices[currentIndex] = orderedPoints[i];
                vertices[currentIndexTop] = orderedPoints[i] + topOffset;
            }

            for (int i = 0; i < segmentCount; i++)
            {
                int nextI = (i + 1) % pointsCount;

                int ci = i * 2;        // current bottom
                int cit = ci + 1;       // current top
                int ni = nextI * 2;    // next bottom
                int nit = ni + 1;       // next top

                // --- Mặt ngoài (front face) ---
                int b = i * 12;
                tris[b] = ci;
                tris[b + 1] = ni;
                tris[b + 2] = cit;

                tris[b + 3] = ni;
                tris[b + 4] = nit;
                tris[b + 5] = cit;

                // --- Mặt trong (back face) — winding ngược để double-sided ---
                // Quan trọng: non-convex MeshCollider PhysX chỉ block 1 phía
                // Phải có cả 2 chiều để block player từ mọi hướng
                tris[b + 6] = cit;
                tris[b + 7] = ni;
                tris[b + 8] = ci;

                tris[b + 9] = cit;
                tris[b + 10] = nit;
                tris[b + 11] = ni;
            }

            Mesh mesh = new Mesh
            {
                vertices = vertices,
                triangles = tris,
                name = "Baked Collider"
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Đảm bảo MeshCollider được config đúng
            MeshCollider.convex = false;  // non-convex để khớp với shape bất kỳ
            MeshCollider.sharedMesh = null;   // clear trước để force Unity refresh
            MeshCollider.sharedMesh = mesh;

            // Debug: kiểm tra mesh đã được assign chưa
            Debug.Log($"[ColliderTool] Baked {segmentCount} segments, " +
                      $"{vertices.Length} vertices, {tris.Length / 3} triangles. " +
                      $"Mesh assigned: {MeshCollider.sharedMesh != null}");
        }

        public override void Clear()
        {
            base.Clear();
            MeshCollider.sharedMesh = null;
        }

        #endregion
    }
}