using System.Reflection;
using Mono.Cecil;
using SoupCommonLib;
using System.Diagnostics.CodeAnalysis;

namespace BepInEmbed;

public interface IAssemblyConvert
{
	Assembly GetSysAssembly();
	AssemblyDefinition GetCecilAssembly();
}

public interface IAssemblyConvertSafe : IAssemblyConvert
{
	bool TryGetSysAssembly(
		[NotNullWhen(true)] out Assembly? assembly,
		[NotNullWhen(false)] out Exception? exception);
	bool TryGetCecilAssembly(
		[NotNullWhen(true)] out AssemblyDefinition? assembly,
		[NotNullWhen(false)] out Exception? exception);
}

public static class AssemblyConvert
{
	public readonly record struct FileAssembly(string Path) : IAssemblyConvertSafe
	{
		public Assembly GetSysAssembly() => Assembly.LoadFrom(Path);
		public AssemblyDefinition GetCecilAssembly() => AssemblyDefinition.ReadAssembly(Path);

		public bool TryGetSysAssembly(
			[NotNullWhen(true)] out Assembly? assembly,
			[NotNullWhen(false)] out Exception? exception
		) => this.TryMap(
			@try: src => src.GetSysAssembly(),
			out assembly, out exception);


		public bool TryGetCecilAssembly(
			[NotNullWhen(true)] out AssemblyDefinition? assembly,
			[NotNullWhen(false)] out Exception? exception
		) => this.TryMap(
			@try: src => src.GetCecilAssembly(),
			out assembly, out exception);
	}

	public readonly record struct ResourceAssembly(
		Assembly Container, 
		string Name) : IAssemblyConvertSafe
	{
		public Assembly GetSysAssembly() {
			using var stream = Container.GetManifestResourceStream(Name);
			using var memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			return Assembly.Load(memoryStream.ToArray());
		}

		public AssemblyDefinition GetCecilAssembly() {
			using var stream = Container.GetManifestResourceStream(Name);
			return AssemblyDefinition.ReadAssembly(stream);
		}

		public bool TryGetSysAssembly(
			[NotNullWhen(true)] out Assembly? assembly,
			[NotNullWhen(false)] out Exception? exception
		) => this.TryMap(
			@try: src => src.GetSysAssembly(),
			out assembly, out exception);

		public bool TryGetCecilAssembly(
			[NotNullWhen(true)] out AssemblyDefinition? assembly,
			[NotNullWhen(false)] out Exception? exception
		) => this.TryMap(
			@try: src => src.GetCecilAssembly(),
			out assembly, out exception);
	}
}
