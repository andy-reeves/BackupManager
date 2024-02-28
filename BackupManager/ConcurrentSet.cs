using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BackupManager;

internal sealed class ConcurrentSet<T> : ISet<T>
{
    private readonly ConcurrentDictionary<T, byte> dictionary = new();

    private readonly ReaderWriterLockSlim @lock = new(LockRecursionPolicy.SupportsRecursion);

    /// <summary>
    ///     Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>
    ///     A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
    /// </returns>
    public IEnumerator<T> GetEnumerator()
    {
        return dictionary.Keys.GetEnumerator();
    }

    /// <summary>
    ///     Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>
    ///     An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
    /// </returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    ///     Removes the first occurrence of a specific object from the
    ///     <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <returns>
    ///     true if <paramref name="item" /> was successfully removed from the
    ///     <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if
    ///     <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </returns>
    /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
    /// <exception cref="T:System.NotSupportedException">
    ///     The <see cref="T:System.Collections.Generic.ICollection`1" /> is
    ///     read-only.
    /// </exception>
    public bool Remove(T item)
    {
        return TryRemove(item);
    }

    /// <summary>
    ///     Gets the number of elements in the set.
    /// </summary>
    public int Count => dictionary.Count;

    /// <summary>
    ///     Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
    /// </summary>
    /// <returns>
    ///     true if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, false.
    /// </returns>
    public bool IsReadOnly => false;

    /// <summary>
    ///     Gets a value that indicates if the set is empty.
    /// </summary>
    public bool IsEmpty => dictionary.IsEmpty;

    private ICollection<T> Values => dictionary.Keys;

    /// <summary>
    ///     Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
    /// <exception cref="T:System.NotSupportedException">
    ///     The <see cref="T:System.Collections.Generic.ICollection`1" /> is
    ///     read-only.
    /// </exception>
    void ICollection<T>.Add(T item)
    {
        if (item is not null && !Add(item)) throw new ArgumentException("Item already exists in set.");
    }

    /// <summary>
    ///     Modifies the current set so that it contains all elements that are present in both the current set and in the
    ///     specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="other" /> is null.</exception>
    public void UnionWith(IEnumerable<T> other)
    {
        foreach (var item in other)
        {
            _ = TryAdd(item);
        }
    }

    /// <summary>
    ///     Modifies the current set so that it contains only elements that are also in a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="other" /> is null.</exception>
    public void IntersectWith(IEnumerable<T> other)
    {
        var enumerable = other as IList<T> ?? other.ToArray();

        foreach (var item in this.Where(item => !enumerable.Contains(item)))
        {
            _ = TryRemove(item);
        }
    }

    /// <summary>
    ///     Removes all elements in the specified collection from the current set.
    /// </summary>
    /// <param name="other">The collection of items to remove from the set.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="other" /> is null.</exception>
    public void ExceptWith(IEnumerable<T> other)
    {
        foreach (var item in other)
        {
            _ = TryRemove(item);
        }
    }

    /// <summary>
    ///     Modifies the current set so that it contains only elements that are present either in the current set or in the
    ///     specified collection, but not both.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="other" /> is null.</exception>
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Determines whether a set is a subset of a specified collection.
    /// </summary>
    /// <returns>
    ///     true if the current set is a subset of <paramref name="other" />; otherwise, false.
    /// </returns>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="other" /> is null.</exception>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        var enumerable = other as IList<T> ?? other.ToArray();
        return this.AsParallel().All(enumerable.Contains);
    }

    /// <summary>
    ///     Determines whether the current set is a superset of a specified collection.
    /// </summary>
    /// <returns>
    ///     true if the current set is a superset of <paramref name="other" />; otherwise, false.
    /// </returns>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="other" /> is null.</exception>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        return other.AsParallel().All(Contains);
    }

    /// <summary>
    ///     Determines whether the current set is a correct superset of a specified collection.
    /// </summary>
    /// <returns>
    ///     true if the <see cref="T:System.Collections.Generic.ISet`1" /> object is a correct superset of
    ///     <paramref name="other" />; otherwise, false.
    /// </returns>
    /// <param name="other">The collection to compare to the current set. </param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="other" /> is null.</exception>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        var enumerable = other as IList<T> ?? other.ToArray();
        return Count != enumerable.Count && IsSupersetOf(enumerable);
    }

    /// <summary>
    ///     Determines whether the current set is a property (strict) subset of a specified collection.
    /// </summary>
    /// <returns>
    ///     true if the current set is a correct subset of <paramref name="other" />; otherwise, false.
    /// </returns>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="other" /> is null.</exception>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        var enumerable = other as IList<T> ?? other.ToArray();
        return Count != enumerable.Count && IsSubsetOf(enumerable);
    }

    /// <summary>
    ///     Determines whether the current set overlaps with the specified collection.
    /// </summary>
    /// <returns>
    ///     true if the current set and <paramref name="other" /> share at least one common element; otherwise, false.
    /// </returns>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="other" /> is null.</exception>
    public bool Overlaps(IEnumerable<T> other)
    {
        return other.AsParallel().Any(Contains);
    }

    /// <summary>
    ///     Determines whether the current set and the specified collection contain the same elements.
    /// </summary>
    /// <returns>
    ///     true if the current set is equal to <paramref name="other" />; otherwise, false.
    /// </returns>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="other" /> is null.</exception>
    public bool SetEquals(IEnumerable<T> other)
    {
        var enumerable = other as IList<T> ?? other.ToArray();
        return Count == enumerable.Count && enumerable.AsParallel().All(Contains);
    }

    /// <summary>
    ///     Adds an element to the current set and returns a value to indicate if the element was successfully added.
    /// </summary>
    /// <returns>
    ///     true if the element is added to the set; false if the element is already in the set.
    /// </returns>
    /// <param name="item">The element to add to the set.</param>
    public bool Add(T item)
    {
        return TryAdd(item);
    }

    public bool AddOrUpdate(T item)
    {
        @lock.EnterWriteLock();

        try
        {
            if (dictionary.ContainsKey(item)) _ = Remove(item);
            return Add(item);
        }
        finally
        {
            if (@lock.IsWriteLockHeld) @lock.ExitWriteLock();
        }
    }

    public void Clear()
    {
        dictionary.Clear();
    }

    public bool Contains(T item)
    {
        return item is not null && dictionary.ContainsKey(item);
    }

    /// <summary>
    ///     Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an
    ///     Array, starting at a particular <see cref="T:System.Array" /> index.
    /// </summary>
    /// <param name="array">
    ///     The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied
    ///     from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have
    ///     zero-based indexing.
    /// </param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="array" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex" /> is less than 0.</exception>
    public void CopyTo(T[] array, int arrayIndex)
    {
        Values.CopyTo(array, arrayIndex);
    }

    public T[] ToArray()
    {
        return dictionary.Keys.ToArray();
    }

    private bool TryAdd(T item)
    {
        return dictionary.TryAdd(item, default);
    }

    private bool TryRemove(T item)
    {
        return dictionary.TryRemove(item, out _);
    }
}