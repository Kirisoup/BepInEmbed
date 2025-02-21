using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mono.Cecil;

namespace BepInEmbed;

internal sealed class DependencyResolver : IDisposable
{
	const string namePrefix = @$"__{nameof(BepInEmbed)}__";

	public DependencyResolver() {
		AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
		Plugin.instance.OnUnload += Dispose;
	}

	~DependencyResolver() => Dispose();
	public void Dispose() {
		_disposed = true;
		AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
	}

	readonly Dictionary<string, ResolvedDependency> _requestMap = [];
	bool _disposed;

	public record struct ResolvedDependency(
		long Tick,
		string Name,
		Assembly Assembly,
		Assembly RequestingAssembly,
		List<PluginGuid> Plugins);

	private Assembly? ResolveAssembly(object sender, ResolveEventArgs args)
	{
		if (_disposed) throw new InvalidOperationException(
			$"trying to call {nameof(ResolveAssembly)} on a disposed {nameof(DependencyResolver)}");

		if (args?.Name is null) return null;

		var requestedName = new AssemblyName(args.Name);

		var requester = args.RequestingAssembly;
		if (requester is null) {
			Plugin.Logger.LogWarning($"a null {nameof(args.RequestingAssembly)} is trying to request {requestedName}");
			return null;
		}

		if (_requestMap.TryGetValue(requestedName.Name, out var info)) {
			Plugin.Logger.LogInfo($"request {requestedName} exists in _requestMap");
			return info.Assembly;
		}

		if (!(requester.GetCustomAttribute<UseEmbedAttribute>() is {
			ResourceFilter: var resourceFilter,
			ResourceMap: var resourceMap
		})) {
			return null;
		}

		ResolvedDependency? resolved = resourceMap is null 
			? null
			: FindDependencyExplicit(requester, requestedName.Name, resourceMap); 

		if (resolved is null) {
			List<string> resources = resourceFilter is not null
				? [.. requester.GetManifestResourceNames().Where(resourceFilter.Contains)]
				: [.. requester.GetManifestResourceNames()];

			resolved = resources.Contains(requestedName.Name + ".dll")
				? LoadAssembly(
					resource: new(requester, requestedName.Name + ".dll"), 
					requestedName.Name,
					@unsafe: false)
				: FindDependency(requester, requestedName.Name, resources);

			if (resolved is null) return null;
		}

		Plugin.Logger.LogInfo($"Saving assembly info {resolved}");

		_requestMap.Add(resolved.Value.Name, resolved.Value);
		return resolved.Value.Assembly;
	}

	private static ResolvedDependency? FindDependencyExplicit(
		Assembly requester,
		string requestedName,
		Dictionary<string, string> resourceMap
	) {
		if (!resourceMap.TryGetValue(requestedName, out var resourceName)) {
			return null;
		}

		var resource = new AssemblyConvert.Resource(requester, resourceName);

		(var value, var ex) = resource.GetDefinition();

		if (value is null) {
			Plugin.Logger.LogWarning($"{resourceName} not found because {ex}");
			return null;
		}

		Plugin.Logger.LogInfo($"Loading assembly {requestedName} into the current context");

		using var definition = value;

		return LoadAssembly(
			resource: new(requester, resourceName),
			requestedName,
			@unsafe: true);
	}

	private static ResolvedDependency? FindDependency(
		Assembly requester,
		string requestedName,
		List<string> resources
	) => resources
		.Select(resourceName => {
		var resource = new AssemblyConvert.Resource(requester, resourceName);

		(var value, var ex) = resource.GetDefinition();

		if (value is null) {
			Plugin.Logger.LogWarning($"{resourceName} not found because {ex}");
			return null;
		}

		using var definition = value;

		if (!string.Equals(definition.Name.Name, requestedName,
			StringComparison.InvariantCultureIgnoreCase)) return null;

		return LoadAssembly(
			resource: new(requester, resourceName),
			requestedName,
			@unsafe: true);
		})
		.FirstOrDefault(resolvedDependency => resolvedDependency is not null);

	private static ResolvedDependency? LoadAssembly(
		AssemblyConvert.Resource resource,
		string resourceName,
		bool @unsafe
	) {
		(var value, var ex) = resource.GetDefinition();

		if (!@unsafe && value is null) {
			Plugin.Logger.LogWarning($"{resourceName} not found because {ex}");
			return null;
		}

		using var definition = value!;

		Plugin.Logger.LogInfo($"Loading assembly '{definition.Name}' into the current context");

		long tick = DateTime.UtcNow.Ticks;
		string name = definition.Name.Name;

		(var newAsm, _) = resource.Map(def => {
			def.Name.Name = namePrefix + def.Name.Name;
			return def;
		})!;

		var plugins = PluginManager.Instance.LoadPlugins(newAsm!, out var assembly);

		ResolvedDependency asmInfo = new(
			tick,
			name,
			assembly!,
			resource.Source,
			plugins);

		return asmInfo;
	}
}