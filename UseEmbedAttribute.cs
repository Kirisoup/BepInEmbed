namespace BepInEmbed;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class UseEmbedAttribute : Attribute
{
	public string[]? ResourceFilter = null;
	public string[]? ResourceMap = null;

	internal HashSet<string>? GetResourceFilter() => ResourceFilter is null
		? null
		: [.. ResourceFilter];
	
	internal Dictionary<string, string>? GetResourceMap() => ResourceMap?
		.Aggregate((Dictionary<string, string>?)[], (map, line) => {
			if (map is null) return null;
			var sides = line.Split(':');
			if (sides.Length != 2) return null;
			map.Add(sides[0], sides[1]);
			return map;
		});

	// internal HashSet<string>? ResourceFilter_internal => [.. ResourceFilter];
	
	// internal Dictionary<string, string>? ResourceMap_internal { get; private init; }
	// public string[] ResourceMap {
	// 	init => ResourceMap_internal = value
	// 		.Aggregate((Dictionary<string, string>?)[], (map, line) => {
	// 			if (map is null) return null;
	// 			var sides = line.Split(':');
	// 			if (sides.Length != 2) return null;
	// 			map.Add(sides[0], sides[1]);
	// 			return map;
	// 		});
	// }
}

public sealed class UseEmbedExplicitAttribute : Attribute
{
	public Dictionary<string, string>? ResourceMap { get; init; }
}