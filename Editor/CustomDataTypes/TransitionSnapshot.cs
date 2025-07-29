using UnityEditor.Animations;
using UnityEngine;

public struct TransitionSnapshot
{
    public float exitTime;
    public float duration;
    public float offset;
    public bool hasExitTime;
    public bool hasFixedDuration;

    public TransitionSnapshot(AnimatorStateTransition t)
    {
        exitTime = t.exitTime;
        duration = t.duration;
        offset = t.offset;
        hasExitTime = t.hasExitTime;
        hasFixedDuration = t.hasFixedDuration;
    }

    public bool EqualsTo(AnimatorStateTransition t)
    {
        return Mathf.Approximately(exitTime, t.exitTime)
            && Mathf.Approximately(duration, t.duration)
            && Mathf.Approximately(offset, t.offset)
            && hasExitTime == t.hasExitTime
            && hasFixedDuration == t.hasFixedDuration;
    }
}