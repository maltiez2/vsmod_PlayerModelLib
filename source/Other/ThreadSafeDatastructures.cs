using System.Diagnostics.CodeAnalysis;

namespace PlayerModelLib;

public class ThreadSafeList<TElement>
{
    public ThreadSafeList(IEnumerable<TElement> value)
    {
        _value = value.ToList();
    }

    public IReadOnlyList<TElement> Get()
    {
        _lock.EnterReadLock();
        IReadOnlyList<TElement> result = _value;
        _lock.ExitReadLock();
        return result;
    }

    public int Count()
    {
        _lock.EnterReadLock();
        int result = _value.Count;
        _lock.ExitReadLock();
        return result;
    }

    public void Set(IEnumerable<TElement> value)
    {
        _lock.EnterWriteLock();
        _value = value.ToList();
        _lock.ExitWriteLock();
    }

    private List<TElement> _value;
    private readonly ReaderWriterLockSlim _lock = new();
}

public class ThreadSafeDictionary<TKey, TValue>
{
    public ThreadSafeDictionary(Dictionary<TKey, TValue> value)
    {
        _value = value;
    }

    public IReadOnlyDictionary<TKey, TValue> Get()
    {
        _lock.EnterReadLock();
        IReadOnlyDictionary<TKey, TValue> result = _value;
        _lock.ExitReadLock();
        return result;
    }

    public void Set(Dictionary<TKey, TValue> value)
    {
        _lock.EnterWriteLock();
        _value = value;
        _lock.ExitWriteLock();
    }

    public int Count()
    {
        _lock.EnterReadLock();
        int result = _value.Count;
        _lock.ExitReadLock();
        return result;
    }

    public bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue? value)
    {
        _lock.EnterReadLock();
        bool result = _value.TryGetValue(key, out value);
        _lock.ExitReadLock();
        return result;
    }

    public TValue GetValue(TKey key)
    {
        _lock.EnterReadLock();
        TValue result = _value[key];
        _lock.ExitReadLock();
        return result;
    }

    public void SetValue(TKey key, TValue value)
    {
        _lock.EnterWriteLock();
        _value[key] = value;
        _lock.ExitWriteLock();
    }

    private Dictionary<TKey, TValue> _value;
    private readonly ReaderWriterLockSlim _lock = new();
}

public class ThreadSafeValue<TValue>
    where TValue : class
{
    public ThreadSafeValue(TValue value)
    {
        _value = value;
    }

    public TValue Value
    {
        get => Volatile.Read(ref _value);
        set => Interlocked.Exchange(ref _value, value);
    }

    private TValue _value;
}

public class ThreadSafeString
{
    public ThreadSafeString(string value)
    {
        _value = value;
    }

    public string Value
    {
        get => Volatile.Read(ref _value);
        set => Interlocked.Exchange(ref _value, value);
    }

    private string _value;
}
