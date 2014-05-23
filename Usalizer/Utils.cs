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
using System.Windows;
using Usalizer.Analysis;

namespace Usalizer.TreeNodes
{
	static class Utils
	{
		public static bool TryParse(string value, out Point result)
		{
			try {
				result = Point.Parse(value);
				return true;
			} catch (Exception ex) {
				result = new Point(0,0);
				return false;
			}
		}
		
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
		public static void OrderedInsert<T>(this IList<T> list, T item, IComparer<T> comparer, int offset = 0)
		{
			int pos = BinarySearch(list, offset, list.Count - offset, item, x => x, comparer);
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
}


