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
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Usalizer.Analysis;

namespace Usalizer
{
	class InfoAdorner : Adorner
	{
		readonly DelphiAnalysis analysis;

		public InfoAdorner(TabControl target, DelphiAnalysis analysis) : base(target)
		{
			if (analysis == null)
				throw new ArgumentNullException("analysis");
			this.analysis = analysis;
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			string text = "Units: " + analysis.UnitCount + " (" + analysis.UnusedUnits.Count + " unused)    Packages: " + analysis.PackageCount;
			var tabControl = (TabControl)AdornedElement;
			var font = new Typeface(tabControl.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
			var formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, font, tabControl.FontSize, Brushes.Black);
			var position = new Point(tabControl.ActualWidth - formattedText.WidthIncludingTrailingWhitespace - 40, 3);
			drawingContext.DrawText(formattedText, position);
		}
	}
}

