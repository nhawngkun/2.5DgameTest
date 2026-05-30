using UnityEngine;

namespace Common.Scripts
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshCollider))]
    public sealed class EditableColliderTool : EditableShape
    {
        #region Inspector

        [SerializeField]
        public float height = 2;

        [SerializeField]
        public bool reverseTriangles = false;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            GenerateMesh();
        }

        private void OnEnable()
        {
            GenerateMesh();
        }

        private void OnValidate()
        {
            GenerateMesh();
        }

        #endregion

        #region Methods

        public void GenerateMesh()
        {
            MeshCollider meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null) return;

            ClearOldMesh(meshCollider);

            if (points == null || points.Count < 2)
            {
                meshCollider.sharedMesh = null;
                return;
            }

            Vector3 topOffset = new Vector3(0, height, 0);
            int pointsCount = points.Count;

            Vector3[] orderedPoints = points.ToArray();
            if (reverseTriangles)
            {
                System.Array.Reverse(orderedPoints);
            }

            int segmentCount = isClosedShape ? pointsCount : pointsCount - 1;
            Vector3[] vertices = new Vector3[pointsCount * 2];
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

                int b = i * 12;
                // --- Front face ---
                tris[b] = ci;
                tris[b + 1] = ni;
                tris[b + 2] = cit;

                tris[b + 3] = ni;
                tris[b + 4] = nit;
                tris[b + 5] = cit;

                // --- Back face ---
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

            meshCollider.convex = false;
            meshCollider.sharedMesh = mesh;
        }

        private void ClearOldMesh(MeshCollider meshCollider)
        {
            if (meshCollider != null && meshCollider.sharedMesh != null && meshCollider.sharedMesh.name == "Baked Collider")
            {
                if (Application.isPlaying)
                {
                    Destroy(meshCollider.sharedMesh);
                }
                else
                {
                    DestroyImmediate(meshCollider.sharedMesh);
                }
            }
        }

        #endregion
    }
}