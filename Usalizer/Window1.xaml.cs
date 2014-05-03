// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
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
		
		void StartClick(object sender, RoutedEventArgs e)
		{
			string path = baseDirectory.Text;
			if (!Directory.Exists(path)) {
				MessageBox.Show(this, path + " does not exist!");
				return;
			}
			
			startButton.IsEnabled = false;
			SetProgress("Preparing analysis...", true);
			resultsView.Visibility = Visibility.Hidden;
			progressView.Visibility = Visibility.Visible;
			
			string[] symbols = directives.Text.Split(',', ';')
				.Select(s => s.Trim().ToUpperInvariant()).ToArray();
			
			var cancellation = new CancellationTokenSource();
			
			var analysis = new DelphiAnalysis(path, symbols, this);
			analysis.PrepareAnalysis(cancellation.Token)
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
		
		void SetProgress(string text, bool isIndeterminate)
		{
			progress.IsIndeterminate = isIndeterminate;
			progress.Value = 0;
			progressText.Text = text;
		}
		
		#region IProgress implementation
		
		public void Report(Tuple<string, double> value)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate {
				lock (progressLock) {
					progress.IsIndeterminate = false;
					progressText.Text = value.Item1;
					progress.Value += value.Item2;
				}
			});
		}
		
		#endregion
	}
	
	class DelphiAnalysis
	{
		string path;
		string[] symbols;
		Dictionary<string, DelphiFile> allUnits;
		string[] pasFiles;
		string[] incFiles;
		readonly IProgress<Tuple<string, double>> progress;
		
		public DelphiAnalysis(string path, string[] symbols, IProgress<Tuple<string, double>> progress)
		{
			this.path = path;
			this.symbols = symbols;
			this.progress = progress;
		}
		
		public Task<DelphiAnalysis> PrepareAnalysis(CancellationToken cancellation = default(CancellationToken))
		{
			return Task.Run(() => {
				pasFiles = new DirectoryInfo(path)
					.EnumerateFiles("*.pas", SearchOption.AllDirectories)
					.Select(f => f.FullName).ToArray();
				if (cancellation.IsCancellationRequested)
					throw new OperationCanceledException();
				incFiles = new DirectoryInfo(path)
					.EnumerateFiles("*.inc", SearchOption.AllDirectories)
					.Select(f => f.FullName).ToArray();
				if (cancellation.IsCancellationRequested)
					throw new OperationCanceledException();
				return this;
			});
		}
		
		public void Analyse(CancellationToken cancellation = default(CancellationToken))
		{
			#if DEBUG
			foreach (var f in pasFiles) MakeFile(Stream(f, symbols, incFiles, pasFiles), f);
			#else
			Parallel.ForEach(pasFiles, f => MakeFile(Stream(f, symbols, incFiles, pasFiles), f));
			#endif
		}
		
		IEnumerable<Token> StreamSingleFile(string fileName)
		{
			using (StreamReader reader = new StreamReader(fileName)) {
				DelphiTokenizer tokenizer = new DelphiTokenizer(reader);
				Token t;
				while ((t = tokenizer.Next()).Kind != TokenKind.EOF)
					yield return t;
			}
		}
		
		IEnumerable<Token> Stream(string fileName, string[] symbols, string[] incFiles, string[] pasFiles)
		{
			foreach (var token in StreamSingleFile(fileName)) {
				if (token.Kind == TokenKind.Directive) {
					string[] parameters;
					var directiveKind = ParseDirective(token.Value, out parameters);
				} else {
					yield return token;
				}
			}
		}
		
		DirectiveKind ParseDirective(string value, out string[] parameters)
		{
			parameters = new string[0];
			return DirectiveKind.Include;
		}
		
		DelphiFile MakeFile(IEnumerable<Token> tokens, string fileName)
		{
			string unitName = null;
			Console.WriteLine("file: " + fileName);
			Token prev = null;
			foreach (var t in tokens) {
				Console.WriteLine(t);
				switch (t.Kind) {
					case TokenKind.Identifier:
						if (prev != null && prev.Kind == TokenKind.Keyword && string.Equals(prev.Value, "unit", StringComparison.OrdinalIgnoreCase))
							unitName = t.Value;
						break;
				}
				prev = t;
			}
			progress.Report(Tuple.Create(fileName, 1.0 / pasFiles.Length));
			if (unitName == null)
				return null;
			return new DelphiFile(unitName, fileName);
		}
	}
}