/**
 * MyAIAgent | Console Client · Tests
 *
 * Isolates tests that temporarily change process-wide configuration environment variables.
 */

namespace MyAIAgent.ConsoleClient.Tests;

[CollectionDefinition("Configuration", DisableParallelization = true)]
public sealed class ConfigurationCollection;
