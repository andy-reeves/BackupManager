// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="TmdbMovie.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace BackupManager.Entities;

/// <summary>
///     This class allows us to keep a Collection of TmdbMovies with the id and runningTime it was last changed
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class TmdbMovie : IEquatable<TmdbMovie>
{
    private int runtime;

    private readonly int id;

    public TmdbMovie() { }

    public TmdbMovie(int tmdbId, int runtimeInMinutes = -1)
    {
        id = tmdbId;
        runtime = runtimeInMinutes;
    }

    /// <summary>
    ///     The path to the FileSystemEntry that changed
    /// </summary>
    public int Id
    {
        get => id;

        init
        {
            if (id == value) return;

            id = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     This is set to True if any data has changed, and we need to Save.
    /// </summary>
    [XmlIgnore]
    public bool Changed { get; set; }

    /// <summary>
    /// </summary>
    public int Runtime
    {
        get => runtime;

        set
        {
            if (runtime == value) return;

            runtime = value;
            Changed = true;
        }
    }

    public bool Equals(TmdbMovie other)
    {
        return null != other && id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as TmdbMovie);
    }

    public override int GetHashCode()
    {
        return id.GetHashCode();
    }

    public override string ToString()
    {
        return id.ToString();
    }
}