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
    /// <summary>
    ///     Called by the Application monitoring
    /// </summary>
    /// <param name="methodName"></param>
    private void TaskWrapper(Action methodName)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        ResetTokenSource();

        _ = Task.Run(methodName, ct).ContinueWith(static u =>
        {
            if (u.Exception == null) return;

            Utils.Trace("Exception in the TaskWrapper");

            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u.Exception));
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Cancelling {methodName.Method}");
            ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
    }

    /// <summary>
    ///     Called by the Check, Copy repeater
    /// </summary>
    /// <param name="methodName"></param>
    /// <param name="param1"></param>
    private async Task TaskWrapper(Action<bool> methodName, bool param1)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        try
        {
            var task = Task.Run(() => methodName(param1), ct);
            await task;
            if (longRunningActionExecutingRightNow) ASyncTasksCleanUp();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Cancelling {methodName.Method}");
            ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
    }

    private async Task TaskWrapper(Action<string> methodName, string param1)
    {
        try
        {
            var task = Task.Run(() => methodName(param1), ct);
            await task;
            if (longRunningActionExecutingRightNow) ASyncTasksCleanUp();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Cancelling {methodName.Method}");
            ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
    }

    /// <summary>
    ///     Called by ProcessFiles
    /// </summary>
    /// <param name="methodName"></param>
    /// <param name="param1"></param>
    /// <param name="scanId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private Task<bool> TaskWrapper(Func<string[], string, CancellationToken, bool> methodName, string[] param1, string scanId,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        return Task.Run(() => methodName(param1, scanId, token), ct);
    }

    /// <summary>
    ///     Called by GetFiles
    /// </summary>
    /// <param name="methodName"></param>
    /// <param name="param1"></param>
    /// <param name="scanId"></param>
    /// <returns></returns>
    private Task TaskWrapper(Action<string[], string> methodName, string[] param1, string scanId)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        var task = Task.Run(() => methodName(param1, scanId), ct);
        return task;
    }

    /// <summary>
    ///     Called by Check,Copy
    /// </summary>
    /// <param name="methodName"></param>
    /// <param name="param1"></param>
    /// <param name="param2"></param>
    private async Task TaskWrapper(Action<bool, bool> methodName, bool param1, bool param2)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        try
        {
            var task = Task.Run(() => methodName(param1, param2), ct);
            await task;
            if (longRunningActionExecutingRightNow) ASyncTasksCleanUp();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Cancelling {methodName.Method}");
            ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
    }
}