using UnityEngine;
using UnityEngine.Pool;

public class PrefabPool<T> where T : MonoBehaviour, IPoolable
{
    private readonly T _prefab;
    private readonly Transform _parent;
    private readonly ObjectPool<T> _pool;

    public PrefabPool(T prefab, Transform parent, bool collectionCheck = false, int defaultCapacity = 10, int maxSize = 50)
    {
        _prefab = prefab;
        _parent = parent;

        _pool = new(
            createFunc: CreateFunc,
            actionOnGet: OnGet,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyItem,
            collectionCheck: collectionCheck,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    public T Get()
    {
        return _pool.Get();
    }

    public void Release(T item)
    {
        _pool.Release(item);    
    }

    private T CreateFunc()
    {
        T item = Object.Instantiate(_prefab, _parent);
        item.gameObject.SetActive(false);
        
        return item;
    }

    private void OnGet(T item)
    {
        item.gameObject.SetActive(true);
        item.OnSpawned();
    }

    private void OnRelease(T item)
    {
        item.OnDespawned();
        item.gameObject.SetActive(false);
    }

    private void OnDestroyItem(T item)
    {
        item.OnDestroyed();
        
        if (item != null)
        {
            Object.Destroy(item.gameObject);
        }
    }
}
