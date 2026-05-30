// Assets/Editor/MapObject25DPlacer.cs
// Cách dùng: Window → 2.5D Map Placer
// Yêu cầu: Unity 2022+

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MapObject25DPlacer : EditorWindow
{
    // ── State ────────────────────────────────────────────────────────────────
    private enum PlaceMode { None, Placing, Selecting }
    private PlaceMode _mode = PlaceMode.None;

    // Tilt settings
    private float _tiltAngleX = 30f;       // Góc nghiêng X của map (thường 30-45°)
    private bool _lockTiltOnDrag = true;  // Khi kéo vật → giữ nguyên góc nghiêng

    // Prefab & placement
    private GameObject _selectedPrefab = null;
    private GameObject _previewObj = null;
    private Vector3 _lastHitPoint = Vector3.zero;
    private bool _hitValid = false;

    // Layer mask để raycast đúng mặt phẳng
    private LayerMask _groundLayer = ~0;
    private bool _usePlaneInstead = false;   // Dùng plane ảo thay vì collider
    private float _planeY = 0f;               // Chiều cao plane ảo

    // Recent prefabs
    private List<GameObject> _recentPrefabs = new List<GameObject>();
    private const int MAX_RECENT = 6;

    // Align options
    private bool _alignToNormal = false;
    private float _randomYRotMin = 0f;
    private float _randomYRotMax = 0f;
    private float _uniformScaleMin = 1f;
    private float _uniformScaleMax = 1f;

    // Snap
    private bool _snapEnabled = false;
    private float _snapSize = 1f;

    // Position offset (thêm vào vị trí hit trước khi đặt)
    private Vector3 _posOffset = Vector3.zero;
    private bool _offsetFoldout = true;

    // History (undo support)
    private List<GameObject> _placedObjects = new List<GameObject>();

    // UI scroll
    private Vector2 _scroll;

    // Preview material
    private Material _previewMat;

    // ── Open window ──────────────────────────────────────────────────────────
    [MenuItem("Window/2.5D Map Placer")]
    public static void Open()
    {
        var w = GetWindow<MapObject25DPlacer>("2.5D Map Placer");
        w.minSize = new Vector2(280, 500);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        _previewMat = CreatePreviewMaterial();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        DestroyPreview();
        if (_previewMat != null)
        {
            DestroyImmediate(_previewMat);
            _previewMat = null;
        }
    }

    // ── Inspector GUI ────────────────────────────────────────────────────────
    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawHeader();
        GUILayout.Space(4);

        DrawTiltSection();
        GUILayout.Space(4);

        DrawPrefabSection();
        GUILayout.Space(4);

        DrawPlacementSection();
        GUILayout.Space(4);

        DrawOffsetSection();
        GUILayout.Space(4);

        DrawTransformSection();
        GUILayout.Space(4);

        DrawGroundSection();
        GUILayout.Space(4);

        DrawHistorySection();

        EditorGUILayout.EndScrollView();
    }

    // ── Header ───────────────────────────────────────────────────────────────
    private void DrawHeader()
    {
        Color bg = _mode == PlaceMode.Placing
            ? new Color(0.2f, 0.6f, 0.2f, 0.3f)
            : new Color(0.3f, 0.3f, 0.3f, 0.2f);

        var rect = EditorGUILayout.BeginVertical();
        EditorGUI.DrawRect(rect, bg);
        GUILayout.Space(6);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("2.5D Map Placer", titleStyle, GUILayout.Height(22));

        string statusText = _mode == PlaceMode.Placing
            ? "● Đang đặt vật  [Click: đặt | Shift+Click: xoay | Esc: thoát]"
            : "○ Chưa hoạt động";
        GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = _mode == PlaceMode.Placing ? Color.green : Color.gray }
        };
        EditorGUILayout.LabelField(statusText, statusStyle);
        GUILayout.Space(4);
        EditorGUILayout.EndVertical();
    }

    // ── Tilt section ─────────────────────────────────────────────────────────
    private void DrawTiltSection()
    {
        DrawSectionHeader("Góc nghiêng 2.5D");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Góc nghiêng X (rotation.x = 0)", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        _tiltAngleX = EditorGUILayout.Slider("  Map tilt X°", _tiltAngleX, 0f, 85f);
        if (EditorGUI.EndChangeCheck())
            UpdatePreviewRotation();

        _lockTiltOnDrag = EditorGUILayout.Toggle(
            new GUIContent("  Giữ góc khi kéo",
                "Khi kéo vật trong scene, rotation.x luôn bằng tilt X°"),
            _lockTiltOnDrag);

        EditorGUILayout.HelpBox(
            "Tất cả vật đặt xuống sẽ có rotation.x = " + _tiltAngleX.ToString("F1") +
            "°  (rotation.y & .z tùy chỉnh bên dưới)",
            MessageType.Info);
    }

    // ── Prefab section ───────────────────────────────────────────────────────
    private void DrawPrefabSection()
    {
        DrawSectionHeader("Prefab");

        EditorGUI.BeginChangeCheck();
        _selectedPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Prefab", _selectedPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            AddToRecent(_selectedPrefab);
            RefreshPreview();
        }

        if (_recentPrefabs.Count > 0)
        {
            EditorGUILayout.LabelField("Dùng gần đây:", EditorStyles.miniLabel);
            int cols = 3;
            int rows = Mathf.CeilToInt(_recentPrefabs.Count / (float)cols);
            for (int r = 0; r < rows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    if (idx >= _recentPrefabs.Count) break;
                    var pf = _recentPrefabs[idx];
                    if (pf == null) continue;
                    bool active = pf == _selectedPrefab;
                    GUI.backgroundColor = active ? Color.green : Color.white;
                    if (GUILayout.Button(pf.name, GUILayout.Height(22)))
                    {
                        _selectedPrefab = pf;
                        RefreshPreview();
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    // ── Placement mode section ───────────────────────────────────────────────
    private void DrawPlacementSection()
    {
        DrawSectionHeader("Chế độ đặt vật");

        bool isPlacing = _mode == PlaceMode.Placing;
        GUI.backgroundColor = isPlacing ? Color.red : Color.green;

        string btnLabel = isPlacing ? "■ Dừng đặt vật  [Esc]" : "▶ Bắt đầu đặt vật";
        if (GUILayout.Button(btnLabel, GUILayout.Height(34)))
        {
            if (isPlacing) StopPlacing();
            else StartPlacing();
        }
        GUI.backgroundColor = Color.white;

        _snapEnabled = EditorGUILayout.Toggle("Snap lưới", _snapEnabled);
        if (_snapEnabled)
            _snapSize = EditorGUILayout.FloatField("  Kích thước snap", _snapSize);
    }

    // ── Offset section ───────────────────────────────────────────────────────
    private void DrawOffsetSection()
    {
        DrawSectionHeader("Offset vị trí");

        _offsetFoldout = EditorGUILayout.Foldout(_offsetFoldout, "Chỉnh offset X / Y / Z", true);
        if (!_offsetFoldout) return;

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("X", GUILayout.Width(12));
        _posOffset.x = EditorGUILayout.FloatField(_posOffset.x);
        if (GUILayout.Button("0", GUILayout.Width(22))) _posOffset.x = 0f;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Y", GUILayout.Width(12));
        _posOffset.y = EditorGUILayout.FloatField(_posOffset.y);
        if (GUILayout.Button("0", GUILayout.Width(22))) _posOffset.y = 0f;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Z", GUILayout.Width(12));
        _posOffset.z = EditorGUILayout.FloatField(_posOffset.z);
        if (GUILayout.Button("0", GUILayout.Width(22))) _posOffset.z = 0f;
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset cả 3", GUILayout.Height(22)))
            _posOffset = Vector3.zero;
        if (GUILayout.Button("Áp offset cho vật đã chọn", GUILayout.Height(22)))
            ApplyOffsetToSelection();
        EditorGUILayout.EndHorizontal();

        if (_posOffset != Vector3.zero)
        {
            EditorGUILayout.HelpBox(
                $"Mỗi vật đặt xuống sẽ dịch thêm  X:{_posOffset.x:F2}  Y:{_posOffset.y:F2}  Z:{_posOffset.z:F2}",
                MessageType.None);
        }
    }

    // ── Transform section ────────────────────────────────────────────────────
    private void DrawTransformSection()
    {
        DrawSectionHeader("Transform ngẫu nhiên");

        EditorGUILayout.LabelField("Xoay Y ngẫu nhiên (độ):");
        EditorGUILayout.BeginHorizontal();
        _randomYRotMin = EditorGUILayout.FloatField("  Min", _randomYRotMin);
        _randomYRotMax = EditorGUILayout.FloatField("  Max", _randomYRotMax);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Scale ngẫu nhiên:");
        EditorGUILayout.BeginHorizontal();
        _uniformScaleMin = EditorGUILayout.FloatField("  Min", _uniformScaleMin);
        _uniformScaleMax = EditorGUILayout.FloatField("  Max", _uniformScaleMax);
        EditorGUILayout.EndHorizontal();

        _alignToNormal = EditorGUILayout.Toggle(
            new GUIContent("Align theo normal mặt đất",
                "Xoay vật theo hướng normal của bề mặt (tắt đi nếu muốn giữ tilt X cứng)"),
            _alignToNormal);
    }

    // ── Ground section ───────────────────────────────────────────────────────
    private void DrawGroundSection()
    {
        DrawSectionHeader("Mặt phẳng / Layer");

        _usePlaneInstead = EditorGUILayout.Toggle("Dùng plane ảo (không cần collider)", _usePlaneInstead);
        if (_usePlaneInstead)
        {
            _planeY = EditorGUILayout.FloatField("  Chiều cao plane Y", _planeY);
        }
        else
        {
            _groundLayer = LayerMaskField("  Ground layer", _groundLayer);
        }
    }

    // ── History section ──────────────────────────────────────────────────────
    private void DrawHistorySection()
    {
        DrawSectionHeader("Lịch sử");

        EditorGUILayout.LabelField($"Đã đặt: {_placedObjects.Count} vật", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Undo vật cuối", GUILayout.Height(24)))
            UndoLastPlaced();
        if (GUILayout.Button("Xóa lịch sử", GUILayout.Height(24)))
            _placedObjects.Clear();
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Áp tilt X cho vật đã chọn", GUILayout.Height(28)))
            ApplyTiltToSelection();
    }

    // ── Scene GUI ────────────────────────────────────────────────────────────
    private void OnSceneGUI(SceneView sceneView)
    {
        if (_mode != PlaceMode.Placing) return;

        Event e = Event.current;

        // Thoát bằng Esc
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            StopPlacing();
            e.Use();
            return;
        }

        // Ăn tất cả input để scene ko nhảy selection
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // Raycast tìm điểm đặt
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        _hitValid = GetPlacementPoint(ray, out _lastHitPoint);

        if (_hitValid)
        {
            // Cập nhật preview
            UpdatePreviewTransform(_lastHitPoint);

            // Click chuột trái → đặt vật
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                PlaceObject(_lastHitPoint);
                e.Use();
            }

            // Vẽ handle
            DrawPlacementHandle(_lastHitPoint);
        }

        sceneView.Repaint();
    }

    // ── Placement logic ──────────────────────────────────────────────────────
    private bool GetPlacementPoint(Ray ray, out Vector3 point)
    {
        point = Vector3.zero;

        if (_usePlaneInstead)
        {
            Plane p = new Plane(Vector3.up, new Vector3(0, _planeY, 0));
            if (p.Raycast(ray, out float d))
            {
                point = ray.GetPoint(d);
                return true;
            }
            return false;
        }

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _groundLayer))
        {
            point = hit.point;
            return true;
        }
        return false;
    }

    private void PlaceObject(Vector3 position)
    {
        if (_selectedPrefab == null) return;

        Vector3 snappedPos = _snapEnabled
            ? SnapPosition(position)
            : position;

        // Cộng offset
        snappedPos += _posOffset;

        // Tính rotation: X = tiltAngleX (cứng), Y = random, Z = 0
        float yRot = Random.Range(_randomYRotMin, _randomYRotMax);
        Quaternion rot = Quaternion.Euler(_tiltAngleX, yRot, 0f);

        // Instantiate
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(_selectedPrefab);
        go.transform.position = snappedPos;
        go.transform.rotation = rot;

        float scale = Random.Range(_uniformScaleMin, _uniformScaleMax);
        go.transform.localScale = Vector3.one * scale;

        // Đặt tên + parent
        go.name = _selectedPrefab.name;
        GameObjectUtility.SetStaticEditorFlags(go, 0);

        Undo.RegisterCreatedObjectUndo(go, "Place 2.5D Object");

        _placedObjects.Add(go);
        Selection.activeGameObject = go;
    }

    private Vector3 SnapPosition(Vector3 pos)
    {
        return new Vector3(
            Mathf.Round(pos.x / _snapSize) * _snapSize,
            pos.y,
            Mathf.Round(pos.z / _snapSize) * _snapSize
        );
    }

    // ── Preview ──────────────────────────────────────────────────────────────
    private void RefreshPreview()
    {
        DestroyPreview();
        if (_selectedPrefab == null || _mode != PlaceMode.Placing) return;

        _previewObj = Instantiate(_selectedPrefab);
        _previewObj.name = "__2.5D_PREVIEW__";
        SetPreviewMaterial(_previewObj);

        // Matikan semua komponen agar tidak trigger logic
        foreach (var comp in _previewObj.GetComponentsInChildren<MonoBehaviour>())
            comp.enabled = false;
        foreach (var col in _previewObj.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    private void UpdatePreviewTransform(Vector3 pos)
    {
        if (_previewObj == null) RefreshPreview();
        if (_previewObj == null) return;

        Vector3 finalPos = (_snapEnabled ? SnapPosition(pos) : pos) + _posOffset;
        _previewObj.transform.position = finalPos;

        float midY = (_randomYRotMin + _randomYRotMax) * 0.5f;
        _previewObj.transform.rotation = Quaternion.Euler(_tiltAngleX, midY, 0f);

        float midScale = (_uniformScaleMin + _uniformScaleMax) * 0.5f;
        _previewObj.transform.localScale = Vector3.one * midScale;
    }

    private void UpdatePreviewRotation()
    {
        if (_previewObj == null) return;
        Vector3 e = _previewObj.transform.eulerAngles;
        _previewObj.transform.rotation = Quaternion.Euler(_tiltAngleX, e.y, 0f);
    }

    private void DestroyPreview()
    {
        if (_previewObj != null)
        {
            DestroyImmediate(_previewObj);
            _previewObj = null;
        }
    }

    private void SetPreviewMaterial(GameObject go)
    {
        if (_previewMat == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = _previewMat;
            r.sharedMaterials = mats;
        }
    }

    private Material CreatePreviewMaterial()
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        mat.SetFloat("_Mode", 3);  // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        return mat;
    }

    // ── Handle drawing ────────────────────────────────────────────────────────
    private void DrawPlacementHandle(Vector3 pos)
    {
        // Vẽ vòng tròn tại điểm đặt
        Handles.color = new Color(0.3f, 1f, 0.3f, 0.8f);
        Handles.DrawWireDisc(pos, Vector3.up, 0.5f);

        // Vẽ trục thể hiện góc nghiêng
        Quaternion tiltRot = Quaternion.Euler(_tiltAngleX, 0f, 0f);
        Vector3 tiltUp = tiltRot * Vector3.up;

        Handles.color = Color.yellow;
        Handles.DrawLine(pos, pos + tiltUp * 1.5f);

        // Label góc
        Handles.Label(pos + Vector3.up * 2f + Vector3.right * 0.3f,
            $"X:{_tiltAngleX:F0}°",
            new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.yellow } });
    }

    // ── Mode control ─────────────────────────────────────────────────────────
    private void StartPlacing()
    {
        if (_selectedPrefab == null)
        {
            EditorUtility.DisplayDialog("2.5D Map Placer",
                "Hãy chọn Prefab trước!", "OK");
            return;
        }
        _mode = PlaceMode.Placing;
        RefreshPreview();
        SceneView.RepaintAll();
        Repaint();
    }

    private void StopPlacing()
    {
        _mode = PlaceMode.None;
        DestroyPreview();
        SceneView.RepaintAll();
        Repaint();
    }

    // ── Apply offset to selection ─────────────────────────────────────────────
    private void ApplyOffsetToSelection()
    {
        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("2.5D Map Placer", "Chưa chọn vật nào trong scene!", "OK");
            return;
        }
        Undo.RecordObjects(Selection.transforms, "Apply Position Offset");
        foreach (var go in Selection.gameObjects)
            go.transform.position += _posOffset;
    }

    // ── Apply tilt to selection ───────────────────────────────────────────────
    private void ApplyTiltToSelection()
    {
        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("2.5D Map Placer",
                "Chưa chọn vật nào trong scene!", "OK");
            return;
        }

        Undo.RecordObjects(Selection.transforms, "Apply 2.5D Tilt");
        foreach (var go in Selection.gameObjects)
        {
            Vector3 e = go.transform.eulerAngles;
            go.transform.rotation = Quaternion.Euler(_tiltAngleX, e.y, e.z);
        }
    }

    // ── Undo helpers ─────────────────────────────────────────────────────────
    private void UndoLastPlaced()
    {
        for (int i = _placedObjects.Count - 1; i >= 0; i--)
        {
            if (_placedObjects[i] != null)
            {
                Undo.DestroyObjectImmediate(_placedObjects[i]);
                _placedObjects.RemoveAt(i);
                return;
            }
            _placedObjects.RemoveAt(i);
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────────
    private void AddToRecent(GameObject prefab)
    {
        if (prefab == null) return;
        _recentPrefabs.Remove(prefab);
        _recentPrefabs.Insert(0, prefab);
        if (_recentPrefabs.Count > MAX_RECENT)
            _recentPrefabs.RemoveAt(_recentPrefabs.Count - 1);
    }

    private void DrawSectionHeader(string title)
    {
        GUILayout.Space(2);
        var rect = EditorGUILayout.BeginHorizontal();
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.15f));
        GUILayout.Space(4);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(2);
    }

    private static LayerMask LayerMaskField(string label, LayerMask mask)
    {
        var layers = new List<string>();
        var layerNumbers = new List<int>();
        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(layerName))
            {
                layers.Add(layerName);
                layerNumbers.Add(i);
            }
        }
        int maskVal = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
            if ((mask & (1 << layerNumbers[i])) != 0)
                maskVal |= (1 << i);

        maskVal = EditorGUILayout.MaskField(label, maskVal, layers.ToArray());

        int result = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
            if ((maskVal & (1 << i)) != 0)
                result |= (1 << layerNumbers[i]);
        return result;
    }
}
#endif