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

namespace Usalizer.Analysis
{
	/// <summary>
	/// [Namespace.]Name [in 'InLocation']
	/// </summary>
	public class UsesClause
	{
		public DelphiFile ParentFile { get; private set; }
		public DelphiFile TargetFile { get; private set; }
		
		public string Name { get; private set; }
		public string Namespace { get; private set; }
		public string InLocation { get; private set; }
		
		public UsesClause(DelphiFile file, string name, string @namespace = null, string inLocation = null)
		{
			if (file == null)
				throw new ArgumentNullException("file");
			this.ParentFile = file;
			this.Name = name;
			this.Namespace = @namespace;
			this.InLocation = inLocation;
		}
		
		public void Resolve(DelphiAnalysis container)
		{
			TargetFile = container.ResolveUnitName(ParentFile.FileName, Name);
			// TODO : split 'Resolve' and add to list and parallelise
			if (TargetFile != null) {
				TargetFile.UsedByFiles.Add(ParentFile);
				ParentFile.UsesFiles.Add(TargetFile);
			}
		}
		
		public override string ToString()
		{
			return string.Format("[UsesClause Name={0}, Namespace={1}, InLocation={2}]", Name, Namespace, InLocation);
		}
	}
}

