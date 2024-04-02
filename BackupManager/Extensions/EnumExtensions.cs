// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="EnumExtensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace BackupManager.Extensions;

internal static class EnumExtensions
{
    internal static string ToEnumMember<T>(this T value) where T : Enum
    {
        return typeof(T).GetTypeInfo().DeclaredMembers.SingleOrDefault(x => x.Name == value.ToString())?.GetCustomAttribute<EnumMemberAttribute>(false)?.Value;
    }
}