using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mono.Cecil;

namespace BepInEmbed;

internal sealed class InterResourceRequestHandler
{
	readonly Dictionary<string, Assembly> _requesters = [];

	public InterResourceRequestHandler(DependencyResolver resolver) {
		resolver.AfterResolve += AfterResolve;
		resolver.OnResolveNotFound += OnResolveFallback;
	}

	private void AfterResolve(Assembly source, Assembly result, List<PluginContext> plugins) {
		_requesters.Add(source.GetName().Name, result);
	}

	private Assembly? OnResolveFallback(Assembly source, AssemblyName request)
	{
		if (source
			.GetCustomAttribute<RuntimeDecorations.ResolvedAssemblyAttribute>()
			is not { Requester: var parentName }
		) return null;

		if (!_requesters.TryGetValue(parentName, out var parent)) return null;

		Plugin.Logger.LogInfo($"Request from {source.GetName().Name} is resolved from {parentName}");

		switch (DependencyResolver.TryResolveResource(parent, request)) {
		case ((var assembly, _), _):
			Plugin.Logger.LogInfo(
				$"Request from {source.GetName().Name} is found in {parentName}, " +
				$"loading {assembly} into the current context");
			return assembly;
		case (_, Exception ex):
			Plugin.Logger.LogInfo($"Assembly {request} failed to load because {ex}");
			return null;
		default:
			return null;
		}
	}
}

internal static class AssemblyAttributes
{
	static AssemblyAttributes() {
		string asmName = $"{nameof(BepInEmbed)}.{nameof(BepInEmbed)}.{nameof(RuntimeDecorations)}.dll";
		string typeName = typeof(RuntimeDecorations.ResolvedAssemblyAttribute).FullName;
		var resource = new AssemblyConvert.Resource(
			Assembly.GetExecutingAssembly(),
			asmName);
		(var value, var ex) = resource.GetDefinition();
		if (value is null) {
			Plugin.Logger.LogWarning($"{asmName} not found because {ex}");
			throw ex!;
		}
		using var assembly = value;
		resolvedAttributeCtor = assembly.MainModule
			.Types
			.First(x => x.FullName == typeName)
			.Methods
			.First(m => m.Name == ".ctor");
	}

	public static readonly MethodReference resolvedAttributeCtor;
}

internal sealed class DependencyResolver : IDisposable
{
	const string nameDecoration = @$"<{nameof(BepInEmbed)}_{{0}}>";

	public DependencyResolver() {
		AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
	}

	~DependencyResolver() => Dispose();
	public void Dispose() {
		_disposed = true;
		AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
	}

	bool _disposed;

	public event ResultHandler? AfterResolve = null;
	public event RequestHandler? OnResolveNotFound = null;

	public delegate Assembly? RequestHandler(Assembly source, AssemblyName request);
	public delegate void ResultHandler(Assembly source, Assembly result, List<PluginContext> plugins);

	private Assembly? ResolveAssembly(object sender, ResolveEventArgs args) {
		if (_disposed) throw new InvalidOperationException(
			$"trying to call {nameof(ResolveAssembly)} on a disposed {nameof(DependencyResolver)}");

		if (args?.Name is null) return null;

		var request = new AssemblyName(args.Name);

		var source = args.RequestingAssembly;
		if (source is null) {
			Plugin.Logger.LogWarning($"a null {nameof(args.RequestingAssembly)} is trying to request {request}");
			return null;
		}

		switch (TryResolveResource(source, request)) {
		case ((var assembly, var plugins), _):
			Plugin.Logger.LogInfo($"Loading assembly '{assembly}' into the current context");
			AfterResolve?.Invoke(source, assembly, plugins!);
			return assembly;
		case (_, Exception ex):
			Plugin.Logger.LogInfo($"Assembly {request} failed to load because {ex}");
			return null;
		default:
			Plugin.Logger.LogInfo($"{request} not found, fallback to {nameof(OnResolveNotFound)}");
			if (OnResolveNotFound?.GetInvocationList()
				.Cast<RequestHandler>() is not (var handlers and not null)) return null;
			foreach (var handler in handlers) {
				if (handler.Invoke(source, request) is not Assembly handlerResult) continue;
				return handlerResult;
			}
			return null;
		}
	}

	public static Result<(Assembly assembly, List<PluginContext> plugins), Exception>? TryResolveResource(
		Assembly source, AssemblyName request
	) {
		// assembly = null;
		// plugins = null;
		var sourceResource = source.GetManifestResourceNames();
		if (sourceResource is []) {
			return null;
		}

		if (source.GetCustomAttribute<UseEmbedAttribute>() is not UseEmbedAttribute attr) {
			return null;
		}

		var sourceName = source.GetName();

		var resourceMap = attr.GetResourceMap();
		var resourceFilter = attr.GetResourceFilter();

		if (resourceMap is not null &&
			resourceMap.TryGetValue(request.Name, out var resourceName) &&
			source.GetManifestResourceNames().Contains(resourceName)
		) {
			return LoadAssembly(new(source, resourceName), sourceName);
		}

		var resourceNames = resourceFilter is null
			? sourceResource
			: sourceResource.Where(resourceFilter.Contains);

		Func<Tx, Ty> WrapDispose<Tx, Ty>(Func<Tx, Ty> f) where Tx : IDisposable => 
			x => {
				try {
					return f(x);
				}
				finally {
					x.Dispose();
				}
		};

		var resource = resourceNames
			.Select(name => new AssemblyConvert.Resource(source, name))
			.Where(resource => resource.GetDefinition()
				.Map(WrapDispose ((AssemblyDefinition definition) => 
					string.Equals(definition.Name.Name, request.Name,
						StringComparison.InvariantCultureIgnoreCase)))
				.GetValue(or: false))
			.FirstOrDefault();
		
		// if ()

		return LoadAssembly(resource, sourceName);
	}

	private static Result<(Assembly, List<PluginContext>), Exception> LoadAssembly(
		AssemblyConvert.Resource resource,
		AssemblyName sourceName
	) => resource
		.Modify(resource => DecorateAssembly(resource, sourceName.Name))
		.AndThen(convert => convert.GetBoth())
		.Map(both => {
			var assembly = both.assembly;
			using var definition = both.definition;
			return (both.assembly,
				plugins: PluginManager.Instance.LoadPlugins(both.assembly, both.definition));
		});

	static AssemblyDefinition DecorateAssembly(AssemblyDefinition def, string requester) {
		def.Name.Name = string.Format(nameDecoration, DateTime.UtcNow.Ticks) + def.Name.Name;
		var ctor = def.MainModule.ImportReference(AssemblyAttributes.resolvedAttributeCtor);
		var attr = new CustomAttribute(ctor);
		var arg = new CustomAttributeArgument(
			def.MainModule.ImportReference(typeof(string)),
			requester);
		attr.ConstructorArguments.Add(arg);
		def.CustomAttributes.Add(attr);
		return def;
	}
}