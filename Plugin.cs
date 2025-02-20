
global using UnityEngine;
global using Object = UnityEngine.Object;
using System.ComponentModel.Design.Serialization;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;

namespace BepInEmbed;

[BepInPlugin(GUID, NAME, PluginInfo.PLUGIN_VERSION)]
[BepInProcess("Human.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string NAME = $"{nameof(BepInEmbed)}";
    public const string GUID = $"hff.kirisoup.{NAME}";

    public static Plugin instance = null!;

    internal static new ManualLogSource Logger = null!;

	internal event Action? OnUnload;

	private Plugin() {
		instance ??= this;
		Logger ??= base.Logger;
		AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
	}

	private void Awake() {
	}

	private List<PluginGuid>? _testPlugin;

	private void Update() {
		if (Input.GetKeyDown(KeyCode.RightShift)) {
			_testPlugin?.ForEach(plugin => plugin.Unload());
			_testPlugin = PluginManager.Instance.LoadPlugins(path: 
				@"C:\Program Files (x86)\Steam\steamapps\common\Human Fall Flat\BepInEx\scripts\HFFCatCore.CPReversed.dll");
		}
	}

	private void OnDestroy() {
		AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
		OnUnload?.Invoke();
	}

    public static Assembly? ResolveAssembly(object sender, ResolveEventArgs e)
    {
		if (e?.Name is null) return null;
        var nameStr = e.Name;
        var name = new AssemblyName(nameStr);

		foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
			var asmName = asm.GetName();
			if (string.Equals(asmName.Name, name.Name,
				StringComparison.InvariantCultureIgnoreCase)
			) {
				Logger.LogInfo($"Assembly '{asm.FullName}' already loaded, returning existing assembly");
				return asm;
			}
			if (!(asm.GetCustomAttribute<ResolveFromResourcesAttribute>() is { 
				ResourceNames: var resources
			} )) {
				continue;
			}
			foreach (var resource in asm.GetManifestResourceNames()
				.Where(r => resources?.Remove(r) ?? true)
			) {
				using var stream = asm.GetManifestResourceStream(resource);
				Assembly? embedAsm = null;
				try {
					byte[] buffer = new byte[stream.Length];
					if (stream.Read(buffer, 0, buffer.Length) is 0) {
						Logger.LogWarning($"skipping {resource} because resource is empty");
						continue;
					}
					embedAsm = Assembly.Load(buffer);
				} catch (BadImageFormatException) {
					Logger.LogWarning($"skipping {resource} because resource is not a valid assembly");
					continue;
				} catch (Exception ex) {
					Logger.LogWarning($"skipping {resource} because {ex}");
					continue;
				}
				var embedAsmName = embedAsm.GetName();
				if (string.Equals(embedAsmName.Name, name.Name,
					StringComparison.InvariantCultureIgnoreCase)
				) {
					Logger.LogInfo($"Loading assembly '{embedAsmName}' into the current context");
					PluginManager.Instance.LoadPlugins(stream);
					return embedAsm;
				}
			}
		}
		return null;
	}
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public class ResolveFromResourcesAttribute : Attribute
{
	public HashSet<string>? ResourceNames { get; } = null;

	// public bool Always
}