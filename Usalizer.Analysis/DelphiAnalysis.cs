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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Usalizer.Analysis
{
	public class DelphiAnalysis
	{
		string path;
		string[] symbols;
		Dictionary<string, DelphiFile> allUnits;
		string[] pasFiles;
		string[] incFiles;
		readonly IProgress<Tuple<string, double>> progress;
		DelphiIncludeResolver resolver;
		
		public DelphiAnalysis(string path, string[] symbols, IProgress<Tuple<string, double>> progress)
		{
			this.path = path;
			this.symbols = symbols;
			this.progress = progress;
		}
		
		public Task<DelphiAnalysis> PrepareAnalysis(CancellationToken cancellation = default(CancellationToken))
		{
			return Task.Run(() =>  {
				pasFiles = new DirectoryInfo(path).EnumerateFiles("*.pas", SearchOption.AllDirectories).Select(f => f.FullName).ToArray();
				if (cancellation.IsCancellationRequested)
					throw new OperationCanceledException();
				incFiles = new DirectoryInfo(path).EnumerateFiles("*.inc", SearchOption.AllDirectories).Select(f => f.FullName).ToArray();
				if (cancellation.IsCancellationRequested)
					throw new OperationCanceledException();
				resolver = new DelphiIncludeResolver(pasFiles, incFiles);
				return this;
			});
		}
		
		public void Analyse(CancellationToken cancellation = default(CancellationToken))
		{
			#if DEBUG
			foreach (var f in pasFiles)
			MakeFile(Stream(f), f);
			#else
			Parallel.ForEach(pasFiles, f => MakeFile(Stream(f, symbols, incFiles, pasFiles), f));
			#endif
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
			
			foreach (var token in StreamSingleFile(fileName)) {
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
				}
				else {
					if (openedIfs.Count == 0 || openedIfs.Peek().Item3)
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
			Unit,
			InterfaceUses,
			ImplementationUses
		}
		
		DelphiFile MakeFile(IEnumerable<Token> tokens, string fileName)
		{
			string unitName = null;
			Console.WriteLine("file: " + fileName);
			Console.WriteLine(File.ReadAllText(fileName));
			
			var implementationUses = new List<UsesClause>();
			var interfaceUses = new List<UsesClause>();
			
			Token prev = new Token(TokenKind.EOF);
			LookFor state = LookFor.Unit;
			var tokenizer = tokens.GetEnumerator();
			
			while (tokenizer.MoveNext()) {
				var t = tokenizer.Current;
				Console.WriteLine(t);
				switch (state) {
					case LookFor.Unit:
						if (t.Kind == TokenKind.Identifier && prev.IsKeyword("unit")) {
							unitName = t.Value;
							state = LookFor.InterfaceUses;
						}
						break;
					case LookFor.InterfaceUses:
						if (t.IsKeyword("uses") && prev.IsKeyword("interface")) {
							state = LookFor.ImplementationUses;
							Console.WriteLine("found interface uses!");
							while (t.Kind != TokenKind.Semicolon && tokenizer.MoveNext()) {
								t = tokenizer.Current;
								if (t.Kind == TokenKind.Identifier)
									interfaceUses.Add(new UsesClause(t.Value));
							}
						}
						break;
					case LookFor.ImplementationUses:
						if (t.IsKeyword("uses") && prev.IsKeyword("implementation")) {
							Console.WriteLine("found implementation uses!");
							while (t.Kind != TokenKind.Semicolon && tokenizer.MoveNext()) {
								t = tokenizer.Current;
								if (t.Kind == TokenKind.Identifier)
									implementationUses.Add(new UsesClause(t.Value));
							}
						}
						break;
			}
				switch (t.Kind) {
					case TokenKind.Identifier:
						if (prev != null && prev.IsKeyword("unit"))
							unitName = t.Value;
						break;
			}
				prev = t;
			}
			progress.Report(Tuple.Create(fileName, 1.0 / pasFiles.Length));
			if (unitName == null)
				return null;
			var file = new DelphiFile(unitName, fileName);
			file.ImplementationUses.AddRange(implementationUses);
			file.InterfaceUses.AddRange(interfaceUses);
			return file;
		}
	}
}