// using SoupCommonLib;

namespace BepInEmbed;

public readonly record struct ScriptInfo(
	string Guid,
	IAssemblyConvert Source,
	List<Guid> PluginDependency
);

public sealed class ScriptLoader
{
	const string nameDecoration = @$"<{nameof(BepInEmbed)}.{nameof(ScriptLoader)}_{{0}}>";

	public void LoadScripts(IAssemblyConvert convert) {}

	// readonly Dictionary<Assembly, List<PluginInfo>> _scriptMap = []; 
	
	// internal ScriptLoader(DependencyResolver resolver) {
	// 	resolver.AfterResolve += AfterResolve;
	// }

	// public void LoadScripts(IAssemblyConvert convert)
	// {
	// 	(var modified, var ex) = convert.Map(def => {
	// 		def.Name.Name = string.Format(nameDecoration, DateTime.UtcNow.Ticks) + def.Name.Name;
	// 		return def;
	// 	});
	// 	if (modified is null) {
	// 		Plugin.Logger.LogWarning(
	// 			$"error while modifying assembly before loading script {ex}");
	// 		return;
	// 	}
	// 	(var both, var ex1) = convert.GetBoth();
	// 	if (both is null) {
	// 		Plugin.Logger.LogWarning(
	// 			$"error while converting assembly before loading plugins {ex1}");
	// 		return;
	// 	}
	// 	var assembly = both.Value.assembly;
	// 	if (_scriptMap.ContainsKey(assembly)) {
	// 		Plugin.Logger.LogWarning($"scripts from {assembly} already loaded, skipping");
	// 		return;
	// 	}
	// 	using var definition = both.Value.definition;
	// 	if (_scriptMap.ContainsKey(assembly)) return;
	// 	var scripts = PluginManager.Instance.LoadPlugins(assembly, definition);
	// 	if (assembly is null) return;
	// 	_scriptMap.Add(assembly, scripts);
	// }

	// // public bool ReloadScripts(IAssemblyConvert convert) {

	// // }

	// private void AfterResolve(Assembly source, Assembly result, List<PluginInfo> plugins) {
	// 	if (!_scriptMap.TryGetValue(source, out var scripts)) return;
	// 	plugins.ForEach(scripts.Add);
	// }
}
