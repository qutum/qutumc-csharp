//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace qutum.parser
{
	public interface Scan<T, K> where T : class
	{
		IEnumerable<object> Key(string name);
		void Load(T tokens);
		bool Next();
		bool Is(K key);
		T Tokens(int from, int to);
		void Unload();
	}

	public class ScanStr : Scan<string, char>
	{
		public IEnumerable<object> Key(string name) => name.Cast<object>();

		protected string text;
		protected int x;

		public void Load(string text) { this.text = text; x = -1; }

		public bool Next() => ++x < text.Length;

		public virtual bool Is(char key) => text[x] == key;

		public string Tokens(int from, int to) => text.Substring(from, to - from);

		public void Unload() => text = null;
	}

	public class LinkTree<T> : IEnumerable<T> where T : LinkTree<T>
	{
		public T up, prev, next, head, tail;

		public T Add(T sub)
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

		public T Insert(T sub)
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

		public T Append(T next)
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

		public T Remove()
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

		public T Dump(string ind = "", int pos = 0)
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

		public virtual string DumpSelf(string ind, string pos) => $"{ind}{pos} dump";

		public IEnumerator<T> GetEnumerator()
		{
			for (var x = head; x != null; x = x.next)
				yield return x;
		}
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		class Backwarder : IEnumerable<T>
		{
			internal T tail;

			public IEnumerator<T> GetEnumerator()
			{
				for (var x = tail; x != null; x = x.prev)
					yield return x;
			}
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		public IEnumerable<T> Backward() => new Backwarder { tail = tail };
	}
}
