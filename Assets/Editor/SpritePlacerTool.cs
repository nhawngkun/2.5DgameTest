using UnityEngine;
using UnityEditor;

public class SpritePlacerTool : EditorWindow
{
    // ── Prefab & rotation ──────────────────────────────────────────
    private GameObject prefab;
    private float rotX = 0f;
    private float rotY = 0f;
    private float rotZ = 0f;

    // ── Plane ──────────────────────────────────────────────────────
    private GameObject targetPlane;
    private float manualPlaneY = 0f;

    // ── PPU ────────────────────────────────────────────────────────
    private bool autoDetectPPU = true;
    private float fallbackPPU = 100f;

    // ── Placement mode ─────────────────────────────────────────────
    private static bool isPlacing = false;
    private static SpritePlacerTool instance;

    [MenuItem("Tools/Sprite Placer Tool")]
    public static void ShowWindow()
    {
        var w = GetWindow<SpritePlacerTool>("Sprite Placer");
        w.minSize = new Vector2(360, 460);
        instance = w;
    }

    private void OnEnable() { instance = this; SceneView.duringSceneGui += OnSceneGUI; }
    private void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; StopPlacing(); }

    // ─────────────────────────────────────────────────────────────
    //  GUI
    // ─────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Sprite Placer Tool", new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 });
        EditorGUILayout.LabelField("Object cha dat xuong plane | Object con xoay Rotation X", EditorStyles.miniLabel);
        Separator();

        // ── Prefab ────────────────────────────────────────────────
        EditorGUILayout.LabelField("PREFAB", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(
            "Prefab phai co:\n" +
            "  • Root (object cha) — khong co component gi\n" +
            "  • Child (object con) — co SpriteRenderer, se duoc xoay Rotation X",
            MessageType.None);
        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);

        // Preview cấu trúc prefab
        if (prefab != null) DrawPrefabPreview(prefab);

        EditorGUILayout.Space(6);

        // ── Rotation ──────────────────────────────────────────────
        EditorGUILayout.LabelField("ROTATION OBJECT CON", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("(Object cha khong xoay — chi la anchor dat xuong plane)", EditorStyles.miniLabel);
        rotX = EditorGUILayout.Slider("Rotation X (con)", rotX, -180f, 180f);
        rotY = EditorGUILayout.Slider("Rotation Y (con)", rotY, -180f, 180f);
        rotZ = EditorGUILayout.Slider("Rotation Z (con)", rotZ, -180f, 180f);

        EditorGUILayout.Space(6);

        // ── Plane ─────────────────────────────────────────────────
        EditorGUILayout.LabelField("TARGET PLANE", EditorStyles.miniBoldLabel);
        targetPlane = (GameObject)EditorGUILayout.ObjectField("Plane Object", targetPlane, typeof(GameObject), true);
        if (targetPlane != null)
            EditorGUILayout.HelpBox("Plane Y = " + targetPlane.transform.position.y.ToString("F4"), MessageType.None);
        else
            manualPlaneY = EditorGUILayout.FloatField("Plane Y (nhap tay)", manualPlaneY);

        EditorGUILayout.Space(4);
        autoDetectPPU = EditorGUILayout.Toggle("Auto Pixels Per Unit", autoDetectPPU);
        if (!autoDetectPPU)
            fallbackPPU = EditorGUILayout.FloatField("Pixels Per Unit", fallbackPPU);

        // Preview Y offset
        if (prefab != null)
        {
            float off = ComputeYOffset(prefab, rotX, rotZ);
            EditorGUILayout.HelpBox(
                "Y offset tinh duoc: " + off.ToString("F5") + " u\n" +
                "Root se dat tai Y = " + (PlaneY() + off).ToString("F5"),
                MessageType.None);
        }

        Separator();

        // ── Nút ───────────────────────────────────────────────────
        if (!isPlacing)
        {
            GUI.enabled = prefab != null;
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
            if (GUILayout.Button("Bat dau dat  (click vao Scene)", GUILayout.Height(38)))
                StartPlacing();
            GUI.backgroundColor = old;
            GUI.enabled = true;
            if (prefab == null)
                EditorGUILayout.HelpBox("Keo Prefab vao o tren de bat dau.", MessageType.Warning);
        }
        else
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Dung dat  (hoac nhan Escape)", GUILayout.Height(38)))
                StopPlacing();
            GUI.backgroundColor = old;
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Dang dat — Click TRAI len Plane de spawn\nShift+Click: dat nhieu lan lien tiep\nESC: thoat",
                MessageType.Info);
        }
    }

    // Hiển thị nhanh cấu trúc prefab để user kiểm tra
    private void DrawPrefabPreview(GameObject go)
    {
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        SpriteRenderer srChild = go.GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
        {
            EditorGUILayout.HelpBox(
                "CANH BAO: SpriteRenderer nam tren ROOT!\n" +
                "Tool mong doi SR nam o object CON, khong phai cha.\n" +
                "Ket qua van chay nhung tinh toan co the lech.",
                MessageType.Warning);
        }
        else if (srChild != null)
        {
            string childName = srChild.gameObject.name;
            string spriteName = srChild.sprite != null ? srChild.sprite.name : "(null)";
            Vector3 childLocalPos = srChild.transform.localPosition;
            EditorGUILayout.HelpBox(
                "OK — Tim thay SpriteRenderer o con:\n" +
                "  Child: " + childName + "\n" +
                "  Sprite: " + spriteName + "\n" +
                "  Local pos cua con: " + childLocalPos.ToString("F3"),
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Khong tim thay SpriteRenderer trong prefab!", MessageType.Error);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  SCENE GUI
    // ─────────────────────────────────────────────────────────────
    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isPlacing || prefab == null) return;

        Handles.BeginGUI();
        GUI.color = Color.green;
        GUI.Label(new Rect(10, 10, 380, 24), "  [SpritePlacer] Click len Plane de dat  |  ESC de dung");
        GUI.color = Color.white;
        Handles.EndGUI();

        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        { StopPlacing(); e.Use(); return; }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Vector3? hit = RaycastToPlane(e.mousePosition);
            if (hit.HasValue)
            {
                PlacePrefab(hit.Value);
                if (!e.shift) StopPlacing();
            }
            e.Use();
        }

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    // ─────────────────────────────────────────────────────────────
    //  RAYCAST
    // ─────────────────────────────────────────────────────────────
    private Vector3? RaycastToPlane(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        if (targetPlane != null)
        {
            Collider col = targetPlane.GetComponent<Collider>();
            if (col != null && col.Raycast(ray, out RaycastHit hit, 10000f))
                return hit.point;
        }
        Plane horizontal = new Plane(Vector3.up, new Vector3(0, PlaneY(), 0));
        if (horizontal.Raycast(ray, out float dist))
            return ray.GetPoint(dist);
        return null;
    }

    // ─────────────────────────────────────────────────────────────
    //  SPAWN
    // ─────────────────────────────────────────────────────────────
    private void PlacePrefab(Vector3 clickPoint)
    {
        float py = PlaneY();
        float yOff = ComputeYOffset(prefab, rotX, rotZ);

        // Root (cha) đặt xuống plane — không xoay
        Vector3 rootPos = new Vector3(clickPoint.x, py + yOff, clickPoint.z);

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Place " + prefab.name);

        // Root: không xoay, chỉ set position
        go.transform.position = rootPos;
        go.transform.rotation = Quaternion.identity;

        // Con đầu tiên có SpriteRenderer: set rotation X/Y/Z
        SpriteRenderer srChild = go.GetComponentInChildren<SpriteRenderer>();
        if (srChild != null)
            srChild.transform.localRotation = Quaternion.Euler(rotX, rotY, rotZ);

        go.name = prefab.name;
        Debug.Log("[SpritePlacer] Dat '" + go.name + "'  rootY=" + rootPos.y.ToString("F5") + "  yOff=" + yOff.ToString("F5"));
        Repaint();
    }

    // ─────────────────────────────────────────────────────────────
    //  TÍNH Y OFFSET — tính đúng với cấu trúc cha/con
    //
    //  Sơ đồ:
    //    Root pivot (cha) = điểm đặt xuống plane  ← ta cần tìm Y của điểm này
    //    Child local pos  = offset của con so với cha (thường (0,0,0) nhưng có thể khác)
    //    Child local rot  = Quaternion.Euler(rotX, rotY, rotZ)
    //    Sprite corners   = 4 góc trong local space của con
    //
    //  World pos của 1 góc = Root.pos + childLocalPos + childRot * (corner * childScale)
    //  Ta cần:  min( worldY của các góc ) = planeY
    //  Suy ra:  Root.pos.y = planeY - min( childLocalPos.y + (childRot*corner).y )
    //  Offset   = -min( childLocalPos.y + (childRot*corner).y )
    // ─────────────────────────────────────────────────────────────
    private float ComputeYOffset(GameObject rootPrefab, float rx, float rz)
    {
        // Tìm child có SpriteRenderer
        SpriteRenderer sr = rootPrefab.GetComponent<SpriteRenderer>();
        bool srOnRoot = sr != null;
        if (sr == null) sr = rootPrefab.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return 0f;

        Sprite sprite = sr.sprite;
        float ppu = autoDetectPPU ? sprite.pixelsPerUnit : fallbackPPU;
        if (ppu <= 0) ppu = 100f;

        Rect rect = sprite.rect;
        float w = rect.width / ppu;
        float h = rect.height / ppu;

        // Pivot offset từ tâm sprite (units)
        Vector2 pivNorm = sprite.pivot / new Vector2(rect.width, rect.height);
        float pivOffX = (pivNorm.x - 0.5f) * w;
        float pivOffY = (pivNorm.y - 0.5f) * h;

        // Scale của child (local scale trong prefab)
        Vector3 sc = sr.transform.localScale;

        // Local position của child so với root
        // Nếu SR nằm trên root thì localPosition = zero
        Vector3 childLocalPos = srOnRoot ? Vector3.zero : sr.transform.localPosition;

        // 4 góc sprite trong local space của child (relative to child pivot)
        Vector3[] corners =
        {
            new Vector3((-w * 0.5f - pivOffX) * sc.x, (-h * 0.5f - pivOffY) * sc.y, 0f),
            new Vector3(( w * 0.5f - pivOffX) * sc.x, (-h * 0.5f - pivOffY) * sc.y, 0f),
            new Vector3((-w * 0.5f - pivOffX) * sc.x, ( h * 0.5f - pivOffY) * sc.y, 0f),
            new Vector3(( w * 0.5f - pivOffX) * sc.x, ( h * 0.5f - pivOffY) * sc.y, 0f),
        };

        // Rotation của child (rx, rz — ry không ảnh hưởng minY)
        Quaternion childRot = Quaternion.Euler(rx, 0f, rz);

        // Tìm điểm thấp nhất trong world Y
        // worldY = rootY + childLocalPos.y + (childRot * corner).y
        // ta cần rootY sao cho min(worldY) = planeY
        // → offset = -min(childLocalPos.y + (childRot*corner).y)
        float minY = float.MaxValue;
        foreach (var c in corners)
        {
            float worldY = childLocalPos.y + (childRot * c).y;
            if (worldY < minY) minY = worldY;
        }

        return -minY;
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────
    private void StartPlacing() { isPlacing = true; SceneView.RepaintAll(); Repaint(); }
    private static void StopPlacing() { isPlacing = false; SceneView.RepaintAll(); instance?.Repaint(); }
    private float PlaneY() => targetPlane != null ? targetPlane.transform.position.y : manualPlaneY;

    private void Separator()
    {
        EditorGUILayout.Space(5);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.35f));
        EditorGUILayout.Space(5);
    }
}