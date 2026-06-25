using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// Editor Window
// ─────────────────────────────────────────────────────────────────────────────

public class TerrainDetailAutoPainter : EditorWindow
{
    private TerrainDetailRuleset ruleset;
    private SerializedObject     serializedRuleset;

    private bool hasSample = false;

    private const float AbsoluteMax = 10000f;

    private float ActiveMin => ruleset == null || ruleset.heightMode == HeightRangeMode.Absolute
        ? 0f : ruleset.sampledHeightMin;
    private float ActiveMax => ruleset == null || ruleset.heightMode == HeightRangeMode.Absolute
        ? AbsoluteMax : ruleset.sampledHeightMax;

    [SerializeField] private List<Terrain> targets       = new List<Terrain>();
    [SerializeField] private bool          clearFirst     = true;

    private ReorderableList  terrainList;
    private SerializedObject serializedSelf;
    private Vector2          scroll;

    [MenuItem("Tools/Terrain Detail Auto Painter")]
    public static void Open()
    {
        var win = GetWindow<TerrainDetailAutoPainter>("Terrain Detail Auto Painter");
        win.minSize = new Vector2(440f, 500f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        serializedSelf = new SerializedObject(this);
        BuildTerrainList();
    }

    private void OnRulesetChanged()
    {
        serializedRuleset = ruleset != null ? new SerializedObject(ruleset) : null;
        hasSample = ruleset != null
            && ruleset.heightMode != HeightRangeMode.Absolute
            && ruleset.sampledHeightMax > ruleset.sampledHeightMin;
        Repaint();
    }

    private void BuildTerrainList()
    {
        var prop = serializedSelf.FindProperty("targets");
        terrainList = new ReorderableList(serializedSelf, prop,
            draggable: true, displayHeader: true,
            displayAddButton: true, displayRemoveButton: true);

        terrainList.drawHeaderCallback = r =>
            EditorGUI.LabelField(r, "Target Terrain Tiles");

        terrainList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;

        terrainList.drawElementCallback = (rect, index, active, focused) =>
        {
            var elemProp = prop.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y + 2f, rect.width, EditorGUIUtility.singleLineHeight),
                elemProp, GUIContent.none);
        };

        terrainList.onAddCallback = list =>
        {
            targets.Add(null);
            serializedSelf.Update();
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GUI
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        serializedSelf.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        // ── Ruleset asset ─────────────────────────────────────────────────────
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Ruleset", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        ruleset = (TerrainDetailRuleset)EditorGUILayout.ObjectField(
            "Active Ruleset", ruleset, typeof(TerrainDetailRuleset), false);
        if (EditorGUI.EndChangeCheck())
            OnRulesetChanged();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("New Ruleset"))
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "New Detail Ruleset", "NewTerrainDetailRuleset", "asset", "Save ruleset as");
                if (!string.IsNullOrEmpty(path))
                {
                    var newRuleset = CreateInstance<TerrainDetailRuleset>();
                    AssetDatabase.CreateAsset(newRuleset, path);
                    AssetDatabase.SaveAssets();
                    ruleset = newRuleset;
                    OnRulesetChanged();
                }
            }

            GUI.enabled = ruleset != null;
            if (GUILayout.Button("Save Ruleset"))
            {
                EditorUtility.SetDirty(ruleset);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TerrainDetailAutoPainter] Ruleset saved: {AssetDatabase.GetAssetPath(ruleset)}");
            }
            GUI.enabled = true;
        }

        if (ruleset == null)
        {
            EditorGUILayout.HelpBox("Create or load a ruleset to continue.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            serializedSelf.ApplyModifiedProperties();
            return;
        }

        serializedRuleset?.Update();

        // ── Target terrains ───────────────────────────────────────────────────
        EditorGUILayout.Space(6f);
        terrainList.DoLayoutList();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add All Scene Terrains"))
            {
                foreach (var t in FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                    if (!targets.Contains(t))
                        targets.Add(t);
                serializedSelf.Update();
            }
            if (GUILayout.Button("Remove All"))
            {
                targets.Clear();
                serializedSelf.Update();
            }
        }

        // ── Paint options ─────────────────────────────────────────────────────
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Paint Options", EditorStyles.boldLabel);
        clearFirst = EditorGUILayout.Toggle(
            new GUIContent("Clear Before Paint",
                "Erase all existing detail layers on the target terrains before writing new ones."),
            clearFirst);

        // ── Height range mode ─────────────────────────────────────────────────
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Height Range", EditorStyles.boldLabel);

        ruleset.heightMode = (HeightRangeMode)EditorGUILayout.EnumPopup("Mode", ruleset.heightMode);

        if (ruleset.heightMode == HeightRangeMode.Absolute)
        {
            EditorGUILayout.HelpBox(
                "Rules use 0 – 10,000 m (Unity absolute). Portable across any terrain.",
                MessageType.None);
        }
        else
        {
            bool localReady = HasValidTargets();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = localReady;
                if (GUILayout.Button("Sample Local Tiles"))
                    DoSample(globalScan: false);
                GUI.enabled = true;

                if (GUILayout.Button("Sample All Scene Terrains"))
                {
                    if (EditorUtility.DisplayDialog("Global Sample",
                        "This scans every Terrain in the scene and may be slow on large worlds. Continue?",
                        "Sample", "Cancel"))
                        DoSample(globalScan: true);
                }
            }

            if (!localReady && ruleset.heightMode == HeightRangeMode.LocalSample)
                EditorGUILayout.HelpBox("Add target tiles before sampling locally.", MessageType.Warning);

            if (hasSample)
                EditorGUILayout.HelpBox(
                    $"Sampled range: {ruleset.sampledHeightMin:F1} m – {ruleset.sampledHeightMax:F1} m",
                    MessageType.None);
            else
                EditorGUILayout.HelpBox("No sample taken yet. Run a sample to set the height range.", MessageType.Warning);
        }

        // ── Rules ─────────────────────────────────────────────────────────────
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Detail Rules  (each layer is independent – order sets prototype index)", EditorStyles.boldLabel);
        EditorGUILayout.Space(2f);

        for (int i = 0; i < ruleset.rules.Count; i++)
            DrawRule(i);

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Rule"))
            {
                Undo.RecordObject(ruleset, "Add Detail Rule");
                ruleset.rules.Add(new TerrainDetailRule
                {
                    name      = $"Rule {ruleset.rules.Count}",
                    heightMin = ActiveMin,
                    heightMax = ActiveMax
                });
                EditorUtility.SetDirty(ruleset);
            }
            GUILayout.FlexibleSpace();
        }

        // ── Actions ───────────────────────────────────────────────────────────
        EditorGUILayout.Space(10f);
        bool canPaint = HasValidTargets() && HasValidRules()
                     && (ruleset.heightMode == HeightRangeMode.Absolute || hasSample);
        GUI.enabled = canPaint;
        if (GUILayout.Button("Paint Selected Terrains", GUILayout.Height(30f)))
            PaintAll();
        GUI.enabled = true;

        if (!HasValidTargets())
            EditorGUILayout.HelpBox("Add at least one target terrain tile.", MessageType.Info);
        else if (!HasValidRules())
            EditorGUILayout.HelpBox("Each rule needs a detail prototype configured (assign a texture or mesh).", MessageType.Warning);
        else if (ruleset.heightMode != HeightRangeMode.Absolute && !hasSample)
            EditorGUILayout.HelpBox("Run a sample before painting.", MessageType.Warning);

        EditorGUILayout.Space(6f);
        EditorGUILayout.EndScrollView();

        serializedRuleset?.ApplyModifiedProperties();
        serializedSelf.ApplyModifiedProperties();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(ruleset);
            Repaint();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Rule drawer
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawRule(int index)
    {
        var rule = ruleset.rules[index];

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            rule.editorColor = EditorGUILayout.ColorField(
                GUIContent.none, rule.editorColor,
                showEyedropper: false, showAlpha: false, hdr: false,
                GUILayout.Width(30f));

            rule.foldout = EditorGUILayout.Foldout(rule.foldout, $"{index}: {rule.name}", toggleOnLabelClick: true);

            GUILayout.FlexibleSpace();

            if (index > 0 && GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                Undo.RecordObject(ruleset, "Reorder Detail Rule");
                (ruleset.rules[index], ruleset.rules[index - 1]) = (ruleset.rules[index - 1], ruleset.rules[index]);
                EditorUtility.SetDirty(ruleset);
                return;
            }
            if (index < ruleset.rules.Count - 1 && GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                Undo.RecordObject(ruleset, "Reorder Detail Rule");
                (ruleset.rules[index], ruleset.rules[index + 1]) = (ruleset.rules[index + 1], ruleset.rules[index]);
                EditorUtility.SetDirty(ruleset);
                return;
            }
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                Undo.RecordObject(ruleset, "Remove Detail Rule");
                ruleset.rules.RemoveAt(index);
                EditorUtility.SetDirty(ruleset);
                return;
            }
        }

        if (!rule.foldout) return;

        EditorGUI.BeginChangeCheck();

        using (new EditorGUI.IndentLevelScope())
        {
            rule.name = EditorGUILayout.TextField("Name", rule.name);

            EditorGUILayout.Space(4f);

            // ── Prototype fields ──────────────────────────────────────────────
            EditorGUILayout.LabelField("Detail Prototype", EditorStyles.boldLabel);

            rule.detailPrototype.usePrototypeMesh =
                EditorGUILayout.Toggle("Use Mesh (vs Texture)", rule.detailPrototype.usePrototypeMesh);

            if (rule.detailPrototype.usePrototypeMesh)
            {
                rule.detailPrototype.prototype = (GameObject)EditorGUILayout.ObjectField(
                    "Mesh Prefab", rule.detailPrototype.prototype, typeof(GameObject), false);

                rule.detailPrototype.renderMode =
                    (DetailRenderMode)EditorGUILayout.EnumPopup("Render Mode", rule.detailPrototype.renderMode);
            }
            else
            {
                rule.detailPrototype.prototypeTexture = (Texture2D)EditorGUILayout.ObjectField(
                    "Grass Texture", rule.detailPrototype.prototypeTexture, typeof(Texture2D), false);

                rule.detailPrototype.renderMode = DetailRenderMode.GrassBillboard;
            }

            EditorGUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                rule.detailPrototype.minWidth  = EditorGUILayout.FloatField("Min Width",  rule.detailPrototype.minWidth);
                rule.detailPrototype.maxWidth  = EditorGUILayout.FloatField("Max Width",  rule.detailPrototype.maxWidth);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                rule.detailPrototype.minHeight = EditorGUILayout.FloatField("Min Height", rule.detailPrototype.minHeight);
                rule.detailPrototype.maxHeight = EditorGUILayout.FloatField("Max Height", rule.detailPrototype.maxHeight);
            }

            rule.detailPrototype.noiseSpread =
                EditorGUILayout.Slider("Noise Spread", rule.detailPrototype.noiseSpread, 0f, 1f);

            rule.detailPrototype.healthyColor =
                EditorGUILayout.ColorField("Healthy Color", rule.detailPrototype.healthyColor);
            rule.detailPrototype.dryColor =
                EditorGUILayout.ColorField("Dry Color", rule.detailPrototype.dryColor);

            EditorGUILayout.Space(4f);

            // ── Density ───────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Density", EditorStyles.boldLabel);
            rule.maxDensity = EditorGUILayout.IntSlider("Max Density", rule.maxDensity, 0, 255);

            EditorGUILayout.Space(4f);

            // ── Height ────────────────────────────────────────────────────────
            float rangeMin = ActiveMin;
            float rangeMax = ActiveMax;

            string heightLabel = ruleset.heightMode == HeightRangeMode.Absolute
                ? "Height Rule  (meters, 0 – 10,000)"
                : $"Height Rule  (meters, {rangeMin:F1} – {rangeMax:F1})";
            EditorGUILayout.LabelField(heightLabel, EditorStyles.boldLabel);

            rule.heightMin     = EditorGUILayout.FloatField("Min (m)", rule.heightMin);
            rule.heightMax     = EditorGUILayout.FloatField("Max (m)", rule.heightMax);
            rule.heightFalloff = EditorGUILayout.Slider("Falloff", rule.heightFalloff, 0f, 1f);

            EditorGUILayout.Space(4f);

            // ── Slope ─────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Slope Rule  (degrees)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                float sMin = rule.slopeMin, sMax = rule.slopeMax;
                EditorGUILayout.MinMaxSlider(ref sMin, ref sMax, 0f, 90f);
                EditorGUILayout.LabelField($"{sMin:F1}°–{sMax:F1}°", GUILayout.Width(90f));
                rule.slopeMin = sMin;
                rule.slopeMax = sMax;
            }
            rule.slopeFalloff = EditorGUILayout.Slider("Falloff (°)", rule.slopeFalloff, 0f, 45f);

            EditorGUILayout.Space(6f);
        }

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(ruleset);

        var divRect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(divRect, new Color(0.5f, 0.5f, 0.5f, 0.35f));
        EditorGUILayout.Space(4f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sampling
    // ─────────────────────────────────────────────────────────────────────────

    private void DoSample(bool globalScan)
    {
        IEnumerable<Terrain> pool = globalScan
            ? FindObjectsByType<Terrain>(FindObjectsSortMode.None)
            : targets.Where(t => t != null);

        float foundMin = float.MaxValue;
        float foundMax = float.MinValue;
        int   count    = 0;

        try
        {
            var terrainArray = pool.ToArray();
            for (int t = 0; t < terrainArray.Length; t++)
            {
                var terrain = terrainArray[t];
                EditorUtility.DisplayProgressBar("Sampling Terrain Heights",
                    terrain.name, (float)t / terrainArray.Length);

                TerrainData data   = terrain.terrainData;
                int         hmRes  = data.heightmapResolution;
                float       baseY  = terrain.transform.position.y;
                float[,]    heights = data.GetHeights(0, 0, hmRes, hmRes);
                float       terrH  = data.size.y;

                for (int row = 0; row < hmRes; row++)
                for (int col = 0; col < hmRes; col++)
                {
                    float worldH = baseY + heights[row, col] * terrH;
                    if (worldH < foundMin) foundMin = worldH;
                    if (worldH > foundMax) foundMax = worldH;
                }
                count++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (count == 0)
        {
            Debug.LogWarning("[TerrainDetailAutoPainter] No terrains found to sample.");
            return;
        }

        float margin = (foundMax - foundMin) * 0.01f;
        Undo.RecordObject(ruleset, "Sample Terrain Heights");
        ruleset.sampledHeightMin = Mathf.Floor(foundMin - margin);
        ruleset.sampledHeightMax = Mathf.Ceil(foundMax  + margin);
        hasSample = true;
        EditorUtility.SetDirty(ruleset);

        Debug.Log($"[TerrainDetailAutoPainter] Sampled {count} terrain(s): {ruleset.sampledHeightMin:F1} m – {ruleset.sampledHeightMax:F1} m");
        Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Painting
    // ─────────────────────────────────────────────────────────────────────────

    private void PaintAll()
    {
        float hMin = ActiveMin;
        float hMax = ActiveMax;

        // Build prototype array from rules – one prototype per rule, in order.
        // This is what gets written to terrain.terrainData.detailPrototypes.
        var prototypes = ruleset.rules
            .Select(r => r.detailPrototype)
            .ToArray();

        try
        {
            for (int t = 0; t < targets.Count; t++)
            {
                if (targets[t] == null) continue;
                EditorUtility.DisplayProgressBar("Terrain Detail Auto Painter",
                    $"Painting {targets[t].name}  ({t + 1}/{targets.Count})",
                    (float)t / targets.Count);
                PaintTerrain(targets[t], prototypes, ruleset.rules, hMin, hMax, clearFirst);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[TerrainDetailAutoPainter] Done. {targets.Count} tile(s), {ruleset.rules.Count} rule(s).");
    }

    private static void PaintTerrain(Terrain terrain, DetailPrototype[] prototypes,
                                     List<TerrainDetailRule> rules,
                                     float hRangeMin, float hRangeMax, bool clearFirst)
    {
        TerrainData data  = terrain.terrainData;
        int         dw    = data.detailWidth;
        int         dh    = data.detailHeight;
        int         hmRes = data.heightmapResolution;
        float       baseY = terrain.transform.position.y;
        float       hSpan = hRangeMax - hRangeMin;

        Undo.RegisterCompleteObjectUndo(data, "Terrain Detail Auto Paint");

        // Replace prototype list with our ruleset's prototypes.
        // Existing detail data is cleared automatically when the list changes.
        data.detailPrototypes = prototypes;

        // If the prototype count didn't change Unity may not have auto-cleared,
        // so honour the explicit clearFirst flag.
        if (clearFirst)
        {
            var empty = new int[dh, dw];
            for (int layer = 0; layer < prototypes.Length; layer++)
                data.SetDetailLayer(0, 0, layer, empty);
        }

        for (int layer = 0; layer < rules.Count; layer++)
        {
            var   rule    = rules[layer];
            int[,] density = new int[dh, dw];

            for (int dy = 0; dy < dh; dy++)
            {
                for (int dx = 0; dx < dw; dx++)
                {
                    float nx = (float)dx / (dw - 1);
                    float nz = (float)dy / (dh - 1);

                    float worldH = baseY + data.GetHeight(
                        Mathf.RoundToInt(nx * (hmRes - 1)),
                        Mathf.RoundToInt(nz * (hmRes - 1)));

                    float slope  = data.GetSteepness(nx, nz);
                    float weight = EvaluateRule(rule, worldH, slope, hRangeMin, hSpan);

                    density[dy, dx] = Mathf.RoundToInt(weight * rule.maxDensity);
                }
            }

            data.SetDetailLayer(0, 0, layer, density);
        }

        EditorUtility.SetDirty(data);
    }

    private static float EvaluateRule(TerrainDetailRule rule, float worldH, float slope,
                                      float hRangeMin, float hSpan)
    {
        float hFalloffMeters = rule.heightFalloff * hSpan;
        float hWeight = RangeWeight(worldH, rule.heightMin, rule.heightMax, hFalloffMeters);
        float sWeight = RangeWeight(slope,  rule.slopeMin,  rule.slopeMax,  rule.slopeFalloff);
        return hWeight * sWeight;
    }

    private static float RangeWeight(float value, float min, float max, float falloff)
    {
        if (value < min - falloff || value > max + falloff) return 0f;
        if (falloff <= 0f) return (value >= min && value <= max) ? 1f : 0f;
        float lower = Mathf.SmoothStep(0f, 1f, (value - (min - falloff)) / falloff);
        float upper = Mathf.SmoothStep(0f, 1f, ((max + falloff) - value) / falloff);
        return Mathf.Min(lower, upper);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Validation
    // ─────────────────────────────────────────────────────────────────────────

    private bool HasValidTargets() => targets.Count > 0 && targets.Exists(t => t != null);

    private bool HasValidRules()
    {
        if (ruleset == null || ruleset.rules.Count == 0) return false;
        return ruleset.rules.Exists(r =>
            r.detailPrototype.usePrototypeMesh
                ? r.detailPrototype.prototype != null
                : r.detailPrototype.prototypeTexture != null);
    }
}
