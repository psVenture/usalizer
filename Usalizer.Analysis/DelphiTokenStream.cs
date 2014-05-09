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

namespace Usalizer.Analysis
{
	/// <summary>
	/// Description of DelphiTokenStream.
	/// </summary>
	public class DelphiTokenStream
	{
		static IEnumerable<Token> StreamSingleFile(string fileName)
		{
			using (StreamReader reader = new StreamReader(fileName)) {
				DelphiTokenizer tokenizer = new DelphiTokenizer(reader);
				Token t;
				while ((t = tokenizer.Next()).Kind != TokenKind.EOF)
					yield return t;
			}
		}
		
		public static IEnumerable<Token> Stream(string fileName, DelphiIncludeResolver resolver, string[] symbols)
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
							foreach (var nestedToken in Stream(fullName, resolver, symbols)) {
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
		
		static DirectiveKind ParseDirective(string value, out string[] parameters)
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
	}
}
