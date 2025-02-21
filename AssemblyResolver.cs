using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mono.Cecil;
using SoupCommonLib;

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

	private record struct ResolvedAssemblyInfo(
		long Tick,
		Assembly Assembly,
		Assembly RequestingAssembly,
		List<PluginGuid> Plugins);

	Dictionary<string, ResolvedAssemblyInfo> _requestMap = [];
	// Dictionary<Assembly, >
	const string namePrefix = @$"__{nameof(BepInEmbed)}__";

	const string soupLibName = @"SoupCommonLib";
	const string soupLibResource = @$"BepInEmbed.{soupLibName}.dll";
	bool _soupLibResolved;

    private Assembly? ResolveAssembly(object sender, ResolveEventArgs args)
    {
		if (args?.Name is null) return null;

		var requestedName = new AssemblyName(args.Name);

		if (!_soupLibResolved && string.Equals(requestedName.Name, soupLibName,
			StringComparison.InvariantCultureIgnoreCase)) 
		{
			_soupLibResolved = TryLoadSoupLib(out Assembly? soup);
			return soup;
		}

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
			IncludeResources: var resources
		})) {
			return null;
		}

		foreach (var resourceName in resources is null
			? requester.GetManifestResourceNames()
			: requester.GetManifestResourceNames().Where(resources.Contains)
		) {
			var resourceAsm = new AssemblyConvert.ResourceAssembly(requester, resourceName);
			if (!resourceAsm.TryGetCecilAssembly(
				out var assembly,
				out Exception? exception
			)) {
				Plugin.Logger.LogWarning($"skipping {resourceName} because {exception}");
				return null;
			}
			try {
				if (!string.Equals(assembly.Name.Name, requestedName.Name,
					StringComparison.InvariantCultureIgnoreCase)) return null;

				Plugin.Logger.LogInfo($"Loading assembly '{assembly.Name}' into the current context");

				long tick = DateTime.UtcNow.Ticks;
				string name = assembly.Name.Name;
				var plugins = PluginManager.Instance.LoadPlugins(assembly);

				// assembly.Name.Name = namePrefix + name;

				Assembly requested;
				using (var ms = new MemoryStream()) {
					assembly.Write(ms);
					requested = Assembly.Load(ms.ToArray());
				}

				ResolvedAssemblyInfo value = new(
					tick,
					requested!,
					requester,
					plugins);

				_requestMap.Add(name, value);

				return requested;
			} finally {
				assembly.Dispose();
			}
		}
		return null;
	}

	private static bool TryLoadSoupLib(
		[NotNullWhen(true)] out Assembly? assembly
	) {
		try {
			using var stream = Assembly.GetExecutingAssembly()
				.GetManifestResourceStream(soupLibResource);
			using var memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			assembly = Assembly.Load(memoryStream.ToArray());
			return true;
		} catch (Exception ex) {
			Plugin.Logger.LogError($"failed loading souplib because {ex}");
			assembly = null;
			return false;
		}
	}
}
