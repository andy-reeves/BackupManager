// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ControlExtensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Windows.Forms;

namespace BackupManager.Extensions;

internal static class ControlExtensions
{
    internal static void Invoke<T>(this T c, Action<T> action) where T : Control
    {
        if (c.InvokeRequired)
            _ = c.Invoke(new Action<T, Action<T>>(Invoke), c, action);
        else
            action(c);
    }
}

internal static class ObjectExtensions
{
    /// <summary>
    ///     Checks the value is in the given range (inclusive)
    /// </summary>
    /// <typeparam name="T">Any type that implements IComparable</typeparam>
    /// <param name="value">The value to check</param>
    /// <param name="minimum">The minimum value (inclusive)</param>
    /// <param name="maximum">The maximum value (inclusive)</param>
    /// <returns></returns>
    public static bool IsInRange<T>(this T value, T minimum, T maximum) where T : IComparable<T>
    {
        if (value.CompareTo(minimum) < 0) return false;

        return value.CompareTo(maximum) <= 0;
    }
}