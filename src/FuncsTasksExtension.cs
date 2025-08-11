using System;
using System.Threading.Tasks;

namespace Soenneker.Extensions.Funcs.Tasks;

/// <summary>
/// A collection of helpful Func Task extension methods.
/// </summary>
public static class FuncsTasksExtension
{
    /// <summary>
    /// Invoke a multicast event (Func&lt;T, Task&gt;) if it's not null.
    /// Awaits all subscribers and aggregates exceptions via Task.WhenAll.
    /// Optimized to avoid LINQ/iterator allocations and fast-path small arities.
    /// </summary>
    public static Task InvokeIfDefined<T>(this Func<T, Task>? handler, T arg)
    {
        if (handler is null)
            return Task.CompletedTask;

        Delegate[] list = handler.GetInvocationList();
        int len = list.Length;

        // 0: impossible (handler != null), but keep for completeness
        if (len == 0)
            return Task.CompletedTask;

        // 1: direct call, no array/allocations
        if (len == 1)
            return ((Func<T, Task>) list[0]).Invoke(arg);

        // 2: use two-arg WhenAll overload (no array allocation)
        if (len == 2)
        {
            Task t0 = ((Func<T, Task>) list[0]).Invoke(arg);
            Task t1 = ((Func<T, Task>) list[1]).Invoke(arg);
            return Task.WhenAll(t0, t1);
        }

        // 3+: build exact-size array (single allocation), fill via for-loop
        var tasks = new Task[len];

        for (var i = 0; i < len; i++)
        {
            tasks[i] = ((Func<T, Task>) list[i]).Invoke(arg);
        }

        return Task.WhenAll(tasks);
    }

    /// <summary>
    /// Variant for parameterless Func&lt;Task&gt; events, with same optimizations.
    /// </summary>
    public static Task InvokeIfDefined(this Func<Task>? handler)
    {
        if (handler is null)
            return Task.CompletedTask;

        Delegate[] list = handler.GetInvocationList();
        int len = list.Length;

        if (len == 0)
            return Task.CompletedTask;

        if (len == 1)
            return ((Func<Task>) list[0]).Invoke();

        if (len == 2)
        {
            Task t0 = ((Func<Task>) list[0]).Invoke();
            Task t1 = ((Func<Task>) list[1]).Invoke();
            return Task.WhenAll(t0, t1);
        }

        var tasks = new Task[len];
        for (var i = 0; i < len; i++)
        {
            tasks[i] = ((Func<Task>) list[i]).Invoke();
        }

        return Task.WhenAll(tasks);
    }
}