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

		foreach (var resource in resources is null
			? requester.GetManifestResourceNames()
			: requester.GetManifestResourceNames().Where(resources.Remove)
		) {
			using var stream = requester.GetManifestResourceStream(resource);
			Assembly? embedAsm = null;
			try {
				byte[] buffer = new byte[stream.Length];
				if (stream.Read(buffer, 0, buffer.Length) is 0) {
					Plugin.Logger.LogWarning($"skipping {resource} because resource is empty");
					continue;
				}
				embedAsm = Assembly.Load(buffer);
			} catch (BadImageFormatException) {
				Plugin.Logger.LogWarning($"skipping {resource} because resource is not a valid assembly");
				continue;
			} catch (Exception ex) {
				Plugin.Logger.LogWarning($"skipping {resource} because {ex}");
				continue;
			}
			var embedAsmName = embedAsm.GetName();
			if (string.Equals(embedAsmName.Name, requestedName.Name,
				StringComparison.InvariantCultureIgnoreCase)
			) {
				Plugin.Logger.LogInfo($"Loading assembly '{embedAsmName}' into the current context");
				PluginManager.Instance.LoadPlugins(stream);
				return embedAsm;
			}
		}
		return null;
	}
}
