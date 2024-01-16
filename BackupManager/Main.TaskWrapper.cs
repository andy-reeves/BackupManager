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
    private async Task<bool> TaskWrapper(Task<bool> task, string methodName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        try
        {
            Utils.Trace($"{methodName} just before await");
            return await task;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Canceling {methodName}");
            ASyncTasksCleanUp(methodName);
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
        return false;
    }

    /// <summary>
    /// </summary>
    /// <param name="task"></param>
    /// <param name="methodName"></param>
    /// <param name="ct"></param>
    private async Task TaskWrapper(Task task, string methodName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        try
        {
            Utils.Trace($"{methodName} just before await");
            await task;
            Utils.Trace($"{methodName} just after await");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Canceling {methodName}");
            ASyncTasksCleanUp(methodName);
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
    }
}