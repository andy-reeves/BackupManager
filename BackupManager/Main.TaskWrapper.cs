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
    private async Task<bool> TaskWrapper(Task<bool> task, CancellationToken ct)
    {
        try
        {
            Utils.TraceIn();
            return await task;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Log(Resources.Cancelling);
            ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High, string.Format(Resources.TaskWrapperException, u));
            ASyncTasksCleanUp();
        }
        return Utils.TraceOut(false);
    }

    private async Task TaskWrapper(Action action, bool withAsyncTasksCleanup, CancellationToken ct)
    {
        try
        {
            Utils.TraceIn($"withAsyncTasksCleanup = {withAsyncTasksCleanup}");
            await Task.Run(action, ct);
            Utils.Trace("After await");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Log(Resources.Cancelling);
            if (withAsyncTasksCleanup) ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High, string.Format(Resources.TaskWrapperException, u));
            if (withAsyncTasksCleanup) ASyncTasksCleanUp();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private async Task TaskWrapper(Action action, CancellationToken ct)
    {
        Utils.TraceIn();
        await TaskWrapper(action, true, ct);
        Utils.TraceOut();
    }
}