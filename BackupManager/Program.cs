// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager
{
    using System;
    using System.Runtime.Versioning;
    using System.Windows.Forms;

    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [SupportedOSPlatform("windows")]
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main());
        }
    }
}
