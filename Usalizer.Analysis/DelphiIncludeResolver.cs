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
using System.Linq;
using System.Text;

namespace Usalizer.Analysis
{
	public class DelphiIncludeResolver
	{
		string[] pasFiles;
		string[] incFiles;
		
		public DelphiIncludeResolver(string[] pasFiles, string[] incFiles)
		{
			this.pasFiles = pasFiles;
			this.incFiles = incFiles;
		}
		
		public string ResolveFileName(string fileNamePart, string currentFile)
		{
			if (!Path.HasExtension(fileNamePart))
				fileNamePart += ".pas";
			// look in the current directory
			string extension = Path.GetExtension(fileNamePart);
			string currentDir = Path.GetDirectoryName(currentFile);
			string firstTry = NormalizePath(Path.Combine(currentDir, fileNamePart));
			if (SearchFileLists(extension, firstTry))
				return firstTry;
			string fileName = Path.GetFileName(fileNamePart);
			if (string.Equals(extension, ".pas", StringComparison.OrdinalIgnoreCase)) {
				return pasFiles.FirstOrDefault(f => f.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
			}
			if (string.Equals(extension, ".inc", StringComparison.OrdinalIgnoreCase)) {
				return incFiles.FirstOrDefault(f => f.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
			}
			return null;
		}
		
		bool SearchFileLists(string extension, string fileName)
		{
			if (string.Equals(extension, ".pas", StringComparison.OrdinalIgnoreCase)) {
				if (pasFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
					return true;
			}
			else if (string.Equals(extension, ".inc", StringComparison.OrdinalIgnoreCase)) {
				if (incFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}
		
		/// <summary>
		/// Gets the normalized version of fileName.
		/// Slashes are replaced with backslashes, backreferences "." and ".." are 'evaluated'.
		/// </summary>
		/// Copied from ICSharpCode.Core project
		static string NormalizePath(string fileName)
		{
			if (string.IsNullOrEmpty(fileName)) return fileName;
			
			int i;
			
			bool isWeb = false;
			for (i = 0; i < fileName.Length; i++) {
				if (fileName[i] == '/' || fileName[i] == '\\')
					break;
				if (fileName[i] == ':') {
					if (i > 1)
						isWeb = true;
					break;
				}
			}
			
			char outputSeparator = isWeb ? '/' : System.IO.Path.DirectorySeparatorChar;
			bool isRelative;
			
			StringBuilder result = new StringBuilder();
			if (isWeb == false && fileName.StartsWith(@"\\", StringComparison.Ordinal) || fileName.StartsWith("//", StringComparison.Ordinal)) {
				// UNC path
				i = 2;
				result.Append(outputSeparator);
				isRelative = false;
			} else {
				i = 0;
				isRelative = !isWeb && (fileName.Length < 2 || fileName[1] != ':');
			}
			int levelsBack = 0;
			int segmentStartPos = i;
			for (; i <= fileName.Length; i++) {
				if (i == fileName.Length || fileName[i] == '/' || fileName[i] == '\\') {
					int segmentLength = i - segmentStartPos;
					switch (segmentLength) {
						case 0:
							// ignore empty segment (if not in web mode)
							if (isWeb) {
								result.Append(outputSeparator);
							}
							break;
						case 1:
							// ignore /./ segment, but append other one-letter segments
							if (fileName[segmentStartPos] != '.') {
								if (result.Length > 0) result.Append(outputSeparator);
								result.Append(fileName[segmentStartPos]);
							}
							break;
						case 2:
							if (fileName[segmentStartPos] == '.' && fileName[segmentStartPos + 1] == '.') {
								// remove previous segment
								int j;
								for (j = result.Length - 1; j >= 0 && result[j] != outputSeparator; j--);
								if (j > 0) {
									result.Length = j;
								} else if (isRelative) {
									if (result.Length == 0)
										levelsBack++;
									else
										result.Length = 0;
								}
								break;
							} else {
								// append normal segment
								goto default;
							}
						default:
							if (result.Length > 0) result.Append(outputSeparator);
							result.Append(fileName, segmentStartPos, segmentLength);
							break;
				}
					segmentStartPos = i + 1; // remember start position for next segment
				}
			}
			if (isWeb == false) {
				if (isRelative) {
					for (int j = 0; j < levelsBack; j++) {
						result.Insert(0, ".." + outputSeparator);
					}
				}
				if (result.Length > 0 && result[result.Length - 1] == outputSeparator) {
					result.Length -= 1;
				}
				if (result.Length == 2 && result[1] == ':') {
					result.Append(outputSeparator);
				}
				if (result.Length == 0)
					return ".";
			}
			return result.ToString();
		}
	}
}
