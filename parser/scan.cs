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
	public interface Scan<in I, K, T, out S> where S : IEnumerable<T>
	{
		void Load(I input);
		bool Next();
		// current token index, -1 before first Next()
		int Loc();
		T Token();
		bool Is(K key);
		bool Is(K key1, K key);
		// tokens from index to index excluded
		S Tokens(int from, int to);
		// tokens from index to index excluded
		Span<T> Tokens(int from, int to, Span<T> s);
		void Unload();
		// text to single or several keys
		IEnumerable<K> Keys(string text);
	}

	public class ScanStr : Scan<string, char, char, string>
	{
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

		public string Tokens(int from, int to) => input[from..to];

		public Span<char> Tokens(int from, int to, Span<char> s)
		{
			input.AsSpan(from, to - from).CopyTo(s); return s;
		}

		public void Unload() => input = null;

		public IEnumerable<char> Keys(string text) => text;
	}

	public class ScanByte : Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>>
	{
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

		public void Unload() { input = null; iter = null; }

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
				'Q' => t == '?' || t == '*' || t == '+', // quantifier
				'H' => t >= ' ' && t < 127 && t != '=' || t == '\t', // hint
				'E' => t > ' ' && t < 127 && t != 'u',    // escape except \u
				'V' => t >= ' ' && t != 127 || t == '\t', // comment, also utf
				'B' => t < 127 && B[t], // byte
				'R' => t < 127 && B[t] && t != '-' && t != '^', // range
				_ => t == key,
			};
		}

		static bool[] W = new bool[127], X = new bool[127], O = new bool[127], B = new bool[127];

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
			}
		}

		internal static string Esc(string s, int f, int t, int u) => Esc(s, ref f, t, u);

		internal static string Esc(string s, ref int f, int t, int u)
		{
			if (s[f++] != '\\')
				return s[(f - 1)..t];
			return s[f++] switch
			{
				's' => " ",
				't' => "\t",
				'n' => "\n",
				'r' => "\r",
				'U' => u < 0 ? "\x81" : "U", // lexer only
				'u' => u <= 0 ? "u" : // parser only
					((char)((s[f] & 15) + (s[f++] < 'A' ? 0 : 9) << 12
					| (s[f] & 15) + (s[f++] < 'A' ? 0 : 9) << 8
					| (s[f] & 15) + (s[f++] < 'A' ? 0 : 9) << 4
					| (s[f] & 15) + (s[f++] < 'A' ? 0 : 9))).ToString(),
				_ => s[f - 1].ToString(),
			};
		}
	}
}
