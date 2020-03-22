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
	public interface Scan<in I, K, T, out S> where S : IEnumerable<T>
	{
		IEnumerable<K> Keys(string text);
		void Load(I input);
		bool Next();
		// current token index
		int Loc();
		T Token();
		bool Is(K key);
		bool Is(K key1, K key);
		// from token index to token index excluded
		S Tokens(int from, int to);
		// from token index to token index excluded
		T[] Tokens(int from, int to, T[] array, int index = 0);
		void Unload();
	}

	public class ScanStr : Scan<string, char, char, string>
	{
		public IEnumerable<char> Keys(string text) => text;

		protected string input;
		protected int loc;

		public void Load(string input)
		{
			this.input = input; loc = -1;
		}

		public bool Next() => ++loc < input.Length;

		public int Loc() => Math.Min(loc, input.Length);

		public char Token() => input[loc];

		public virtual bool Is(char key) => input[loc] == key;

		public virtual bool Is(char key1, char key) => key1 == key;

		public string Tokens(int from, int to)
			=> input.Substring(from, to - from);

		public char[] Tokens(int from, int to, char[] s, int x)
		{
			input.CopyTo(from, s, x, to - from); return s;
		}

		public void Unload() => input = null;
	}

	public class ScanByte : Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>>
	{
		public IEnumerable<byte> Keys(string text) => text.Select(k => (byte)k);

		protected IEnumerable<byte> input;
		protected IEnumerator<byte> iter;
		protected int loc;

		public void Load(IEnumerable<byte> input)
		{
			this.input = input; iter = input.GetEnumerator(); loc = -1;
		}

		public bool Next() { loc++; return iter.MoveNext(); }

		public int Loc() => loc;

		public byte Token() => iter.Current;

		public virtual bool Is(byte key) => iter.Current == key;

		public virtual bool Is(byte key1, byte key) => key1 == key;

		public IEnumerable<byte> Tokens(int from, int to)
		{
			if (input is List<byte> l)
				return l.GetRange(from, to - from);
			if (input is byte[] a)
				return new ArraySegment<byte>(a, from, to - from);
			return input.Skip(from).Take(to - from);
		}

		public byte[] Tokens(int from, int to, byte[] bs, int x)
		{
			if (input is List<byte> s)
				s.CopyTo(from, bs, x, to - from);
			else if (input is byte[] a)
				Array.Copy(a, from, bs, x, to - from);
			else
				foreach (var v in input.Skip(from).Take(to - from))
					bs[x++] = v;
			return bs;
		}

		public void Unload() { input = null; iter = null; }
	}

	class BootScan : ScanStr
	{
		public override bool Is(char key)
		{
			char t = input[loc];
			switch (key)
			{
				case 'S': return t == ' ' || t == '\t'; // space
				case 'W': return t < 127 && W[t];       // word
				case 'X': return t < 127 && X[t];       // hexadecimal
				case 'O': return t < 127 && O[t];       // operator
				case 'Q': return t == '?' || t == '*' || t == '+';  // quantifier
				case 'H': return t >= ' ' && t < 127 && t != '=';   // hint
				case 'E': return t > ' ' && t < 127 && t != 'u';    // escape except \u
				case 'V': return t >= ' ' && t != 127 || t == '\t'; // comment, also utf
				case 'B': return t < 127 && B[t];                         // byte
				case 'R': return t < 127 && B[t] && t != '-' && t != '^'; // range
				default: return t == key;
			}
		}

		static bool[] W = new bool[127], X = new bool[127], O = new bool[127], B = new bool[127];

		static BootScan()
		{
			//	t > ' ' && t < '0' && t != '*' && t != '+' || t > '9' && t < 'A' && t != '=' && t != '?'
			//		|| t > 'Z' && t < 'a' && t != '\\' && t != '_' || t > 'z' && t <= '~' && t != '|'
			foreach (var t in "!\"#$%&'(),-./:;<>@[]^`{}~")
				O[t] = true;
			for (char t = '\0'; t < 127; t++)
			{
				W[t] = t >= 'a' && t <= 'z' || t >= 'A' && t <= 'Z' || t >= '0' && t <= '9' || t == '_';
				X[t] = t >= 'a' || t <= 'f' || t >= 'A' && t <= 'F' || t >= '0' && t <= '9';
				B[t] = (W[t] || O[t]) && t != '[' && t != ']' || t == '=';
			}
		}

		internal static string Esc(string s, int f, int t, int u) => Esc(s, ref f, t, u);

		internal static string Esc(string s, ref int f, int t, int u)
		{
			if (s[f++] != '\\')
				return s.Substring(f - 1, t - f + 1);
			switch (s[f++])
			{
				case 's': return " ";
				case 't': return "\t";
				case 'n': return "\n";
				case 'r': return "\r";
				case 'U': return u < 0 ? "\x81" : "U"; // lexer only
				case 'u': // parser only
					return u > 0 ? ((char)(s[f] - (s[f++] < 'a' ? '0' : 87) << 12
						| s[f] - (s[f++] < 'a' ? '0' : 87) << 8
						| s[f] - (s[f++] < 'a' ? '0' : 87) << 4
						| s[f] - (s[f++] < 'a' ? '0' : 87))).ToString() : "u";
				default: return s[f - 1].ToString();
			}
		}
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

		public T Adds(T sub)
		{
			if (sub == null)
				return (T)this;
			var x = sub.head;
			x.up = null;
			sub.head = sub.tail = null;
			return Add(x);
		}

		public T Remove(bool clear = true)
		{
			if (prev != null)
				prev.next = next;
			if (next != null)
				next.prev = prev;
			if (up != null && up.head == this)
				up.head = next;
			if (up != null && up.tail == this)
				up.tail = prev;
			if (clear) up = prev = next = null;
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
