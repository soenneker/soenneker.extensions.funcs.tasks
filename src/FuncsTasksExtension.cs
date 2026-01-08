using System;
using System.Buffers;
using System.Threading.Tasks;

namespace Soenneker.Extensions.Funcs.Tasks;

/// <summary>
/// A collection of helpful Func Task extension methods.
/// </summary>
public static class FuncsTasksExtension
{
    /// <summary>
    /// Invokes a multicast handler (Func&lt;T, Task&gt;) if it's not null.
    /// Awaits all subscribers and aggregates exceptions via Task.WhenAll.
    /// Optimized to avoid GetInvocationList allocations for single-cast and to minimize allocations for multi-cast.
    /// </summary>
    public static Task InvokeIfDefined<T>(this Func<T, Task>? handler, T arg)
    {
        if (handler is null)
            return Task.CompletedTask;

        // Fast-path: single-cast delegate (no GetInvocationList allocation)
        if (handler.Target is not null || handler.Method is not null) // always true, but keeps intent clear
        {
            // If it's *not* multicast, this stays null
            if (handler is not MulticastDelegate multicase)
                return handler(arg);
        }

        // In practice, Func<...> is always a MulticastDelegate, but the cast is cheap.
        MulticastDelegate multi = handler;

        // If invocation list is just one, avoid array allocations by calling directly
        // Unfortunately, the only way to know arity > 1 is GetInvocationList(), so:
        // - we pay it only when the delegate is multicast (handler combines).
        Delegate[] list = multi.GetInvocationList();
        int len = list.Length;

        if (len == 0)
            return Task.CompletedTask;

        if (len == 1)
            return ((Func<T, Task>)list[0]).Invoke(arg);

        if (len == 2)
        {
            Task t0 = ((Func<T, Task>)list[0]).Invoke(arg);
            Task t1 = ((Func<T, Task>)list[1]).Invoke(arg);
            return Task.WhenAll(t0, t1);
        }

        // 3+: rent buffer to avoid allocating Task[] every call
        Task[] rented = ArrayPool<Task>.Shared.Rent(len);
        try
        {
            for (int i = 0; i < len; i++)
                rented[i] = ((Func<T, Task>)list[i]).Invoke(arg);

            // WhenAll only observes the first 'len' tasks
            return WhenAllAndReturn(rented, len);
        }
        finally
        {
            // Clear to avoid holding Task references (and their captured state) in the pool
            Array.Clear(rented, 0, len);
            ArrayPool<Task>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Invokes a multicast handler (Func&lt;Task&gt;) if it's not null.
    /// Awaits all subscribers and aggregates exceptions via Task.WhenAll.
    /// Optimized to avoid GetInvocationList allocations for single-cast and to minimize allocations for multi-cast.
    /// </summary>
    public static Task InvokeIfDefined(this Func<Task>? handler)
    {
        if (handler is null)
            return Task.CompletedTask;

        // Fast-path: single-cast (no GetInvocationList allocation)
        // Note: a Func<Task> can still be multicast; we only pay GetInvocationList when it is.
        if (handler.GetInvocationList()
                   .Length == 1)
            return handler();

        Delegate[] list = handler.GetInvocationList();
        int len = list.Length;

        if (len == 0)
            return Task.CompletedTask;

        if (len == 1)
            return ((Func<Task>)list[0]).Invoke();

        if (len == 2)
        {
            Task t0 = ((Func<Task>)list[0]).Invoke();
            Task t1 = ((Func<Task>)list[1]).Invoke();
            return Task.WhenAll(t0, t1);
        }

        Task[] rented = ArrayPool<Task>.Shared.Rent(len);
        try
        {
            for (int i = 0; i < len; i++)
                rented[i] = ((Func<Task>)list[i]).Invoke();

            return WhenAllAndReturn(rented, len);
        }
        finally
        {
            Array.Clear(rented, 0, len);
            ArrayPool<Task>.Shared.Return(rented);
        }
    }

    // Avoid allocating an exact-sized array; still feed WhenAll with only the used prefix.
    private static Task WhenAllAndReturn(Task[] tasks, int length)
    {
        return Task.WhenAll(tasks.AsSpan(0, length));
    }
}