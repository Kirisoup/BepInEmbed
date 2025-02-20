using System.Collections;
using System.Reflection;
using Mono.Cecil;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace BepInEmbed;

public sealed class PluginManager : MonoBehaviour
{
	internal static PluginManager Instantiate() {
		GameObject obj = new ($"{nameof(PluginManager)}_{DateTime.UtcNow.Ticks}");
		DontDestroyOnLoad(obj);
		return obj.AddComponent<PluginManager>();
	}

	private PluginManager() {
		if (_instance is not null) {
			Destroy(this);
			throw new InvalidOperationException(
				$"cannot create {nameof(PluginManager)} when an instance already exists"
			);
		}
		Plugin.instance.OnUnload += Destroy;
	}

	private static PluginManager? _instance;
	internal static PluginManager Instance {
		get => _instance ??= Instantiate();
	}

	private readonly HashSet<string> _guids = [];

	internal bool RemovePlugin(string guid) {
		bool removed = 
			_guids.Remove(guid) && 
			Chainloader.PluginInfos.Remove(guid);
		if (removed) Plugin.Logger.LogInfo($"Unloading plugin {guid}");
		return true;
	}

	internal void Destroy() => Destroy(gameObject);
	private void OnDestroy() {
		Plugin.instance.OnUnload -= Destroy;
		foreach (var guid in _guids) RemovePlugin(guid);
	}

	public void LoadPlugins(Assembly source) {
		foreach (var name in source.GetManifestResourceNames()) {
			using Stream resource = source.GetManifestResourceStream(name);
			if (resource is null) continue;
			LoadPlugins(resource);
		}
	}

	public List<PluginGuid> LoadPlugins(string path) {
		try {
			using var asmDef = AssemblyDefinition.ReadAssembly(path);
			return LoadPlugins(asmDef);
		} catch (Exception ex) {
			Plugin.Logger.LogWarning($"error loading resource {name}: {ex.Message}");
			return [];
		}
	}

	public List<PluginGuid> LoadPlugins(Stream resource) {
		try {
			using var asmDef = AssemblyDefinition.ReadAssembly(resource)
				?? throw new InvalidOperationException(
					$"Cannot load assemblyDefinition from {nameof(resource)}");
			return LoadPlugins(asmDef);
		} catch (Exception ex) {
			Plugin.Logger.LogWarning($"error loading resource {name}: {ex.Message}");
			return [];
		}
	}

	public List<PluginGuid> LoadPlugins(AssemblyDefinition asmDef) {
		Assembly? asm = null;
		Plugin.Logger.LogInfo($"Looking for plugins to load from assembly {asmDef.Name}");
		asmDef.Name.Name += $"_{DateTime.UtcNow.Ticks}";
		using (var ms = new MemoryStream()) {
			asmDef.Write(ms);
			asm = Assembly.Load(ms.ToArray());
		}

		if (asm is null) throw new InvalidOperationException(
			$"cannot load assembly from {asmDef}");

		Plugin.Logger.LogInfo($"assembly key: {asm.GetName().GetPublicKeyToken().Aggregate("", (acc, cur) => acc + cur.ToString())}");

		return GetTypesSafe(asm)
			.Select(type => LoadPlugin(type, asmDef))
			.Where(guid => guid is not null)
			.Select(guid => {
				_guids.Add(guid!);
				return new PluginGuid(guid!, this);
			})
			.ToList();
	}
	
	private IEnumerable<Type> GetTypesSafe(Assembly ass) {
		try {
			return ass.GetTypes();
		} catch (ReflectionTypeLoadException ex) {
			Plugin.Logger.LogError($"""
				LoaderExceptions: {ex.LoaderExceptions.Select(ex => "\r\n" + ex)}
				StackTrace: 
				{ex.StackTrace}
				""");
			return ex.Types.Where(x => x is not null);
		}
	}

	private string? LoadPlugin(Type type, AssemblyDefinition asmDef) 
	{
		try {
			if (!typeof(BaseUnityPlugin).IsAssignableFrom(type)) return null;

			var metadata = MetadataHelper.GetMetadata(type) 
				?? throw new InvalidOperationException($"cannot get metadata from type {type}");

			if (Chainloader.PluginInfos.TryGetValue(metadata.GUID, out var existingPluginInfo))
				throw new InvalidOperationException(
					$"A plugin with GUID {metadata.GUID} is already loaded! " + 
					$"({existingPluginInfo?.Metadata?.GUID} v{existingPluginInfo?.Metadata?.Version})");

			Plugin.Logger.LogInfo($"Loading {metadata.GUID}");

			var typeDef = asmDef.MainModule.Types.First(x => x.FullName == type.FullName);
			var pluginInfo = Chainloader.ToPluginInfo(typeDef);
			
			StartCoroutine(CreatePlugin(type, metadata, pluginInfo));

			return metadata.GUID;
		} catch (Exception ex) {
			Plugin.Logger.LogError($"Failed to load plugin of type {type.Name} because of exception: {ex}");
			return null;
		}
	}
	
	private IEnumerator CreatePlugin(
		Type type,
		BepInPlugin metadata,
		BepInEx.PluginInfo pluginInfo
	) {
		yield return null;
		Plugin.Logger.LogInfo($"Creating {metadata.GUID}");
		try {
		Chainloader.PluginInfos[metadata.GUID] = pluginInfo;
		var instance = gameObject.AddComponent(type);
		var pluginInfoTraverse = Traverse.Create(pluginInfo);
		pluginInfoTraverse
			.Property<BaseUnityPlugin>(nameof(pluginInfo.Instance))
			.Value = (BaseUnityPlugin)instance;
		} catch (Exception ex) {
			Plugin.Logger.LogError($"Failed to load plugin with GUID {metadata.GUID} because of exception: {ex}");
			Chainloader.PluginInfos.Remove(metadata.GUID);
		}
	}
}