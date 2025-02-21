using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;

namespace BepInEmbed;
using Definition = AssemblyDefinition;
using Both = (Assembly assembly, AssemblyDefinition definition);

public interface IAssemblyConvert
{
	Result<Assembly, Exception> GetAssembly();
	Result<Definition, Exception> GetDefinition();
	Result<Both, Exception> GetBoth();
	Result<IAssemblyConvert, Exception> Map(Func<Definition, Definition> f);
}

// public interface IAssemblyConvert
// {
// 	Assembly? GetAssembly();
// 	Definition? GetDefinition();
// 	(Assembly, Definition)? GetBoth();
// 	IAssemblyConvert? Map(Func<Definition, Definition> f);
// }

public static class AssemblyConvert
{
	public readonly record struct Definition(AssemblyDefinition AsmDefinition) : IAssemblyConvert
	{
		public Result<AssemblyDefinition, Exception> GetDefinition() => Result.Ok(AsmDefinition);

		public Result<Assembly, Exception> GetAssembly() {
			try {
				using var ms = new MemoryStream();
				AsmDefinition.Write(ms);
				return Result.Ok(Assembly.Load(ms?.ToArray()));
			} catch (Exception ex) {
				return Result.Ex(ex);
			}
		}

		public Result<Both, Exception> GetBoth() => GetAssembly() switch {
			(Assembly assembly, _) => Result.Ok((assembly, AsmDefinition)),
			(_, Exception ex) => Result.Ex(ex),
			_ => default
		};

		public Result<IAssemblyConvert, Exception> Map(Func<AssemblyDefinition, AssemblyDefinition> f) =>
			Result.Ok<IAssemblyConvert>(new Definition(f(AsmDefinition)));
	}

	public readonly record struct File(string Path) : IAssemblyConvert
	{
		public Result<Assembly, Exception> GetAssembly() {
			try {
				return Result.Ok(Assembly.LoadFrom(Path));
			} catch (Exception ex) {
				return Result.Ex(ex);
			}
		}

		public Result<AssemblyDefinition, Exception> GetDefinition() {
			try {
				return Result.Ok(AssemblyDefinition.ReadAssembly(Path));
			} catch (Exception ex) {
				return Result.Ex(ex);
			}
		}

		public Result<Both, Exception> GetBoth() => 
			(GetAssembly(), GetDefinition()) switch {
				((Assembly assembly, _), (AssemblyDefinition definition, _)) =>
					Result.Ok((assembly, definition)),
				((_, Exception ex), _) => Result.Ex(ex),
				(_, (_, Exception ex)) => Result.Ex(ex),
				_ => default
			};

		public Result<IAssemblyConvert, Exception> Map(Func<AssemblyDefinition, AssemblyDefinition> f) =>
			GetDefinition() switch {
				(AssemblyDefinition definition, _) =>
					Result.Ok<IAssemblyConvert>(new Definition(f(definition))),
				(_, Exception ex) => Result.Ex(ex),
				_ => default
			};
	}

	public readonly record struct Resource(
		Assembly Source, 
		string ResourceName) : IAssemblyConvert
	{
		public Result<Assembly, Exception> GetAssembly() {
			try {
				using var stream = Source.GetManifestResourceStream(ResourceName);
				using var memoryStream = new MemoryStream();
				stream.CopyTo(memoryStream);
				return Result.Ok(Assembly.Load(memoryStream.ToArray()));
			} catch (Exception ex) {
				return Result.Ex(ex);
			}
		}

		public Result<AssemblyDefinition, Exception> GetDefinition() {
			try {
				var stream = Source.GetManifestResourceStream(ResourceName);
				return Result.Ok(AssemblyDefinition.ReadAssembly(stream));
			} catch (Exception ex) {
				return Result.Ex(ex);
			}
		}

		public Result<Both, Exception> GetBoth() => 
			(GetAssembly(), GetDefinition()) switch {
				((Assembly assembly, _), (AssemblyDefinition definition, _)) =>
					Result.Ok((assembly, definition)),
				((_, Exception ex), _) => Result.Ex(ex),
				(_, (_, Exception ex)) => Result.Ex(ex),
				_ => default
			};

		public Result<IAssemblyConvert, Exception> Map(Func<AssemblyDefinition, AssemblyDefinition> f) =>
			GetDefinition() switch {
				(AssemblyDefinition definition, _) =>
					Result.Ok<IAssemblyConvert>(new Definition(f(definition))),
				(_, Exception ex) => Result.Ex(ex),
				_ => default
			};
	}
}
