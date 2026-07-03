using UnityEngine;

public abstract class UIPanelView : MonoBehaviour
{
    public PanelId Id { get; }

    public virtual void Show()
    {
        gameObject.SetActive(true);
    }

    public virtual void Hide()
    {
        gameObject.SetActive(false);
    }
}
