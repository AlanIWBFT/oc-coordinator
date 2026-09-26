#!/usr/bin/env dotnet
#:property PublishAot=false

using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length != 3)
    throw new ArgumentException("Usage: dotnet Stage-OpenCodeSdk.cs -- <OpenCode root> <new staging directory> <pinned version>");

var root = Path.GetFullPath(args[0]);
var destination = Path.GetFullPath(args[1]);
var version = args[2];
if (Path.Exists(destination)) throw new IOException($"SDK staging directory already exists: {destination}");
var catalog = ReadPackage(root)["workspaces"]?["catalog"]?.AsObject()
    ?? throw new InvalidOperationException("OpenCode workspace catalog is missing.");
string[] packages = ["schema", "protocol", "client"];
var manifests = packages.ToDictionary(name => name, name => ReadPackage(Path.Combine(root, "packages", name)));
foreach (var (name, manifest) in manifests)
{
    if (manifest["name"]?.GetValue<string>() != $"@opencode/{name}" || manifest["version"]?.GetValue<string>() != version)
        throw new InvalidOperationException($"Local SDK package {name} must have pinned version {version}.");
}

Directory.CreateDirectory(destination);
foreach (var (name, original) in manifests)
{
    var source = Path.Combine(root, "packages", name, "dist");
    if (!Directory.Exists(source)) throw new DirectoryNotFoundException($"Built SDK dist is missing: {source}");
    var manifest = original.DeepClone().AsObject();
    manifest.Remove("scripts");
    manifest.Remove("devDependencies");
    var exports = manifest["exports"]?.AsObject() ?? throw new InvalidOperationException($"SDK exports missing: {name}");
    foreach (var (key, value) in exports.ToArray())
    {
        var entry = value?.GetValue<string>() ?? "";
        if (!entry.StartsWith("./src/", StringComparison.Ordinal) || !entry.EndsWith(".ts", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected SDK export {name} {key}: {entry}");
        var emitted = "./dist/" + entry[6..^3];
        if (!emitted.Contains('*'))
        {
            RequireFile(Path.Combine(root, "packages", name, emitted + ".js"));
            RequireFile(Path.Combine(root, "packages", name, emitted + ".d.ts"));
        }
        exports[key] = new JsonObject { ["types"] = emitted + ".d.ts", ["import"] = emitted + ".js" };
    }

    foreach (var field in new[] { "dependencies", "optionalDependencies", "peerDependencies" })
    {
        if (manifest[field] is not JsonObject dependencies) continue;
        foreach (var (dependency, value) in dependencies.ToArray())
        {
            var specifier = value!.GetValue<string>();
            if (specifier == "catalog:")
                dependencies[dependency] = catalog[dependency]?.GetValue<string>()
                    ?? throw new InvalidOperationException($"SDK dependency is absent from catalog: {dependency}");
            else if (specifier == "workspace:*" && packages.Any(item => dependency == $"@opencode/{item}"))
                dependencies[dependency] = version;
            else if (specifier.StartsWith("catalog:", StringComparison.Ordinal) || specifier.StartsWith("workspace:", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsupported SDK dependency: {dependency} = {specifier}");
        }
    }

    var target = Path.Combine(destination, "packages", name);
    Directory.CreateDirectory(target);
    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var output = Path.Combine(target, "dist", Path.GetRelativePath(source, file));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.Copy(file, output);
    }
    WritePackage(target, manifest);
}

var workspaceDependencies = new JsonObject();
foreach (var name in packages) workspaceDependencies[$"@opencode/{name}"] = "workspace:*";
WritePackage(destination, new JsonObject
{
    ["name"] = "local-opencode-sdk",
    ["private"] = true,
    ["workspaces"] = new JsonArray("packages/*"),
    ["dependencies"] = workspaceDependencies,
});
Console.WriteLine($"Staged @opencode client/schema/protocol {version}: {destination}");

static JsonObject ReadPackage(string directory) => JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "package.json")))!.AsObject();

static void WritePackage(string directory, JsonObject package) =>
    File.WriteAllText(Path.Combine(directory, "package.json"), package.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\r\n") + "\r\n");

static void RequireFile(string file)
{
    if (!File.Exists(file)) throw new FileNotFoundException("Built SDK export is missing.", file);
}
