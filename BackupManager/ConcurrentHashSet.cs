// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ConcurrentHashSet.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace BackupManager;

// ReSharper disable once UnusedType.Global
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal sealed class ConcurrentHashSet<T> : IDisposable, IEnumerable<T>
{
    private readonly HashSet<T> hashSet = new();

    private readonly ReaderWriterLockSlim @lock = new(LockRecursionPolicy.SupportsRecursion);

    public ConcurrentHashSet(IEnumerable<T> collection)
    {
        foreach (var v in collection)
        {
            _ = Add(v);
        }
    }

    public int Count
    {
        get
        {
            @lock.EnterReadLock();

            try
            {
                return hashSet.Count;
            }
            finally
            {
                if (@lock.IsReadLockHeld) @lock.ExitReadLock();
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return hashSet.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Add(T item)
    {
        @lock.EnterWriteLock();

        try
        {
            return hashSet.Add(item);
        }
        finally
        {
            if (@lock.IsWriteLockHeld) @lock.ExitWriteLock();
        }
    }

    public bool AddOrUpdate(T item)
    {
        @lock.EnterWriteLock();

        try
        {
            if (hashSet.Contains(item)) _ = hashSet.Remove(item);
            return hashSet.Add(item);
        }
        finally
        {
            if (@lock.IsWriteLockHeld) @lock.ExitWriteLock();
        }
    }

    public void Clear()
    {
        @lock.EnterWriteLock();

        try
        {
            hashSet.Clear();
        }
        finally
        {
            if (@lock.IsWriteLockHeld) @lock.ExitWriteLock();
        }
    }

    public bool Contains(T item)
    {
        @lock.EnterReadLock();

        try
        {
            return hashSet.Contains(item);
        }
        finally
        {
            if (@lock.IsReadLockHeld) @lock.ExitReadLock();
        }
    }

    public bool Remove(T item)
    {
        @lock.EnterWriteLock();

        try
        {
            return hashSet.Remove(item);
        }
        finally
        {
            if (@lock.IsWriteLockHeld) @lock.ExitWriteLock();
        }
    }

    private void Dispose(bool disposing)
    {
        if (disposing) @lock?.Dispose();
    }

    ~ConcurrentHashSet()
    {
        Dispose(false);
    }
}