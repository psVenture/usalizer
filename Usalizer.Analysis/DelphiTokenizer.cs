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
		readonly StringReader reader;

		public DelphiTokenizer(StringReader reader)
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
			}
			while (value <= (int)' ');
			char ch = (char)value;
			switch (ch) {
				case '(':
					int next = reader.Peek();
					if (next == (int)'*')
						return Comment('*');
					if (next == (int)'.') {
						reader.Read();
						return new Token(TokenKind.Any);
					}
					return new Token(TokenKind.OpenParens);
				case ')':
					return new Token(TokenKind.CloseParens);
				case '{':
					if (reader.Peek() == (int)'$')
						return PreprocessorDirecive();
					return Comment('{');
				default:
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
					reader.Read();
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

		Token PreprocessorDirecive()
		{
			StringBuilder sb = new StringBuilder();
			int val;
			do {
				val = reader.Read();
				if (val > 0)
					sb.Append((char)val);
			}
			while (val > -1 && val != (int)'}');
			return new Token(TokenKind.Comment, sb.ToString());
		}
	}
}

