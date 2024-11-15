// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using BackupManager.Properties;

namespace BackupManager;

file static class Program
{
    private static readonly string _appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), true)[0]).Value;

    private static readonly Mutex _singleton = new(true, _appGuid + Utils.InDebugBuild);

    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        if (!_singleton.WaitOne(TimeSpan.Zero, true))
        {
            _ = MessageBox.Show(Resources.AlreadyRunning);
            Environment.Exit(-1);
        }

        // Add handler to handle the exception raised by main threads
        Application.ThreadException += Application_ThreadException;

        // Add handler to handle the exception raised by additional threads
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Main());

        // Stop the application and all the threads in suspended state.
        Environment.Exit(-1);
    }

    private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        // All exceptions thrown by the main thread are handled over this method
        ShowExceptionDetails(e.Exception);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // All exceptions thrown by additional threads are handled in this method
        ShowExceptionDetails(e.ExceptionObject as Exception);

        // Stop the application and all the threads in suspended state.
        Environment.Exit(-1);
    }

    private static void ShowExceptionDetails(Exception ex)
    {
        // text log first in case message sending is failing
        Utils.Log(BackupAction.Error, ex.Message + " " + ex.TargetSite);

        // show the message box before attempting pushover
        _ = MessageBox.Show(ex.Message, ex.TargetSite?.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
        Utils.LogWithPushover(BackupAction.Error, ex.Message + " " + ex.TargetSite);
    }
}