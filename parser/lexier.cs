//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace qutum.parser;

// lexic token
public partial struct Lexi<K> where K : struct
{
	public K key;
	public int from, to; // read from loc to excluded loc
	public int err; // error: <0, no error: >=0
	public object value;
}

// lexic grammar
public class LexGram<K> where K : struct
{
	public readonly List<Prod> prods = [];
	public class Prod : List<Wad>
	{
		public K key;
	}
	public class Wad : List<Alt>
	{
		// skip mismatched read then redo this wad
		public bool redo;
	}
	public class Alt : List<Con>
	{
		// match to loop wad
		public bool loop;
	}
	public class Con
	{
		// byte sequence
		public string str = "";
		// inclusive range of one byte
		public ReadOnlyMemory<char> inc;
		// duplicate last byte of sequence, or duplicate inclusive range
		public bool dup;
	}

	public LexGram<K> k(K key) { prods.Add(new() { key = key }); return this; }
	public LexGram<K> w { get { prods[^1].Add([]); return this; } }
	public LexGram<K> redo { get { prods[^1].Add(new() { redo = true }); return this; } }
	// string : byte sequence, ReadOnlyMemory<char> : inclusive range, .. : duplicate
	public LexGram<K> this[params object[] cons] {
		get {
			Alt a = [];
			prods[^1][^1].Add(a);
			foreach (var c in cons)
				if (c is Range) a[^1].dup = true;
				else if (c is string str) a.Add(new() { str = str });
				else if (c is ReadOnlyMemory<char> inc) a.Add(new() { inc = inc });
				else throw new($"wrong altern content {c?.GetType()}");
			return this;
		}
	}
	public LexGram<K> loop { get { prods[^1][^1][^1].loop = true; return this; } }
}

// lexic parser
// K for lexic key i.e lexeme
public partial class Lexier<K> : LexierBuf<K> where K : struct
{
	// each unit is just before next byte or after the last byte of wad
	sealed class Unit
	{
		internal int id;
		internal K key;
		internal int wad; // first is 1
		internal int prez; // for next[byte] are this unit, count bytes
		internal Unit[] next; // [next unit of byte], byte >= 128: [128]
		internal Unit go; // when next==null or next[byte]==null or backward
						  // go.next != null, go to begin: eor or error
		internal int mode; // match: -1, mismatch to error: -3, mismatch to backward bytes: >=0
						   // no backward cross wads nor duplicate bytes

		internal Unit(Lexier<K> l) => id = l.uid++;
	}

	readonly Unit begin;
	int uid;
	protected Lexer<byte, byte> read;
	int bz; // total bytes got
	int bf, bt; // read from loc to excluded loc for each wad
	readonly byte[] bytes = new byte[AltByteN + 1]; // {latest bytes}[read loc & AltByteN]
	protected int from; // read loc for current lexi

	public const int AltByteN = 15;

	public virtual IDisposable Begin(Lexer<byte, byte> read)
	{
		Dispose();
		this.read = read;
		return this;
	}

	// results keep available
	public override void Dispose()
	{
		base.Dispose();
		read?.Dispose(); read = null;
		bz = bf = bt = 0; from = -1;
	}

	public override bool Next()
	{
		if (loc.IncLess(size)) return true;
		var u = begin;
	Wad: bf = bt;
	Next: if (bt >= bz)
			if (read.Next()) {
				if ((bytes[bz++ & AltByteN] = read.Lex()) == '\n')
					lines.Add(bz);
			}
			else if (u == begin || bt > bz) {
				if (bz >= 0) {
					Eor(bz); // call only once
					bz = -1;
				}
				return loc < size;
			}
			else // eor but lexi not end
				goto Go;
		var b = bytes[bt & AltByteN];
		if (u.next[b < 128 ? b : 128] is Unit v) { // one byte by one, even for utf
			u = v; ++bt;
			if (u.next != null)
				goto Next;
		}
	Go: var go = u.go;
		if (u.mode >= 0) { // failed to greedy
			if (u.mode == 0)
				BuildBack(u);
			bt -= u.mode; u = u.go; go = u.go; // backward bytes directly
		}
		if (u.mode == -1) { // match a wad 
			var e = go == begin;
			Wad(u.key, u.wad, ref e, bf, bt);
			if (e) go = begin;
		}
		else { // error wad
			WadErr(u.key, u.wad, go == begin || bt >= bz,
				bt < bz ? bytes[bt & AltByteN] : -1, bf, bt);
			++bt; // shift a byte
		}
		if (go == begin && loc < size)
			return true;
		u = go;
		goto Wad;
	}

	private static void BuildBack(Unit u)
	{
		var go = u.go;
		if (go.mode < 0) u.mode = 1;
		else {
			BuildBack(go); u.mode = go.mode + 1; u.go = go.go;
		}
	}

	protected void BackByte(int to)
	{
		if (to < bz - AltByteN)
			throw new("back too much bytes");
		bt = to;
	}

	// make each wad of a lexi
	protected virtual void Wad(K key, int wad, ref bool end, int f, int to)
	{
		if (from < 0) from = f;
		if (end) {
			Span<byte> s = to - from <= 1024 ? stackalloc byte[to - from] : new byte[to - from];
			read.Lexs(from, to, s);
			Lexi(key, from, to, Encoding.UTF8.GetString(s));
			from = -1;
		}
	}

	// error wad. eor: Byte < 0
	protected virtual void WadErr(K key, int wad, bool end, int Byte, int f, int to)
	{
		if (from < 0) from = f;
		Error(key, f, to, Byte >= 0 ? (char)Byte : null);
		if (end) from = -1;
	}

	// from f loc of read to excluded loc
	protected void Lexi(K key, int f, int to, object value)
		=> Add(new() { key = key, from = f, to = to, value = value });
	// from f loc of read to excluded loc
	protected void Error(K key, int f, int to, object value)
	{
		var l = new Lexi<K> { key = key, from = f, to = to, value = value, err = -1 };
		if (errs != null) errs.Add(l); else Add(l);
	}

	// eor, read to excluded loc
	protected virtual void Eor(int to) { }
}

public abstract class LexierBuf<K> : LexerSeg<K, Lexi<K>> where K : struct
{
	protected int size, loc; // lexs size, current loc
	protected Lexi<K>[] lexs;
	public List<int> lines = []; // [0, loc of read after eol...]
	public List<Lexi<K>> errs = []; // [error lexi]: not null, merge to lexs: null

	public virtual void Dispose()
	{
		size = 0; loc = -1; lexs = [];
		lines.Clear(); lines.Add(0); errs?.Clear();
	}

	protected virtual void Add(Lexi<K> lex)
	{
		if (size == lexs.Length) Array.Resize(ref lexs, int.Max(lexs.Length << 1, 65536));
		lexs[size++] = lex;
	}

	public abstract bool Next();
	public int Loc() => loc;
	public Lexi<K> Lex() => lexs[loc];
	public Lexi<K> Lex(int loc) => lexs.AsSpan(0, size)[loc];

	public bool Is(K aim) => Is(lexs[loc].key, aim);
	public bool Is(int loc, K aim) => Is(Lex(loc).key, aim);
	public virtual bool Is(K key, K aim) => aim.Equals(key);

	public Span<Lexi<K>> Lexs(int from, int to, Span<Lexi<K>> s)
	{
		if (from >= size || to > size) throw new IndexOutOfRangeException();
		lexs.AsSpan(from, to - from).CopyTo(s);
		return s;
	}
	public ArraySegment<Lexi<K>> Lexs(int from, int to)
	{
		if (to > size) throw new IndexOutOfRangeException();
		return lexs.Seg(from, to);
	}
	IEnumerable<Lexi<K>> Lexer<K, Lexi<K>>.Lexs(int from, int to) => Lexs(from, to);

	public static IEnumerable<K> Keys_(string keys) => [Enum.Parse<K>(keys)];
	public IEnumerable<K> Keys(string keys) => Keys_(keys);

	// first line and col are 1, col is byte index inside line
	public (int line, int column) LineCol(int readLoc)
	{
		var line = lines.BinarySearch(readLoc);
		line = (line ^ line >> 31) + (~line >>> 31);
		return (line, readLoc - lines[line - 1] + 1);
	}

	// excluded to, ~from ~to for errs, first line and col are 1, col is byte index inside line
	public (int fromL, int fromC, int toL, int toC) LineCol(int from, int to)
	{
		if (size == 0)
			return (1, 1, 1, 1);
		int bf, bt;
		if (from >= 0) {
			bf = from < size ? Lex(from).from : Lex(from - 1).to;
			bt = from < to ? int.Max(Lex(to - 1).to, bf) : bf;
		}
		else {
			from = ~from; to = ~to;
			bf = from < errs.Count ? errs[from].from : errs[from - 1].to;
			bt = from < to ? int.Max(errs[to - 1].to, bf) : bf;
		}
		var (fl, fc) = LineCol(bf); var (tl, tc) = LineCol(bt);
		return (fl, fc, tl, tc);
	}
}

public partial class Lexier<K>
{
	public Lexier(LexGram<K> grammar, bool dumpGram = false)
		=> begin = new Build(this).Do(grammar, dumpGram);

	class Build(Lexier<K> ler)
	{
		internal Unit Do(LexGram<K> grammar, bool dump)
		{
			if (grammar.prods.Count < 1)
				throw new("No product");
			var begin = new Unit(ler);
			// build prod
			foreach (var prod in grammar.prods) {
				var k = prod.key;
				if (prod.Count == 0) throw new($"No wad in {k}");
				// first unit of each wad
				var wus = prod.Skip(1).Select((_, px) => new Unit(ler) { key = k, wad = px + 2 })
					.Prepend(begin).Prepend(null)
					.Append(begin).ToArray(); // current lexi end
				foreach (var (w, wad) in prod.Each(1)) {
					var u = wus[wad];
					if (w.Count == 0)
						throw new($"No altern in {k}.{wad}");
					if (w[0].Count == 0) // empty alt for option wad
						if (wad == 1) throw new($"Empty altern in first wad {k}.1");
						else if (w.Count == 1) throw new($"No byte in {k}.{wad}");
						else if (w.redo) throw new($"Redo empty {k}.{wad}.1");
						else { u.go = wus[wad + 1]; u.mode = -1; }
					else if (w.redo) // shift read then redo wad like the begin
						if (wad == 1) throw new($"Redo first wad {k}.1");
						else { u.go = u; u.mode = -3; }
					else // no backward cross wads
						{ u.go = begin; u.mode = -3; }
				}
				Unit[] aus = null;
				// build wad
				foreach (var (w, wad) in prod.Each(1)) {
					var u = wus[wad];
					// build alt
					var Aus = w.Select((a, alt) => {
						++alt;
						var bz = a.Sum(b => b.str.Length + (b.inc.Length > 0 ? 1 : 0));
						if (a.Count == 0 ? alt > 1 : bz == 0)
							throw new($"No byte in {k}.{wad}.{alt}");
						if (bz > AltByteN)
							throw new($"{k}.{wad}.{alt} exceeds {AltByteN} bytes");
						u = wus[wad];
						var ok = !a.loop ? wus[wad + 1] // go for match
							: wad > 1 ? u : throw new($"Can not loop first wad {k}.1.{alt}");
						var dup = w.redo ? u : begin; // error for repeat
						var bx = 0; // build units from contents
						foreach (var c in a) {
							for (int x = 0; x < c.str.Length; x++)
								Byte(c.str.AsMemory(x, 1), ref u, k, wad, ++bx >= bz, ok,
									c.dup && x == c.str.Length - 1 ? dup : null);
							if (c.inc.Length > 0)
								Byte(c.inc, ref u, k, wad, ++bx >= bz, ok, c.dup ? dup : null);
						}
						return u; // the last unit of this alt
					}).Where(u => u.next != null).ToArray();
					if (aus != null) {
						u = wus[wad];
						for (int x = 0; x <= 128; x++)
							if (u.next[x] != null && aus.Any(au => au.next[x] != null))
								throw new($"{k}.{wad} and {k}.{wad - 1} clash over dup");
					}
					aus = Aus;
				};
			}
			if (dump) Dump(begin, "");
			return begin;
		}

		void Byte(ReadOnlyMemory<char> bs,
			ref Unit u, K k, int wad, bool end, Unit ok, Unit dup)
		{
			// buid next
			var go = u; var mode = 0; // mismatch to backward
			if (end) // the last byte of alt
				{ go = ok; mode = -1; } // match wad
			else if (dup != null) // inside duplicate bytes
				{ go = dup; mode = -3; } // mismatch to error
			var next = Next(k, wad, u, bs.Span, go, mode, false);
			if (dup != null)
				Next(k, wad, next, bs.Span, go, mode, true);
			u = next;
		}

		Unit Next(K key, int wad, Unit u, ReadOnlySpan<char> bs, Unit go, int mode, bool dup)
		{
			Unit n = u.next?[bs[0]];
			if (u.next == null)
				u.next = new Unit[129];
			else
				// all exist nexts must be the same
				for (int x = 1; x < bs.Length; x++)
					if (u.next[bs[x]] != n)
						throw new($"Prefix '{bs[x]}' of {key}.{wad} and {(n ?? u.next[bs[x]]).key}"
							+ $".{(n ?? u.next[bs[x]]).wad} must be the same or distinct");
			if (n == null) {
				n = dup ? u // duplicate bytes
					: new Unit(ler) { key = key, wad = wad, prez = bs.Length, go = go, mode = mode };
				for (int x = 0; x < bs.Length; x++)
					u.next[bs[x]] = n;
				if (dup && u.mode == -1 && mode == -3)
					throw new($"{key}.{wad} and {u.key}.{u.wad} clash over byte dup");
			}
			else if (n.prez != bs.Length) // all already nexts must be the same
				throw new($"Prefixs of {key}.{wad} and {n.key}.{n.wad}"
					+ " must be the same or distinct");
			else if (dup != (n == u)) // already exist next must not clash
				throw new($"{key}.{wad} and {n.key}.{n.wad} clash over dup byte");
			else if (!dup)
				Mode(key, wad, n, go, mode); // check mode for already exist next
			return n;
		}

		static void Mode(K key, int wad, Unit u, Unit go, int mode)
		{
			if (u.mode + mode == -2) // both match
				throw new($"{key}.{wad} and {u.key}.{u.wad} clash over match");
			if (u.mode + mode == -6 && u.go != go) // both error
				throw new($"{key}.{wad} and {u.key}.{u.wad} clash over error");
			if (u.mode + mode == -3) // error and backward
				throw new($"{key}.{wad} and {u.key}.{u.wad} clash over duplicate");
			if (u.mode == 0 && mode != 0) {
				u.key = key; u.wad = wad; u.go = go; u.mode = mode;
			}
			else if (u.mode == -3 && mode == -1) {
				u.go = go; u.mode = mode;
			}
		}
	}
}
