using UnityEditor.Animations;
using UnityEngine;

public class TransitionEditState
{
    public float exitTime;
    public float duration;
    public float offset;
    public bool hasExitTime;
    public bool hasFixedDuration;

    public bool mixedExitTime;
    public bool mixedDuration;
    public bool mixedOffset;
    public bool mixedHasExitTime;
    public bool mixedHasFixedDuration;

    public static TransitionEditState FromSelection(AnimatorStateTransition[] transitions)
    {
        var state = new TransitionEditState();
        if (transitions.Length == 0) return state;

        var first = transitions[0];
        state.exitTime = first.exitTime;
        state.duration = first.duration;
        state.offset = first.offset;
        state.hasExitTime = first.hasExitTime;
        state.hasFixedDuration = first.hasFixedDuration;

        for (int i = 1; i < transitions.Length; i++)
        {
            var t = transitions[i];
            if (!Mathf.Approximately(state.exitTime, t.exitTime)) state.mixedExitTime = true;
            if (!Mathf.Approximately(state.duration, t.duration)) state.mixedDuration = true;
            if (!Mathf.Approximately(state.offset, t.offset)) state.mixedOffset = true;
            if (state.hasExitTime != t.hasExitTime) state.mixedHasExitTime = true;
            if (state.hasFixedDuration != t.hasFixedDuration) state.mixedHasFixedDuration = true;
        }

        return state;
    }

    public void ApplyTo(AnimatorStateTransition t)
    {
        if (!mixedHasExitTime) t.hasExitTime = hasExitTime;
        if (!mixedExitTime) t.exitTime = exitTime;
        if (!mixedHasFixedDuration) t.hasFixedDuration = hasFixedDuration;
        if (!mixedDuration) t.duration = duration;
        if (!mixedOffset) t.offset = offset;
    }
}