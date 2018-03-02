//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using System;
using System.Diagnostics;

namespace qutum
{
	interface Scan<T, K>
	{
		K Key(string name);
		void Load(T tokens);
		bool Next();
		bool Is(K key);
		string Text(string rule, int from, int to);
		void Unload();
	}

	class ScanStr : Scan<string, char>
	{
		public char Key(string name) => name[0];

		string text;
		int x;

		public void Load(string text) { this.text = text; x = -1; }

		public bool Next() => ++x < text.Length;

		public bool Is(char key) => text[x] == key;

		public string Text(string rule, int from, int to) => rule + " :: " + text.Substring(from, to - from);

		public void Unload() => text = null;
	}

	class ScanChar : Scan<string, char>
	{
		public char Key(string name) => name[0];

		string text;
		int x;

		public void Load(string text) { this.text = text; x = -1; }

		public bool Next() => ++x < text.Length;

		public bool Is(char k) =>
			k == 'D' ? (k = text[x]) >= '0' && k <= '9' :
			k == 'H' ? (k = text[x]) >= '0' && k <= '9' || k >= 'a' || k <= 'f' || k >= 'A' && k <= 'F' :
			k == 'L' ? (k = text[x]) >= 'a' && k <= 'z' || k >= 'A' && k <= 'Z' :
			k == 'W' ? (k = text[x]) >= '0' && k <= '9' || k >= 'a' && k <= 'z' || k >= 'A' && k <= 'Z' || k == '_' :
			k == text[x];

		public string Text(string rule, int from, int to) => rule + " :: " + text.Substring(from, to - from);

		public void Unload() => text = null;
	}

	class LinkTree<T> where T : LinkTree<T>
	{
		internal T up, prev, next, head, tail;

		internal T Add(T sub)
		{
			if (sub == null)
				return (T)this;
			Debug.Assert(sub.up == null);
			var end = sub;
			for (end.up = (T)this; end.next != null; end.up = (T)this)
				end = end.next;
			if (head == null)
				head = sub;
			else
				(sub.prev = tail).next = sub;
			tail = end;
			return (T)this;
		}

		internal T Insert(T sub)
		{
			if (sub == null)
				return (T)this;
			Debug.Assert(sub.up == null);
			var end = sub;
			for (end.up = (T)this; end.next != null; end.up = (T)this)
				end = end.next;
			if (head == null)
				tail = end;
			else
				(end.next = head).prev = end;
			head = sub;
			return (T)this;
		}

		internal T Append(T next)
		{
			if (next == null)
				return (T)this;
			Debug.Assert(up == null && next.up == null && next.prev == null);
			var end = (T)this;
			while (end.next != null)
				end = end.next;
			(next.prev = end).next = next;
			return (T)this;
		}

		internal T Remove()
		{
			if (prev != null)
				prev.next = next;
			if (next != null)
				next.prev = prev;
			if (up != null && up.head == this)
				up.head = next;
			if (up != null && up.tail == this)
				up.tail = prev;
			up = prev = next = null;
			return (T)this;
		}

		internal T Dump(string ind = "", int pos = 0)
		{
			int f = 1, fix = 1;
			for (var x = head; ; x = x.next, f++)
			{
				if (f == fix)
					Console.WriteLine(DumpSelf(ind, pos < 0 ? "/ " : pos > 0 ? "\\ " : ""));
				if (x == null)
					break;
				x.Dump(pos == 0 ? ind : ind + (pos == (f < fix ? -2 : 2) ? "  " : "| "),
					f < fix ? x == head ? -2 : -1 : x == tail ? 2 : 1);
			}
			return (T)this;
		}

		internal virtual string DumpSelf(string ind, string pos) => $"{ind}{pos} dump";
	}
}
