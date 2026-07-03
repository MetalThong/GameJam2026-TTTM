using UnityEngine;

public abstract class UIPanelView : MonoBehaviour
{
    [SerializeField] private PanelId id;

    public PanelId Id => id;

    public virtual void Show()
    {
        gameObject.SetActive(true);
    }

    public virtual void Hide()
    {
        gameObject.SetActive(false);
    }
}
