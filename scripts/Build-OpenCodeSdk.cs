#!/usr/bin/env dotnet
#:property PublishAot=false
#:include Build.Processes.cs

using LocalFork;

if (args.Length != 2) throw new ArgumentException("Usage: dotnet Build-OpenCodeSdk.cs -- <OpenCode root> <Bun executable>");
var root = Path.GetFullPath(args[0]);
BuildProcesses.Run(new[] { "schema", "protocol", "client" }.Select(name =>
    new BuildBranch($"OpenCode {name}", new BuildCommand(args[1], ["run", "--cwd", Path.Combine(root, "packages", name), "build"], root))).ToArray(),
    new Dictionary<string, string?>());
