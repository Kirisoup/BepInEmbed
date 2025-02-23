using System.Collections;
using System.Reflection;
using Mono.Cecil;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
// using SoupCommonLib;

namespace BepInEmbed;

public sealed class PluginManager : MonoBehaviour
{
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

	// internal Result<List<PluginContext>, Exception> LoadPlugins(
	// 	IAssemblyConvert convert
	// ) {
	// 	// assembly 
	// }

	public Result<List<PluginContext>, Exception> LoadPlugins(IAssemblyConvert convert) => 
		convert.GetBoth()
		.Map(both => {
			try {
				return LoadPlugins(both.assembly, both.definition);
			} finally {
				both.definition.Dispose();
			}
		});

	internal List<PluginContext> LoadPlugins(Assembly assembly, AssemblyDefinition definition) {
		Plugin.Logger.LogInfo($"Looking for plugins to load from assembly {definition.Name}");

		return GetTypes(assembly)
			.Select(type => LoadPlugin(type, definition))
			.Where(guid => guid is not null)
			.Select(guid => {
				_guids.Add(guid!);
				return new PluginContext(guid!, assembly, this);
			})
			.ToList();
	
		static IEnumerable<Type> GetTypes(Assembly asm) {
			try {
				return asm.GetTypes();
			} catch (ReflectionTypeLoadException typeEx) {
				Plugin.Logger.LogWarning(typeEx);
				return typeEx.Types.Where(x => x is not null);
			}
		}
	}

	private string? LoadPlugin(Type type, AssemblyDefinition asmDef)
	{
		try {
			if (!typeof(BaseUnityPlugin).IsAssignableFrom(type)) return null;

			var metadata = MetadataHelper.GetMetadata(type) 
				?? throw new InvalidOperationException($"cannot get metadata from type {type}");

			if (Chainloader.PluginInfos.TryGetValue(metadata.GUID, out var existingPluginInfo)) {
				Plugin.Logger.LogError(
					$"A plugin with GUID {metadata.GUID} is already loaded! " + 
					$"({existingPluginInfo?.Metadata?.GUID ?? "null"} {existingPluginInfo?.Metadata?.Version.ToString() ?? "null"})");
				return null;
			}

			Plugin.Logger.LogInfo($"Loading plugin {metadata.GUID}");

			var pluginInfo = Chainloader.ToPluginInfo(asmDef.MainModule.GetType(type.FullName));

			// Plugin.Logger.LogInfo(pluginInfo?.Metadata);
			
			StartCoroutine(InstantiatePlugin(type, metadata, pluginInfo));
			return metadata.GUID;
		} catch (Exception ex) {
			Plugin.Logger.LogError($"Failed to load plugin of type {type.FullName} because of exception: {ex}");
			return null;
		}

		IEnumerator InstantiatePlugin(
			Type type,
			BepInPlugin metadata,
			BepInEx.PluginInfo pluginInfo
		) {
			yield return null;
			Plugin.Logger.LogInfo($"Instantiating {metadata.GUID}");
			try {
				Chainloader.PluginInfos[metadata.GUID] = pluginInfo;
				var instance = gameObject.AddComponent(type);
				var pluginInfoTraverse = Traverse.Create(pluginInfo);
				pluginInfoTraverse
					.Property<BaseUnityPlugin>(nameof(pluginInfo.Instance))
					.Value = (BaseUnityPlugin)instance;
			} catch (Exception ex) {
				Plugin.Logger.LogError($"Failed to instantiating plugin with GUID {metadata.GUID} because of exception: {ex}");
				Chainloader.PluginInfos.Remove(metadata.GUID);
			}
		}
	}
}