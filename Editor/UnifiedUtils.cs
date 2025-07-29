using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
public static class UnifiedUtils
{
    public static bool TryGetUnifiedValue<T>(
        AnimatorTransitionBase[] transitions,
        Func<AnimatorStateTransition, T> selector,
        out T unifiedValue
    )
    {
        unifiedValue = default;
        bool hasReference = false;

        foreach (var t in transitions)
        {
            if (t is AnimatorStateTransition stateTransition)
            {
                T value = selector(stateTransition);
                if (!hasReference)
                {
                    unifiedValue = value;
                    hasReference = true;
                }
                else if (!EqualityComparer<T>.Default.Equals(unifiedValue, value))
                {
                    return false;
                }
            }
        }

        return hasReference;
    }

    public static T DrawUnifiedInputField<T>(
        string label,
        AnimatorTransitionBase[] transitions,
        Func<AnimatorStateTransition, T> selector,
        ref T cachedValue,
        Func<string, T, T> fieldRenderer
    )
    {
        bool isUnified = TryGetUnifiedValue(transitions, selector, out T liveValue);
        if (isUnified) cachedValue = liveValue;

        EditorGUI.showMixedValue = !isUnified;
        T newValue = fieldRenderer(label, cachedValue);
        EditorGUI.showMixedValue = false;

        return newValue;
    }

    public static T DrawUnifiedPopup<T>(
        string label,
        AnimatorTransitionBase[] transitions,
        Func<AnimatorStateTransition, T> selector,
        ref T cachedValue,
        List<T> options
    )
    {
        bool isUnified = TryGetUnifiedValue(transitions, selector, out T liveValue);
        if (isUnified) cachedValue = liveValue;

        // Prepare display names
        var displayOptions = new string[options.Count];
        for (var i = 0; i < options.Count; i++)
        {
            displayOptions[i] = options[i]?.ToString() ?? "<null>";
        }

        var currentIndex = options.IndexOf(cachedValue);
        if (currentIndex < 0) currentIndex = 0;

        EditorGUI.showMixedValue = !isUnified;
        var newIndex = EditorGUILayout.Popup(label, currentIndex, displayOptions);
        EditorGUI.showMixedValue = false;

        return options[Mathf.Clamp(newIndex, 0, options.Count - 1)];
    }

    public static bool DrawUnifiedToggle(
        string label,
        AnimatorTransitionBase[] transitions,
        Func<AnimatorStateTransition, bool> selector,
        ref bool cachedValue
    )
    {
        bool isUnified = TryGetUnifiedValue(transitions, selector, out bool liveValue);
        if (isUnified) cachedValue = liveValue;

        EditorGUI.showMixedValue = !isUnified;
        bool newValue = EditorGUILayout.Toggle(label, cachedValue);
        EditorGUI.showMixedValue = false;

        return newValue;
    }
}