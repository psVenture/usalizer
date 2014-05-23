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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Search;
using ICSharpCode.TreeView;
using Usalizer.Analysis;
using Usalizer.TreeNodes;

namespace Usalizer
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class Window1 : Window, IProgress<Tuple<string, double, bool>>
	{
		public Window1()
		{
			InitializeComponent();
			SearchPanel.Install(codeBrowser);
			try {
				LoadSettings();
			// disable once EmptyGeneralCatchClause
			} catch (Exception) {
				// ignore any exceptions when loading settings
			}
		}
		
		readonly object progressLock = new object();
		
		static DelphiAnalysis currentAnalysis;
		
		public static DelphiAnalysis CurrentAnalysis {
			get {
				return currentAnalysis;
			}
		}
		
		public static void BrowseUnit(string fileName)
		{
			Window1 current = Application.Current.MainWindow as Window1;
			if (current == null)
				return;
			
			current.codeBrowser.Document.Text = File.ReadAllText(fileName);
			current.resultsView.SelectedIndex = 2;
		}
		
		void StartClick(object sender, RoutedEventArgs e)
		{
			string path = baseDirectory.Text;
			if (!Directory.Exists(path)) {
				MessageBox.Show(this, path + " does not exist!");
				return;
			}
			
			string packagePath = packageDir.Text;
			if (!Directory.Exists(packagePath)) {
				MessageBox.Show(this, packagePath + " does not exist!");
				return;
			}
			
			startButton.IsEnabled = false;
			SetProgressIndeterminate("Preparing analysis...", true);
			resultsView.Visibility = Visibility.Hidden;
			progressView.Visibility = Visibility.Visible;
			
			string[] symbols = directives.Text.Split(',', ';')
				.Select(s => s.Trim().ToUpperInvariant()).ToArray();
			
			var cancellation = new CancellationTokenSource();
			
			currentAnalysis = new DelphiAnalysis(path, packagePath, symbols, this);
			currentAnalysis.PrepareAnalysis(cancellation.Token)
				.ContinueWith(t => t.Result.Analyse(cancellation.Token))
				.ContinueWith(t => {
				              	Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate {
				              	                       	if (t.Exception != null)
				              	                       		MessageBox.Show(t.Exception.ToString());
				              	                       	resultsView.Visibility = Visibility.Visible;
				              	                       	progressView.Visibility = Visibility.Hidden;
				              	                       	unitCount.Content = "Units: " + currentAnalysis.UnitCount;
				              	                       	packageCount.Content = "Packages: " + currentAnalysis.PackageCount;
				              	                       	startButton.IsEnabled = true;
				              	                       });
				              });
		}
		
		void SetProgressIndeterminate(string text, bool isIndeterminate)
		{
			progress.IsIndeterminate = isIndeterminate;
			progress.Value = 0;
			progressText.Text = text;
		}
		
		void IProgress<Tuple<string, double, bool>>.Report(Tuple<string, double, bool> value)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate {
			                       	lock (progressLock) {
			                       		progress.IsIndeterminate = false;
			                       		progressText.Text = value.Item1;
			                       		if (value.Item3)
			                       			progress.Value = value.Item2;
			                       		else
			                       			progress.Value += value.Item2;
			                       	}
			                       });
		}

		void FilterNodes(string text)
		{
			var root = new SharpTreeNode();
			try {
				foreach (var result in currentAnalysis.FindPartialName(text)) {
					root.Children.Add(SearchReferences(result));
				}
				if (root.Children.Count == 0) {
					root.Children.Add(new NoResultTreeNode(text));
				}
			} finally {
				resultsTree.Root = root;
			}
		}
		
		
		public static void AnalyzeThis(DelphiFile result)
		{
			var root = new SharpTreeNode();
			try {
				root.Children.Add(SearchReferences(result));
			} finally {
				((Window1)Application.Current.MainWindow).resultsTree.Root = root;
			}
		}

		static SharpTreeNode SearchReferences(DelphiFile result)
		{
			Dictionary<DelphiFile, DelphiFile> parent;
			var endPoints = currentAnalysis.FindContainingPackages(result, out parent);
			var resultTreeNode = new DelphiFileTreeNode(result);
			foreach (var endPoint in endPoints) {
				foreach (var package in endPoint.DirectlyInPackages) {
					var p = package;
					var packageNode = resultTreeNode.Children.OfType<PackageTreeNode>().FirstOrDefault(n => n.Package == p);
					if (packageNode == null) {
						packageNode = new PackageTreeNode(package, result);
						resultTreeNode.Children.OrderedInsert(packageNode, PathTreeNode.NodeTextComparer, 2);
					}
					packageNode.AddResult(endPoint, parent);
				}
			}
			return resultTreeNode;
		}
		
		void TreeViewSearchBoxKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
				FilterNodes(((TextBox)sender).Text);
		}
		
		void SearchClick(object sender, System.Windows.RoutedEventArgs e)
		{
			FilterNodes(searchText.Text);
		}

		void LoadSettings()
		{
			if (!File.Exists("settings.xml")) return;
			XDocument settings = XDocument.Load("settings.xml");
			var value = settings.Root.Element("location").Value;
			Point location;
			if (Utils.TryParse(value, out location)) {
				Left = location.X;
				Top = location.Y;
			}
			value = settings.Root.Element("size").Value;
			Size size;
			if (Utils.TryParse(value, out size)) {
				Width = size.Width;
				Height = size.Height;
			}
			value = settings.Root.Element("windowstate").Value;
			WindowState state;
			if (Enum.TryParse(value, out state))
				this.WindowState = state;
			baseDirectory.Text = settings.Root.Element("baseDirectory").Value;
			packageDir.Text = settings.Root.Element("packageDir").Value;
			directives.Text = settings.Root.Element("directives").Value;
		}

		void SaveSettings()
		{
			XDocument settings = XDocument.Parse("<settings></settings>");
			
			settings.Root.Add(new XElement("location", new Point(Left, Top).ToString(CultureInfo.InvariantCulture)));
			settings.Root.Add(new XElement("size", new Size(ActualWidth, ActualHeight).ToString(CultureInfo.InvariantCulture)));
			settings.Root.Add(new XElement("windowstate", WindowState));
			settings.Root.Add(new XElement("baseDirectory", baseDirectory.Text));
			settings.Root.Add(new XElement("packageDir", packageDir.Text));
			settings.Root.Add(new XElement("directives", directives.Text));
			settings.Save("settings.xml");
		}

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			SaveSettings();
			base.OnClosing(e);
		}
	}
}