/**
 * MyAIAgent | Common · Tests
 *
 * Supplies asynchronous sequences for tests that exercise streaming interfaces without external I/O.
 */

namespace MyAIAgent.Common.Tests;

public static class AsyncEnumerableHelper
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
