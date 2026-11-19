//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
// key ordinal index
global using Kordx = qutum.parser.X<char>;
// name ordinal index
global using Nordx = qutum.parser.X<ushort>;
// alt index
global using Altx = qutum.parser.X<qutum.parser.XAlt>;
// alt.con index
global using Conx = qutum.parser.X<qutum.parser.XAltCon>;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace qutum.parser;

// lexic token
public partial struct Lexi<K> where K : struct
{
	public K key;
	public JovBx j; // jot of read: on >=0, jot of error read: on <0
	public int err; // error: <0, no error: >=0
	public object value;
}

// lexic grammar
public partial class LexGram<K> where K : struct
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
	public class Alt : List<Conx, Con>
	{
		// match then loop wad
		public bool loop;
	}
	public struct Con
	{
		// byte sequence
		public string str;
		// one byte set
		public ByteSet.One one;
		// duplicate latest byte of sequence, or duplicate latest one byte of set
		public bool dup;
	}

	// add a product with key
	public LexGram<K> k(K key) { prods.Add(new() { key = key }); return this; }
	// add a wad to latest product
	public LexGram<K> w { get { prods[^1].Add([]); return this; } }
	// add a redo wad to latest Prod
	public LexGram<K> redo { get { prods[^1].Add(new() { redo = true }); return this; } }
	// add a altern to latest wad
	// string : byte sequence, ByteSet.One : one byte set, .. : duplicate
	public LexGram<K> this[params object[] cons] {
		get {
			Alt a = [];
			prods[^1][^1].Add(a);
			foreach (var c in cons)
				if (c is Range) a.tail.dup = true;
				else if (c is string str) a.Ad(new() { str = str });
				else if (c is ByteSet.One one) a.Ad(new() { str = "", one = one });
				else throw new($"wrong altern content {c?.GetType()}");
			return this;
		}
	}
	// mark latest altern loop
	public LexGram<K> loop { get { prods[^1][^1][^1].loop = true; return this; } }
}

// lexic parser
// K for lexic key i.e lexeme
public partial class Lexier<K> : LexierArr<K> where K : struct
{
	// each unit is just before next byte or after latest byte of wad
	sealed class Unit
	{
		internal int id;
		internal K key;
		internal int wad; // first is 1
		internal int prez; // count bytes which next[byte] are this unit
		internal Unit[] next; // [next unit of byte], byte >= 128: [128]
		internal Unit go; // when next==null or next[byte]==null or backward
						  // go.next != null, go to begin: eor or error
		internal int mode; // match: -1, mismatch to error: -3, mismatch to backward bytes: >=0
						   // no backward cross wads nor duplicate bytes

		internal Unit(Lexier<K> l) => id = l.uid++;
		internal string Kw => field ??= Kw(key, wad);
	}

	readonly Unit begin;
	int uid;
	protected Lexer<byte, byte> read;
	Bytex bz; // total bytes got
	readonly Arr<Bytex, byte> bytes = new((Bytex)AltBytez + 1); // {latest bytes}[read.loc & AltBytez]
	JovBx bj; // jot of read for each wad
	protected Bytex lon; // read.loc for current lexi

	public const int AltBytez = 15;
	ref byte Byte(Bytex x) => ref bytes.s[+x & AltBytez];

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
		bz = Bytex.Z; bj = default; lon = Bytex.No;
	}

	public override bool Next()
	{
		if (loc.IncLess(+lexs.size)) return true;
		var u = begin; byte b;
	Wad: bj.on = bj.via;
	Next: if (bj.via >= bz)
			if (read.Next()) {
				b = Byte(bz++) = read.Lex();
				if (b == '\n')
					lines.Add(bz);
			}
			else if (u == begin || bj.via > bz) {
				if (bz.yes) {
					Eor(bz); // call only once
					bz = Bytex.No;
				}
				return loc < lexs.size;
			}
			else // eor but lexi not end
				goto Go;
		b = Byte(bj.via);
		if (u.next[b < 128 ? b : 128] is Unit v) { // one byte by one, even for utf
			u = v; bj.via++;
			if (u.next != null)
				goto Next;
		}
	Go: var go = u.go;
		if (u.mode >= 0) { // failed to greedy
			if (u.mode == 0)
				BuildBack(u);
			bj.via -= u.mode; u = u.go; go = u.go; // backward bytes directly
		}
		if (u.mode == -1) { // match a wad 
			var e = go == begin;
			Wad(u.key, u.wad, ref e, bj);
			if (e) go = begin;
		}
		else { // error wad
			WadErr(u.key, u.wad, go == begin || bj.via >= bz,
				bj.via < bz ? Byte(bj.via) : -1, bj);
			bj.via++; // shift a byte
		}
		if (go == begin && loc < lexs.size)
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

	protected void BackByte(Bytex via)
	{
		if (via < bz - AltBytez)
			throw new("back too much bytes");
		bj.via = via;
	}

	// make each wad of a lexi
	protected virtual void Wad(K key, int wad, ref bool end, JovBx j)
	{
		if (lon.no) lon = j.on;
		if (end) {
			JovBx lj = (lon, j.via);
			Span<byte> s = +lj.size <= 1024 ? stackalloc byte[+lj.size] : new byte[+lj.size];
			read.Lexs(lj, s);
			Lexi(key, lj, Encoding.UTF8.GetString(s));
			lon = Bytex.No;
		}
	}

	// error wad. eor: Byte < 0
	protected virtual void WadErr(K key, int wad, bool end, int Byte, JovBx j)
	{
		if (lon.no) lon = j.on;
		Error(key, j, Byte >= 0 ? (char)Byte : null);
		if (end) lon = Bytex.No;
	}

	protected void Lexi(K key, JovBx j, object value) => Add(new() { key = key, j = j, value = value });
	protected void Error(K key, JovBx j, object value)
	{
		var l = new Lexi<K> { key = key, j = j, value = value, err = -1 };
		if (errs != null) errs.Add(l); else Add(l);
	}

	// exclusive loc via end of read
	protected virtual void Eor(Bytex via) { }
}

public abstract class LexierArr<K> : Lexer<K, Lexi<K>> where K : struct
{
	protected X<Lexi<K>> loc; // lexs size, current loc
	protected Arr<X<Lexi<K>>, Lexi<K>> lexs;
	public List<Bytex> lines = []; // [0, read.loc after eol...], assume read.loc is byte index
	public List<Lexi<K>> errs = []; // [error lexi]: not null, merge to lexs: null

	public virtual void Dispose()
	{
		loc = X<Lexi<K>>.No; lexs = [];
		lines.Clear(); lines.Add(Bytex.Z); errs?.Clear();
	}

	protected virtual void Add(Lexi<K> lex) => lexs.Ad(lex);

	public abstract bool Next();
	public X<Lexi<K>> Loc() => loc;
	public Lexi<K> Lex() => lexs[loc];
	public Lexi<K> Lex(X<Lexi<K>> loc) => lexs[loc];

	public bool Is(K aim) => Is(lexs[loc].key, aim);
	public bool Is(X<Lexi<K>> loc, K aim) => Is(Lex(loc).key, aim);
	public virtual bool Is(K key, K aim) => aim.Equals(key);

	public Span<Lexi<K>> Lexs(Jov<X<Lexi<K>>> j, Span<Lexi<K>> s)
	{
		lexs.Span[j.range].CopyTo(s); return s;
	}
	public IEnumerable<Lexi<K>> Lexs(Jov<X<Lexi<K>>> j) => lexs.Jot(j);

	public static IEnumerable<K> Keys_(string keys) => [Enum.Parse<K>(keys)];
	public IEnumerable<K> Keys(string keys) => Keys_(keys);

	// first line and column are 1, column is byte index inside line
	public (int line, int col) LineCol(Bytex readLoc)
	{
		var line = lines.BinarySearch(readLoc);
		line = (line ^ line >> 31) + (~line >>> 31);
		return (line, +readLoc - +lines[line - 1] + 1);
	}
	public (int line, int col) LineCol(X<Lexi<K>> loc) => LineCol(Lex(loc).j.on);

	// first line and column are 1, column is byte index inside line, error: ~err index
	public (Jot line, Jov col) LineCol(Jov<X<Lexi<K>>> jot)
	{
		if (lexs.size.zero)
			return ((1, 1), (1, 1));
		var (on, via) = jot; Bytex bo, bv;
		if (+on >= 0) {
			bo = on < lexs.size ? Lex(on).j.on : Lex(on - 1).j.via;
			bv = on < via ? Lex(via - 1).j.via.Max(bo) : bo;
		}
		else {
			on = ~on; via = ~via;
			bo = +on < errs.Count ? errs[+on].j.on : errs[+on - 1].j.via;
			bv = on < via ? errs[+via - 1].j.via.Max(bo) : bo;
		}
		var (lo, co) = LineCol(bo); var (lt, cv) = LineCol(bv);
		return ((lo, lt), (co, cv));
	}
}

public partial class Lexier<K>
{
	internal static partial string Kw(K key, int wad);

	public Lexier(LexGram<K> grammar, bool dumpGram = false)
		=> begin = new Build(this).Do(grammar, dumpGram);

	class Build(Lexier<K> ler)
	{
		internal Unit Do(LexGram<K> grammar, bool dump)
		{
			if (grammar.prods.Count < 1)
				throw new("No product");
			if (dump)
				grammar.Dump();
			var begin = new Unit(ler);
			var aus = new Unit[grammar.prods.Max(p => p.Count) + 1][];
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
						throw new($"No altern in {Kw(k, wad)}");
					if (w[0].size.zero) // empty alt for option wad
						if (wad == 1) throw new($"Empty altern in first wad {Kw(k, 1)}");
						else if (w.Count == 1) throw new($"No byte in {Kw(k, wad)}");
						else if (w.redo) throw new($"Redo empty {Kw(k, wad)}.1");
						else { u.go = wus[wad + 1]; u.mode = -1; }
					else if (w.redo) // shift read then redo wad like the begin
						if (wad == 1) throw new($"Redo first wad {Kw(k, 1)}");
						else { u.go = u; u.mode = -3; }
					else // no backward cross wads
						{ u.go = begin; u.mode = -3; }
				}
				// build wad
				foreach (var (wad, wx) in prod.Each(1)) {
					// build alt
					aus[wx] = [.. wad.Select((alt, ax) => {
						++ax;
						var bz = (Bytex)alt.Sum(b => b.str.Length + (b.one.size > 0 ? 1 : 0));
						if (alt.size.zero ? ax > 1 : bz.zero)
							throw new($"No byte in {Kw(k, wx)}.{ax}");
						if (+bz > AltBytez)
							throw new($"{Kw(k, wx)}.{ax} exceeds {AltBytez} bytes");
						var u = wus[wx];
						var ok = !alt.loop ? wus[wx + 1] // go for match
							: wx > 1 ? u : throw new($"Can not loop first wad {Kw(k, 1)}.{ax}");
						var dup = wad.redo ? u : begin; // error for repeat
						var bx = Bytex.Z; // build units from contents
						foreach (var c in alt) {
							for (int x = 0; x < c.str.Length; x++)
								Byte(c.str.AsMemory(x, 1), ref u, k, wx, ++bx >= bz, ok,
									c.dup && x == c.str.Length - 1 ? dup : null);
							if (c.one.size > 0)
								Byte(c.one, ref u, k, wx, ++bx >= bz, ok, c.dup ? dup : null);
						}
						return u; // the last unit of this alt
					}).Where(u => u.next != null)];
					var u = wus[wx];
					if (aus[wx - 1] != null)
						for (int x = 0; x <= 128; x++) {
							if (u.next[x] == null) continue;
							for (int y = wx - 1; aus[y] != null; y--)
								if (aus[y].Any(au => au.next[x] != null))
									throw new($"{Kw(k, wx)} and {Kw(k, y)} clash over dup");
						}
					if (wad[0].size.pos)
						aus[wx - 1] = null;
				}
			}
			if (dump) Dump(begin, "");
			return begin;
		}

		void Byte(ByteSet.One bs, ref Unit u, K k, int wad, bool end, Unit ok, Unit dup)
		{
			// buid next
			var go = u; var mode = 0; // mismatch to backward
			if (end) // is last byte of alt
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
						throw new($"Prefix '{bs[x]}' of {Kw(key, wad)} and {(n ?? u.next[bs[x]]).Kw}"
							+ " must be the same or distinct");
			if (n == null) {
				n = dup ? u // duplicate bytes
					: new Unit(ler) { key = key, wad = wad, prez = bs.Length, go = go, mode = mode };
				for (int x = 0; x < bs.Length; x++)
					u.next[bs[x]] = n;
				if (dup && u.mode == -1 && mode == -3)
					throw new($"{Kw(key, wad)} and {u.Kw} clash over byte dup");
			}
			else if (n.prez != bs.Length) // all already nexts must be the same
				throw new($"Prefixs of {Kw(key, wad)} and {n.Kw}"
					+ " must be the same or distinct");
			else if (dup != (n == u)) // already exist next must not clash
				throw new($"{Kw(key, wad)} and {n.Kw} clash over dup byte");
			else if (!dup)
				Mode(key, wad, n, go, mode); // check mode for already exist next
			return n;
		}

		static void Mode(K key, int wad, Unit u, Unit go, int mode)
		{
			if (u.mode + mode == -2) // both match
				throw new($"{Kw(key, wad)} and {u.Kw} clash over match");
			if (u.mode + mode == -6 && u.go != go) // both error
				throw new($"{Kw(key, wad)} and {u.Kw} clash over error");
			if (u.mode + mode == -3) // error and backward
				throw new($"{Kw(key, wad)} and {u.Kw} clash over duplicate");
			if (u.mode == 0 && mode != 0) {
				u.key = key; u.wad = wad; u.go = go; u.mode = mode;
			}
			else if (u.mode == -3 && mode == -1) {
				u.go = go; u.mode = mode;
			}
		}
	}
}

public struct XAlt { }
public struct XAltCon { }
