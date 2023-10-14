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