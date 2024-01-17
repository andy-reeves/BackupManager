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
            Utils.Trace(Resources.Main_Cancelling);
            ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
        return false;
    }

    private async Task TaskWrapper(Task task, bool withAsyncTasksCleanup, CancellationToken ct)
    {
        try
        {
            Utils.TraceIn($"withAsyncTasksCleanup = {withAsyncTasksCleanup}");
            await task;
            Utils.Trace("After await");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace(Resources.Main_Cancelling);
            if (withAsyncTasksCleanup) ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
    }

    private async Task TaskWrapper(Task task, CancellationToken ct)
    {
        await TaskWrapper(task, true, ct);
    }
}