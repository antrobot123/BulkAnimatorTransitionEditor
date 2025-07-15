// File: Editor/AnimatorTransitionBulkEditor.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

public class AnimatorTransitionBulkEditor : EditorWindow
{
    private string selectedLayerName = null;
    private string selectedParamFromName = null;
    private string selectedParamToName = null;
    private AnimatorController animatorController;
    private string[] layerOptions;
    private int selectedLayer = 0;
    private bool editAllLayers = false;

    private AnimatorControllerParameter[] parameters;
    private string[] parameterOptions;
    private int paramFrom = 0, paramTo = 0;

    [MenuItem("antrobot/Animator Transition Bulk Edit")]
    public static void ShowWindow()
    {
        var window = GetWindow<AnimatorTransitionBulkEditor>("Animator Bulk Edit");
        window.minSize = new Vector2(500, 220);  // Adjust height and width

    }
    private void OnFocus()
    {
        if (layerOptions != null && selectedLayer >= 0 && selectedLayer < layerOptions.Length)
        {
            string raw = layerOptions[selectedLayer];
            selectedLayerName = raw.Substring(raw.IndexOf("-") + 2);
        }

        if (parameterOptions != null && paramFrom >= 0 && paramFrom < parameterOptions.Length)
            selectedParamFromName = parameterOptions[paramFrom];

        if (parameterOptions != null && paramTo >= 0 && paramTo < parameterOptions.Length)
            selectedParamToName = parameterOptions[paramTo];
        RefreshLayersAndParameters();
    }

    private void OnGUI()
    {
        GUILayout.Label("Animator Transition Bulk Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Animator Controller selector
        var newController = (AnimatorController)EditorGUILayout.ObjectField(
            "Animator Controller",
            animatorController,
            typeof(AnimatorController),
            false);

        if (newController != animatorController)
        {
            animatorController = newController;
            RefreshLayersAndParameters();
        }

        // Layer selection
        EditorGUI.BeginDisabledGroup(animatorController == null);
        {
            if (layerOptions != null && layerOptions.Length > 0)
            {
                selectedLayer = EditorGUILayout.Popup("Layer", selectedLayer, layerOptions);
            }

            editAllLayers = EditorGUILayout.ToggleLeft("Edit All Layers", editAllLayers);
        }
        EditorGUI.EndDisabledGroup();

        // Parameter dropdowns
        EditorGUI.BeginDisabledGroup(animatorController == null || (!editAllLayers && layerOptions == null));
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Change parameter", GUILayout.Width(110));
                paramFrom = EditorGUILayout.Popup(paramFrom, parameterOptions, GUILayout.Width(140));
                EditorGUILayout.LabelField("to", GUILayout.Width(20));
                paramTo = EditorGUILayout.Popup(paramTo, parameterOptions, GUILayout.Width(140));
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.EndDisabledGroup();

        // Action buttons
        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(
            animatorController == null ||
            (parameters == null) ||
            paramFrom == paramTo
        );
        {
            if (GUILayout.Button("Save to animator"))
            {
                ApplyChanges(false);
            }

            if (GUILayout.Button("Save as copy"))
            {
                ApplyChanges(true);
            }
        }
        EditorGUI.EndDisabledGroup();
    }

    private void RefreshLayersAndParameters() {
        if (animatorController == null) {
            layerOptions = null;
            parameterOptions = null;
            return;
        }

        // Layers
        var layers = animatorController.layers;
        layerOptions = new string[layers.Length];
        for (int i = 0; i < layers.Length; i++) {
            layerOptions[i] = $"{i} - {layers[i].name}";
        }
        selectedLayer = 0;

        // Parameters
        parameters = animatorController.parameters;
        parameterOptions = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++) {
            parameterOptions[i] = parameters[i].name;
        }
        paramFrom = paramTo = 0;
        // Match layer name
        selectedLayer = 0; // fallback default
        if (!string.IsNullOrEmpty(selectedLayerName))
        {
            for (int i = 0; i < layerOptions.Length; i++)
            {
                // Extract layer name from format "# - LayerName"
                string layerName = layerOptions[i].Substring(layerOptions[i].IndexOf("-") + 2);
                if (layerName == selectedLayerName)
                {
                    selectedLayer = i;
                    break;
                }
            }
        }
        else if (layerOptions.Length > 0)
        {
            // Save current layer name for future reference
            string defaultName = layerOptions[selectedLayer];
            selectedLayerName = defaultName.Substring(defaultName.IndexOf("-") + 2);
        }

        // Match parameter names
        if (!string.IsNullOrEmpty(selectedParamFromName))
        {
            for (int i = 0; i < parameterOptions.Length; i++)
            {
                if (parameterOptions[i] == selectedParamFromName)
                {
                    paramFrom = i;
                    break;
                }
            }
        }
        if (!string.IsNullOrEmpty(selectedParamToName))
        {
            for (int i = 0; i < parameterOptions.Length; i++)
            {
                if (parameterOptions[i] == selectedParamToName)
                {
                    paramTo = i;
                    break;
                }
            }
        }

    }
    private void ApplyChanges(bool saveAsCopy)
    {
        string assetPath = AssetDatabase.GetAssetPath(animatorController);
        AnimatorController targetController = animatorController;

        if (saveAsCopy)
        {
            string directory = Path.GetDirectoryName(assetPath);
            string filename = Path.GetFileNameWithoutExtension(assetPath);
            string newPath = Path.Combine(directory, filename + "_TransitionCopy.controller");
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);

            AssetDatabase.CopyAsset(assetPath, newPath);
            AssetDatabase.ImportAsset(newPath);
            AssetDatabase.Refresh();
            EditorApplication.delayCall += () =>
            {
                targetController = AssetDatabase.LoadAssetAtPath<AnimatorController>(newPath);
                if (targetController != null)
                    ApplyChanges(false);  // apply changes after re-import
            };
            return; // prevent double application
        }

        // Iterate layers
        for (int i = 0; i < targetController.layers.Length; i++)
        {
            if (!editAllLayers && i != selectedLayer) continue;

            var layer = targetController.layers[i];
            BulkEditStateMachine(layer.stateMachine);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Animator Bulk Edit", "Parameter transitions updated.", "OK");
    }

    private void BulkEditStateMachine(AnimatorStateMachine sm)
    {
        // AnyState transitions
        foreach (var anyTrans in sm.anyStateTransitions)
            EditTransition(anyTrans);

        // State-specific transitions
        foreach (var state in sm.states)
        {
            foreach (var trans in state.state.transitions)
                EditTransition(trans);
        }

        // Recursively edit sub-state machines
        foreach (var sub in sm.stateMachines)
            BulkEditStateMachine(sub.stateMachine);
    }

    private void EditTransition(AnimatorTransitionBase transition)
    {
        var conditions = transition.conditions;
        bool changed = false;

        for (int i = 0; i < conditions.Length; i++)
        {
            if (conditions[i].parameter == parameters[paramFrom].name)
            {
                conditions[i].parameter = parameters[paramTo].name;
                changed = true;
            }
        }

        if (changed)
            transition.conditions = conditions;
    }
}
