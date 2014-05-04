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
using Usalizer.Analysis;
using Usalizer.TreeNodes;

namespace Usalizer
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class Window1 : Window, IProgress<Tuple<string, double>>
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
		void StartClick(object sender, RoutedEventArgs e)
		{
			string path = baseDirectory.Text;
			if (!Directory.Exists(path)) {
				MessageBox.Show(this, path + " does not exist!");
				return;
			}
			
			startButton.IsEnabled = false;
			SetProgressIndeterminate("Preparing analysis...", true);
			resultsView.Visibility = Visibility.Hidden;
			progressView.Visibility = Visibility.Visible;
			
			string[] symbols = directives.Text.Split(',', ';')
				.Select(s => s.Trim().ToUpperInvariant()).ToArray();
			
			var cancellation = new CancellationTokenSource();
			
			currentAnalysis = new DelphiAnalysis(path, symbols, this);
			currentAnalysis.PrepareAnalysis(cancellation.Token)
				.ContinueWith(t => t.Result.Analyse(cancellation.Token))
				.ContinueWith(t => {
					Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate {
						if (t.Exception != null)
							MessageBox.Show(t.Exception.ToString());
						PopulateTreeView(currentAnalysis);
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
		
		void IProgress<Tuple<string, double>>.Report(Tuple<string, double> value)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate {
				lock (progressLock) {
					progress.IsIndeterminate = false;
					progressText.Text = value.Item1;
					progress.Value += value.Item2;
				}
			});
		}
		
		void TreeViewSearchBoxKeyDown(object sender, KeyEventArgs e)
		{
			throw new NotImplementedException();
		}

		void PopulateTreeView(DelphiAnalysis analysis)
		{
			resultsTree.Root = new ICSharpCode.TreeView.SharpTreeNode();
			resultsTree.Root.Children.AddRange(analysis.AllUnits.OrderBy(p => p.Key).Select(p => new DelphiFileTreeNode(p.Value)));
		}
	}
}