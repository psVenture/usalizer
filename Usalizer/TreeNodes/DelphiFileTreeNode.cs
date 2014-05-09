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
		
		public ResultTreeNode(DelphiFile file)
		{
			if (file == null)
				throw new ArgumentNullException("file");
			this.file = file;
		}
		
		public override object Text {
			get { return file.UnitName; }
		}
		
		public override void ActivateItem(System.Windows.RoutedEventArgs e)
		{
			Window1.BrowseUnit(file);
		}
	}
	
	public class PathTreeNode : SharpTreeNode
	{
		readonly DelphiFile[] path;
		readonly int current;
		
		public PathTreeNode(DelphiFile[] path, int current)
		{
			if (path == null)
				throw new ArgumentNullException("path");
			if (path.Length <= current)
				throw new ArgumentException();
			this.path = path;
			this.current = current;
			this.LazyLoading = true;
		}
		
		public override object Text {
			get { return (current > 0 ? "used by " : "") + path[current].UnitName + (current == 0 ? " (directly in Package)" : ""); }
		}
		
		protected override void LoadChildren()
		{
			Children.Add(new PathTreeNode(path, current + 1));
		}
		
		public override void ActivateItem(System.Windows.RoutedEventArgs e)
		{
			Window1.BrowseUnit(path[current]);
		}
	}
	
	public class ResultInfo
	{
		public DelphiFile endPoint;
		public Dictionary<DelphiFile, DelphiFile> parent;
	}
	
	public class PackageTreeNode : SharpTreeNode
	{
		Package package;
		List<ResultInfo> results = new List<ResultInfo>();

		public List<ResultInfo> Results {
			get {
				return results;
			}
		}
		
		public PackageTreeNode(Package package)
		{
			if (package == null)
				throw new ArgumentNullException("package");
			this.package = package;
			LazyLoading = true;
		}
		
		public override object Text {
			get { return "Package " + package.PackageName; }
		}

		protected override void LoadChildren()
		{
			foreach (var result in results) {
				List<DelphiFile> path = new List<DelphiFile>();
				var currentFile = result.endPoint;
				DelphiFile parentFile;
				path.Add(currentFile);
				while (result.parent.TryGetValue(currentFile, out parentFile)) {
					currentFile = parentFile;
					path.Add(currentFile);
				}
				Children.Add(new PathTreeNode(path.ToArray(), 0));
			}
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
