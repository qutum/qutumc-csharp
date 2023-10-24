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

	public ArraySegment<byte> Tokens(int from, int to) => input.AsSeg(from, to);

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
