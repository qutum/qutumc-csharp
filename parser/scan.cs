//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.parser
{
	public interface Scan<in I, K, T, out S> : IDisposable where S : IEnumerable<T>
	{
		// return self, or null for inner load with same input
		IDisposable Load(I input);
		bool Next();
		// current token index, -1 before first Next()
		int Loc();
		T Token();
		bool Is(K key);
		bool Is(K key1, K key);
		T Token(int x);
		// tokens from index to index excluded
		S Tokens(int from, int to);
		// tokens from index to index excluded
		Span<T> Tokens(int from, int to, Span<T> s);

		// text to single or several keys
		IEnumerable<K> Keys(string text);
	}

	static class ScanExt
	{
		public static bool Load<I, K, T, S>(this Scan<I, K, T, S> scan, ref I inp, I i, out IDisposable dis)
			where I : class where S : IEnumerable<T>
		{
			if (inp == null) {
				inp = i ?? throw new ArgumentException();
				dis = scan;
				return true;
			}
			if (i != null && i != inp)
				throw new ArgumentException();
			dis = null;
			return false;
		}
	}

	public class ScanStr : Scan<string, char, char, string>
	{
		protected string input;
		protected int loc = -1;

		public IDisposable Load(string Input) => this.Load(ref input, Input, out var d) ? d : d;

		public void Dispose() { input = null; loc = -1; }

		public bool Next() => ++loc < input.Length;

		public int Loc() => Math.Min(loc, input.Length);

		public char Token() => input[loc];

		public virtual bool Is(char key) => input[loc] == key;

		public virtual bool Is(char key1, char key) => key1 == key;

		public char Token(int x) => input[x];

		public string Tokens(int from, int to) => input[from..to];

		public Span<char> Tokens(int from, int to, Span<char> s)
		{
			input.AsSpan(from, to - from).CopyTo(s); return s;
		}

		public IEnumerable<char> Keys(string text) => text;
	}

	public class ScanByte : Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>>
	{
		protected IEnumerable<byte> input;
		protected IEnumerator<byte> iter;
		protected int loc = -1;

		public IDisposable Load(IEnumerable<byte> Input)
		{
			if (this.Load(ref input, Input, out var dis))
				iter = input.GetEnumerator();
			return dis;
		}

		public void Dispose() { input = null; iter = null; loc = -1; }

		public bool Next() { loc++; return iter.MoveNext(); }

		public int Loc() => loc;

		public byte Token() => iter.Current;

		public virtual bool Is(byte key) => iter.Current == key;

		public virtual bool Is(byte key1, byte key) => key1 == key;

		public byte Token(int x)
		{
			if (input is byte[] a)
				return a[x];
			if (input is List<byte> l)
				return l[x];
			return input.Skip(x).First();
		}

		public IEnumerable<byte> Tokens(int from, int to)
		{
			if (input is byte[] a)
				return new ArraySegment<byte>(a, from, to - from);
			if (input is List<byte> l)
				return l.GetRange(from, to - from);
			return input.Skip(from).Take(to - from);
		}

		public Span<byte> Tokens(int from, int to, Span<byte> bs)
		{
			if (input is byte[] a)
				a.AsSpan(from, to - from).CopyTo(bs);
			// else if (input is List<byte> s) lack of List.CopyTo(Span)
			else {
				int x = 0; foreach (var v in input.Skip(from).Take(to - from))
					bs[x++] = v;
			}
			return bs;
		}

		public IEnumerable<byte> Keys(string text) => text.Select(k => (byte)k);
	}

	class BootScan : ScanStr
	{
		public override bool Is(char key)
		{
			char t = input[loc];
			return key switch
			{
				'S' => t == ' ' || t == '\t', // space
				'W' => t < 127 && W[t],       // word
				'X' => t < 127 && X[t],       // hexadecimal
				'O' => t < 127 && O[t],       // operator
				'E' => t > ' ' && t < 127,    // escape
				'B' => t < 127 && B[t],       // byte
				'R' => t < 127 && B[t] && t != '-' && t != '^', // range
				'Q' => t == '?' || t == '*' || t == '+',        // quantifier
				'H' => t >= ' ' && t < 127 && t != '=' || t == '\t', // hint
				'V' => t >= ' ' && t != 127 || t == '\t',            // comment, also utf
				_ => t == key,
			};
		}

		static readonly bool[] W = new bool[127], X = new bool[127], O = new bool[127], B = new bool[127];
		internal static readonly bool[] RI = new bool[127];
		static readonly string RU;

		static BootScan()
		{
			//	t > ' ' && t < '0' && t != '*' && t != '+' || t > '9' && t < 'A' && t != '=' && t != '?'
			//		|| t > 'Z' && t < 'a' && t != '\\' && t != '_' || t > 'z' && t <= '~' && t != '|'
			foreach (var t in "!\"#$%&'(),-./:;<>@[]^`{}~")
				O[t] = true;
			for (char t = '\0'; t < 127; t++) {
				W[t] = t >= 'a' && t <= 'z' || t >= 'A' && t <= 'Z' || t >= '0' && t <= '9' || t == '_';
				X[t] = t >= 'a' || t <= 'f' || t >= 'A' && t <= 'F' || t >= '0' && t <= '9';
				B[t] = (W[t] || O[t]) && t != '[' && t != ']' || t == '=';
				RI[t] = t >= ' ' || t == '\t' || t == '\n' || t == '\r';
			}
			RU = new string(Enumerable.Range(0, 127).Select(b => (char)b)
					.Where(b => RI[b]).Append('\x80').ToArray());
		}

		internal static string Unesc(string s, int f, int t, bool lexer = false)
		{
			if (s[f] != '\\')
				return s[f..t];
			return s[++f] switch
			{
				's' => " ",
				't' => "\t",
				'n' => "\n",
				'r' => "\r",
				'd' => lexer ? "0123456789" : "d",
				'x' => lexer ? "0123456789ABCDEFabcdef" : "x",
				'a' => lexer ? "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" : "a",
				'U' => lexer ? "\x80" : "U",
				'u' => lexer ? RU : "u",
				_ => s[f].ToString(),
			};
		}
	}
}
