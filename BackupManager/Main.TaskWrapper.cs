// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.TaskWrapper.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    private void TaskWrapper(Action methodName)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        tokenSource?.Dispose();
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;

        Task.Run(methodName, ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Trace("Exception in the TaskWrapper");
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, string.Format(Resources.Main_TaskWrapperException, u.Exception));
            CancelButton_Click(null, null);
            Utils.Trace("CancelButton_Click done");
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void TaskWrapper(Action<bool> methodName, bool param1)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        tokenSource?.Dispose();
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;

        Task.Run(() => methodName(param1), ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Trace("Exception in the TaskWrapper");
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, string.Format(Resources.Main_TaskWrapperException, u.Exception));
            CancelButton_Click(null, null);
            Utils.Trace("CancelButton_Click done");
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void TaskWrapper(Action<string> methodName, string param1)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        tokenSource?.Dispose();
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;

        Task.Run(() => methodName(param1), ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Trace("Exception in the TaskWrapper");
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, string.Format(Resources.Main_TaskWrapperException, u.Exception));
            CancelButton_Click(null, null);
            Utils.Trace("CancelButton_Click done");
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void TaskWrapper(Action<bool, bool> methodName, bool param1, bool param2)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        tokenSource?.Dispose();
        tokenSource = new CancellationTokenSource();
        ct = tokenSource.Token;

        Task.Run(() => methodName(param1, param2), ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Trace("Exception in the TaskWrapper");
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, string.Format(Resources.Main_TaskWrapperException, u.Exception));
            CancelButton_Click(null, null);
            Utils.Trace("CancelButton_Click done");
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }
}