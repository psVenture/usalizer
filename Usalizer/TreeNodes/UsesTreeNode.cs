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
	public class UsesTreeNode : SharpTreeNode
	{
		DelphiFile file;

		UsesSection section;

		public UsesTreeNode(DelphiFile file, UsesSection section)
		{
			if (file == null)
				throw new ArgumentNullException("file");
			this.file = file;
			this.section = section;
			this.LazyLoading = true;
		}

		public override object Text {
			get {
				return "uses" + section.GetSectionText();
			}
		}

		protected override void LoadChildren()
		{
			IEnumerable<UsesClause> source = Enumerable.Empty<UsesClause>();
			switch (section) {
				case UsesSection.Both:
					source = file.InterfaceUses.Concat(file.ImplementationUses);
					break;
				case UsesSection.Interface:
					source = file.InterfaceUses;
					break;
				case UsesSection.Implementation:
					source = file.ImplementationUses;
					break;
			}
			Children.AddRange(source.OrderBy(c => c.Name).Select(c =>  {
				var resolved = Window1.CurrentAnalysis.ResolveUnitName(file.FileName, c.Name, c.InLocation);
				SharpTreeNode node;
				if (resolved == null)
					node = new UnresolvedReferenceTreeNode(c);
				else
					node = new DelphiFileTreeNode(resolved);
				return node;
			}));
		}
	}
}


