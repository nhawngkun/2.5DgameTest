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
            if (ColliderShape != null)
            {
                Undo.RecordObject(ColliderShape, "Bake Collider");
                ColliderShape.GenerateMesh();
                EditorUtility.SetDirty(ColliderShape);
                if (!Application.isPlaying && ColliderShape.gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ColliderShape.gameObject.scene);
                }
                Debug.Log("[ColliderTool] Baked successfully.");
            }
        }

        public override void Clear()
        {
            if (ColliderShape != null)
            {
                Undo.RecordObject(ColliderShape, "Clear Collider");
                base.Clear();
                ColliderShape.GenerateMesh();
                EditorUtility.SetDirty(ColliderShape);
                if (!Application.isPlaying && ColliderShape.gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ColliderShape.gameObject.scene);
                }
            }
        }

        #endregion
    }
}