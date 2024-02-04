// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="IEnumerableExtensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace BackupManager.Extensions;

internal static class EnumerableExtensions
{
    public static bool Empty<TSource>(this IEnumerable<TSource> source)
    {
        return !source.Any();
    }
}