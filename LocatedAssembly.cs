using System.Reflection;
using Mono.Cecil;
using val = System.Diagnostics.CodeAnalysis.NotNullWhenAttribute;

namespace BepInEmbed;

public interface ILocatedAssembly
{
	Assembly GetAssembly();
	AssemblyDefinition GetAssemblyDef();
}

public interface ILocatedAssemblySafe : ILocatedAssembly
{
	bool TryGetAssembly(
		[val(true)] out Assembly? assembly,
		[val(false)] out Exception? exception);
	bool TryGetAssemblyDef(
		[val(true)] out AssemblyDefinition? definition,
		[val(false)] out Exception? exception);
}

public static class LocatedAssembly
{
	public readonly record struct FileAssembly(string Path) : ILocatedAssemblySafe
	{
		public Assembly GetAssembly() => Assembly.LoadFrom(Path);
		public AssemblyDefinition GetAssemblyDef() => AssemblyDefinition.ReadAssembly(Path);

		public bool TryGetAssembly(
			[val(true)] out Assembly? assembly,
			[val(false)] out Exception? exception
		) => TryGet(this, out assembly, out exception);

		public bool TryGetAssemblyDef(
			[val(true)] out AssemblyDefinition? definition,
			[val(false)] out Exception? exception
		) => TryGetDef(this, out definition, out exception);
	}

	public readonly record struct ResourceAssembly(
		Assembly Container, 
		string Name) : ILocatedAssemblySafe
	{
		public Assembly GetAssembly() {
			using var stream = Container.GetManifestResourceStream(Name);
			using var memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			return Assembly.Load(memoryStream.ToArray());
		}

		public AssemblyDefinition GetAssemblyDef() {
			using var stream = Container.GetManifestResourceStream(Name);
			return AssemblyDefinition.ReadAssembly(stream);
		}

		public bool TryGetAssembly(
			[val(true)] out Assembly? assembly,
			[val(false)] out Exception? exception
		) => TryGet(this, out assembly, out exception);

		public bool TryGetAssemblyDef(
			[val(true)] out AssemblyDefinition? definition,
			[val(false)] out Exception? exception
		) => TryGetDef(this, out definition, out exception);
	}

	private static bool TryGet<T>(
		T source,
		[val(true)] out Assembly? assembly,
		[val(false)] out Exception? exception
	) where T: ILocatedAssembly {
		(assembly, exception) = (null, null);
		try {
			assembly = source.GetAssembly();
			return true;
		} catch (Exception ex) {
			exception = ex;
			return false;
		}
	}

	private static bool TryGetDef<T>(
		T source,
		[val(true)] out AssemblyDefinition? definition,
		[val(false)] out Exception? exception
	) where T: ILocatedAssembly {
		(definition, exception) = (null, null);
		try {
			definition = source.GetAssemblyDef();
			return true;
		} catch (Exception ex) {
			exception = ex;
			return false;
		}
	}
}
