//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize

using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.parser;

public interface Scan<K, T> : IDisposable
{
	bool Next();
	// current token index, <0 before first Next()
	int Loc();
	T Token();
	bool Is(K key);
	bool Is(int loc, K key);
	bool Is(K testee, K key);
	T Token(int loc);
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

	void IDisposable.Dispose() { input = null; loc = -1; }

	public bool Next() => ++loc < input.Length;

	public int Loc() => loc;

	public char Token() => input[loc];

	public virtual bool Is(char key) => input[loc] == key;

	public virtual bool Is(int loc, char key) => input[loc] == key;

	public virtual bool Is(char testee, char key) => testee == key;

	public char Token(int loc) => input[loc];

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

	void IDisposable.Dispose() { input = null; loc = -1; }

	public bool Next() => ++loc < input.Length;

	public int Loc() => loc;

	public byte Token() => input[loc];

	public virtual bool Is(byte key) => input[loc] == key;

	public virtual bool Is(int loc, byte key) => input[loc] == key;

	public virtual bool Is(byte testee, byte key) => testee == key;

	public byte Token(int loc) => input[loc];

	public ArraySegment<byte> Tokens(int from, int to) => input.Seg(from, to);

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

	void IDisposable.Dispose() { input = null; loc = -1; }

	public bool Next() => ++loc < input.Count;

	public int Loc() => loc;

	public byte Token() => input[loc];

	public virtual bool Is(byte key) => input[loc] == key;

	public virtual bool Is(int loc, byte key) => input[loc] == key;

	public virtual bool Is(byte testee, byte key) => testee == key;

	public byte Token(int loc) => input[loc];

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
	protected int loc = -1;

	public ScanByteList(List<byte> input) => this.input = input;

	void IDisposable.Dispose() { input = null; loc = -1; }

	public bool Next() => ++loc < input.Count;

	public int Loc() => loc;

	public byte Token() => input[loc];

	public virtual bool Is(byte key) => input[loc] == key;

	public virtual bool Is(int loc, byte key) => input[loc] == key;

	public virtual bool Is(byte testee, byte key) => testee == key;

	public byte Token(int loc) => input[loc];

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

public static class ScanSet
{

	internal static bool[] L = new bool[129], D = new bool[129], X = new bool[129],
							A = new bool[129], W = new bool[129], O = new bool[129],
							G = new bool[129], I = new bool[129],
							RI = new bool[129]; // default inclusive range
	internal static string ALL, LINE, DEC, HEX, ALPHA, WORD, OP;
	internal static string[] ONE; // one bytes

	public static readonly ReadOnlyMemory<char> All, Line, Dec, Hex, Alpha, Word, Op;

	static ScanSet()
	{
		foreach (var t in "!\"#$%&'()*+,-./:;<=>?@[\\]^`{|}~") {
			O[t] = true;
			G[t] = t is not ('*' or '+' or '=' or '?' or '\\' or '|');
		}
		for (char t = '\0'; t < 127; t++) {
			L[t] = t is >= ' ' or '\t';
			D[t] = t is >= '0' and <= '9';
			X[t] = (D[t]) || t is >= 'a' and <= 'f' or >= 'A' and <= 'F';
			A[t] = t is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
			W[t] = (D[t] || A[t]) || t == '_';
			I[t] = (W[t] || G[t]) && t != '[' && t != ']' || t == '=';
			RI[t] = t is >= ' ' or '\t' or '\n' or '\r';
		}
		L[128] = true;
		var all = Enumerable.Range(0, 129);
		ALL = new string(all.Select(b => (char)b).ToArray());
		LINE = new string(all.Where(b => L[b]).Select(b => (char)b).ToArray());
		DEC = new string(all.Where(b => D[b]).Select(b => (char)b).ToArray());
		HEX = new string(all.Where(b => X[b]).Select(b => (char)b).ToArray());
		ALPHA = new string(all.Where(b => A[b]).Select(b => (char)b).ToArray());
		WORD = new string(all.Where(b => W[b]).Select(b => (char)b).ToArray());
		OP = new string(all.Where(b => O[b]).Select(b => (char)b).ToArray());
		ONE = Enumerable.Range(0, 129).Select(b => new string((char)b, 1)).ToArray();
		All = ALL.Mem();
		Line = LINE.Mem();
		Dec = DEC.Mem();
		Hex = HEX.Mem();
		Alpha = ALPHA.Mem();
		Word = WORD.Mem();
		Op = OP.Mem();
	}

	public static ReadOnlyMemory<char> Inc(this ReadOnlyMemory<char> inc, ReadOnlyMemory<char> more)
		=> inc.Enum().Union(more.Enum()).ToArray().AsMemory();
	public static ReadOnlyMemory<char> Inc(this ReadOnlyMemory<char> inc, string more)
		=> Inc(inc, more.Mem());
	public static ReadOnlyMemory<char> Exc(this ReadOnlyMemory<char> inc, ReadOnlyMemory<char> exc)
		=> inc.Enum().Except(exc.Enum()).ToArray().AsMemory();
	public static ReadOnlyMemory<char> Exc(this ReadOnlyMemory<char> inc, string exc)
		=> Exc(inc, exc.Mem());
}
