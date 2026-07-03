using UnityEngine;

public class MashStoryInteractable : StoryInteractable
{
    [SerializeField] private int requiredPressCount = 5;
    [SerializeField] private float decayDelay = 0.5f;
    [SerializeField] private float decayPerSecond = 2f;
    [SerializeField] private bool resetProgressOnSuccess = true;

    private float _progress;
    private float _lastPressTime = float.NegativeInfinity;

    private void Update()
    {
        DecayProgress();
    }

    protected override void Interact()
    {
        DecayProgress();

        int targetPressCount = Mathf.Max(1, requiredPressCount);
        _progress = Mathf.Min(targetPressCount, _progress + 1f);
        _lastPressTime = Time.time;

        if (_progress < targetPressCount)
        {
            return;
        }

        OnInteractSucceeded();

        if (resetProgressOnSuccess)
        {
            _progress = 0f;
        }
    }

    private void DecayProgress()
    {
        if (_progress <= 0f || Time.time - _lastPressTime <= decayDelay)
        {
            return;
        }

        _progress = Mathf.Max(0f, _progress - Mathf.Max(0f, decayPerSecond) * Time.deltaTime);
    }
}
