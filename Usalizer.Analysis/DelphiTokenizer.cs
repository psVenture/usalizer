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
using System.IO;
using System.Text;

namespace Usalizer.Analysis
{
	public class DelphiTokenizer
	{
		readonly TextReader reader;
		
		public DelphiTokenizer(TextReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException("reader");
			this.reader = reader;
		}
		
		public Token Next()
		{
			int value;
			do {
				value = reader.Read();
				if (value < 0)
					return new Token(TokenKind.EOF);
			} while (value <= (int)' ');
			
			char ch = (char)value;
			switch (ch) {
				case '(':
					int next = reader.Peek();
					if (next == (int)'*') {
						reader.Read();
						if (reader.Peek() == (int)'$')
							return PreprocessorDirecive('*');
						return Comment('*');
					}
					if (next == (int)'.') {
						reader.Read();
						return new Token(TokenKind.Any);
					}
					return new Token(TokenKind.OpenParens);
				case ')':
					return new Token(TokenKind.CloseParens);
				case '{':
					if (reader.Peek() == (int)'$')
						return PreprocessorDirecive('{');
					return Comment('{');
				case ';':
					return new Token(TokenKind.Semicolon);
				case '.':
					return new Token(TokenKind.Dot);
				case ':':
					return new Token(TokenKind.Colon);
				case ',':
					return new Token(TokenKind.Comma);
				case '/':
					if (reader.Peek() == (int)'/')
						return Comment('/');
					return new Token(TokenKind.Divide);
				default:
					if (char.IsLetter(ch)) {
						string identifier = Identifier(ch);
						if (IsKeyword(identifier)) {
							return new Token(TokenKind.Keyword, identifier.ToLowerInvariant());
						}
						return new Token(TokenKind.Identifier, identifier);
					}
					return new Token(TokenKind.Any);
			}
		}
		
		Token Comment(char c)
		{
			int val;
			switch (c) {
				case '/':
					reader.ReadLine();
					return new Token(TokenKind.Comment);
				case '{':
					do {
						val = reader.Read();
					}
					while (val > -1 && val != (int)'}');
					return new Token(TokenKind.Comment);
				case '*':
					while (true) {
						val = reader.Read();
						if (val == -1)
							break;
						if (val != (int)'*')
							continue;
						val = reader.Peek();
						if (val == -1 || val == (int)')')
							break;
					}
					return new Token(TokenKind.Comment);
				default:
					throw new NotSupportedException();
			}
		}
		
		Token PreprocessorDirecive(char ch)
		{
			reader.Read(); // skip $
			StringBuilder sb = new StringBuilder();
			int val = -1;
			switch (ch) {
				case '{':
					do {
						if (val > -1)
							sb.Append((char)val);
						val = reader.Read();
					}
					while (val > -1 && val != (int)'}');
					break;
				case '*':
					while (true) {
						val = reader.Read();
						if (val == -1)
							break;
						if (val != (int)'*') {
							sb.Append((char)val);
							continue;
						}
						val = reader.Peek();
						if (val == -1 || val == (int)')') {
							reader.Read();
							break;
						}
						sb.Append('*');
					}
					break;
				default:
					throw new NotSupportedException();
			}
			return new Token(TokenKind.Directive, sb.ToString());
		}
		
		string Identifier(char ch)
		{
			StringBuilder sb = new StringBuilder(ch.ToString());
			int val = reader.Peek();
			while (val > 0 && (char.IsLetterOrDigit((char)val) || val == (int)'_')) {
				sb.Append((char)reader.Read());
				val = reader.Peek();
			}
			return sb.ToString();
		}
		
		bool IsKeyword(string identifier)
		{
			switch (identifier.ToUpperInvariant()) {
				case "AND":
					return true;
				case "ARRAY":
					return true;
				case "AS":
					return true;
				case "ASM":
					return true;
				case "END":
					return true;
				case "IMPLEMENTATION":
					return true;
				case "INTERFACE":
					return true;
				case "UNIT":
					return true;
				case "UNTIL":
					return true;
				case "USES":
					return true;
		}
			return false;
		}
	}
}

