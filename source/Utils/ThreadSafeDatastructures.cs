using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace PlayerModelLib;

public static class ThreadSafeUtils
{
    public static void InsertTextureIntoAtlas(CompositeTexture compositeTexture, ICoreClientAPI api, Entity entity, ITextureAtlasAPI? targetAtlas = null, PlayerSkinBehavior? skinBehavior = null)
    {
        skinBehavior ??= entity.GetBehavior<PlayerSkinBehavior>();
        skinBehavior?.TexturesAwaitingToBeAddedToAtlas.Increment();

        if (Environment.CurrentManagedThreadId != RuntimeEnv.MainThreadId)
        {
            api.Event.EnqueueMainThreadTask(() => InsertTextureIntoAtlasTask(compositeTexture, api, skinBehavior, targetAtlas), "PlayerSkinBehavior.AddSkinPart");
        }
        else
        {
            InsertTextureIntoAtlasTask(compositeTexture, api, skinBehavior, targetAtlas);
        }
    }

    private static void InsertTextureIntoAtlasTask(CompositeTexture compositeTexture, ICoreClientAPI api, PlayerSkinBehavior? skinBehavior, ITextureAtlasAPI? targetAtlas = null)
    {
        try
        {
            if (compositeTexture.Baked == null)
            {
                compositeTexture.Bake(api.Assets);
            }

            if ((targetAtlas ?? api.EntityTextureAtlas).GetOrInsertTexture(compositeTexture.Baked?.TextureFilenames[0], out int textureSubId, out _) && compositeTexture.Baked != null)
            {
                compositeTexture.Baked.TextureSubId = textureSubId;
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"InsertTextureIntoAtlasTask error:\n{exception}");
        }
        finally
        {
            skinBehavior?.TexturesAwaitingToBeAddedToAtlas.Decrement();
        }
    }
}

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
    where TKey : notnull
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

public class ThreadSafeInt
{
    public ThreadSafeInt(int value)
    {
        _value = value;
    }

    public int Value
    {
        get => Volatile.Read(ref _value);
        set => Interlocked.Exchange(ref _value, value);
    }

    public int Increment() => Interlocked.Increment(ref _value);

    public int Decrement() => Interlocked.Decrement(ref _value);

    public int Add(int amount) => Interlocked.Add(ref _value, amount);

    private int _value;
}

public class ThreadSafeUInt
{
    private int _value;

    public ThreadSafeUInt(uint value)
    {
        _value = unchecked((int)value);
    }

    public uint Value
    {
        get => unchecked((uint)Volatile.Read(ref _value));
        set => Interlocked.Exchange(ref _value, unchecked((int)value));
    }

    public uint Increment() => unchecked((uint)Interlocked.Increment(ref _value));

    public uint Add(uint amount) => unchecked((uint)Interlocked.Add(ref _value, unchecked((int)amount)));

    public uint Decrement()
    {
        while (true)
        {
            int snapshot = Volatile.Read(ref _value);
            uint current = unchecked((uint)snapshot);

            if (current == 0)
            {
                return 0;
            }

            uint updated = current - 1;
            int updatedInt = unchecked((int)updated);

            if (Interlocked.CompareExchange(ref _value, updatedInt, snapshot) == snapshot)
            {
                return updated;
            }
        }
    }
}

public class ThreadSafeBool
{
    public ThreadSafeBool(bool value)
    {
        _value = value ? 1 : 0;
    }

    public bool Value
    {
        get => Volatile.Read(ref _value) != 0;
        set => Interlocked.Exchange(ref _value, value ? 1 : 0);
    }

    public bool SetTrue() => Interlocked.Exchange(ref _value, 1) != 0;

    public bool SetFalse() => Interlocked.Exchange(ref _value, 0) != 0;

    public bool TrySet(bool expected, bool newValue)
    {
        int expectedInt = expected ? 1 : 0;
        int newValueInt = newValue ? 1 : 0;

        return Interlocked.CompareExchange(ref _value, newValueInt, expectedInt) == expectedInt;
    }

    private int _value; // 0 = false, 1 = true
}
