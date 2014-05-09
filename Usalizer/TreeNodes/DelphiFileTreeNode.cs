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
			get { return (current > 0 ? "used by " : "") + node.UnitName + (paths.Any(path => current == path.Length - 1) ? " (directly in package)" : ""); }
		}

		public DelphiFile Node {
			get {
				return node;
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
		
		public override void ActivateItem(System.Windows.RoutedEventArgs e)
		{
			Window1.BrowseUnit(node);
		}
	}
	
	public class PackageTreeNode : SharpTreeNode
	{
		readonly Package package;
		readonly List<Tuple<DelphiFile, Dictionary<DelphiFile, DelphiFile>>> results = new List<Tuple<DelphiFile, Dictionary<DelphiFile, DelphiFile>>>();
		
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
				var currentFile = result.Item1;
				DelphiFile parentFile;
				path.Add(currentFile);
				while (result.Item2.TryGetValue(currentFile, out parentFile)) {
					currentFile = parentFile;
					path.Add(currentFile);
				}
				path.Reverse();
				int current = Math.Min(1, path.Count - 1);
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

		public Package Package {
			get {
				return package;
			}
		}
		
		public void AddResult(DelphiFile endPoint, Dictionary<DelphiFile, DelphiFile> parents)
		{
			results.Add(Tuple.Create(endPoint, parents));
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
		
		/// <summary>
		/// Inserts an item into a sorted list.
		/// </summary>
		public static void OrderedInsert<T>(this IList<T> list, T item, IComparer<T> comparer)
		{
			int pos = BinarySearch(list, item, x => x, comparer);
			if (pos < 0)
				pos = ~pos;
			list.Insert(pos, item);
		}
		
		/// <summary>
		/// Searches a sorted list
		/// </summary>
		/// <param name="list">The list to search in</param>
		/// <param name="key">The key to search for</param>
		/// <param name="keySelector">Function that maps list items to their sort key</param>
		/// <param name="keyComparer">Comparer used for the sort</param>
		/// <returns>Returns the index of the element with the specified key.
		/// If no such element is found, this method returns a negative number that is the bitwise complement of the
		/// index where the element could be inserted while maintaining the order.</returns>
		public static int BinarySearch<T, K>(this IList<T> list, K key, Func<T, K> keySelector, IComparer<K> keyComparer = null)
		{
			return BinarySearch(list, 0, list.Count, key, keySelector, keyComparer);
		}
		
		/// <summary>
		/// Searches a sorted list
		/// </summary>
		/// <param name="list">The list to search in</param>
		/// <param name="index">Starting index of the range to search</param>
		/// <param name="length">Length of the range to search</param>
		/// <param name="key">The key to search for</param>
		/// <param name="keySelector">Function that maps list items to their sort key</param>
		/// <param name="keyComparer">Comparer used for the sort</param>
		/// <returns>Returns the index of the element with the specified key.
		/// If no such element is found in the specified range, this method returns a negative number that is the bitwise complement of the
		/// index where the element could be inserted while maintaining the order.</returns>
		public static int BinarySearch<T, K>(this IList<T> list, int index, int length, K key, Func<T, K> keySelector, IComparer<K> keyComparer = null)
		{
			if (keyComparer == null)
				keyComparer = Comparer<K>.Default;
			int low = index;
			int high = index + length - 1;
			while (low <= high) {
				int mid = low + (high - low >> 1);
				int r = keyComparer.Compare(keySelector(list[mid]), key);
				if (r == 0) {
					return mid;
				} else if (r < 0) {
					low = mid + 1;
				} else {
					high = mid - 1;
				}
			}
			return ~low;
		}
	}
	
	public static class KeyComparer
	{
		public static KeyComparer<TElement, TKey> Create<TElement, TKey>(Func<TElement, TKey> keySelector)
		{
			return new KeyComparer<TElement, TKey>(keySelector, Comparer<TKey>.Default, EqualityComparer<TKey>.Default);
		}
		
		public static KeyComparer<TElement, TKey> Create<TElement, TKey>(Func<TElement, TKey> keySelector, IComparer<TKey> comparer, IEqualityComparer<TKey> equalityComparer)
		{
			return new KeyComparer<TElement, TKey>(keySelector, comparer, equalityComparer);
		}
		
		public static IComparer<TElement> Create<TElement, TKey>(Func<TElement, TKey> keySelector, IComparer<TKey> comparer)
		{
			return new KeyComparer<TElement, TKey>(keySelector, comparer, EqualityComparer<TKey>.Default);
		}
		
		public static IEqualityComparer<TElement> Create<TElement, TKey>(Func<TElement, TKey> keySelector, IEqualityComparer<TKey> equalityComparer)
		{
			return new KeyComparer<TElement, TKey>(keySelector, Comparer<TKey>.Default, equalityComparer);
		}
	}
	
	public class KeyComparer<TElement, TKey> : IComparer<TElement>, IEqualityComparer<TElement>
	{
		readonly Func<TElement, TKey> keySelector;
		readonly IComparer<TKey> keyComparer;
		readonly IEqualityComparer<TKey> keyEqualityComparer;
		
		public KeyComparer(Func<TElement, TKey> keySelector, IComparer<TKey> keyComparer, IEqualityComparer<TKey> keyEqualityComparer)
		{
			if (keySelector == null)
				throw new ArgumentNullException("keySelector");
			if (keyComparer == null)
				throw new ArgumentNullException("keyComparer");
			if (keyEqualityComparer == null)
				throw new ArgumentNullException("keyEqualityComparer");
			this.keySelector = keySelector;
			this.keyComparer = keyComparer;
			this.keyEqualityComparer = keyEqualityComparer;
		}
		
		public int Compare(TElement x, TElement y)
		{
			return keyComparer.Compare(keySelector(x), keySelector(y));
		}
		
		public bool Equals(TElement x, TElement y)
		{
			return keyEqualityComparer.Equals(keySelector(x), keySelector(y));
		}
		
		public int GetHashCode(TElement obj)
		{
			return keyEqualityComparer.GetHashCode(keySelector(obj));
		}
	}
}
