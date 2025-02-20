
global using UnityEngine;
global using Object = UnityEngine.Object;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
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
	private AssemblyResolver? _resolver = new(); 

	private Plugin() {
		instance ??= this;
		Logger ??= base.Logger;
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
		_resolver?.Dispose();
		_resolver = null;
		OnUnload?.Invoke();
	}
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public class ResolveFromResourcesAttribute : Attribute
{
	public HashSet<string>? ResourceNames { get; } = null;

	// public bool 
}