namespace BepInEmbed.RuntimeDecorations;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ResolvedAssemblyAttribute(string requester) : Attribute {
	public string Requester { get; } = requester;
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ScriptAssemblyAttribute(string guid) : Attribute {
	public string Guid { get; } = guid;
}
