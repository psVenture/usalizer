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
using System.Diagnostics;
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
			Children.Add(new DirectlyInPackageTreeNode(file));
		}
		
		public override object Text {
			get { return file.UnitName; }
		}
		
		public override void ActivateItem(System.Windows.RoutedEventArgs e)
		{
			Window1.BrowseUnit(file);
		}
	}
	
	public class DirectlyInPackageTreeNode : SharpTreeNode
	{
		DelphiFile file;
		
		public DirectlyInPackageTreeNode(DelphiFile file)
		{
			if (file == null)
				throw new ArgumentNullException("file");
			this.file = file;
			this.LazyLoading = true;
		}
		
		public override object Text {
			get { return "directly in package"; }
		}
		
		protected override void LoadChildren()
		{
			foreach (var p in file.DirectlyInPackages) {
				Children.Add(new PackageTreeNode(p));
			}
		}
	}
	
	public class NoResultTreeNode : SharpTreeNode
	{
		string text;
		
		public NoResultTreeNode(string text)
		{
			this.text = text;
		}
		
		public override object Text {
			get {
				return "Search term '" + text + "' yielded no results!";
			}
		}
	}
	
	public class PathTreeNode : SharpTreeNode
	{
		readonly List<DelphiFile[]> paths = new List<DelphiFile[]>();
		readonly int current;
		readonly DelphiFile node;
		
		public PathTreeNode(DelphiFile[] path, int current)
		{
			if (path == null)
				throw new ArgumentNullException("path");
			if (path.Length <= current)
				throw new ArgumentException();
			Debug.Assert(current > 0);
			this.paths.Add(path);
			this.current = current;
			this.node = path[current];
			this.LazyLoading = current < path.Length - 1;
		}
		
		public override object Text {
			get { return (current > 0 ? "used by " : "") + node.UnitName + (paths.Any(path => current == path.Length - 1) ? " (directly in package)" : ""); }
		}

		public DelphiFile Node {
			get {
				return node;
			}
		}
		
		void PrintPath(DelphiFile[] path)
		{
			foreach (var element in path) {
				Console.Write(element + " > ");
			}
			Console.WriteLine();
		}
		
		public void AddPath(DelphiFile[] path)
		{
			Console.WriteLine("AddPath:");
			PrintPath(path);
			this.paths.Add(path);
			if (!IsVisible) {
				Children.Clear();
				LazyLoading = true;
				return;
			}
			UpdateChildren(path);
			RaisePropertyChanged("Text");
		}

		void UpdateChildren(DelphiFile[] path)
		{
			var nextNode = path.ElementAtOrDefault(current + 1);
			if (nextNode == null)
				return;
			var pathTreeNode = Children.OfType<PathTreeNode>().FirstOrDefault(n => n.node == nextNode);
			if (pathTreeNode == null) {
				pathTreeNode = new PathTreeNode(path, current + 1);
				Children.Add(pathTreeNode);
				Console.WriteLine(current + ": new node for " + nextNode);
			} else {
				pathTreeNode.AddPath(path);
				Console.WriteLine(current + ": add path " + nextNode);
			}
		}
		
		protected override void LoadChildren()
		{
			foreach (var path in paths) {
				UpdateChildren(path);
			}
		}
		
		public override void ActivateItem(System.Windows.RoutedEventArgs e)
		{
			Window1.BrowseUnit(node);
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
				path.Reverse();
				int current = Math.Min(1, path.Count - 1);
				PathTreeNode pathTreeNode = Children.OfType<PathTreeNode>().FirstOrDefault(n => n.Node == path[current]);
				var array = path.ToArray();
				if (pathTreeNode == null) {
					pathTreeNode = new PathTreeNode(array, current);
					Children.Add(pathTreeNode);
				} else {
					pathTreeNode.AddPath(array);
				}
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
