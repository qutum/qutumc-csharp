//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
global using Bytex = qutum.parser.X<byte>;
global using JovBx = qutum.parser.Jov<qutum.parser.X<byte>>;

using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.parser;

// lexical parser
public interface Lexer<K, L> : IDisposable
{
	bool Next();
	// current lex location, -1 before first Next()
	X<L> Loc();
	L Lex();
	L Lex(X<L> loc);

	bool Is(K aim);
	bool Is(X<L> loc, K aim);
	bool Is(K key, K aim);

	Span<L> Lexs(Jov<X<L>> j, Span<L> s);
	IEnumerable<L> Lexs(Jov<X<L>> j);

	// get single or several keys
	IEnumerable<K> Keys(string keys);
}

public interface LexerSeg<K, L> : Lexer<K, L>
{
	new ArraySegment<L> Lexs(Jov<X<L>> j);
}

public class LerStr(string read) : Lexer<char, char>
{
	protected string read = read;
	protected X<char> loc = X<char>.No;

	void IDisposable.Dispose() { read = null; loc = X<char>.No; }

	public bool Next() => loc.IncLess(read.Length);
	public X<char> Loc() => loc;
	public char Lex() => read[loc];
	public char Lex(X<char> loc) => read[loc];

	public virtual bool Is(char aim) => read[loc] == aim;
	public virtual bool Is(X<char> loc, char aim) => read[loc] == aim;
	public virtual bool Is(char key, char aim) => key == aim;

	public Span<char> Lexs(Jov<X<char>> j, Span<char> s)
	{
		read.AsSpan(j.on, j.size).CopyTo(s); return s;
	}
	public string Lexs(Jov<X<char>> j) => read[j.range];
	IEnumerable<char> Lexer<char, char>.Lexs(Jov<X<char>> j) => Lexs(j);

	public IEnumerable<char> Keys(string keys) => keys;
}

public class LerByte(byte[] read) : LexerSeg<byte, byte>
{
	protected byte[] read = read;
	protected Bytex loc = Bytex.No;

	void IDisposable.Dispose() { read = null; loc = Bytex.No; }

	public bool Next() => loc.IncLess(read.Length);
	public Bytex Loc() => loc;
	public byte Lex() => read[loc];
	public byte Lex(Bytex loc) => read[loc];

	public virtual bool Is(byte aim) => read[loc] == aim;
	public virtual bool Is(Bytex loc, byte aim) => read[loc] == aim;
	public virtual bool Is(byte key, byte aim) => key == aim;

	public Span<byte> Lexs(JovBx j, Span<byte> s)
	{
		read.AsSpan(j.on, j.size).CopyTo(s); return s;
	}
	public ArraySegment<byte> Lexs(JovBx j) => read.Seg((Jov)j);
	IEnumerable<byte> Lexer<byte, byte>.Lexs(JovBx j) => Lexs(j);

	public IEnumerable<byte> Keys(string keys) => keys.Select(k => (byte)k);
}

public class LerByteSeg(ArraySegment<byte> read) : LexerSeg<byte, byte>
{
	protected ArraySegment<byte> read = read;
	protected Bytex loc = Bytex.No;

	void IDisposable.Dispose() { read = null; loc = Bytex.No; }

	public bool Next() => loc.IncLess(read.Count);
	public Bytex Loc() => loc;
	public byte Lex() => read[loc];
	public byte Lex(Bytex loc) => read[loc];

	public virtual bool Is(byte aim) => read[loc] == aim;
	public virtual bool Is(Bytex loc, byte aim) => read[loc] == aim;
	public virtual bool Is(byte key, byte aim) => key == aim;

	public Span<byte> Lexs(JovBx j, Span<byte> s)
	{
		read.AsSpan(j.on, j.size).CopyTo(s); return s;
	}
	public ArraySegment<byte> Lexs(JovBx j) => read.Slice(j.on, j.size);
	IEnumerable<byte> Lexer<byte, byte>.Lexs(JovBx j) => Lexs(j);

	public IEnumerable<byte> Keys(string keys) => keys.Select(k => (byte)k);
}

public class LerByteList(List<byte> read) : Lexer<byte, byte>
{
	protected List<byte> read = read;
	protected Bytex loc = Bytex.No;

	void IDisposable.Dispose() { read = null; loc = Bytex.No; }

	public bool Next() => loc.IncLess(read.Count);
	public Bytex Loc() => loc;
	public byte Lex() => read[loc];
	public byte Lex(Bytex loc) => read[loc];

	public virtual bool Is(byte aim) => read[loc] == aim;
	public virtual bool Is(Bytex loc, byte aim) => read[loc] == aim;
	public virtual bool Is(byte key, byte aim) => key == aim;

	public Span<byte> Lexs(JovBx j, Span<byte> s)
	{
		read.GetRange(j.on, j.size).CopyTo(s); return s;
	}
	public IEnumerable<byte> Lexs(JovBx j) => read.GetRange(j.on, j.size);

	public IEnumerable<byte> Keys(string keys) => keys.Select(k => (byte)k);
}

public static class ByteSet
{
	public static readonly bool[] L = new bool[129],// single line, no \r \n, \x80
								LL = new bool[129], // multi lines
								D = new bool[129],  // decimal
								X = new bool[129],  // hexadecimal
								A = new bool[129],  // alphabet
								W = new bool[129],  // word, decimal or alphabet or _
								O = new bool[129];  // operator
	public static readonly string ALL, LINE, LINES, DEC, HEX, ALPHA, WORD, OP;
	public static readonly string[] BYTE; // [each byte]

	public static readonly One All, Line, Lines, Dec, Hex, Alpha, Word, Op;

	static ByteSet()
	{
		for (char t = '\0'; t < 127; t++) {
			L[t] = t is >= ' ' or '\t';
			LL[t] = t is >= ' ' or '\t' or '\n' or '\r';
			D[t] = t is >= '0' and <= '9';
			X[t] = D[t] || t is >= 'a' and <= 'f' or >= 'A' and <= 'F';
			A[t] = t is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
			W[t] = D[t] || A[t] || t == '_';
		}
		L[128] = LL[128] = true;
		foreach (var t in "!\"#$%&'()*+,-./:;<=>?@[\\]^`{|}~")
			O[t] = true;
		var all = Enumerable.Range(0, 129).Select(b => (char)b);
		ALL = new string([.. all]);
		LINE = new string([.. all.Where(b => L[b])]);
		LINES = new string([.. all.Where(b => LL[b])]);
		DEC = new string([.. all.Where(b => D[b])]);
		HEX = new string([.. all.Where(b => X[b])]);
		ALPHA = new string([.. all.Where(b => A[b])]);
		WORD = new string([.. all.Where(b => W[b])]);
		OP = new string([.. all.Where(b => O[b])]);
		BYTE = [.. all.Select(b => new string(b, 1))];
		(All, Line, Lines, Dec, Hex, Alpha, Word, Op) = (ALL, LINE, LINES, DEC, HEX, ALPHA, WORD, OP);
	}

	public struct One
	{
		public ReadOnlyMemory<char> set;

		public static implicit operator One(ReadOnlyMemory<char> s) => new() { set = s };
		public static implicit operator One(Memory<char> s) => new() { set = s };
		public static implicit operator One(string s) => s.AsMemory();
		public static One operator +(One o, One include)
			=> o.set.Enum().Union(include.set.Enum()).ToArray().AsMemory();
		public static One operator -(One o, One exclude)
			=> o.set.Enum().Except(exclude.set.Enum()).ToArray().AsMemory();
	}

	public static string Esc<K>(K k)
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
