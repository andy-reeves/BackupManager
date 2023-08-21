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
        public static void ThreadSafeCall(this Control control, Action method)
        {
            if (control.InvokeRequired)
            {
                _ = control.Invoke(method);
            }
            else
            {
                method();
            }
        }
    }
}