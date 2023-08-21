// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ControlExtensions.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager
{
    using System;
    using System.Windows.Forms;

    public static class ControlExtensions
    {
        public static void Invoke<T>(this T c, Action<T> action) where T : Control
        {
            if (c.InvokeRequired)
            {
                _ = c.Invoke(new Action<T, Action<T>>(Invoke), new object[] { c, action });
            }
            else
            {
                action(c);
            }
        }
    }
}