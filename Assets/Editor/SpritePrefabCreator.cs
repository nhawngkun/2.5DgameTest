// SpritePrefabCreator.cs
// Đặt file này vào thư mục: Assets/Editor/
// Unity 6 - 3D Project

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class SpritePrefabCreator : EditorWindow
{
    // ── Settings ──────────────────────────────────────────────────────────────
    private string imagesFolderPath = "Assets/Images";   // folder chứa ảnh
    private string outputPrefabPath = "Assets/Prefabs";  // folder xuất prefab
    private float  rotationX        = 0f;                // rotation X cho child
    private bool   useCustomName    = false;
    private string customPrefix     = "";

    // ── State ─────────────────────────────────────────────────────────────────
    private Vector2    scrollPos;
    private List<Texture2D> previewTextures = new();
    private string     statusMessage  = "";
    private MessageType statusType    = MessageType.None;
    private bool       isProcessing   = false;

    // ── Style cache ───────────────────────────────────────────────────────────
    private GUIStyle headerStyle;
    private GUIStyle sectionBoxStyle;

    // ── Menu entry ────────────────────────────────────────────────────────────
    [MenuItem("Tools/Sprite Prefab Creator")]
    public static void ShowWindow()
    {
        var win = GetWindow<SpritePrefabCreator>("Sprite Prefab Creator");
        win.minSize = new Vector2(420, 560);
        win.Show();
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        InitStyles();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawHeader();
        GUILayout.Space(10);

        DrawPathSection();
        GUILayout.Space(8);

        DrawRotationSection();
        GUILayout.Space(8);

        DrawNamingSection();
        GUILayout.Space(8);

        DrawPreviewSection();
        GUILayout.Space(8);

        DrawActionButtons();
        GUILayout.Space(6);

        DrawStatusBar();

        EditorGUILayout.EndScrollView();
    }

    // ── Draw helpers ──────────────────────────────────────────────────────────

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("⚙  Sprite → Prefab Batch Creator", headerStyle);
        EditorGUILayout.LabelField("Tạo hàng loạt Prefab từ folder ảnh  |  Unity 6 / 3D",
            EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawPathSection()
    {
        EditorGUILayout.BeginVertical(sectionBoxStyle);
        EditorGUILayout.LabelField("📁  Đường dẫn", EditorStyles.boldLabel);
        GUILayout.Space(4);

        // Images folder
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Folder ảnh:", GUILayout.Width(90));
        imagesFolderPath = EditorGUILayout.TextField(imagesFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string picked = EditorUtility.OpenFolderPanel("Chọn folder ảnh",
                Application.dataPath, "");
            if (!string.IsNullOrEmpty(picked))
                imagesFolderPath = "Assets" + picked.Replace(Application.dataPath, "");
        }
        EditorGUILayout.EndHorizontal();

        // Output folder
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Xuất Prefab:", GUILayout.Width(90));
        outputPrefabPath = EditorGUILayout.TextField(outputPrefabPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string picked = EditorUtility.OpenFolderPanel("Chọn folder xuất prefab",
                Application.dataPath, "");
            if (!string.IsNullOrEmpty(picked))
                outputPrefabPath = "Assets" + picked.Replace(Application.dataPath, "");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawRotationSection()
    {
        EditorGUILayout.BeginVertical(sectionBoxStyle);
        EditorGUILayout.LabelField("🔄  Rotation Child Object", EditorStyles.boldLabel);
        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Rotation X:", GUILayout.Width(90));
        rotationX = EditorGUILayout.Slider(rotationX, -360f, 360f);
        EditorGUILayout.LabelField($"{rotationX:F1}°", GUILayout.Width(48));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Cấu trúc Prefab:\n" +
            "  [Parent]  — Transform gốc (không có gì)\n" +
            "  └─ [Child] — Sprite Renderer, Rotation X = " + rotationX.ToString("F1") + "°",
            MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    private void DrawNamingSection()
    {
        EditorGUILayout.BeginVertical(sectionBoxStyle);
        EditorGUILayout.LabelField("🏷  Đặt tên Prefab", EditorStyles.boldLabel);
        GUILayout.Space(4);

        useCustomName = EditorGUILayout.ToggleLeft("Thêm prefix vào tên", useCustomName);
        if (useCustomName)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prefix:", GUILayout.Width(90));
            customPrefix = EditorGUILayout.TextField(customPrefix);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"Ví dụ: {customPrefix}ImageName.prefab",
                EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Ví dụ: ImageName.prefab", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewSection()
    {
        EditorGUILayout.BeginVertical(sectionBoxStyle);
        EditorGUILayout.LabelField("🖼  Preview ảnh tìm thấy", EditorStyles.boldLabel);
        GUILayout.Space(4);

        if (GUILayout.Button("🔍  Scan folder ảnh"))
            ScanImages();

        if (previewTextures.Count > 0)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField($"Tìm thấy {previewTextures.Count} ảnh:",
                EditorStyles.miniLabel);
            GUILayout.Space(2);

            float thumbSize = 56f;
            float padding   = 4f;
            float winW      = position.width - 40f;
            int   cols      = Mathf.Max(1, (int)(winW / (thumbSize + padding)));

            int row = 0;
            for (int i = 0; i < previewTextures.Count; i++)
            {
                if (i % cols == 0)
                {
                    if (row > 0) EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    row++;
                }
                if (previewTextures[i] != null)
                    GUILayout.Label(previewTextures[i],
                        GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
            }
            if (previewTextures.Count > 0) EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("Chưa scan. Nhấn 'Scan folder ảnh' để xem.",
                EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawActionButtons()
    {
        GUI.enabled = !isProcessing;

        Color old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.4f);
        if (GUILayout.Button("✅  Tạo tất cả Prefab", GUILayout.Height(36)))
            CreatePrefabs();
        GUI.backgroundColor = old;

        GUI.enabled = true;
    }

    private void DrawStatusBar()
    {
        if (!string.IsNullOrEmpty(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, statusType);
    }

    // ── Logic ─────────────────────────────────────────────────────────────────

    private void ScanImages()
    {
        previewTextures.Clear();
        statusMessage = "";

        if (!Directory.Exists(imagesFolderPath))
        {
            SetStatus($"❌ Không tìm thấy folder: {imagesFolderPath}", MessageType.Error);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { imagesFolderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) previewTextures.Add(tex);
        }

        SetStatus(previewTextures.Count > 0
            ? $"✅ Tìm thấy {previewTextures.Count} ảnh trong folder."
            : "⚠️ Không có ảnh nào trong folder này.",
            previewTextures.Count > 0 ? MessageType.Info : MessageType.Warning);

        Repaint();
    }

    private void CreatePrefabs()
    {
        // Validate
        if (!Directory.Exists(imagesFolderPath))
        {
            SetStatus($"❌ Folder ảnh không tồn tại: {imagesFolderPath}", MessageType.Error);
            return;
        }

        // Ensure output folder exists
        if (!Directory.Exists(outputPrefabPath))
        {
            Directory.CreateDirectory(outputPrefabPath);
            AssetDatabase.Refresh();
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { imagesFolderPath });
        if (guids.Length == 0)
        {
            SetStatus("⚠️ Không tìm thấy ảnh nào để tạo prefab.", MessageType.Warning);
            return;
        }

        isProcessing = true;
        int created = 0;
        int skipped = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < guids.Length; i++)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (texture == null) { skipped++; continue; }

                // Convert texture → Sprite (or load existing sprite)
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
                if (sprite == null)
                {
                    // Force sprite import mode
                    TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
                    if (ti != null && ti.textureType != TextureImporterType.Sprite)
                    {
                        ti.textureType        = TextureImporterType.Sprite;
                        ti.spriteImportMode   = SpriteImportMode.Single;
                        ti.SaveAndReimport();
                        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
                    }
                }

                // ── Build hierarchy in scene ──────────────────────────────────
                // Parent: empty GameObject
                string imageName   = Path.GetFileNameWithoutExtension(texPath);
                string prefabName  = useCustomName ? $"{customPrefix}{imageName}" : imageName;

                GameObject parent = new GameObject(prefabName);
                parent.transform.position   = Vector3.zero;
                parent.transform.rotation   = Quaternion.identity;
                parent.transform.localScale = Vector3.one;

                // Child: SpriteRenderer with rotationX
                GameObject child = new GameObject(imageName + "_Sprite");
                child.transform.SetParent(parent.transform, false);
                child.transform.localPosition   = Vector3.zero;
                child.transform.localEulerAngles = new Vector3(rotationX, 0f, 0f);
                child.transform.localScale      = Vector3.one;

                var sr = child.AddComponent<SpriteRenderer>();
                if (sprite != null) sr.sprite = sprite;

                // ── Save as Prefab ────────────────────────────────────────────
                string savePath = $"{outputPrefabPath}/{prefabName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(parent, savePath);

                // Cleanup scene
                DestroyImmediate(parent);
                created++;

                // Progress bar
                EditorUtility.DisplayProgressBar(
                    "Tạo Prefab...",
                    $"{created}/{guids.Length}: {prefabName}",
                    (float)(i + 1) / guids.Length);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
            isProcessing = false;
        }

        // Refresh preview list
        ScanImages();
        SetStatus($"✅ Đã tạo {created} prefab! (bỏ qua: {skipped})  →  {outputPrefabPath}",
            MessageType.Info);
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private void SetStatus(string msg, MessageType type)
    {
        statusMessage = msg;
        statusType    = type;
        Repaint();
    }

    private void InitStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 14,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.85f, 0.95f, 1f)
                : new Color(0.1f, 0.25f, 0.5f);
        }

        if (sectionBoxStyle == null)
        {
            sectionBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };
        }
    }
}
