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

        if (handler.HasSingleTarget)
            return handler(arg);

        var invocationList = Delegate.EnumerateInvocationList(handler);
        var len = 0;
        foreach (Func<T, Task> _ in invocationList)
            len++;

        if (len == 2)
        {
            var enumerator = invocationList.GetEnumerator();
            enumerator.MoveNext();
            Task t0 = enumerator.Current(arg);
            enumerator.MoveNext();
            Task t1 = enumerator.Current(arg);
            return Task.WhenAll(t0, t1);
        }

        // 3+: rent buffer to avoid allocating Task[] every call
        Task[] rented = ArrayPool<Task>.Shared.Rent(len);
        try
        {
            var i = 0;
            foreach (Func<T, Task> subscriber in invocationList)
                rented[i++] = subscriber(arg);

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

        if (handler.HasSingleTarget)
            return handler();

        var invocationList = Delegate.EnumerateInvocationList(handler);
        var len = 0;
        foreach (Func<Task> _ in invocationList)
            len++;

        if (len == 2)
        {
            var enumerator = invocationList.GetEnumerator();
            enumerator.MoveNext();
            Task t0 = enumerator.Current();
            enumerator.MoveNext();
            Task t1 = enumerator.Current();
            return Task.WhenAll(t0, t1);
        }

        Task[] rented = ArrayPool<Task>.Shared.Rent(len);
        try
        {
            var i = 0;
            foreach (Func<Task> subscriber in invocationList)
                rented[i++] = subscriber();

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
