namespace BepInEmbed;

public record class PluginGuid
{
	internal PluginGuid(string guid, PluginManager manager) => 
		(Guid, _manager) = (guid, manager);

	private readonly PluginManager _manager;
	public string Guid { get; }

	public bool Unload() => _manager.RemovePlugin(Guid);
}
