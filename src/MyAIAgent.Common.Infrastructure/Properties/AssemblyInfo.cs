/**
 * MyAIAgent | Common · Infrastructure
 *
 * Exposes selected internal types to the named test assemblies without making them public APIs.
 */

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MyAIAgent.Gateway.Tests")]
