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
    /// <param name="ct"></param>
    private async Task TaskWrapper(Action methodName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        try
        {
            var task = Task.Run(methodName, ct);
            await task;
            Utils.Trace($"{methodName} just after await");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Canceling {methodName.Method}");
            ASyncTasksCleanUp(methodName.Method);
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
    }

    private async Task TaskWrapper(Action<CancellationToken> methodName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        try
        {
            var task = Task.Run(() => methodName(ct), ct);
            await task;
            Utils.Trace($"{methodName} just after await");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Canceling {methodName.Method}");
            ASyncTasksCleanUp(methodName.Method);
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
    /// <param name="ct"></param>
    private async Task TaskWrapper(Action<bool, CancellationToken> methodName, bool param1, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        try
        {
            var task = Task.Run(() => methodName(param1, ct), ct);
            await task;
            Utils.Trace($"{methodName} just after await");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Canceling {methodName.Method}");
            ASyncTasksCleanUp(methodName.Method);
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
    }

    private async Task TaskWrapper(Action<string, CancellationToken> methodName, string param1, CancellationToken ct)
    {
        try
        {
            var task = Task.Run(() => methodName(param1, ct), ct);
            await task;
            Utils.Trace($"{methodName} just after await");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Canceling {methodName.Method}");
            ASyncTasksCleanUp(methodName.Method);
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
    /// <param name="param2"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private Task<bool> TaskWrapper(Func<string[], string, CancellationToken, bool> methodName, string[] param1, string param2,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        return Task.Run(() => methodName(param1, param2, ct), ct);
    }

    /// <summary>
    ///     Called by GetFiles
    /// </summary>
    /// <param name="methodName"></param>
    /// <param name="param1"></param>
    /// <param name="param2"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private Task TaskWrapper(Action<string[], string, CancellationToken> methodName, string[] param1, string param2,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        return Task.Run(() => methodName(param1, param2, ct), ct);
    }

    /// <summary>
    ///     Called by Check,Copy
    /// </summary>
    /// <param name="methodName"></param>
    /// <param name="param1"></param>
    /// <param name="param2"></param>
    /// <param name="ct"></param>
    private async Task TaskWrapper(Action<bool, bool, CancellationToken> methodName, bool param1, bool param2,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        try
        {
            var task = Task.Run(() => methodName(param1, param2, ct), ct);
            await task;
            Utils.Trace($"{methodName} just after await");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace($"Canceling {methodName.Method}");
            ASyncTasksCleanUp(methodName.Method);
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
    }
}