using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class RadialTimerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;

    [Header("Timer")]
    [SerializeField, Min(0.01f)] private float duration = 3f;
    [SerializeField] private bool countDown = true;
    [SerializeField] private bool playOnEnable;
    [SerializeField] private bool useUnscaledTime;

    [Header("Image Setup")]
    [SerializeField] private bool configureImageAsRadial = true;

    [Header("Completion")]
    [SerializeField] private bool hideWhenComplete;
    [SerializeField] private UnityEvent completed;

    private float _elapsed;
    private bool _isRunning;

    public bool IsRunning => _isRunning;
    public float Duration => Mathf.Max(0.01f, duration);
    public float NormalizedProgress => Mathf.Clamp01(_elapsed / Duration);
    public Image FillImage
    {
        get
        {
            ResolveImage();
            return fillImage;
        }
    }

    private void Awake()
    {
        ResolveImage();
        ConfigureImageIfNeeded();
        RefreshFill();
    }

    private void OnEnable()
    {
        ResolveImage();
        ConfigureImageIfNeeded();

        if (playOnEnable)
        {
            Play();
            return;
        }

        RefreshFill();
    }

    private void Update()
    {
        if (!_isRunning)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _elapsed = Mathf.Min(Duration, _elapsed + deltaTime);
        RefreshFill();

        if (_elapsed >= Duration)
        {
            Complete();
        }
    }

    public void Play()
    {
        Play(duration);
    }

    public void Play(float timerDuration)
    {
        duration = Mathf.Max(0.01f, timerDuration);
        _elapsed = 0f;
        _isRunning = true;
        RefreshFill();
    }

    public void Pause()
    {
        _isRunning = false;
    }

    public void Resume()
    {
        if (_elapsed < Duration)
        {
            _isRunning = true;
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _elapsed = 0f;
        RefreshFill();
    }

    public void SetNormalizedProgress(float normalizedProgress)
    {
        _elapsed = Mathf.Clamp01(normalizedProgress) * Duration;
        RefreshFill();
    }

    public void SetRemainingNormalized(float normalizedRemaining)
    {
        _elapsed = (1f - Mathf.Clamp01(normalizedRemaining)) * Duration;
        RefreshFill();
    }

    public void SetFillColor(Color color)
    {
        ResolveImage();

        if (fillImage != null)
        {
            fillImage.color = color;
        }
    }

    private void Complete()
    {
        _isRunning = false;
        RefreshFill();
        completed?.Invoke();

        if (hideWhenComplete)
        {
            gameObject.SetActive(false);
        }
    }

    private void RefreshFill()
    {
        if (fillImage == null)
        {
            return;
        }

        float progress = NormalizedProgress;
        fillImage.fillAmount = countDown ? 1f - progress : progress;
    }

    private void ResolveImage()
    {
        if (fillImage == null)
        {
            fillImage = GetComponent<Image>();
        }
    }

    private void ConfigureImageIfNeeded()
    {
        if (!configureImageAsRadial || fillImage == null)
        {
            return;
        }

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        duration = Mathf.Max(0.01f, duration);
        ResolveImage();
        ConfigureImageIfNeeded();
        RefreshFill();
    }
#endif
}
