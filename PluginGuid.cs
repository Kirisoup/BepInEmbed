using System.Reflection;

namespace BepInEmbed;

public record class PluginContext
{
	internal PluginContext(
		string guid,
		Assembly assembly,
		PluginManager manager
	) => 
		(Guid, Assembly, _manager) = (guid, assembly, manager);

	private readonly PluginManager _manager;
	public string Guid { get; }
	public Assembly Assembly { get; }

	public bool Unload() => _manager.RemovePlugin(Guid);
}
