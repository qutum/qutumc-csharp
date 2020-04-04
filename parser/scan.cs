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
	public interface Scan<K, T> : IDisposable
	{
		bool Next();
		// current token index, <0 before first Next()
		int Loc();
		T Token();
		bool Is(K key);
		bool Is(K key1, K key);
		T Token(int x);
		// tokens from index to index excluded
		IEnumerable<T> Tokens(int from, int to);
		// tokens from index to index excluded
		Span<T> Tokens(int from, int to, Span<T> s);

		// text to single or several keys
		IEnumerable<K> Keys(string text);
	}

	public interface ScanSeg<K, T> : Scan<K, T>
	{
		// tokens from index to index excluded
		new ArraySegment<T> Tokens(int from, int to);
	}

	public class ScanStr : Scan<char, char>
	{
		protected string input;
		protected int loc = -1;

		public ScanStr(string input) => this.input = input;

		public void Dispose() { input = null; loc = -1; }

		public bool Next() => ++loc < input.Length;

		public int Loc() => loc;

		public char Token() => input[loc];

		public virtual bool Is(char key) => input[loc] == key;

		public virtual bool Is(char key1, char key) => key1 == key;

		public char Token(int x) => input[x];

		public string Tokens(int from, int to) => input[from..to];

		IEnumerable<char> Scan<char, char>.Tokens(int from, int to) => Tokens(from, to);

		public Span<char> Tokens(int from, int to, Span<char> s)
		{
			input.AsSpan(from, to - from).CopyTo(s); return s;
		}

		public IEnumerable<char> Keys(string text) => text;
	}

	public class ScanByte : ScanSeg<byte, byte>
	{
		protected byte[] input;
		protected int loc = -1;

		public ScanByte(byte[] input) => this.input = input;

		public void Dispose() { input = null; loc = -1; }

		public bool Next() => ++loc < input.Length;

		public int Loc() => loc;

		public byte Token() => input[loc];

		public virtual bool Is(byte key) => input[loc] == key;

		public virtual bool Is(byte key1, byte key) => key1 == key;

		public byte Token(int x) => input[x];

		public ArraySegment<byte> Tokens(int from, int to)
		{
			return new ArraySegment<byte>(input, from, to - from);
		}

		IEnumerable<byte> Scan<byte, byte>.Tokens(int from, int to) => Tokens(from, to);

		public Span<byte> Tokens(int from, int to, Span<byte> s)
		{
			input.AsSpan(from, to - from).CopyTo(s); return s;
		}

		public IEnumerable<byte> Keys(string text) => text.Select(k => (byte)k);
	}

	public class ScanByteSeg : ScanSeg<byte, byte>
	{
		protected ArraySegment<byte> input;
		protected int loc = -1;

		public ScanByteSeg(ArraySegment<byte> input) => this.input = input;

		public void Dispose() { input = null; loc = -1; }

		public bool Next() => ++loc < input.Count;

		public int Loc() => loc;

		public byte Token() => input[loc];

		public virtual bool Is(byte key) => input[loc] == key;

		public virtual bool Is(byte key1, byte key) => key1 == key;

		public byte Token(int x) => input[x];

		public ArraySegment<byte> Tokens(int from, int to) => input.Slice(from, to - from);

		IEnumerable<byte> Scan<byte, byte>.Tokens(int from, int to) => Tokens(from, to);

		public Span<byte> Tokens(int from, int to, Span<byte> s)
		{
			input.AsSpan(from, to - from).CopyTo(s); return s;
		}

		public IEnumerable<byte> Keys(string text) => text.Select(k => (byte)k);
	}

	public class ScanByteList : Scan<byte, byte>
	{
		protected List<byte> input;
		protected IEnumerator<byte> iter;
		protected int loc = -1;

		public ScanByteList(List<byte> input) { this.input = input; iter = input.GetEnumerator(); }

		public void Dispose() { input = null; iter = null; loc = -1; }

		public bool Next() { loc++; return iter.MoveNext(); }

		public int Loc() => loc;

		public byte Token() => iter.Current;

		public virtual bool Is(byte key) => iter.Current == key;

		public virtual bool Is(byte key1, byte key) => key1 == key;

		public byte Token(int x) => input[x];

		public IEnumerable<byte> Tokens(int from, int to) => input.GetRange(from, to - from);

		public Span<byte> Tokens(int from, int to, Span<byte> bs)
		{
			// lack of List.CopyTo(Span ...)
			int x = 0; foreach (var v in input.GetRange(from, to - from))
				bs[x++] = v;
			return bs;
		}

		public IEnumerable<byte> Keys(string text) => text.Select(k => (byte)k);
	}

	class BootScan : ScanStr
	{
		public BootScan(string input) : base(input) { }

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
