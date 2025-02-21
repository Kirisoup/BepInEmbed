namespace BepInEmbed;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class UseEmbedAttribute : Attribute
{
	public HashSet<string>? ResourceFilter { get; init; }
	public Dictionary<string, string>? ResourceMap { get; init; }
}