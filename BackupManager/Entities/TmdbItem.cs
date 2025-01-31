// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="TmdbItem.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;

namespace BackupManager.Entities;

/// <summary>
///     This class allows us to keep a Collection of TmdbItem with an ID and runningTime
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class TmdbItem : IEquatable<TmdbItem>
{
    public TmdbItem() { }

    public TmdbItem(string tmdbId, int runtimeInMinutes = -1)
    {
        Id = tmdbId;
        Runtime = runtimeInMinutes;
    }

    /// <summary>
    ///     The id of the item. For movies, we use an integer id of the TmdbId but for TV episodes we use
    ///     'seriesTvdbID:seasonNumber:episodeNumber'
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// </summary>
    public int Runtime { get; set; }

    public bool Equals(TmdbItem other)
    {
        return null != other && Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as TmdbItem);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return Id;
    }
}