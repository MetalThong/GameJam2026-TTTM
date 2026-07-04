using DG.Tweening;
using UnityEngine;

public class PushFlagObject : FlagBasedObject
{
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private Vector3 offset;

    private Tween _movingTween;

    private void OnDestroy()
    {
        _movingTween?.Kill();
    }

    protected override void Refresh()
    {
        _movingTween?.Kill();

        _movingTween = transform.DOMove(transform.position + offset, duration);
    }
}
