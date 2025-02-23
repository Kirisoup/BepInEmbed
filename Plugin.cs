global using KiriLib.LinqBackport;
global using KiriLib.ErrorHandling;
global using UnityEngine;
global using Object = UnityEngine.Object;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;

[assembly: BepInEmbed.UseEmbed()]

namespace BepInEmbed;

[BepInPlugin(GUID, NAME, PluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string NAME = $"{nameof(BepInEmbed)}";
    public const string GUID = $"hff.kirisoup.{NAME}";

    public static Plugin instance = null!;

    internal static new ManualLogSource Logger = null!;

	internal event Action? OnUnload;

	private static readonly string[] _dependencies = [
		$"{nameof(KiriLib)}.{nameof(KiriLib.LinqBackport)}",
		$"{nameof(KiriLib)}.{nameof(KiriLib.ErrorHandling)}",
		$"{nameof(BepInEmbed)}.{nameof(BepInEmbed.RuntimeDecorations)}",
	];

	private DependencyResolver _resolver = null!;

	private Plugin() {
		instance ??= this;
		Logger ??= base.Logger;
	}

	private void Awake() {
		LoadDependencies();
		_resolver = new();
		_ = new InterResourceRequestHandler(_resolver);
	}

	private void LoadDependencies() {
		var source = Assembly.GetExecutingAssembly();
		foreach (var name in _dependencies) {
			try {
				using var stream = source.GetManifestResourceStream($"{nameof(BepInEmbed)}.{name}.dll");
				using var memoryStream = new MemoryStream();
				stream.CopyTo(memoryStream);
				Logger.LogInfo(AppDomain.CurrentDomain.Load(memoryStream.ToArray()));
			} catch (Exception ex) {
				Logger.LogError($"failed to load {name} because {ex}");
			}
		}
	}

	private List<PluginContext>? _testPlugin;

	private void Update() {
		if (Input.GetKeyDown(KeyCode.RightShift)) {
			_testPlugin?.ForEach(plugin => plugin.Unload());
			if (PluginManager.Instance.LoadPlugins(new AssemblyConvert.File(
				@"C:\Program Files (x86)\Steam\steamapps\common\Human Fall Flat\BepInEx\scripts\HFFCatCore.CPReversed.dll"))
				.IsOk(out var plugins)) _testPlugin = plugins;
		}
	}

	private void OnDestroy() {
		OnUnload?.Invoke();
	}
}