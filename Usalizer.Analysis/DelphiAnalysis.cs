// Copyright (c) 2014 Stallinger Michael and Pammer Siegfried
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Usalizer.Analysis
{
	public enum UsesSection
	{
		Both, Interface, Implementation
	}
	
	public class DelphiAnalysis
	{
		string path;
		string projectGroupFile;
		string[] symbols;
		readonly MultiDictionary<string, DelphiFile> allUnits;
		readonly List<DelphiFile> unusedUnits;
		readonly List<Package> packages;
		string[] pasFiles;
		string[] incFiles;
		string[] dpkFiles;
		readonly IProgress<Tuple<string, double, bool>> progress;
		DelphiIncludeResolver resolver;
		
		public int UnitCount {
			get { return allUnits.Count; }
		}
		
		public int PackageCount {
			get { return packages.Count; }
		}

		public IReadOnlyList<DelphiFile> UnusedUnits {
			get { return unusedUnits; }
		}
		
		PackageOrderIComparer packageOrder;
		
		public IComparer<Package> PackageOrderComparer {
			get { return packageOrder; }
		}
		
		class PackageOrderIComparer : IComparer<Package>
		{
			string[] dpkFiles;
			
			public PackageOrderIComparer(string[] dpkFiles)
			{
				if (dpkFiles == null)
					throw new ArgumentNullException("dpkFiles");
				this.dpkFiles = dpkFiles;
			}
			
			public int Compare(Package x, Package y)
			{
				return Array.IndexOf(dpkFiles, x.Location) - Array.IndexOf(dpkFiles, y.Location);
			}
		}
		
		public DelphiAnalysis(string path, string projectGroupFile, string[] symbols, IProgress<Tuple<string, double, bool>> progress)
		{
			this.path = path;
			this.projectGroupFile = projectGroupFile;
			this.symbols = symbols;
			this.progress = progress;
			allUnits = new MultiDictionary<string, DelphiFile>(StringComparer.OrdinalIgnoreCase);
			unusedUnits = new List<DelphiFile>();
			packages = new List<Package>();
		}
		
		public Task<DelphiAnalysis> PrepareAnalysis(CancellationToken cancellation = default(CancellationToken))
		{
			return Task.Run(
				() =>  {
					pasFiles = new DirectoryInfo(path).EnumerateFiles("*.pas", SearchOption.AllDirectories).Select(f => f.FullName).ToArray();
					if (cancellation.IsCancellationRequested)
						throw new OperationCanceledException();
					incFiles = new DirectoryInfo(path).EnumerateFiles("*.inc", SearchOption.AllDirectories).Select(f => f.FullName).ToArray();
					if (cancellation.IsCancellationRequested)
						throw new OperationCanceledException();
					dpkFiles = FindDpkFiles().ToArray();
					packageOrder = new PackageOrderIComparer(dpkFiles);
					if (cancellation.IsCancellationRequested)
						throw new OperationCanceledException();
					resolver = new DelphiIncludeResolver(pasFiles, incFiles);
					return this;
				});
		}
		
		IEnumerable<string> FindDpkFiles()
		{
			var namespaceManager = new XmlNamespaceManager(new NameTable());
			namespaceManager.AddNamespace("msb", "http://schemas.microsoft.com/developer/msbuild/2003");
			var fileNames = ((IEnumerable<object>)XDocument.Load(projectGroupFile)
			               .XPathEvaluate("/msb:Project/msb:ItemGroup/msb:Projects/@Include", namespaceManager))
				.OfType<XAttribute>()
				.Select(a => DelphiIncludeResolver.MakeAbsolute(projectGroupFile, a.Value));
			foreach (var fileName in fileNames) {
				var sourceNames = (XDocument.Load(fileName)
				                   .XPathSelectElements("/msb:Project/msb:PropertyGroup/msb:MainSource", namespaceManager))
					.Select(e => DelphiIncludeResolver.MakeAbsolute(fileName, e.Value));
				foreach (var source in sourceNames) {
					if (Path.GetExtension(source).Equals(".dpk", StringComparison.OrdinalIgnoreCase))
						yield return source;
				}
			}
		}
		
		public void Analyse(CancellationToken cancellation = default(CancellationToken))
		{
			progress.Report(Tuple.Create("Parse .pas files...", 0.0, true));
			Parallel.ForEach(pasFiles, AnalyseDelphiFile);
			progress.Report(Tuple.Create("Parse .dpk files...", 0.0, true));
			foreach (var p in dpkFiles) {
				AnalysePackageFile(p);
			}
			progress.Report(Tuple.Create("Resolve graph...", 0.0, true));
			foreach (var unit in allUnits.SelectMany(u => u)) {
				foreach (var use in unit.Uses) {
					use.Resolve(this);
				}
				progress.Report(Tuple.Create(unit.UnitName, 1.0 / allUnits.Count, false));
			}
			progress.Report(Tuple.Create("Find unused units...", 0.0, true));
			foreach (var unit in allUnits.SelectMany(u => u)) {
				if (!unit.UsedByFiles.Any() && !unit.DirectlyInPackages.Any())
					unusedUnits.Add(unit);
				progress.Report(Tuple.Create(unit.UnitName, 1.0 / allUnits.Count, false));
			}
		}

		public IEnumerable<DelphiFile> FindPartialName(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				yield break;
			foreach (var unit in allUnits.SelectMany(u => u)) {
				if (unit.UnitName.IndexOf(text, StringComparison.OrdinalIgnoreCase) > -1)
					yield return unit;
			}
		}
		
		public DelphiFile[] FindContainingPackages(DelphiFile unit, out Dictionary<DelphiFile, DelphiFile> parentMapping)
		{
			var nodesToVisit = new Queue<DelphiFile>();
			var endPoints = new List<DelphiFile>();
			parentMapping = new Dictionary<DelphiFile, DelphiFile>();
			
			if (unit.DirectlyInPackages.Count > 0)
				endPoints.Add(unit);
			
			foreach (var u in unit.UsedByFiles) {
				if (parentMapping.ContainsKey(u)) continue;
				parentMapping.Add(u, unit);
				nodesToVisit.Enqueue(u);
			}
			while (nodesToVisit.Count > 0) {
				var u = nodesToVisit.Dequeue();
				if (u.DirectlyInPackages.Count > 0)
					endPoints.Add(u);
				foreach (var c in u.UsedByFiles) {
					if (parentMapping.ContainsKey(c)) continue;
					parentMapping.Add(c, u);
					nodesToVisit.Enqueue(c);
				}
			}
			return endPoints.ToArray();
		}
		
		void AnalysePackageFile(string p)
		{
			var package = MakePackageFile(DelphiTokenStream.Stream(p, resolver, symbols), p);
			if (package == null) {
				return;
			}
			lock (packages) {
				packages.Add(package);
			}
		}
		
		void AnalyseDelphiFile(string f)
		{
			var file = MakeFile(DelphiTokenStream.Stream(f, resolver, symbols), f);
			if (file == null) {
				return;
			}
			lock (allUnits) {
				allUnits.Add(file.UnitName, file);
			}
		}
		
		enum LookFor
		{
			Package,
			ContainsUnit,
			Unit,
			InterfaceUses,
			ImplementationUses
		}
		
		DelphiFile MakeFile(IEnumerable<Token> tokens, string fileName)
		{
			try {
				DelphiFile unit = null;
				
				var implementationUses = new List<UsesClause>();
				var interfaceUses = new List<UsesClause>();
				
				Token prev = new Token(TokenKind.EOF);
				LookFor state = LookFor.Unit;
				var tokenizer = tokens.GetEnumerator();
				
				while (tokenizer.MoveNext()) {
					var t = tokenizer.Current;
					switch (state) {
						case LookFor.Unit:
							if (t.Kind == TokenKind.Identifier && prev.IsKeyword("unit")) {
								unit = new DelphiFile(ParseUnitIdentifier(ref t, tokenizer), fileName);
								state = LookFor.InterfaceUses;
							}
							break;
						case LookFor.InterfaceUses:
							if (t.IsKeyword("uses") && prev.IsKeyword("interface")) {
								state = LookFor.ImplementationUses;
								while (t.Kind != TokenKind.Semicolon && tokenizer.MoveNext()) {
									t = tokenizer.Current;
									if (t.Kind == TokenKind.Identifier)
										interfaceUses.Add(new UsesClause(unit, ParseUnitIdentifier(ref t, tokenizer)));
								}
							} else if (prev.IsKeyword("interface")) {
								state = LookFor.ImplementationUses;
							}
							break;
						case LookFor.ImplementationUses:
							if (t.IsKeyword("uses") && prev.IsKeyword("implementation")) {
								while (t.Kind != TokenKind.Semicolon && tokenizer.MoveNext()) {
									t = tokenizer.Current;
									if (t.Kind == TokenKind.Identifier)
										implementationUses.Add(new UsesClause(unit, ParseUnitIdentifier(ref t, tokenizer)));
								}
								goto done;
							}
							break;
					}
					prev = t;
				}
			done:
				progress.Report(Tuple.Create(fileName, 1.0 / pasFiles.Length, false));
				if (unit == null)
					return null;
				unit.ImplementationUses.AddRange(implementationUses);
				unit.InterfaceUses.AddRange(interfaceUses);
				return unit;
			} catch (Exception ex) {
				throw new Exception("Error while processing: " + fileName, ex);
			}
		}
		
		Package MakePackageFile(IEnumerable<Token> tokens, string fileName)
		{
			try {
				Package package = null;
				
				var containingUnits = new List<DelphiFile>();
				
				Token prev = new Token(TokenKind.EOF);
				LookFor state = LookFor.Package;
				var tokenizer = tokens.GetEnumerator();
				
				while (tokenizer.MoveNext()) {
					var t = tokenizer.Current;
					switch (state) {
						case LookFor.Package:
							if (t.Kind == TokenKind.Identifier && prev.IsKeyword("package")) {
								package = new Package(ParseUnitIdentifier(ref t, tokenizer), fileName);
								state = LookFor.ContainsUnit;
							}
							break;
						case LookFor.ContainsUnit:
							if (t.IsKeyword("contains")) {
								while (t.Kind != TokenKind.Semicolon && tokenizer.MoveNext()) {
									t = tokenizer.Current;
									if (t.Kind == TokenKind.Identifier) {
										var unit = ResolveUnitName(fileName, ParseUnitIdentifier(ref t, tokenizer));
										if (unit != null) {
											containingUnits.Add(unit);
											if (package != null)
												unit.DirectlyInPackages.Add(package);
										}
									}
								}
								goto done;
							}
							break;
					}
					prev = t;
				}
			done:
				progress.Report(Tuple.Create(fileName, 1.0 / dpkFiles.Length, false));
				if (package == null)
					return null;
				package.ContainingUnits.AddRange(containingUnits);
				return package;
			} catch (Exception ex) {
				throw new Exception("Error while processing: " + fileName, ex);
			}
		}

		string ParseUnitIdentifier(ref Token t, IEnumerator<Token> tokenizer)
		{
			Token prev = t;
			StringBuilder sb = new StringBuilder(prev.Value);
			while (tokenizer.MoveNext()) {
				t = tokenizer.Current;
				if (t.Kind == TokenKind.Semicolon || t.Kind == TokenKind.Comma)
					break;
				if (t.Kind == TokenKind.Identifier)
					sb.Append(t.Value);
				else if (t.Kind == TokenKind.Dot)
					sb.Append('.');
				prev = t;
			}
			return sb.ToString();
		}
		
		public DelphiFile ResolveUnitName(string contextLocation, string unitName, string inLocation = null)
		{
			// does not fully follow the spec, but is sufficient for this use-case.
			// would need a library path...
			var matches = allUnits[unitName];
			if (inLocation != null) {
				inLocation = DelphiIncludeResolver.MakeAbsolute(contextLocation, inLocation);
				return matches.FirstOrDefault(m => string.Equals(m.Location, inLocation, StringComparison.OrdinalIgnoreCase));
			}
			if (matches.Count == 1) {
				return matches[0];
			} else {
				string searchFileName = Path.Combine(Path.GetDirectoryName(contextLocation), unitName + ".pas");
				var result = matches.FirstOrDefault(m => string.Equals(m.Location, searchFileName, StringComparison.OrdinalIgnoreCase));
				if (result != null)
					return result;
				return matches.FirstOrDefault();
			}
		}
		
		public IEnumerable<DelphiFile> FindReferences(string unitName, UsesSection section)
		{
			IEnumerable<UsesClause> source = Enumerable.Empty<UsesClause>();
			switch (section) {
				case UsesSection.Both:
					source = allUnits.SelectMany(g => g.SelectMany(u => u.InterfaceUses))
						.Concat(allUnits.SelectMany(g => g.SelectMany(u => u.ImplementationUses)));
					break;
				case UsesSection.Interface:
					source = allUnits.SelectMany(g => g.SelectMany(u => u.InterfaceUses));
					break;
				case UsesSection.Implementation:
					source = allUnits.SelectMany(g => g.SelectMany(u => u.ImplementationUses));
					break;
			}
			
			return source.AsParallel()
				.Where(c => string.Equals(c.Name, unitName, StringComparison.OrdinalIgnoreCase))
				.OrderBy(c => c.Name)
				.Select(c => c.ParentFile);
		}
	}
}