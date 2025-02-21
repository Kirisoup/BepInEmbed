using System.Reflection;
using Mono.Cecil;

namespace BepInEmbed;

internal sealed class DependencyResolver : IDisposable
{
	const string namePrefix = @$"__{nameof(BepInEmbed)}__";

	static DependencyResolver() {
		string asmName = $"{nameof(BepInEmbed)}.{nameof(BepInEmbed)}.{nameof(RuntimeAssemblyAttributes)}.dll";
		string typeName = typeof(RuntimeAssemblyAttributes.BepInEmbedResolvedAttribute).FullName;
		var resource = new AssemblyConvert.Resource(
			Assembly.GetExecutingAssembly(),
			asmName);
		(var value, var ex) = resource.GetDefinition();
		if (value is null) {
			Plugin.Logger.LogWarning($"{asmName} not found because {ex}");
			throw ex!;
		}
		using var assembly = value;
		_resolvedAttributeCtor = assembly.MainModule
			.Types
			.First(x => x.FullName == typeName)
			.Methods
			.First(m => m.Name == ".ctor");
	}

	private static readonly MethodReference _resolvedAttributeCtor;

	public DependencyResolver() {
		AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
		Plugin.instance.OnUnload += Dispose;
	}

	~DependencyResolver() => Dispose();
	public void Dispose() {
		_disposed = true;
		AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
	}

	bool _disposed;
	readonly Dictionary<string, Assembly> _requesters = [];

	private Assembly? ResolveAssembly(object sender, ResolveEventArgs args)
	{
		if (_disposed) throw new InvalidOperationException(
			$"trying to call {nameof(ResolveAssembly)} on a disposed {nameof(DependencyResolver)}");

		if (args?.Name is null) return null;

		var request = new AssemblyName(args.Name);

		var source = args.RequestingAssembly;
		if (source is null) {
			Plugin.Logger.LogWarning($"a null {nameof(args.RequestingAssembly)} is trying to request {request}");
			return null;
		}

		if (ResolveResource(source, request) is Assembly result) return result;

		if (!(source
			.GetCustomAttribute<RuntimeAssemblyAttributes.BepInEmbedResolvedAttribute>()
			is { Requester: var parentName } &&
			_requesters.TryGetValue(parentName, out var parent))
		) {
			return null;
		}

		Plugin.Logger.LogInfo($"Request from {source.GetName().Name} is resolved from {parentName}");
		return ResolveResource(parent, request);
	}

	private Assembly? ResolveResource(Assembly source, AssemblyName request) 
	{
		if (source.GetManifestResourceNames() is []) return null;

		if (source.GetCustomAttribute<UseEmbedAttribute>() is not UseEmbedAttribute attr) {
			return null;
		}
		var sourceName = source.GetName();

		var resourceMap = attr.GetResourceMap();
		var resourceFilter = attr.GetResourceFilter();

		if (resourceMap?.TryGetValue(request.Name, out var resourceName) is true) {
			var resolved = LoadAssembly(new(source, resourceName));
			if (resolved is not null) return resolved;
		} 

		List<string> resources = resourceFilter is not null
			? [.. source.GetManifestResourceNames().Where(resourceFilter.Contains)]
			: [.. source.GetManifestResourceNames()];

		if (resources.Contains(request.Name + ".dll")) {
			return LoadAssembly(new(source, request.Name + ".dll"));
		}

		foreach (var name in resources) {
			var resource = new AssemblyConvert.Resource(source, name);
			(var value, var ex) = resource.GetDefinition();
			if (value is null) {
				Plugin.Logger.LogWarning($"{name} not found because {ex}");
				continue;
			}
			using var definition = value;

			if (!string.Equals(definition.Name.Name, request.Name,
				StringComparison.InvariantCultureIgnoreCase)) continue;

			var resolved = LoadAssembly(resource);			
			if (resolved is not null) return resolved;
		}

		return null;

		Assembly? LoadAssembly(AssemblyConvert.Resource resource) {
			(var modified, var ex) = resource.Map(def => {
				def.Name.Name = namePrefix + def.Name.Name;
				var ctor = def.MainModule.ImportReference(_resolvedAttributeCtor);
				var attr = new CustomAttribute(ctor);
				var arg = new CustomAttributeArgument(
					def.MainModule.ImportReference(typeof(string)), 
					sourceName.Name);
				attr.ConstructorArguments.Add(arg);
				def.CustomAttributes.Add(attr);
				return def;
			})!;
			if (modified is null) {
				Plugin.Logger.LogWarning(
					$"error while modifying assembly {ex}");
				return null;
			}

			PluginManager.Instance.LoadPlugins(modified, out var assembly);
			if (assembly is not null) {
				Plugin.Logger.LogInfo($"Loading assembly '{assembly.GetName()}' into the current context");
				_requesters.Add(sourceName.Name, source);
			}
			return assembly;
		}
	}

}