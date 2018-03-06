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
	public interface Scan<in I, in K, out T, out S> where S : class, IEnumerable<T>
	{
		IEnumerable<object> Keys(string name);
		void Load(I input);
		bool Next();
		bool Is(K key);
		T Token();
		S Tokens(int from, int to);
		void Unload();
	}

	public class ScanStr : Scan<string, char, char, string>
	{
		public IEnumerable<object> Keys(string name) => name.Cast<object>();

		protected string input;
		protected int x;

		public void Load(string input) { this.input = input; x = -1; }

		public bool Next() => ++x < input.Length;

		public virtual bool Is(char key) => input[x] == key;

		public char Token() => input[x];

		public string Tokens(int from, int to) => input.Substring(from, to - from);

		public void Unload() => input = null;
	}

	public class ScanByte : Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>>
	{
		public IEnumerable<object> Keys(string name) => name.Cast<object>();

		protected IEnumerable<byte> input;
		protected IEnumerator<byte> iter;

		public void Load(IEnumerable<byte> input) { this.input = input; iter = this.input.GetEnumerator(); }

		public bool Next() => iter.MoveNext();

		public virtual bool Is(byte key) => iter.Current == key;

		public byte Token() => iter.Current;

		public IEnumerable<byte> Tokens(int from, int to) =>
			input is List<byte> s ? s.GetRange(from, to - from) : input.Skip(from).Take(to - from);

		public void Unload() { input = null; iter = null; }
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
