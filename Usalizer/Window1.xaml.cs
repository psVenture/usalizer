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
using ICSharpCode.AvalonEdit.Document;
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
		}
		
		readonly object progressLock = new object();
		
		static DelphiAnalysis currentAnalysis;
		
		public static DelphiAnalysis CurrentAnalysis {
			get {
				return currentAnalysis;
			}
		}
		
		public static void BrowseUnit(DelphiFile file)
		{
			Window1 current = Application.Current.MainWindow as Window1;
			if (current == null)
				return;
			
			current.codeBrowser.Document.Text = File.ReadAllText(file.Location);
			current.resultsView.SelectedIndex = 2;
		}
		
		public static void BrowseUnit(Package file)
		{
			Window1 current = Application.Current.MainWindow as Window1;
			if (current == null)
				return;
			
			current.codeBrowser.Document.Text = File.ReadAllText(file.Location);
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
					Dictionary<DelphiFile, DelphiFile> parent;
					var endPoints = currentAnalysis.FindContainingPackages(result, out parent);
					var resultTreeNode = new ResultTreeNode(result);
					foreach (var endPoint in endPoints) {
						foreach (var package in endPoint.DirectlyInPackages) {
							var p = package;
							var packageNode = resultTreeNode.Children.OfType<PackageTreeNode>().FirstOrDefault(n => n.Package == p);
							if (packageNode == null) {
								packageNode = new PackageTreeNode(package);
								resultTreeNode.Children.Add(packageNode);
							}
							packageNode.Results.Add(new ResultInfo { endPoint = endPoint, parent = parent });
						}
					}
					root.Children.Add(resultTreeNode);
				}
			} finally {
				resultsTree.Root = root;
			}
		}
		
		void TreeViewSearchBoxKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Enter) return;
			FilterNodes(((TextBox)sender).Text);
		}
		
		void ParseTextClick(object sender, System.Windows.RoutedEventArgs e)
		{
			using (TextReader reader = new StringReader(test.Document.Text)) {
				DelphiTokenizer tokenizer = new DelphiTokenizer(reader);
				Token t;
				while ((t = tokenizer.Next()).Kind != TokenKind.EOF)
					Console.WriteLine(t);
			}
		}
		
		void SearchClick(object sender, System.Windows.RoutedEventArgs e)
		{
			FilterNodes(searchText.Text);
		}
	}
}