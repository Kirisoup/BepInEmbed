using System.Reflection;
// using Mono.Cecil;
using SoupCommonLib;
using Definition = Mono.Cecil.AssemblyDefinition;

namespace BepInEmbed;

public interface IAssemblyConvert
{
	Assembly? GetAssembly();
	Definition? GetDefinition();
	(Assembly, Definition)? GetBoth();
	IAssemblyConvert? Map(Func<Definition, Definition> f);
}

public static class AssemblyConvert
{
	public readonly record struct CecilAssembly(Definition Definition) : IAssemblyConvert
	{
		public Definition? GetDefinition() => Definition;

		public Assembly? GetAssembly() {
			try {
				using var ms = new MemoryStream();
				Definition.Write(ms);
				return Assembly.Load(ms?.ToArray());
			} catch {
				return null;
			}
		}

		public (Assembly, Definition)? GetBoth() => 
			GetAssembly() is Assembly assembly 
				? (assembly, Definition) 
				: null;

		public IAssemblyConvert? Map(Func<Definition, Definition> f) =>
			new CecilAssembly(f(Definition));
	}

	public readonly record struct FileAssembly(string Path) : IAssemblyConvert
	{
		public Assembly? GetAssembly() {
			try {
				return Assembly.LoadFrom(Path);
			} catch {
				return null;
			}
		}

		public Definition? GetDefinition() {
			try {
				return Definition.ReadAssembly(Path);
			} catch {
				return null;
			}
		}

		public (Assembly, Definition)? GetBoth() => 
			(GetAssembly(), GetDefinition()) is (Assembly, Definition) both
				? both!
				: null;

		public IAssemblyConvert? Map(Func<Definition, Definition> f) => 
			GetDefinition() is Definition definition
				? new CecilAssembly(f(definition))
				: null;
	}

	public readonly record struct ResourceAssembly(
		Assembly Container, 
		string ResourceName) : IAssemblyConvert
	{
		public Assembly? GetAssembly() {
			try {
				using var stream = Container.GetManifestResourceStream(ResourceName);
				using var memoryStream = new MemoryStream();
				stream.CopyTo(memoryStream);
				return Assembly.Load(memoryStream.ToArray());
			} catch {
				return null;
			}
		}

		public Definition? GetDefinition() {
			try {
				var stream = Container.GetManifestResourceStream(ResourceName);
				return Definition.ReadAssembly(stream);
			} catch {
				return null;
			}
		}

		public (Assembly, Definition)? GetBoth() => 
			(GetAssembly(), GetDefinition()) is (Assembly, Definition) both
				? both!
				: null;

		public IAssemblyConvert? Map(Func<Definition, Definition> f) => 
			GetDefinition() is Definition definition
				? new CecilAssembly(f(definition))
				: null;
	}
}
