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
			using var definition = resourceAsm.GetDefinition();
			if (definition is null) {
				Plugin.Logger.LogWarning($"skipping {resourceName} because definition is null");
				continue;
			}

			if (!string.Equals(definition.Name.Name, requestedName.Name,
				StringComparison.InvariantCultureIgnoreCase)) return null;

			Plugin.Logger.LogInfo($"Loading assembly '{definition.Name}' into the current context 11");

			long tick = DateTime.UtcNow.Ticks;
			string name = definition.Name.Name;

			var newAsm = resourceAsm.Map(def => {
				def.Name.Name = namePrefix + def.Name.Name;
				return def;
			})!;

			var plugins = PluginManager.Instance.LoadPlugins(newAsm, out var assembly);

			ResolvedAssemblyInfo value = new(
				tick,
				assembly!,
				requester,
				plugins);

			Plugin.Logger.LogInfo($"Saving assembly info {value}");

			_requestMap.Add(name, value);
			return assembly;
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
