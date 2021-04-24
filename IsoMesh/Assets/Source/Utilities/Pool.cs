using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This is a lot like the implementation of "Pooler" but with some differences.
/// 
/// For one, it's not a monobehaviour (I didn't see the need?), and it's generic,
/// meaning you save on all the casting and component-getting. 
/// 
/// Use OnEnable() and OnDisable() to describe behaviours which are activated
/// whenever an object is taken from/returned to the pool.
/// </summary>
[System.Serializable]
public class Pool<T> where T : Component
{
    private readonly T m_prefab;

    private Transform m_parent;

    [SerializeField]
    private List<T> m_reserveList = new List<T>();
    public IList<T> Reserve { get { return m_reserveList.AsReadOnly(); } }

    [SerializeField]
    private List<T> m_activeList = new List<T>();
    public IList<T> Active { get { return m_activeList.AsReadOnly(); } }

    private T m_lastSpawned;
    public T LastSpawned { get { return m_lastSpawned; } }

    /// <summary>
    /// Create a pool container. Pools reduce instantiation by storing references to inactive
    /// Components. You can also provide a transform to parent the insantiated objects,
    /// and an initial capacity of prefabs to instantiate as active.
    /// </summary>
    public Pool(T prefab, Transform parent = null, int preloadCount = -1)
    {
        m_prefab = prefab;
        m_parent = parent;

        m_reserveList = new List<T>();
        m_activeList = new List<T>();

        if (preloadCount > 0)
            AddToReserve(preloadCount);
    }

    /// <summary>
    /// Instantiates an object of type T.
    /// </summary>
    private T CreateItem()
    {
        T t = Object.Instantiate(m_prefab);

        if (m_parent != null)
            t.transform.SetParent(m_parent, false);

        t.name = t.name.Substring(0, t.name.Length - "(Clone)".Length) + " " + (m_reserveList.Count + m_activeList.Count).ToString();

        return t;
    }

    /// <summary>
    /// Get a 'new' object of type T, whether instantiated new or reused from an old deactivated
    /// object.
    /// </summary>
    public T GetNew()
    {
        T newItem = null;

        while (newItem == null && m_reserveList.Count > 0)
        {
            newItem = m_reserveList[m_reserveList.Count - 1];
            m_reserveList.RemoveAt(m_reserveList.Count - 1);
        }

        if (newItem == null)
        {
            newItem = CreateItem();
        }

        newItem.gameObject.SetActive(true);

        m_activeList.Add(newItem);

        m_lastSpawned = newItem;

        return newItem;
    }

    /// <summary>
    /// Adds a number of deactivated objects to the reserve pool. Use for preloading objects. 
    /// </summary>
    private void AddToReserve(int count)
    {
        for (int i = 0; i < count; i++)
        {
            T newItem = CreateItem();

            newItem.gameObject.SetActive(false);

            m_reserveList.Add(newItem);
        }
    }

    /// <summary>
    /// Returns a number of objects of type T, each of which could be either instantiated or reused.
    /// </summary>
    public IEnumerator<T> Get(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return GetNew();
        }
    }

    /// <summary>
    /// Deactivates the given object and places it back in the reserve pool. 
    /// Object MUST have been created by this pooler in order to place into the pool.
    /// 
    /// Returns true on success.
    /// </summary>
    public bool ReturnToPool(T oldItem)
    {
        if (oldItem == null)
            return false;

        m_activeList.Remove(oldItem);
        m_reserveList.Add(oldItem);

        oldItem.gameObject.SetActive(false);

        return true;
    }

    /// <summary>
    /// Deactivate all instantiated objects and return them all to the reserve pool.
    /// </summary>
    public void ReturnAll()
    {
        for (int i = m_activeList.Count - 1; i >= 0; --i)
            ReturnToPool(m_activeList[i]);
    }

    /// <summary>
    /// Returns the last spawned instantiated object to the pool.
    /// </summary>
    public void ReturnLastSpawned()
    {
        if (m_lastSpawned != null)
        {
            ReturnToPool(m_lastSpawned);

            if (m_activeList != null && m_activeList.Count > 0)
                m_lastSpawned = m_activeList[m_activeList.Count - 1];
            else
                m_lastSpawned = null;
        }
    }

    /// <summary>
    /// Returns whether the given object "belongs to" this pool.
    /// </summary>
    public bool Owns(T t)
    {
        return (m_activeList.Contains(t) || m_reserveList.Contains(t));
    }
}
