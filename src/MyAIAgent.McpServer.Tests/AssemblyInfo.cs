/**
 * MyAIAgent | McpServer · Tests
 *
 * Configures assembly-wide test execution settings for fixtures that share process state.
 */

using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
