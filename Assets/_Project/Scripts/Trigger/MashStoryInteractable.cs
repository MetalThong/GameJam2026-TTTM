using System;
using UnityEngine;

public class MashStoryInteractable : StoryInteractable
{
    [SerializeField] private int requiredPressCount = 5;
    [SerializeField] private float decayDelay = 0.5f;
    [SerializeField] private float decayPerSecond = 2f;
    [SerializeField] private bool resetProgressOnSuccess = true;

    private float _progress;
    private float _lastPressTime = float.NegativeInfinity;

    // Fires each time a mash press is registered, so views can react (shake, sfx).
    public event Action Pressed;

    // Fires whenever the normalized progress changes, so views can update a bar.
    public event Action<float> ProgressChanged;

    // Fires once when the mash reaches the required press count.
    public event Action Succeeded;

    // Current progress in the 0..1 range.
    public float NormalizedProgress => TargetPressCount <= 0 ? 0f : Mathf.Clamp01(_progress / TargetPressCount);

    private int TargetPressCount => Mathf.Max(1, requiredPressCount);

    private void Update()
    {
        DecayProgress();
    }

    protected override void Interact()
    {
        DecayProgress();

        _progress = Mathf.Min(TargetPressCount, _progress + 1f);
        _lastPressTime = Time.time;

        Pressed?.Invoke();
        ProgressChanged?.Invoke(NormalizedProgress);

        if (_progress < TargetPressCount)
        {
            return;
        }

        Succeeded?.Invoke();
        OnInteractSucceeded();

        if (resetProgressOnSuccess)
        {
            _progress = 0f;
            ProgressChanged?.Invoke(NormalizedProgress);
        }
    }

    private void DecayProgress()
    {
        if (_progress <= 0f || Time.time - _lastPressTime <= decayDelay)
        {
            return;
        }

        float previous = _progress;
        _progress = Mathf.Max(0f, _progress - Mathf.Max(0f, decayPerSecond) * Time.deltaTime);

        if (!Mathf.Approximately(previous, _progress))
        {
            ProgressChanged?.Invoke(NormalizedProgress);
        }
    }
}
