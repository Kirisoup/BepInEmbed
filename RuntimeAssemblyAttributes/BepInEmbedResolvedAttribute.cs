namespace BepInEmbed.RuntimeAssemblyAttributes;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class BepInEmbedResolvedAttribute(string requester) : Attribute {
	public string Requester { get; } = requester;
}
