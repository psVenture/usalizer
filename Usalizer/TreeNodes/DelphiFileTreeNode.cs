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
using System.Windows.Controls;
using System.Windows.Media;
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
		
		public override void ShowContextMenu(ContextMenuEventArgs e)
		{
			new ContextMenu {
				ItemsSource = new List<MenuItem> {
					MenuCommands.CreateBrowseCode(file.Location),
					MenuCommands.CreateAnalyzeThis(file)
				},
				IsOpen = true
			};
		}
		
		public override Brush Foreground {
			get {
				return file.DirectlyInPackages.Count > 0 ? Brushes.Green : Brushes.Red;
			}
		}
	}
	
	public class ResultTreeNode : DelphiFileTreeNode
	{
		public ResultTreeNode(DelphiFile result)
			: base(result)
		{
		}
		
		public static readonly IComparer<SharpTreeNode> PackageBuildOrderComparer
			= KeyComparer.Create((SharpTreeNode n) => n.Text.ToString(), StringComparer.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase);
		

		public void AddResult(DelphiFile endPoint, DelphiFile result, Dictionary<DelphiFile, DelphiFile> parent)
		{
			foreach (var package in endPoint.DirectlyInPackages) {
				var p = package;
				var packageNode = Children.OfType<PackageTreeNode>().FirstOrDefault(n => n.Package == p);
				if (packageNode == null) {
					packageNode = new PackageTreeNode(p, result);
					Children.OrderedInsert(packageNode, PathTreeNode.NodeTextComparer, 2);
				}
				packageNode.AddResult(endPoint, parent);
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
		
		public static readonly IComparer<SharpTreeNode> NodeTextComparer = KeyComparer.Create((SharpTreeNode n) => n.Text.ToString(), StringComparer.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase);
		
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
			get { return (current > 0 ? "used by " : "") + node.UnitName; }
		}

		public DelphiFile Node {
			get {
				return node;
			}
		}
		
		public override Brush Foreground {
			get {
				return node.DirectlyInPackages.Count > 0 ? Brushes.Green : Brushes.Red;
			}
		}
		
		public void AddPath(DelphiFile[] path)
		{
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
				Children.OrderedInsert(pathTreeNode, NodeTextComparer);
			} else {
				pathTreeNode.AddPath(path);
			}
		}
		
		protected override void LoadChildren()
		{
			foreach (var path in paths) {
				UpdateChildren(path);
			}
		}
		
		public override void ShowContextMenu(ContextMenuEventArgs e)
		{
			new ContextMenu {
				ItemsSource = new List<MenuItem> {
					MenuCommands.CreateBrowseCode(node.Location),
					MenuCommands.CreateAnalyzeThis(node)
				},
				IsOpen = true
			};
		}
		
	}
	
	public class PackageTreeNode : SharpTreeNode
	{
		readonly DelphiFile target;
		readonly Package package;
		readonly List<Tuple<DelphiFile, Dictionary<DelphiFile, DelphiFile>>> results = new List<Tuple<DelphiFile, Dictionary<DelphiFile, DelphiFile>>>();
		
		public PackageTreeNode(Package package, DelphiFile target = null)
		{
			if (package == null)
				throw new ArgumentNullException("package");
			this.package = package;
			this.target = target;
			LazyLoading = true;
		}
		
		public override object Text {
			get { return "Package " + package.PackageName; }
		}

		protected override void LoadChildren()
		{
			foreach (var result in results) {
				List<DelphiFile> path = new List<DelphiFile>();
				var currentFile = result.Item1;
				DelphiFile parentFile;
				path.Add(currentFile);
				while (result.Item2.TryGetValue(currentFile, out parentFile)) {
					currentFile = parentFile;
					if (currentFile == target)
						break;
					Debug.Assert(!path.Contains(currentFile), "unresolved loop found!");
					path.Add(currentFile);
				}
				path.Reverse();
				int current = Math.Min(1, path.Count - 1);
				if (current > 0) { // currently ignore paths that have only one node
					PathTreeNode pathTreeNode = Children.OfType<PathTreeNode>().FirstOrDefault(n => n.Node == path[current]);
					var array = path.ToArray();
					if (pathTreeNode == null) {
						pathTreeNode = new PathTreeNode(array, current);
						Children.OrderedInsert(pathTreeNode, PathTreeNode.NodeTextComparer);
					} else {
						pathTreeNode.AddPath(array);
					}
				}
			}
		}

		public Package Package {
			get {
				return package;
			}
		}
		
		public void AddResult(DelphiFile endPoint, Dictionary<DelphiFile, DelphiFile> parents)
		{
			Debug.Assert(target != null);
			results.Add(Tuple.Create(endPoint, parents));
		}
		
		public override Brush Foreground {
			get {
				if (target == null)
					return base.Foreground;
				return target.DirectlyInPackages.Contains(package) ? Brushes.Green : Brushes.Red;
			}
		}
		
		public override void ShowContextMenu(System.Windows.Controls.ContextMenuEventArgs e)
		{
			new ContextMenu {
				ItemsSource = new List<MenuItem> {
					MenuCommands.CreateBrowseCode(package.Location)
				},
				IsOpen = true
			};
		}
	}
	
	public static class MenuCommands
	{
		public static MenuItem CreateBrowseCode(string path)
		{
			var item = new MenuItem {
				Header = "Open in Code Browser"
			};
			item.Click += (sender, e) => Window1.BrowseUnit(path);
			return item;
		}
		
		public static MenuItem CreateAnalyzeThis(DelphiFile file)
		{
			if (file == null)
				throw new ArgumentNullException("file");
			var item = new MenuItem {
				Header = "Analyze this!"
			};
			item.Click += (sender, e) => Window1.AnalyzeThis(file);
			return item;
		}
	}
}
