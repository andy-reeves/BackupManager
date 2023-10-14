// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.TaskWrapper.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    private void TaskWrapper(Action methodName)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;

        Task.Run(methodName, ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Log("Exception occurred. Cancelling operation.");
            MessageBox.Show(string.Format(Resources.Main_TaskWrapperException, u.Exception));
            CancelButton_Click(null, null);
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void TaskWrapper(Action<bool> methodName, bool param1)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;

        Task.Run(() => methodName(param1), ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Log("Exception occurred. Cancelling operation.");
            _ = MessageBox.Show(string.Format(Resources.Main_TaskWrapperException, u.Exception));
            CancelButton_Click(null, null);
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void TaskWrapper(Action<bool, bool> methodName, bool param1, bool param2)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;

        Task.Run(() => methodName(param1, param2), ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Log("Exception occurred. Cancelling operation.");
            _ = MessageBox.Show(string.Format(Resources.Main_TaskWrapperException, u.Exception));
            CancelButton_Click(null, null);
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }
}