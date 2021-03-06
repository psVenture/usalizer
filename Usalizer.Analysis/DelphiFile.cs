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
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Usalizer.Analysis
{
	public class DelphiFile
	{
		public string UnitName { get; private set; }
		public string FileName { get; private set; }
		public string Location { get { return Path.GetDirectoryName(FileName); } }
		public List<UsesClause> InterfaceUses { get; private set; }
		public List<UsesClause> ImplementationUses { get; private set; }
		
		public List<DelphiFile> UsesFiles { get; private set; }
		public List<DelphiFile> UsedByFiles { get; private set; }
		public List<Package> DirectlyInPackages { get; private set; }
		
		public IEnumerable<UsesClause> Uses {
			get {
				return InterfaceUses.Concat(ImplementationUses);
			}
		}
		
		public DelphiFile(string unitName, string location)
		{
			this.UnitName = unitName;
			this.FileName = location;
			this.ImplementationUses = new List<UsesClause>();
			this.InterfaceUses = new List<UsesClause>();
			this.UsedByFiles = new List<DelphiFile>();
			this.UsesFiles = new List<DelphiFile>();
			this.DirectlyInPackages = new List<Package>();
		}
		
		public override string ToString()
		{
			return string.Format("[DelphiFile UnitName={0}, Location={1}]", UnitName, FileName);
		}
	}
	
	public class Package
	{
		public string PackageName { get; private set; }
		public string Location { get; private set; }
		public List<DelphiFile> ContainingUnits { get; private set; }
		public List<DelphiFile> ImplicitUses { get; private set; }
		
		public Package(string packageName, string location)
		{
			this.PackageName = packageName;
			this.Location = location;
			this.ContainingUnits = new List<DelphiFile>();
			this.ImplicitUses = new List<DelphiFile>();
		}
		
		public override string ToString()
		{
			return string.Format("[Package PackageName={0}, Location={1}]", PackageName, Location);
		}
	}
}