[![](https://img.shields.io/nuget/v/soenneker.extensions.funcs.tasks.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.extensions.funcs.tasks/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.funcs.tasks/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.funcs.tasks/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.extensions.funcs.tasks.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.extensions.funcs.tasks/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.funcs.tasks/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.funcs.tasks/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Extensions.Funcs.Tasks
Invokes optional asynchronous delegates, including every subscriber in a multicast delegate.

## Installation

```bash
dotnet add package Soenneker.Extensions.Funcs.Tasks
```

## Usage

```csharp
using Soenneker.Extensions.Funcs.Tasks;

Func<Order, Task>? handlers = null;
handlers += SendReceipt;
handlers += RecordAnalytics;

await handlers.InvokeIfDefined(order);
```

There are overloads for `Func<Task>` and `Func<T, Task>`. A null delegate returns `Task.CompletedTask`. For multicast delegates, every subscriber is invoked immediately and the returned task completes when all subscriber tasks complete—subscribers run concurrently rather than one after another.

Failures follow `Task.WhenAll` semantics: the returned task faults after all subscribers finish, and its `Exception` contains the collected failures. An exception thrown synchronously while invoking a subscriber escapes immediately, so later subscribers are not invoked in that case.
