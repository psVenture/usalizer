﻿// Copyright (c) 2014 Stallinger Michael and Pammer Siegfried
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

namespace Usalizer.Analysis
{
	public enum UsesSection
	{
		Both, Interface, Implementation
	}
	
	public class DelphiAnalysis
	{
		string path;
		string packagePath;
		string[] symbols;
		readonly MultiDictionary<string, DelphiFile> allUnits;
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
		
		public DelphiAnalysis(string path, string packagePath, string[] symbols, IProgress<Tuple<string, double, bool>> progress)
		{
			this.path = path;
			this.packagePath = packagePath;
			this.symbols = symbols;
			this.progress = progress;
			allUnits = new MultiDictionary<string, DelphiFile>(StringComparer.OrdinalIgnoreCase);
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
					dpkFiles = new DirectoryInfo(packagePath).EnumerateFiles("*.dpk", SearchOption.AllDirectories).Select(f => f.FullName).ToArray();
					if (cancellation.IsCancellationRequested)
						throw new OperationCanceledException();
					resolver = new DelphiIncludeResolver(pasFiles, incFiles);
					return this;
				});
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
		}

		public IEnumerable<DelphiFile> FindPartialName(string text)
		{
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
			var package = MakePackageFile(Stream(p), p);
			if (package == null) {
				return;
			}
			lock (packages) {
				packages.Add(package);
			}
		}
		
		void AnalyseDelphiFile(string f)
		{
			var file = MakeFile(Stream(f), f);
			if (file == null) {
				return;
			}
			lock (allUnits) {
				allUnits.Add(file.UnitName, file);
			}
		}
		
		IEnumerable<Token> StreamSingleFile(string fileName)
		{
			using (StreamReader reader = new StreamReader(fileName)) {
				DelphiTokenizer tokenizer = new DelphiTokenizer(reader);
				Token t;
				while ((t = tokenizer.Next()).Kind != TokenKind.EOF)
					yield return t;
			}
		}
		
		IEnumerable<Token> Stream(string fileName)
		{
			Stack<Tuple<DirectiveKind, string, bool>> openedIfs = new Stack<Tuple<DirectiveKind, string, bool>>();
			
			foreach (var token in StreamSingleFile(fileName).Where(t => t.Kind != TokenKind.Comment)) {
				if (token.Kind == TokenKind.Directive) {
					string[] parameters;
					string symbol;
					var directiveKind = ParseDirective(token.Value, out parameters);
					switch (directiveKind) {
						case DirectiveKind.Include:
							// possible stack overflow: optimize!
							string fileNamePart = parameters.FirstOrDefault();
							if (string.IsNullOrWhiteSpace(fileNamePart))
								break;
							string fullName = resolver.ResolveFileName(fileNamePart, fileName);
							if (fullName == null)
								break;
							foreach (var nestedToken in Stream(fullName)) {
								yield return nestedToken;
							}
							break;
						case DirectiveKind.IfDef:
							symbol = parameters.FirstOrDefault();
							if (!string.IsNullOrWhiteSpace(symbol) && symbol[0] == '!') {
								bool include = !symbols.Contains(symbol.Substring(1));
								openedIfs.Push(Tuple.Create(DirectiveKind.IfNDef, symbol.Substring(1), include));
							} else {
								bool include = symbols.Contains(symbol);
								openedIfs.Push(Tuple.Create(DirectiveKind.IfDef, symbol, include));
							}
							break;
						case DirectiveKind.IfNDef:
							symbol = parameters.FirstOrDefault();
							if (!string.IsNullOrWhiteSpace(symbol) && symbol[0] == '!') {
								bool include = symbols.Contains(symbol.Substring(1));
								openedIfs.Push(Tuple.Create(DirectiveKind.IfDef, symbol.Substring(1), include));
							} else {
								bool include = !symbols.Contains(symbol);
								openedIfs.Push(Tuple.Create(DirectiveKind.IfNDef, symbol, include));
							}
							break;
						case DirectiveKind.Else:
							if (openedIfs.Count > 0) {
								var old = openedIfs.Pop();
								openedIfs.Push(Tuple.Create(old.Item1, old.Item2, !old.Item3));
							}
							break;
						case DirectiveKind.EndIf:
							if (openedIfs.Count > 0)
								openedIfs.Pop();
							break;
					}
				} else {
					if (openedIfs.Count == 0 || openedIfs.All(i => i.Item3))
						yield return token;
				}
			}
		}
		
		DirectiveKind ParseDirective(string value, out string[] parameters)
		{
			var list = new List<string>();
			StringBuilder sb = new StringBuilder();
			
			bool inString = false;
			for (int i = 0; i < value.Length; i++) {
				var ch = value[i];
				if (ch == '\'') {
					if (inString && i + 1 < value.Length && value[i + 1] == '\'')
						sb.Append('\'');
					else {
						inString = !inString;
						list.Add(sb.ToString());
						sb.Clear();
					}
				} else if (char.IsWhiteSpace(ch) && !inString) {
					list.Add(sb.ToString());
					sb.Clear();
				} else {
					sb.Append(ch);
				}
			}
			list.Add(sb.ToString());
			
			string directive = list.FirstOrDefault();
			parameters = list.Skip(1).ToArray();
			if (string.IsNullOrEmpty(directive))
				return DirectiveKind.Error;
			
			switch (directive.ToUpperInvariant()) {
				case "ELSE":
					return DirectiveKind.Else;
				case "IFDEF":
					return DirectiveKind.IfDef;
				case "IFNDEF":
					return DirectiveKind.IfNDef;
				case "I":
				case "INCLUDE":
					return DirectiveKind.Include;
				case "ENDIF":
					return DirectiveKind.EndIf;
			}
			return DirectiveKind.Error;
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
								unit = new DelphiFile(t.Value, fileName);
								state = LookFor.InterfaceUses;
							}
							break;
						case LookFor.InterfaceUses:
							if (t.IsKeyword("uses") && prev.IsKeyword("interface")) {
								state = LookFor.ImplementationUses;
								while (t.Kind != TokenKind.Semicolon && tokenizer.MoveNext()) {
									t = tokenizer.Current;
									if (t.Kind == TokenKind.Identifier)
										interfaceUses.Add(new UsesClause(unit, t.Value));
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
										implementationUses.Add(new UsesClause(unit, t.Value));
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
								package = new Package(t.Value, fileName);
								state = LookFor.ContainsUnit;
							}
							break;
						case LookFor.ContainsUnit:
							if (t.IsKeyword("contains")) {
								while (t.Kind != TokenKind.Semicolon && tokenizer.MoveNext()) {
									t = tokenizer.Current;
									if (t.Kind == TokenKind.Identifier) {
										var unit = ResolveUnitName(t.Value);
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
		
		public DelphiFile ResolveUnitName(string unitName, string inLocation = null)
		{
			if (inLocation != null)
				throw new NotImplementedException();
			// TODO : implement proper Delphi name resolution
			return allUnits[unitName].FirstOrDefault();
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