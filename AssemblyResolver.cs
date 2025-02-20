using System.Reflection;

namespace BepInEmbed;

public sealed class AssemblyResolver : IDisposable
{
	public AssemblyResolver() {
		AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
	}

	~AssemblyResolver() => Dispose();
	public void Dispose() {
		_requestMap = null!;
		AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
	}

	Dictionary<Assembly, List<AssemblyName>> _requestMap = [];

    private static Assembly? ResolveAssembly(object sender, ResolveEventArgs args)
    {
		if (args?.Name is null) return null;
		if (args.RequestingAssembly is null) {
			Plugin.Logger.LogWarning($"a null {nameof(args.RequestingAssembly)} is trying to request {args.Name}");
			return null;
		}
		var requester = args.RequestingAssembly;
		if (!(requester.GetCustomAttribute<ResolveFromResourcesAttribute>() is { 
			ResourceNames: var resources
		})) {
			return null;
		}
		var requestedName = new AssemblyName(args.Name);

		foreach (var resourceName in resources is null
			? requester.GetManifestResourceNames()
			: requester.GetManifestResourceNames().Where(resources.Remove)
		) {
			var resourceAsm = new LocatedAssembly.ResourceAssembly(requester, resourceName);
			if (!resourceAsm.TryGetAssembly(
				out var assembly,
				out Exception? exception
			)) {
				Plugin.Logger.LogWarning($"skipping {resourceName} because {exception}");
				continue;
			}
			var asmName = assembly.GetName();
			if (string.Equals(asmName.Name, requestedName.Name,
				StringComparison.InvariantCultureIgnoreCase)
			) {
				Plugin.Logger.LogInfo($"Loading assembly '{asmName}' into the current context");
				PluginManager.Instance.LoadPlugins(resourceAsm);
				return assembly;
			}
		}
		return null;
	}
}
