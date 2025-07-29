using UnityEditor.Animations;

public class ConditionRow
    {
        public AnimatorTransitionBase transition;
        public int transitionIndex;
        public int conditionIndex;
        public AnimatorCondition condition;

        public bool mixedParameter;
        public bool mixedMode;
        public bool mixedThreshold;
        
        
        public string fromStateName; // 🧠 stores the source node name


        public ConditionRow(AnimatorTransitionBase transition, int transitionIndex, int conditionIndex, AnimatorCondition condition)
    {
        this.transition = transition;
        this.transitionIndex = transitionIndex;
        this.conditionIndex = conditionIndex;
        this.condition = condition;
    }

        public ConditionRow(ConditionRow source)
        {
            transition = source.transition;
            transitionIndex = source.transitionIndex;
            conditionIndex = source.conditionIndex;
            condition = new AnimatorCondition
            {
                parameter = source.condition.parameter,
                mode = source.condition.mode,
                threshold = source.condition.threshold
            };
        }
    }