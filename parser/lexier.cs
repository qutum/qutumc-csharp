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
public struct Lexi<K> where K : struct
{
	public K key;
	public int from, to; // input from loc to loc excluded
	public int err; // lexis ~loc before this error found
	public object value;

	public override readonly string ToString() => $"{key}{(err < 0 ? "!" : "=")}{value}";

	public readonly string ToString(Func<object, string> dumper)
		=> $"{key}{(err < 0 ? "!" : "=")}{dumper(value)}";
}

public class LexGram<K>
{
	public readonly List<Prod> prods = [];
	public class Prod : List<Wad> { public K key; }
	public class Wad : List<Alt>
	{
		// skip mismatched input and redo this wad
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
	public const int AltByteN = 15;

	public LexGram<K> k(K key) { prods.Add(new() { key = key }); return this; }
	public LexGram<K> w { get { prods[^1].Add([]); return this; } }
	public LexGram<K> redo { get { prods[^1].Add(new() { redo = true }); return this; } }
	// content: string : byte sequence, ReadOnlyMemory<char> : inclusive range, .. : duplicate
	public LexGram<K> this[params object[] cons] {
		get {
			Alt a = [];
			prods[^1][^1].Add(a);
			if (prods[^1][^1].Count == 1 && cons.Length == 1 && "".Equals(cons[0]))
				return this;
			foreach (var v in cons)
				if (v is Range) a[^1].dup = true;
				else if (v is string str) a.Add(new Con { str = str });
				else if (v is ReadOnlyMemory<char> inc) a.Add(new Con { inc = inc });
				else throw new($"wrong altern content {v?.GetType()}");
			return this;
		}
	}
	public LexGram<K> loop { get { prods[^1][^1][^1].loop = true; return this; } }
}

// lexic parser
public class Lexier<K> : Lexier<K, Lexi<K>> where K : struct
{
	public Lexier(LexGram<K> grammar, bool dumpGram = false) : base(grammar, dumpGram) { }

	// from input f loc to loc excluded
	protected override void Lexi(K key, int f, int to, object value)
		=> Lexi(new() { key = key, from = f, to = to, value = value });
	// from input f loc to loc excluded
	protected override void Error(K key, int f, int to, object value)
		=> Lexi(new() { key = key, from = f, to = to, value = value, err = ~lexn }, true);

	public override bool Is(int loc, K aim) => Is(Lex(loc).key, aim);

	// to is excluded, ~from ~to for errs, first line and col are 1, col is byte number inside line
	public (int fromL, int fromC, int toL, int toC) LineCol(int from, int to)
	{
		if (lexn == 0)
			return (1, 1, 1, 1);
		int bf, bt;
		if (from >= 0) {
			bf = from < lexn ? Lex(from).from : Lex(from - 1).to;
			bt = from < to ? Math.Max(Lex(to - 1).to, bf) : bf;
		}
		else {
			from = ~from; to = ~to;
			bf = from < errs.Count ? errs[from].from : errs[from - 1].to;
			bt = from < to ? Math.Max(errs[to - 1].to, bf) : bf;
		}
		var (fl, fc) = LineCol(bf); var (tl, tc) = LineCol(bt);
		return (fl, fc, tl, tc);
	}
}

public abstract class Lexier<K, L> : LexerSeg<K, L> where K : struct where L : struct // L : Lexi<K> fail
{
	// each unit is just before next byte or after the last byte of wad
	sealed class Unit
	{
		internal int id;
		internal K key;
		internal int wad; // first is 1
		internal int pren; // for next[byte] are this unit, count bytes
		internal Unit[] next; // [next unit of byte], byte >= 128: [128]
		internal Unit go; // when next==null or next[byte]==null or backward
						  // go.next != null, go to begin: input end or error
		internal int mode; // match: -1, mismatch to error: -3, mismatch to backward bytes: >=0
						   // no backward cross wads nor duplicate bytes

		internal Unit(Lexier<K, L> l) => id = ++l.uid;
	}

	readonly Unit begin;
	int uid;
	protected Lexer<byte, byte> input;
	int bn; // total bytes got
	int bf, bt; // from input loc to loc excluded for each wad
	readonly List<int> lines = []; // [0, input loc after eol...]
	readonly byte[] bytes = new byte[LexGram<K>.AltByteN + 1]; // [latest byte], @ input loc & AltByteN
	protected int lexn, loc; // lexi count and loc
	protected L[] lexs;
	protected int from; // input loc for current lexi
	public List<L> errs = []; // [error lexi]: not null, merge to lexs: null

	public virtual IDisposable Begin(Lexer<byte, byte> input)
	{
		Dispose(); Clear();
		this.input = input;
		return this;
	}

	// results keep available
	public virtual void Dispose() { input?.Dispose(); input = null; }

	public virtual void Clear()
	{
		bn = bf = bt = 0;
		lines.Clear(); lines.Add(0);
		lexn = 0; loc = -1; lexs = [];
		from = -1; errs?.Clear();
	}

	public bool Next()
	{
		if (++loc < lexn) return true;
		var u = begin;
	Step: bf = bt;
	Next: if (bt >= bn)
			if (input.Next()) {
				if ((bytes[bn++ & LexGram<K>.AltByteN] = input.Lex()) == '\n')
					lines.Add(bn);
			}
			else if (u == begin || bt > bn) {
				if (bn >= 0) {
					InputEnd(bn); // call only once
					bn = -1;
				}
				return loc < lexn;
			}
			else // input end but lexi not
				goto Go;
		var b = bytes[bt & LexGram<K>.AltByteN];
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
			WadErr(u.key, u.wad, go == begin || bt >= bn,
				bt < bn ? bytes[bt & LexGram<K>.AltByteN] : -1, bf, bt);
			++bt; // shift a byte
		}
		if (go == begin && loc < lexn)
			return true;
		u = go;
		goto Step;
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
		if (to < bn - LexGram<K>.AltByteN)
			throw new("back too much bytes");
		bt = to;
	}

	// make each wad of a lexi
	protected virtual void Wad(K key, int wad, ref bool end, int f, int to)
	{
		if (from < 0) from = f;
		if (end) {
			Span<byte> s = to - from <= 1024 ? stackalloc byte[to - from] : new byte[to - from];
			input.Lexs(from, to, s);
			Lexi(key, from, to, Encoding.UTF8.GetString(s));
			from = -1;
		}
	}

	// error wad, input end: Byte < 0
	protected virtual void WadErr(K key, int wad, bool end, int Byte, int f, int to)
	{
		if (from < 0) from = f;
		Error(key, f, to, Byte >= 0 ? (char)Byte : null);
		if (end) from = -1;
	}

	// input end
	protected virtual void InputEnd(int bn) { }

	protected void Lexi(L lexi, bool err = false)
	{
		if (err && errs != null)
			errs.Add(lexi);
		else {
			if (lexn == lexs.Length) Array.Resize(ref lexs, Math.Max(lexs.Length << 1, 65536));
			lexs[lexn++] = lexi;
		}
	}

	protected abstract void Lexi(K key, int f, int to, object value);
	protected abstract void Error(K key, int f, int to, object value);

	public int Loc() => Math.Min(loc, lexn);
	public L Lex() => lexs[loc];
	public L Lex(int loc) => lexs.AsSpan(0, lexn)[loc];

	public bool Is(K aim) => Is(loc, aim);
	public abstract bool Is(int loc, K aim);
	public virtual bool Is(K key, K aim) => Eq.Equals(key, aim);

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2211")]
	protected static EqualityComparer<K> Eq = EqualityComparer<K>.Default;

	public Span<L> Lexs(int from, int to, Span<L> s)
	{
		if (from >= lexn || to > lexn) throw new IndexOutOfRangeException();
		lexs.AsSpan(from, to - from).CopyTo(s);
		return s;
	}
	public ArraySegment<L> Lexs(int from, int to)
	{
		if (to > lexn) throw new IndexOutOfRangeException();
		return lexs.Seg(from, to);
	}
	IEnumerable<L> Lexer<K, L>.Lexs(int from, int to) => Lexs(from, to);

	public static IEnumerable<K> Keyz(string text) => [Enum.Parse<K>(text)];
	public virtual IEnumerable<K> Keys(string text) => Keyz(text);

	// first line and col are 1, col is byte number inside line
	public (int line, int column) LineCol(int inputLoc)
	{
		var line = lines.BinarySearch(inputLoc);
		line = (line ^ line >> 31) + (~line >>> 31);
		return (line, inputLoc - lines[line - 1] + 1);
	}

	static void Dump(Unit u, string pre, Dictionary<Unit, bool> us = null)
	{
		using var env = EnvWriter.Use();
		var uz = us ?? [];
		uz[u] = false; // dumped
		env.WriteLine($"{u.id}: {u.key}.{u.wad} " +
			$"{(u.mode >= 0 ? "back" : u.mode == -1 ? "ok" : "err")}.{u.go.id} < {pre}");
		if (!uz.ContainsKey(u.go))
			uz[u.go] = true; // not dumped yet
		if (u.next == null)
			return;
		foreach (var n in u.next.Where(n => n != null).Distinct()) {
			var s = u.next.Select(
				(nn, b) => nn != n ? null
				: b > ' ' && b < 127 ? ((char)b).ToString()
				: b == ' ' ? "\\s" : b == '\t' ? "\\t" : b == '\n' ? "\\n" : b == '\r' ? "\\r"
				: b >= 128 ? "\\U" : b == 0 ? "\\0"
				: $"\\x{b:x02}")
				.Where(x => x != null);
			using var ind = EnvWriter.Indent("  ");
			if (n == u)
				env.WriteLine($"+ < {string.Join(' ', s)}");
			else
				Dump(n, string.Join(' ', s), uz);
		}
	Go: if (us == null)
			foreach (var go in uz)
				if (go.Value) {
					Dump(go.Key, $"{go.Key.key}.{go.Key.wad - 1}", uz);
					goto Go;
				}
	}

	public Lexier(LexGram<K> grammar, bool dumpGram = false)
		=> begin = new Build(this).Do(grammar, dumpGram);

	class Build(Lexier<K, L> ler)
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
					else if (w.redo) // shift input and redo wad like the begin
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
						var bn = a.Sum(b => b.str.Length + (b.inc.Length > 0 ? 1 : 0));
						if (a.Count == 0 ? alt > 1 : bn == 0)
							throw new($"No byte in {k}.{wad}.{alt}");
						if (bn > LexGram<K>.AltByteN)
							throw new($"{k}.{wad}.{alt} exceeds {LexGram<K>.AltByteN} bytes");
						u = wus[wad];
						var ok = !a.loop ? wus[wad + 1] // go for match
							: wad > 1 ? u : throw new($"Can not loop first wad {k}.1.{alt}");
						var dup = w.redo ? u : begin; // error for repeat
						var bx = 0; // build units from contents
						foreach (var e in a) {
							for (int x = 0; x < e.str.Length; x++)
								Byte(e.str.Mem(x, x + 1), ref u, k, wad, ++bx >= bn, ok,
									e.dup && x == e.str.Length - 1 ? dup : null);
							if (e.inc.Length > 0)
								Byte(e.inc, ref u, k, wad, ++bx >= bn, ok, e.dup ? dup : null);
						}
						return u; // the last unit of this alt
					}).Where(u => u.next != null).ToArray();
					if (aus != null) {
						u = wus[wad];
						for (int x = 0; x <= 128; x++)
							if (u.next[x] != null && aus.Any(au => au.next[x] != null))
								throw new($"{k}.{wad} and {k}.{wad - 1} conflict over dup");
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
					: new Unit(ler) { key = key, wad = wad, pren = bs.Length, go = go, mode = mode };
				for (int x = 0; x < bs.Length; x++)
					u.next[bs[x]] = n;
				if (dup && u.mode == -1 && mode == -3)
					throw new($"{key}.{wad} and {u.key}.{u.wad} conflict over byte dup");
			}
			else if (n.pren != bs.Length) // all already nexts must be the same
				throw new($"Prefixs of {key}.{wad} and {n.key}.{n.wad}"
					+ " must be the same or distinct");
			else if (dup != (n == u)) // already exist next must not conflict
				throw new($"{key}.{wad} and {n.key}.{n.wad} conflict over dup byte");
			else if (!dup)
				Mode(key, wad, n, go, mode); // check mode for already exist next
			return n;
		}

		static void Mode(K key, int wad, Unit u, Unit go, int mode)
		{
			if (u.mode + mode == -2) // both match
				throw new($"{key}.{wad} and {u.key}.{u.wad} conflict over match");
			if (u.mode + mode == -6 && u.go != go) // both error
				throw new($"{key}.{wad} and {u.key}.{u.wad} conflict over error");
			if (u.mode + mode == -3) // error and backward
				throw new($"{key}.{wad} and {u.key}.{u.wad} conflict over duplicate");
			if (u.mode == 0 && mode != 0) {
				u.key = key; u.wad = wad; u.go = go; u.mode = mode;
			}
			else if (u.mode == -3 && mode == -1) {
				u.go = go; u.mode = mode;
			}
		}
	}
}
