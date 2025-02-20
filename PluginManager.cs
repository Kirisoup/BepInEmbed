using System.Collections;
using System.Reflection;
using Mono.Cecil;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace BepInEmbed;

public sealed class PluginManager : MonoBehaviour
{
	#region instantiation
	private PluginManager() {
		if (_instance is not null) {
			Destroy(this);
			throw new InvalidOperationException(
				$"cannot create {nameof(PluginManager)} when an instance already exists");
		}
		Plugin.instance.OnUnload += Destroy;
	}

	private static PluginManager? _instance;
	internal static PluginManager Instance => _instance ??= Instantiate();

	private static PluginManager Instantiate() {
		GameObject obj = new ($"{nameof(PluginManager)}_{DateTime.UtcNow.Ticks}");
		DontDestroyOnLoad(obj);
		return obj.AddComponent<PluginManager>();
	}
	#endregion

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

	public List<PluginGuid> LoadPlugins(ILocatedAssemblySafe assembly) {
		if (!assembly.TryGetAssemblyDef(
			out var definition,
			out Exception? ex
		)) {
			Plugin.Logger.LogWarning(
				$"error while converting assembly before loading plugins: {ex}");
			return [];
		}

		Plugin.Logger.LogInfo($"Looking for plugins to load from assembly {definition.Name}");

		definition.Name.Name += $"_{DateTime.UtcNow.Ticks}";
		return GetTypes(definition)
			.Select(type => LoadPlugin(type, definition))
			.Where(guid => guid is not null)
			.Select(guid => {
				_guids.Add(guid!);
				return new PluginGuid(guid!, this);
			})
			.ToList();
	}

	private static IEnumerable<Type> GetTypes(AssemblyDefinition definition) {
		using var ms = new MemoryStream();
		definition.Write(ms);
		Assembly asm = Assembly.Load(ms.ToArray()) ?? 
			throw new InvalidOperationException($"cannot load assembly from {definition}");
		try {
			return asm.GetTypes();
		} catch (ReflectionTypeLoadException typeEx) {
			Plugin.Logger.LogError(typeEx);
			return typeEx.Types.Where(x => x is not null);
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
			
			StartCoroutine(InstantiatePlugin(type, metadata, pluginInfo));

			return metadata.GUID;
		} catch (Exception ex) {
			Plugin.Logger.LogError($"Failed to load plugin of type {type.Name} because of exception: {ex}");
			return null;
		}
	}
	
	private IEnumerator InstantiatePlugin(
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