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
using System.Linq;
using ICSharpCode.TreeView;
using Usalizer.Analysis;

namespace Usalizer.TreeNodes
{
	public class DelphiFileTreeNode : SharpTreeNode
	{
		DelphiFile file;

		public DelphiFileTreeNode(DelphiFile file)
		{
			if (file == null)
				throw new ArgumentNullException("file");
			this.file = file;
			
			Children.Add(new UsesTreeNode(file, UsesSection.Both));
			Children.Add(new UsedByTreeNode(file, UsesSection.Both));
		}
		
		public override object Text {
			get { return file.UnitName; }
		}
		
		public override void ActivateItem(System.Windows.RoutedEventArgs e)
		{
			Window1.BrowseUnit(file);
		}
	}
	
	public class ResultTreeNode : SharpTreeNode
	{
		DelphiFile file;
		bool firstLevel;

		public bool FirstLevel {
			get {
				return firstLevel;
			}
			set {
				firstLevel = value;
			}
		}
		
		public ResultTreeNode(DelphiFile file, bool firstLevel = false)
		{
			if (file == null)
				throw new ArgumentNullException("file");
			this.file = file;
			this.firstLevel = firstLevel;
		}
		
		public override object Text {
			get { return (firstLevel ? "" : "used by ") + file.UnitName; }
		}
		
		public override void ActivateItem(System.Windows.RoutedEventArgs e)
		{
			Window1.BrowseUnit(file);
		}
	}
	
	public class PackageTreeNode : SharpTreeNode
	{
		Package package;

		public PackageTreeNode(Package package)
		{
			if (package == null)
				throw new ArgumentNullException("package");
			this.package = package;
		}
		
		public override object Text {
			get { return "Package " + package.PackageName; }
		}

		public Package Package {
			get {
				return package;
			}
		}
		
		public override void ActivateItem(System.Windows.RoutedEventArgs e)
		{
			Window1.BrowseUnit(package);
		}
	}
	
	static class Utils
	{
		public static string GetSectionText(this UsesSection section)
		{
			switch (section) {
				case UsesSection.Interface:
					return " (interface)";
				case UsesSection.Implementation:
					return " (implementation)";
			}
			return "";
		}
	}
}
