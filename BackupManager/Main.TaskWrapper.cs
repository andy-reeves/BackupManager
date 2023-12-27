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

        _ = Task.Run(methodName, ct).ContinueWith(static u =>
        {
            if (u.Exception == null) return;

            Utils.Trace("Exception in the TaskWrapper");
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, string.Format(Resources.Main_TaskWrapperException, u.Exception));
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async Task TaskWrapperAsync(Action methodName)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        try
        {
            var task = Task.Run(methodName, ct);
            await task;
            if (longRunningActionExecutingRightNow) ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.Trace("Exception in the TaskWrapper");

            if (u.Message == "The operation was canceled.")
                Utils.LogWithPushover(BackupAction.General, PushoverPriority.Normal, "Cancelling");
            else
                Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, string.Format(Resources.Main_TaskWrapperException, u));
            ASyncTasksCleanUp();
        }
    }

    private void TaskWrapper(Action<bool> methodName, bool param1)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        _ = Task.Run(() => methodName(param1), ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Trace("Exception in the TaskWrapper");
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, string.Format(Resources.Main_TaskWrapperException, u.Exception));
            ASyncTasksCleanUp();
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void TaskWrapper(Action<string> methodName, string param1)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        var scheduler = TaskScheduler.FromCurrentSynchronizationContext();

        _ = Task.Run(() => methodName(param1), ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Trace("Exception in the TaskWrapper");
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, string.Format(Resources.Main_TaskWrapperException, u.Exception));
            ASyncTasksCleanUp();
        }, default, TaskContinuationOptions.OnlyOnFaulted, scheduler);
    }

    private Task TaskWrapper(Action<string[], string> methodName, string[] param1, string scanId)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        var task = Task.Run(() => methodName(param1, scanId), ct);
        return task;
    }

    private void TaskWrapper(Action<bool, bool> methodName, bool param1, bool param2)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        _ = Task.Run(() => methodName(param1, param2), ct).ContinueWith(u =>
        {
            if (u.Exception == null) return;

            Utils.Trace("Exception in the TaskWrapper");
            Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, string.Format(Resources.Main_TaskWrapperException, u.Exception));
            ASyncTasksCleanUp();
        }, default, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
    }
}