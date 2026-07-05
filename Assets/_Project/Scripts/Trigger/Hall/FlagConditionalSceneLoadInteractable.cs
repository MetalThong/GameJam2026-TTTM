using System;
using UnityEngine;

public sealed class FlagConditionalSceneLoadInteractable : SceneLoadInteractable
{
    [Serializable]
    private sealed class ConditionalSceneTarget
    {
        [SerializeField] private StoryFlagCondition condition = new();
        [SerializeField] private SceneId targetScene;

        public SceneId TargetScene => targetScene;

        public bool IsMatched(FlagManager flagManager)
        {
            return flagManager != null
                && condition != null
                && condition.IsMet(flagManager.Flags);
        }
    }

    [Header("Conditional Scene Targets")]
    [SerializeField] private ConditionalSceneTarget[] conditionalTargets;

    protected override SceneId ResolveTargetScene()
    {
        if (conditionalTargets == null)
        {
            return base.ResolveTargetScene();
        }

        FlagManager flagManager = FlagManager.Instance;
        for (int i = 0; i < conditionalTargets.Length; i++)
        {
            ConditionalSceneTarget conditionalTarget = conditionalTargets[i];
            if (conditionalTarget != null && conditionalTarget.IsMatched(flagManager))
            {
                return conditionalTarget.TargetScene;
            }
        }

        return base.ResolveTargetScene();
    }
}
