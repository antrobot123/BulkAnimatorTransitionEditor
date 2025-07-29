using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;

public enum ConditionGroupingType
{
    ParameterName, // Group by transition parameter
    ComparisonMode, //group by transition comparison (Equals, NotEqual, Greater, Less)
    ThresholdValue, //group by transition threshold value
    TransitionTitle, //group by transition title
    ParameterType, //group by transition parameter type (Int, Float, Bool, Trigger)
    FromNode, //group by what node the transition comes from
    ToNode, //group by what node the transition goes to
    All //group all transitions into a single group
}
public enum FloatConditionMode { Equals, NotEqual, Greater, Less }
public enum boolConditionMode { If, IfNot }
#nullable enable
public struct GroupingContext
{
    public ConditionGroupingType Type;
    public Dictionary<string, AnimatorControllerParameterType>? ParameterTypeMap; // null for types that don't need it

    public GroupingContext(ConditionGroupingType type, Dictionary<string, AnimatorControllerParameterType>? map = null)
    {
        Type = type;
        ParameterTypeMap = map;
    }
}


public static class ConditionGrouping
{
    public static Dictionary<string, List<ConditionRow>> GroupRows(
        List<ConditionRow> rows,
        ConditionGroupingType groupingType,
        Dictionary<string, AnimatorControllerParameterType> parameterTypeMap)
    {
        // Step 1: Create a lookup where each key maps to a list of grouped rows
        var grouped = rows
            .GroupBy(row => GetGroupKey(row, groupingType, parameterTypeMap))      // Group rows by their assigned key
            .ToDictionary(                                                         // Convert groupings to a dictionary
                g => g.Key,
                g => g.Select(r => new ConditionRow(r)).ToList());                // Copy rows into new lists

        // Step 2: Sort the keys using a mode-specific strategy
        var sortedKeys = groupingType switch
        {
            // Sort parameter types by enum order
            ConditionGroupingType.ParameterType => grouped.Keys
                .OrderBy(k => Enum.TryParse(k, out AnimatorControllerParameterType type)
                    ? (int)type
                    : int.MaxValue)
                .ToList(),

            // Sort comparison modes by enum value
            ConditionGroupingType.ComparisonMode => grouped.Keys
                .OrderBy(k => Enum.TryParse(k, out AnimatorConditionMode mode)
                    ? (int)mode
                    : int.MaxValue)
                .ToList(),

            // Sort thresholds numerically
            ConditionGroupingType.ThresholdValue => grouped.Keys
                .OrderBy(k => float.TryParse(k, out float val)
                    ? val
                    : float.MaxValue)
                .ToList(),

            // Sort by transition name, putting unnamed ones last
            ConditionGroupingType.TransitionTitle => grouped.Keys
                .OrderBy(k => string.IsNullOrEmpty(k))        // true = after named
                .ThenBy(k => k)
                .ToList(),

            // Sort special nodes first (Any State, Entry, Exit)
            ConditionGroupingType.FromNode or ConditionGroupingType.ToNode => grouped.Keys
                .OrderBy(k => k == "Any State" ? 0 :
                              k == "Entry" ? 1 :
                              k == "Exit" ? 2 : 3)
                .ThenBy(k => k)
                .ToList(),

            // Alphabetical sort fallback
            _ => grouped.Keys.OrderBy(k => k).ToList()
        };
        // Step 3: calculate mixed fields (mixedParameter, mixedThreshold, mixedTransition)

        foreach (var key in sortedKeys)
        {
            var mixedParam = grouped[key].Any(r => r.condition.parameter != grouped[key][0].condition.parameter);
            var mixedMode = grouped[key].Any(r => r.condition.mode != grouped[key][0].condition.mode);
            var mixedThreshold = grouped[key].Any(r => !Mathf.Approximately(r.condition.threshold, grouped[key][0].condition.threshold));

            foreach (var r in grouped[key])
            {
                r.mixedParameter = mixedParam;
                r.mixedMode = mixedMode;
                r.mixedThreshold = mixedThreshold;
            }
        }
        // Step 4: Build a new dictionary using the sorted key order
        return sortedKeys.ToDictionary(k => k, k => grouped[k]);
    }

    public static string GetGroupKey(
        ConditionRow row,
        ConditionGroupingType groupingType,
        Dictionary<string, AnimatorControllerParameterType> parameterTypeMap)
    {
        AnimatorCondition c = row.condition;

        return groupingType switch
        {
            // Group by the name of the parameter driving the condition
            ConditionGroupingType.ParameterName => c.parameter,

            // Group by the comparison type: Equals, Greater, etc.
            ConditionGroupingType.ComparisonMode => c.mode.ToString(),

            // Group by numeric threshold, rounded for consistency
            ConditionGroupingType.ThresholdValue => c.threshold.ToString("F2"),

            // Group by the name of the transition asset
            ConditionGroupingType.TransitionTitle => row.transition.name,

            // Group by parameter type (Float, Int, Bool) using lookup map
            ConditionGroupingType.ParameterType =>
                parameterTypeMap.TryGetValue(c.parameter, out var type)
                    ? type.ToString()
                    : "Unknown",

            // Group by transition's source node name (or "Any State")
            ConditionGroupingType.FromNode =>
                row.transition is AnimatorStateTransition fromTrans
                    ? row.fromStateName ?? "Any State"
                    : "Any State",

            // Group by transition's destination node name (or "Exit")
            ConditionGroupingType.ToNode =>
                row.transition.destinationState?.name ?? "Exit",

            // All rows go into one group
            ConditionGroupingType.All => "All",

            // Fallback
            _ => "Ungrouped"
        };
    }
    public static string GetCompositeGroupKey(ConditionRow row, List<GroupingContext> contexts)
    {
        var keys = new List<string>();
        foreach (var ctx in contexts)
        {
            keys.Add(GetGroupKey(row, ctx.Type, ctx.ParameterTypeMap ?? new()));
        }
        return string.Join("__", keys);
    }
    public static IEnumerable<ConditionRow> SortRows(
        IEnumerable<ConditionRow> rows,
        List<GroupingContext> contexts)
    {
        IOrderedEnumerable<ConditionRow>? sorted = null;

        foreach (var ctx in contexts)
        {
            Func<ConditionRow, object> keySelector = row =>
                GetGroupKey(row, ctx.Type, ctx.ParameterTypeMap ?? new());

            sorted = sorted == null
                ? rows.OrderBy(keySelector)
                : sorted.ThenBy(keySelector);
        }

        return sorted ?? rows;
    }
    public static Dictionary<string, List<ConditionRow>> GroupRowsComplex(
    List<ConditionRow> rows,
    List<ConditionGroupingType> groupingTypes,
    Dictionary<string, AnimatorControllerParameterType> parameterTypeMap)
    {
        // Build key → grouped list using compound keys
        var grouped = rows
            .GroupBy(row => GetCompositeGroupKey(row, groupingTypes
                .Select(gt => new GroupingContext(gt, parameterTypeMap))
                .ToList()))
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new ConditionRow(r)).ToList()
            );

        // Sort keys by layered sort rules
        var sortedKeys = grouped.Keys
            .OrderBy(k => k, new CompositeKeyComparer(groupingTypes))
            .ToList();

        return sortedKeys.ToDictionary(k => k, k => grouped[k]);
    }
    public class CompositeKeyComparer : IComparer<string>
    {
        private readonly List<ConditionGroupingType> sortRules;

        public CompositeKeyComparer(List<ConditionGroupingType> rules)
        {
            sortRules = rules;
        }

        public int Compare(string x, string y)
        {
            var xParts = x.Split("__");
            var yParts = y.Split("__");

            for (int i = 0; i < sortRules.Count; i++)
            {
                string a = i < xParts.Length ? xParts[i] : "";
                string b = i < yParts.Length ? yParts[i] : "";

                int result = CompareByRule(a, b, sortRules[i]);
                if (result != 0)
                    return result;
            }

            return 0;
        }

        private int CompareByRule(string a, string b, ConditionGroupingType rule)
        {
            return rule switch
            {
                ConditionGroupingType.ThresholdValue =>
                    float.TryParse(a, out var va) && float.TryParse(b, out var vb)
                        ? va.CompareTo(vb)
                        : a.CompareTo(b),

                ConditionGroupingType.ParameterType =>
                    Enum.TryParse(a, out AnimatorControllerParameterType ta) &&
                    Enum.TryParse(b, out AnimatorControllerParameterType tb)
                        ? ((int)ta).CompareTo((int)tb)
                        : a.CompareTo(b),

                ConditionGroupingType.ComparisonMode =>
                    Enum.TryParse(a, out AnimatorConditionMode ma) &&
                    Enum.TryParse(b, out AnimatorConditionMode mb)
                        ? ((int)ma).CompareTo((int)mb)
                        : a.CompareTo(b),

                ConditionGroupingType.FromNode or ConditionGroupingType.ToNode =>
                    GetNodeWeight(a).CompareTo(GetNodeWeight(b)) != 0
                        ? GetNodeWeight(a).CompareTo(GetNodeWeight(b))
                        : a.CompareTo(b),

                _ => a.CompareTo(b)
            };
        }

        private int GetNodeWeight(string name)
        {
            return name switch
            {
                "Any State" => 0,
                "Entry" => 1,
                "Exit" => 2,
                _ => 3
            };
        }
    }
}
