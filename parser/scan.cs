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
	public interface Scan<in I, in K, T, out S> where S : IEnumerable<T>
	{
		IEnumerable<object> Keys(string name);
		void Load(I input);
		bool Next();
		bool Is(K key, object keyo);
		int Loc();
		T Token();
		S Tokens(int from, int to);
		void Tokens(int from, int to, T[] array, int ax);
		void Unload();
	}

	public class ScanStr : Scan<string, char, char, string>
	{
		public IEnumerable<object> Keys(string name) => name.Cast<object>();

		protected string input;
		protected int loc;

		public void Load(string input) { this.input = input; loc = -1; }

		public bool Next() => ++loc < input.Length;

		public virtual bool Is(char key, object keyo) => input[loc] == key;

		public int Loc() => loc;

		public char Token() => input[loc];

		public string Tokens(int from, int to) => input.Substring(from, to - from);

		public void Tokens(int from, int to, char[] array, int ax) => input.CopyTo(from, array, ax, to - from);

		public void Unload() => input = null;
	}

	public class ScanByte : Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>>
	{
		public IEnumerable<object> Keys(string name) => name.Select(k => (object)(byte)k);

		protected IEnumerable<byte> input;
		protected IEnumerator<byte> iter;
		protected int loc;

		public void Load(IEnumerable<byte> input) { this.input = input; iter = input.GetEnumerator(); loc = -1; }

		public bool Next() { loc++; return iter.MoveNext(); }

		public virtual bool Is(byte key, object keyo) => iter.Current == key;

		public int Loc() => loc;

		public byte Token() => iter.Current;

		public IEnumerable<byte> Tokens(int from, int to)
		{
			if (input is List<byte> l) return l.GetRange(from, to - from);
			if (input is byte[] a) { var s = new byte[to - from]; Array.Copy(a, from, s, 0, to - from); return s; }
			return input.Skip(from).Take(to - from);
		}

		public void Tokens(int from, int to, byte[] array, int ax)
		{
			if (input is List<byte> s) s.CopyTo(from, array, ax, to - from);
			else if (input is byte[] a) Array.Copy(a, from, array, ax, to - from);
			else foreach (var v in input.Skip(from).Take(to - from)) array[ax++] = v;
		}

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

	class BootScan : ScanStr
	{
		public override bool Is(char key, object keyo)
		{
			char t = input[loc];
			switch (key)
			{
				case 'S': return t == ' ' || t == '\t';
				case 'W': return t < 127 && W[t];
				case 'X': return t < 127 && X[t];
				case 'O': return t < 127 && O[t];
				case 'R': return t == '?' || t == '*' || t == '+';
				case 'H': return t >= ' ' && t < 127 && t != '=';
				case 'E': return t > ' ' && t < 127;
				case 'V': return t >= ' ' && t != 127 || t == '\t';
				case 'B': return t < 127 && (W[t] || O[t]) && t != '[';
				case 'P': return t < 127 && (W[t] || O[t]) && t != ']' && t != '-';
				default: return t == key;
			}
		}

		static bool[] W = new bool[128], X = new bool[128], O = new bool[128];

		static BootScan()
		{
			//	t > ' ' && t < '0' && t != '*' && t != '+' || t > '9' && t < 'A' && t != '=' && t != '?'
			//		|| t > 'Z' && t < 'a' && t != '\\' && t != '_' || t > 'z' && t <= '~' && t != '|'
			foreach (var t in "!\"#$%&'(),-./:;<>@[]^`{}~") O[t] = true;
			var s = Enumerable.Range(0, 128);
			s.Where(t => t >= 'a' && t <= 'z' || t >= 'A' && t <= 'Z' || t >= '0' && t <= '9' || t == '_')
				.Select(t => W[t] = true).Count();
			s.Where(t => t >= 'a' || t <= 'f' || t >= 'A' && t <= 'F' || t >= '0' && t <= '9')
				.Select(t => X[t] = true).Count();
		}

		internal static string Sym(string s)
		{
			if (s[0] != '\\') return s;
			switch (s[1])
			{
				case 's': return " ";
				case 't': return "\t";
				case 'n': return "\n";
				case 'r': return "\r";
				case 'u':
					return ((char)(s[2] - (s[2] < 'a' ? '0' : 87) << 12 | s[3] - (s[3] < 'a' ? '0' : 87) << 8
						| s[4] - (s[4] < 'a' ? '0' : 87) << 4 | s[5] - (s[5] < 'a' ? '0' : 87))).ToString();
				default: return s[1].ToString();
			}
		}
	}
}
