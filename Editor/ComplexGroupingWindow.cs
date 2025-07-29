using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class ComplexGroupingConfig
{
    public static List<ConditionGroupingType> CurrentRules = new List<ConditionGroupingType>();
    public static void serialize() => EditorPrefs.SetString("TransitionEditor/ComplexGroupingRules", JsonUtility.ToJson(CurrentRules));
    public static void deserialize() => CurrentRules = JsonUtility.FromJson<List<ConditionGroupingType>>(EditorPrefs.GetString("TransitionEditor/ComplexGroupingRules"));
}
public class ComplexGroupingWindow : EditorWindow
{
    private static TransitionBulkEditor Editor;
    private List<ConditionGroupingType> SelectedRules => ComplexGroupingConfig.CurrentRules;

    public static void ShowWindow(TransitionBulkEditor editor)
    {
        Editor = editor;
        var window = GetWindow<ComplexGroupingWindow>("Complex Grouping Setup");
        window.Show();
    }
    //private void OnEnable() => ComplexGroupingConfig.deserialize();
    //private void OnDestroy() => ComplexGroupingConfig.serialize();

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Complex Grouping Rules", EditorStyles.boldLabel);

        
        for (int i = 0; i < SelectedRules.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            SelectedRules[i] = (ConditionGroupingType)EditorGUILayout.EnumPopup(SelectedRules[i]);

            if (GUILayout.Button("↑", GUILayout.Width(30)) && i > 0)
            {
                (SelectedRules[i - 1], SelectedRules[i]) = (SelectedRules[i], SelectedRules[i - 1]);
            }

            if (GUILayout.Button("↓", GUILayout.Width(30)) && i < SelectedRules.Count - 1)
            {
                (SelectedRules[i + 1], SelectedRules[i]) = (SelectedRules[i], SelectedRules[i + 1]);
            }

            if (GUILayout.Button("✖", GUILayout.Width(30)))
            {
                SelectedRules.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Add Rule"))
            SelectedRules.Add(ConditionGroupingType.ParameterName);

        if (GUILayout.Button("Apply"))
        {
            Editor.RecalculateGrouping();
            Close();
        }
    }
}