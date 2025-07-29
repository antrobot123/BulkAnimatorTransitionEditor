using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class TransitionBulkEditor : EditorWindow
{
    private BulkSelectionMode bulkMode = BulkSelectionMode.SelectedOnly;
    private List<string> specificLayerNames = new();
    private BulkSelectionMode lastBulkMode = BulkSelectionMode.SelectedOnly;
    public bool UpdateGrouping = true;


    private AnimatorTransitionBase[] selectedTransitions;
    private List<ConditionRow> conditionRows = new();
    private ConditionGroupingType selectedGrouping = ConditionGroupingType.ComparisonMode;
    private Dictionary<string, AnimatorControllerParameterType> parameterTypeMap = new();
    Vector2 scrollPos;
    private string layerInputBuffer = "";
    private bool useComplexGrouping = false;
    Dictionary<string, List<ConditionRow>> grouped;
    string[] ValidNames = { };
    int selectedIndex;


    private void Serialize()
    {
        //BulkSelectionMode
        EditorPrefs.SetInt("TransitionEditor/BulkSelectionMode", (int)bulkMode);
        //specificLayerNames
        EditorPrefs.SetString("TransitionEditor/SpecificLayerNames", string.Join(",", specificLayerNames));
        //useComplexGrouping
        EditorPrefs.SetBool("TransitionEditor/UseComplexGrouping", useComplexGrouping);
        //ComparisonMode
        EditorPrefs.SetInt("TransitionEditor/ComparisonMode", (int)selectedGrouping);
    }
    private void Deserialize()
    {
        //BulkSelectionMode
        bulkMode = (BulkSelectionMode)EditorPrefs.GetInt("TransitionEditor/BulkSelectionMode");
        //specificLayerNames
        specificLayerNames = EditorPrefs.GetString("TransitionEditor/SpecificLayerNames").Split(',').Select(s => s.Trim()).ToList();
        //useComplexGrouping
        useComplexGrouping = EditorPrefs.GetBool("TransitionEditor/UseComplexGrouping");
        //ComparisonMode
        selectedGrouping = (ConditionGroupingType)EditorPrefs.GetInt("TransitionEditor/ComparisonMode");
    }



    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        Deserialize();
        RecalculateGrouping();
    }
    private void OnDestroy()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        Serialize();
    }

    [MenuItem("antrobot/Transition Bulk Editor")]
    public static void ShowWindow()
    {
        GetWindow<TransitionBulkEditor>("Transition Bulk Editor");
    }

    private void OnFocus() => RefreshSelection();
    private void OnSelectionChange()
    {
        UpdateGrouping = true;
        RefreshSelection();
        Repaint();
    }

    private bool ShowSelectionWarning()
    {
        bool needsAnimator = bulkMode is BulkSelectionMode.AllLayers or BulkSelectionMode.SpecificLayers;
        bool needsTransition = bulkMode is BulkSelectionMode.SelectedOnly;

        bool hasAnimator = ResolveControllerFromSelection() != null;
        bool hasTransition = Selection.objects.OfType<AnimatorTransitionBase>().Any();

        if (needsAnimator && !hasAnimator)
        {
            EditorGUILayout.HelpBox("Please select an AnimatorController to use this mode.", MessageType.Warning);
            return true;
        }

        if (needsTransition && !hasTransition)
        {
            EditorGUILayout.HelpBox("Please select one or more AnimatorStateTransitions to use this mode.", MessageType.Warning);
            return true;
        }

        return false;
    }

    private void BuildParameterTypeMap()
    {
        parameterTypeMap.Clear();

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            AssetDatabase.GetAssetPath(Selection.activeObject)
        );

        if (controller != null)
        {
            foreach (var p in controller.parameters)
            {
                if (!parameterTypeMap.ContainsKey(p.name))
                    parameterTypeMap[p.name] = p.type;
            }
        }
    }

    private AnimatorController ResolveControllerFromSelection()
    {
        // Case 1: Direct selection
        if (Selection.activeObject is AnimatorController direct)
            return direct;

        // Case 2: Transition or state machine selected
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller != null)
                    return controller;
            }
        }

        return null;
    }

    private void DrawBulkModeSelector()
    {
        EditorGUILayout.LabelField("Bulk Selection Mode", EditorStyles.boldLabel);
        var newMode = (BulkSelectionMode)EditorGUILayout.EnumPopup(bulkMode);

        if (newMode != lastBulkMode)
        {
            lastBulkMode = newMode;
            bulkMode = newMode;
            RecalculateGrouping();
            RefreshSelection(); // ✅ Trigger refresh on mode change
        }


        if (bulkMode == BulkSelectionMode.SpecificLayers)
        {
            EditorGUILayout.LabelField("Include Layers (comma-separated):");

            // Initialize buffer if needed
            if (string.IsNullOrEmpty(layerInputBuffer))
                layerInputBuffer = string.Join(",", specificLayerNames);

            GUI.SetNextControlName("LayerInputField");
            string newInput = EditorGUILayout.TextField(layerInputBuffer);

            // Detect change and apply immediately
            if (newInput != layerInputBuffer)
            {
                layerInputBuffer = newInput;
                specificLayerNames = layerInputBuffer.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                RefreshSelection();
                Repaint();
            }
        }
        else
        {
            layerInputBuffer = "";
        }
    }

    private bool DrawGroupingSelector()
    {
        EditorGUILayout.LabelField("Group Conditions By", EditorStyles.boldLabel);

        useComplexGrouping = EditorGUILayout.ToggleLeft("Use Complex Grouping", useComplexGrouping);
        if (!useComplexGrouping)
        {
            EditorGUI.BeginChangeCheck();
            selectedGrouping = (ConditionGroupingType)EditorGUILayout.EnumPopup(selectedGrouping);
            if (EditorGUI.EndChangeCheck())
                RecalculateGrouping();
        }
        else
        {
            if (GUILayout.Button("Configure Complex Grouping..."))
                ComplexGroupingWindow.ShowWindow(this); // 👇 opens the composer
        }
        if (useComplexGrouping && ComplexGroupingConfig.CurrentRules != null && ComplexGroupingConfig.CurrentRules.Count == 0)
        {
            EditorGUILayout.HelpBox("No rules selected. Open Configurator to add.", MessageType.Info);
            return false;
        }
        return true;
    }

    private void DrawGroupedRow(string groupKey, List<ConditionRow> group)
    {
        var first = group[0];
        var mixedParam = group.Any(r => r.condition.parameter != first.condition.parameter);
        var mixedMode = group.Any(r => r.condition.mode != first.condition.mode);
        var mixedThreshold = group.Any(r => !Mathf.Approximately(r.condition.threshold, first.condition.threshold));
        EditorGUILayout.BeginHorizontal("box");
        //EditorGUILayout.LabelField($" {groupKey}", GUILayout.Width(100));
        Rect labelRect = GUILayoutUtility.GetRect(100, 16, GUILayout.Width(100));
        DrawGroupLabel(labelRect, groupKey);


        EditorGUI.showMixedValue = mixedParam;
        EditorGUI.BeginChangeCheck();
        selectedIndex = Array.IndexOf(ValidNames, first.condition.parameter);
        if (selectedIndex == -1) selectedIndex = 0; // fallback if value isn't found

        selectedIndex = EditorGUILayout.Popup(selectedIndex, ValidNames, GUILayout.Width(120));
        var newParam = ValidNames[selectedIndex];
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var r in group) r.condition.parameter = newParam;
            foreach (var r in group) r.mixedParameter = false;
        }

        EditorGUI.showMixedValue = mixedMode;
        EditorGUI.BeginChangeCheck();
        var newMode = (AnimatorConditionMode)EditorGUILayout.EnumPopup(first.condition.mode, GUILayout.Width(80));
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var r in group) r.condition.mode = newMode;
            foreach (var r in group) r.mixedMode = false;
        }

        if (newMode is AnimatorConditionMode.Equals or AnimatorConditionMode.NotEqual or AnimatorConditionMode.Greater or AnimatorConditionMode.Less)
        {
            EditorGUI.showMixedValue = mixedThreshold;
            EditorGUI.BeginChangeCheck();
            float newThreshold = EditorGUILayout.FloatField(first.condition.threshold, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var r in group) r.condition.threshold = newThreshold;
                foreach (var r in group) r.mixedThreshold = false;
            }
        }

        EditorGUI.showMixedValue = false;
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"{group.Select(r => r.transition).Distinct().Count()}", GUILayout.Width(25));
        EditorGUILayout.EndHorizontal();
    }
    public static void DrawGroupLabel(Rect rect, string groupKey)
    {
        GUIStyle labelStyle = EditorStyles.label;

        // Calculate label width and prepare truncated text
        float maxWidth = rect.width;
        string displayKey = TruncateWithEllipsis(groupKey, maxWidth, labelStyle);

        // Prepare multiline tooltip by splitting the composite key
        string tooltip = string.Join("\n", groupKey.Split(new[] { "__" }, StringSplitOptions.None));

        // Final content with tooltip
        GUIContent content = new GUIContent(displayKey, tooltip);

        // Draw the label
        GUI.Label(rect, content, labelStyle);
    }

    private static string TruncateWithEllipsis(string input, float maxPixelWidth, GUIStyle style)
    {
        string ellipsis = "...";

        if (style.CalcSize(new GUIContent(input)).x <= maxPixelWidth)
            return input;

        for (int i = input.Length - 1; i > 0; i--)
        {
            string substr = input.Substring(0, i) + ellipsis;
            if (style.CalcSize(new GUIContent(substr)).x <= maxPixelWidth)
                return substr;
        }

        return ellipsis;
    }
    private void RefreshSelection()
    {
        conditionRows.Clear();
        selectedTransitions = Selection.objects.OfType<AnimatorTransitionBase>().ToArray();

        var controller = ResolveControllerFromSelection();
        ValidNames = TransitionUtils.GetParameterNames(controller);

        if (controller == null) return;

        List<AnimatorTransitionBase> transitionsToUse = new();

        switch (bulkMode)
        {
            case BulkSelectionMode.SelectedOnly:
                transitionsToUse.AddRange(selectedTransitions);
                break;

            case BulkSelectionMode.AllLayers:
                transitionsToUse.AddRange(TransitionUtils.GetAllTransitions(controller));
                break;

            case BulkSelectionMode.SpecificLayers:
                transitionsToUse.AddRange(TransitionUtils.GetTransitionsFromLayers(controller, specificLayerNames));
                break;
        }

        for (int i = 0; i < transitionsToUse.Count; i++)
        {
            var t = transitionsToUse[i];
            var conditions = t.conditions;
            for (int j = 0; j < conditions.Length; j++)
            {
                conditionRows.Add(new ConditionRow(t, i, j, conditions[j]));
            }
        }
        conditionRows = TransitionUtils.CollectTransitionConditionsWithSource(
            controller, bulkMode, specificLayerNames, selectedTransitions
        );

        BuildParameterTypeMap();
    }

    private void OnGUI() //
    {
        //EditorGUILayout.LabelField("Condition Splitter", EditorStyles.boldLabel);

        DrawBulkModeSelector();
        if (ShowSelectionWarning()) return;
        if (!DrawGroupingSelector()) return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Grouped Conditions: {conditionRows.Count}", EditorStyles.boldLabel);
        //if we have no grouped conditions but we do have selected transitions, then we need to recalculate
        if (grouped.Count == 0 && selectedTransitions.Length > 0)
        {
            UpdateGrouping = true;
        }

        if (UpdateGrouping)
        {
            RecalculateGrouping();
        }
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

        foreach (var kvp in grouped)
        {
            DrawGroupedRow(kvp.Key, kvp.Value);
        }
        EditorGUILayout.EndScrollView();


        GUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Changes"))
            {
                UpdateGrouping = true;

                foreach (var kvp in grouped)
                {
                    var group = kvp.Value;
                    var first = group[0];

                    foreach (var row in group)
                    {
                        var t = row.transition;
                        var conditions = t.conditions;
                        if (row.conditionIndex < conditions.Length)
                        {
                            Undo.RecordObject(t, "Grouped Condition Edit");
                            Debug.Log($"Applying changes to {t.GetDisplayName(ResolveControllerFromSelection())}");

                            var c = conditions[row.conditionIndex];
                            if (!group.Any(r => r.mixedParameter)) c.parameter = first.condition.parameter;
                            if (!group.Any(r => r.mixedMode)) c.mode = first.condition.mode;
                            if (!group.Any(r => r.mixedThreshold)) c.threshold = first.condition.threshold;

                            conditions[row.conditionIndex] = c;
                            t.conditions = conditions;
                            EditorUtility.SetDirty(t);
                        }
                    }
                }

                RefreshSelection();
            }

            if (GUILayout.Button("Refresh"))
            {
                UpdateGrouping = true;
                RefreshSelection();
            }
        }
    }
    public void RecalculateGrouping()
    {
        if (useComplexGrouping && ComplexGroupingConfig.CurrentRules != null && ComplexGroupingConfig.CurrentRules.Count > 0)
        {
            grouped = ConditionGrouping.GroupRowsComplex(
                conditionRows,
                ComplexGroupingConfig.CurrentRules,
                parameterTypeMap
            );
        }
        else
        {
            grouped = ConditionGrouping.GroupRows(conditionRows, selectedGrouping, parameterTypeMap);
        }

        UpdateGrouping = false;
    }
    private void OnUndoRedo()
    {
        RecalculateGrouping();
        Repaint();
    }
}