//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.parser;

// lexical parser
public interface Lexer<K, L> : IDisposable
{
	bool Next();
	// current lex location, <0 before first Next()
	int Loc();
	L Lex();
	L Lex(int loc);

	bool Is(K aim);
	bool Is(int loc, K aim);
	bool Is(K key, K aim);

	// lexs from loc to loc excluded
	Span<L> Lexs(int from, int to, Span<L> s);
	// lexs from loc to loc excluded
	IEnumerable<L> Lexs(int from, int to);

	// get single or several keys
	IEnumerable<K> Keys(string keys);
}

public interface LexerSeg<K, L> : Lexer<K, L>
{
	// lexs from loc to loc excluded
	new ArraySegment<L> Lexs(int from, int to);
}

public class LerStr(string read) : Lexer<char, char>
{
	protected string read = read;
	protected int loc = -1;

	void IDisposable.Dispose() { read = null; loc = -1; }

	public bool Next() => ++loc < read.Length;
	public int Loc() => loc;
	public char Lex() => read[loc];
	public char Lex(int loc) => read[loc];

	public virtual bool Is(char aim) => read[loc] == aim;
	public virtual bool Is(int loc, char aim) => read[loc] == aim;
	public virtual bool Is(char key, char aim) => key == aim;

	public Span<char> Lexs(int from, int to, Span<char> s)
	{
		read.AsSpan(from, to - from).CopyTo(s); return s;
	}
	public string Lexs(int from, int to) => read[from..to];
	IEnumerable<char> Lexer<char, char>.Lexs(int from, int to) => Lexs(from, to);

	public IEnumerable<char> Keys(string keys) => keys;
}

public class LerByte(byte[] read) : LexerSeg<byte, byte>
{
	protected byte[] read = read;
	protected int loc = -1;

	void IDisposable.Dispose() { read = null; loc = -1; }

	public bool Next() => ++loc < read.Length;
	public int Loc() => loc;
	public byte Lex() => read[loc];
	public byte Lex(int loc) => read[loc];

	public virtual bool Is(byte aim) => read[loc] == aim;
	public virtual bool Is(int loc, byte aim) => read[loc] == aim;
	public virtual bool Is(byte key, byte aim) => key == aim;

	public Span<byte> Lexs(int from, int to, Span<byte> s)
	{
		read.AsSpan(from, to - from).CopyTo(s); return s;
	}
	public ArraySegment<byte> Lexs(int from, int to) => read.Seg(from, to);
	IEnumerable<byte> Lexer<byte, byte>.Lexs(int from, int to) => Lexs(from, to);

	public IEnumerable<byte> Keys(string keys) => keys.Select(k => (byte)k);
}

public class LerByteSeg(ArraySegment<byte> read) : LexerSeg<byte, byte>
{
	protected ArraySegment<byte> read = read;
	protected int loc = -1;

	void IDisposable.Dispose() { read = null; loc = -1; }

	public bool Next() => ++loc < read.Count;
	public int Loc() => loc;
	public byte Lex() => read[loc];
	public byte Lex(int loc) => read[loc];

	public virtual bool Is(byte aim) => read[loc] == aim;
	public virtual bool Is(int loc, byte aim) => read[loc] == aim;
	public virtual bool Is(byte key, byte aim) => key == aim;

	public Span<byte> Lexs(int from, int to, Span<byte> s)
	{
		read.AsSpan(from, to - from).CopyTo(s); return s;
	}
	public ArraySegment<byte> Lexs(int from, int to) => read.Slice(from, to - from);
	IEnumerable<byte> Lexer<byte, byte>.Lexs(int from, int to) => Lexs(from, to);

	public IEnumerable<byte> Keys(string keys) => keys.Select(k => (byte)k);
}

public class LerByteList(List<byte> read) : Lexer<byte, byte>
{
	protected List<byte> read = read;
	protected int loc = -1;

	void IDisposable.Dispose() { read = null; loc = -1; }

	public bool Next() => ++loc < read.Count;
	public int Loc() => loc;
	public byte Lex() => read[loc];
	public byte Lex(int loc) => read[loc];

	public virtual bool Is(byte aim) => read[loc] == aim;
	public virtual bool Is(int loc, byte aim) => read[loc] == aim;
	public virtual bool Is(byte key, byte aim) => key == aim;

	public Span<byte> Lexs(int from, int to, Span<byte> s)
	{
		read.GetRange(from, to - from).CopyTo(s); return s;
	}
	public IEnumerable<byte> Lexs(int from, int to) => read.GetRange(from, to - from);

	public IEnumerable<byte> Keys(string keys) => keys.Select(k => (byte)k);
}

public static class CharSet
{
	internal static bool[] L = new bool[129],  // single line, no \r \n, \x80
							D = new bool[129], // decimal
							X = new bool[129], // hexadecimal
							A = new bool[129], // alphabet
							W = new bool[129], // word, decimal or alphabet or _
							O = new bool[129], // operator
							G = new bool[129], // grammar operator, op except * + = ? \\ |
							I = new bool[129], // grammer single, word or = or grammar op except [ ]
							RI = new bool[129]; // default inclusive range
	internal static string ALL, LINE, DEC, HEX, ALPHA, WORD, OP;
	internal static string[] ONE; // [each byte]

	public static readonly ReadOnlyMemory<char> All, Line, Dec, Hex, Alpha, Word, Op;

	static CharSet()
	{
		foreach (var t in "!\"#$%&'()*+,-./:;<=>?@[\\]^`{|}~") {
			O[t] = true;
			G[t] = t is not ('*' or '+' or '=' or '?' or '\\' or '|');
		}
		for (char t = '\0'; t < 127; t++) {
			L[t] = t is >= ' ' or '\t';
			D[t] = t is >= '0' and <= '9';
			X[t] = D[t] || t is >= 'a' and <= 'f' or >= 'A' and <= 'F';
			A[t] = t is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
			W[t] = D[t] || A[t] || t == '_';
			I[t] = W[t] || G[t] && t != '[' && t != ']' || t == '=';
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

	public static string Unesc<K>(K k)
	{
		if (k is byte bb && bb >= 128)
			return "\\U";
		if (k is char c || k is byte b && (c = (char)b) == c)
			return c is > ' ' and < (char)127 ? c.ToString()
				: c == ' ' ? "\\s" : c == '\t' ? "\\t" : c == '\n' ? "\\n" : c == '\r' ? "\\r"
				: c >= 128 ? $"\\U{c:x04}" : c == 0 ? "\\0" : $"\\x{c:x02}";
		return k.ToString();
	}
}
