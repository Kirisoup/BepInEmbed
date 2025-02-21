namespace BepInEmbed;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class UseEmbedAttribute : Attribute
{
	public HashSet<string>? IncludeResources { get; }

	public UseEmbedAttribute() => IncludeResources = null;

	public UseEmbedAttribute(params HashSet<string> includeResources) => 
		IncludeResources = includeResources.Count is 0 ? null : includeResources;
}