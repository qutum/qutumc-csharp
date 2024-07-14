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
using System.Text;

namespace qutum.parser;

public struct Token<K> where K : struct
{
	public K key;
	public object value;
	public int from, to; // from input index to index excluded, scan.Loc()
	public int err; // token ~index this error found before

	public override readonly string ToString() => $"{key}{(err < 0 ? "!" : "=")}{value}";

	public readonly string ToString(Func<object, string> dumper)
	{
		return $"{key}{(err < 0 ? "!" : "=")}{dumper(value)}";
	}
}

public class Lexer<K> : LexerBase<K, Token<K>> where K : struct
{
	public Lexer(LexerGram<K> grammar, bool dump = false) : base(grammar, dump) { }

	protected int from = -1;
	public bool mergeErr = false; // add error token into corrent tokens
	public readonly List<Token<K>> errs = [];

	public override IDisposable Load(Scan<byte, byte> scan)
	{
		base.Load(scan);
		from = -1; errs.Clear();
		return this;
	}

	protected override void Token(K key, int part, ref bool end, int f, int to)
	{
		if (from < 0) from = f;
		if (end) {
			Span<byte> bs = to - from <= 1024 ? stackalloc byte[to - from] : new byte[to - from];
			scan.Tokens(from, to, bs);
			Add(key, from, to, Encoding.UTF8.GetString(bs));
			from = -1;
		}
	}

	protected override void Error(K key, int part, bool end, int b, int f, int to)
	{
		if (from < 0) from = f;
		if (part >= 0) AddErr(key, f, to, b >= 0 ? (object)(char)b : null);
		if (end) from = -1;
	}

	protected virtual void Add(K key, int f, int to, object value)
	{
		Add(new Token<K> { key = key, from = f, to = to, value = value });
	}

	protected void AddErr(K key, int f, int to, object value)
	{
		var e = new Token<K> { key = key, from = f, to = to, value = value, err = ~tokenn };
		if (mergeErr) Add(e);
		else errs.Add(e);
	}

	protected static EqualityComparer<K> Eq = EqualityComparer<K>.Default;

	public sealed override bool Is(int loc, K key) => Is(Token(loc).key, key);

	public override bool Is(K testee, K key) => Eq.Equals(testee, key);

	public static IEnumerable<K> Keyz(string text) => [Enum.Parse<K>(text)];
	public override IEnumerable<K> Keys(string text) => Keyz(text);

	public (int fromL, int fromC, int toL, int toC) LineCol(int from, int to)
	{
		if (tokenn == 0)
			return (1, 1, 1, 1);
		int f, t;
		if (from >= 0) {
			f = from < tokenn ? Token(from).from : Token(from - 1).to;
			t = from < to ? Math.Max(Token(to - 1).to - 1, f) : f;
		}
		else {
			from = ~from; to = ~to;
			f = from < errs.Count ? errs[from].from : errs[from - 1].to;
			t = from < to ? Math.Max(errs[to - 1].to - 1, f) : f;
		}
		var (fl, fc) = LineCol(f); var (tl, tc) = LineCol(t);
		return (fl, fc, tl, tc);
	}
}

public abstract partial class LexerBase<K, T> : ScanSeg<K, T> where T : struct
{
	// each unit is just before next byte or after last byte of part
	internal sealed class Unit
	{
		internal int id;
		internal K key;
		internal int part; // first is 1
		internal int pren; // number of bytes to this unit
		internal Unit[] next; // >=128: [128]
		internal Unit go; // when next==null or next[byte]==null or backward
						  // go.next != null, go to start: token end or error
		internal int mode; // match: -1, mismatch to error: -3, mismatch to backward bytes: >=0
						   // no backward cross parts nor inside byte repeat

		internal Unit(LexerBase<K, T> l) => id = ++l.id;
	}

	readonly Unit start;
	int id;
	internal Scan<byte, byte> scan;
	int bn; // total bytes got
	int bf, bt; // from index to index excluded for each part
	readonly byte[] bytes = new byte[17]; // latest bytes, [byte index & 15]
	internal int tokenn, loc = -1;
	internal T[] tokens = new T[65536];
	readonly List<int> lines = [];

	public virtual IDisposable Load(Scan<byte, byte> scan)
	{
		this.scan = scan;
		bf = bt = bn = tokenn = 0; loc = -1;
		lines.Clear(); lines.Add(0);
		return this;
	}

	// lexer results keep available
	public virtual void Dispose()
	{
		scan.Dispose(); scan = null;
		Array.Fill(tokens, default, 0, tokenn);
	}

	public bool Next()
	{
		if (++loc < tokenn) return true;
		var u = start;
	Step: bf = bt;
	Next: if (bt >= bn)
			if (scan.Next()) {
				if ((bytes[bn++ & 15] = scan.Token()) == '\n')
					lines.Add(bn);
			}
			else if (u == start || bt > bn) {
				if (bn >= 0) {
					Error(start.key, -1, true, -1, bn, bn);
					bn = -1; // only one error at the end
				}
				return loc < tokenn;
			}
			else // scan ended, token not
				goto Go;
		var b = bytes[bt & 15];
		if (u.next[b < 128 ? b : 128] is Unit v) { // one byte by one, even for utf
			u = v; ++bt;
			if (u.next != null)
				goto Next;
		}
	Go: var go = u.go;
		if (u.mode >= 0) { // failed to greedy
			if (u.mode == 0)
				Backward(u); // backward bytes directly
			bt -= u.mode; u = u.go; go = u.go;
		}
		if (u.mode == -1) { // match a part 
			var e = go == start;
			Token(u.key, u.part, ref e, bf, bt);
			if (e) go = start;
		}
		else { // error part
			Error(u.key, u.part, go == start || bt >= bn,
				bt < bn ? bytes[bt & 15] : -1, bf, bt);
			++bt; // shift a byte
		}
		if (go == start && loc < tokenn)
			return true;
		u = go;
		goto Step;
	}

	private static void Backward(Unit u)
	{
		var go = u.go;
		if (go.mode < 0) u.mode = 1;
		else {
			Backward(go); u.mode = go.mode + 1; u.go = go.go;
		}
	}

	// make each part of a token
	protected abstract void Token(K key, int part, ref bool end, int from, int to);

	// report an error: part >= 0, end of scan: part < 0 and Byte < 0
	protected abstract void Error(K key, int part, bool end, int Byte, int from, int to);

	protected void Add(T token)
	{
		if (tokenn == tokens.Length) Array.Resize(ref tokens, tokens.Length << 1);
		tokens[tokenn++] = token;
	}

	public int Loc() => Math.Min(loc, tokenn);

	public T Token() => tokens[loc];

	public bool Is(K key) => Is(loc, key);

	public abstract bool Is(int loc, K key);

	public abstract bool Is(K key, K testee);

	public T Token(int loc) => loc < tokenn ? tokens[loc] : throw new IndexOutOfRangeException();

	public ArraySegment<T> Tokens(int from, int to)
	{
		if (to > tokenn) throw new IndexOutOfRangeException();
		return tokens.Seg(from, to);
	}

	IEnumerable<T> Scan<K, T>.Tokens(int from, int to) => Tokens(from, to);

	public Span<T> Tokens(int from, int to, Span<T> s)
	{
		if (from >= tokenn || to > tokenn) throw new IndexOutOfRangeException();
		tokens.AsSpan(from, to - from).CopyTo(s);
		return s;
	}

	public abstract IEnumerable<K> Keys(string text);

	// first line and col are 1
	public (int line, int column) LineCol(int byteLoc)
	{
		var line = lines.BinarySearch(byteLoc);
		line = (line ^ line >> 31) + (line >> 31) + 1;
		return (line, byteLoc - lines[line - 1] + 1);
	}

	static void Dump(Unit u, string pre, Dictionary<Unit, bool> us = null)
	{
		using var env = EnvWriter.Use();
		var uz = us ?? [];
		uz[u] = false; // dumped
		env.WriteLine($"{u.id}: {u.key}.{u.part} " +
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
					Dump(go.Key, $"{go.Key.key}.{go.Key.part - 1}", uz);
					goto Go;
				}
	}
}

public class LexerGram<K>
{
	public readonly List<Prod> prods = [];
	public class Prod : List<Part> { public K key; }
	public class Part : List<Alt>
	{
		// skip mismatched input and retry this part
		public bool skip;
	}
	public class Alt : List<Con>
	{
		// match to loop part
		public bool loop;
	}
	public class Con
	{
		// byte sequence
		public string str = "";
		// inclusive range of one byte
		public ReadOnlyMemory<char> inc;
		// repeat last byte of sequence, or repeat inclusive range
		public bool rep;
	}
	public const int AltByteN = 15;

	public LexerGram<K> prod(K key) { prods.Add(new Prod { key = key }); return this; }
	public LexerGram<K> part { get { prods[^1].Add([]); return this; } }
	public LexerGram<K> skip { get { prods[^1].Add(new() { skip = true }); return this; } }
	// content: string : byte sequence, ReadOnlyMemory<char> : inclusive range, .. : repeat
	public LexerGram<K> this[params object[] cons] {
		get {
			Alt a = [];
			prods[^1][^1].Add(a);
			if (prods[^1][^1].Count == 1 && cons.Length == 1 && "".Equals(cons[0]))
				return this;
			foreach (var v in cons)
				if (v is Range) a[^1].rep = true;
				else if (v is string str) a.Add(new Con { str = str });
				else if (v is ReadOnlyMemory<char> inc) a.Add(new Con { inc = inc });
				else throw new($"wrong altern content {v?.GetType()}");
			return this;
		}
	}
	public LexerGram<K> loop { get { prods[^1][^1][^1].loop = true; return this; } }
}

public partial class LexerBase<K, T>
{
	public LexerBase(LexerGram<K> gram, bool dump = false)
	{
		if (gram.prods.Count < 1)
			throw new("No product");
		start = new Unit(this);
		// build prod
		foreach (var prod in gram.prods) {
			var k = prod.key;
			if (prod.Count == 0) throw new($"No part in {k}");
			// first unit of each part
			var pus = prod.Skip(1).Select((_, px) => new Unit(this) { key = k, part = px + 2 })
				.Prepend(start).Prepend(null)
				.Append(start).ToArray(); // token end
			prod.Each((p, part) => {
				var u = pus[++part]; // first part is 1
				if (p.Count == 0)
					throw new($"No altern in {k}.{part}");
				if (p[0].Count == 0) // empty alt for option part
					if (part == 1) throw new($"Empty altern in first part {k}.1");
					else if (p.Count == 1) throw new($"No byte in {k}.{part}");
					else if (p.skip) throw new($"Skip at empty {k}.{part}.1");
					else { u.go = pus[part + 1]; u.mode = -1; }
				else if (p.skip) // shift input and retry part like the start
					if (part == 1) throw new($"Skip at first part {k}.1");
					else { u.go = u; u.mode = -3; }
				else // no backward cross parts
					{ u.go = start; u.mode = -3; }
			});
			Unit[] aus = null;
			// build part
			prod.Each((p, part) => {
				var u = pus[++part];
				// build alt
				var Aus = p.Select((a, alt) => {
					++alt;
					var bn = a.Sum(b => b.str.Length + (b.inc.Length > 0 ? 1 : 0));
					if (a.Count == 0 ? alt > 1 : bn == 0)
						throw new($"No byte in {k}.{part}.{alt}");
					if (bn > LexerGram<K>.AltByteN)
						throw new($"{k}.{part}.{alt} exceeds {LexerGram<K>.AltByteN} bytes");
					u = pus[part];
					var ok = !a.loop ? pus[part + 1] // go for match
						: part > 1 ? u : throw new($"Can not loop first part {k}.1.{alt}");
					var rep = p.skip ? u : start; // error for repeat
					var bx = 0; // build units from contents
					foreach (var e in a) {
						for (int x = 0; x < e.str.Length; x++)
							BuildByte(e.str.Mem(x, x + 1), ref u, k, part, ++bx >= bn, ok,
								e.rep && x == e.str.Length - 1 ? rep : null);
						if (e.inc.Length > 0)
							BuildByte(e.inc, ref u, k, part, ++bx >= bn, ok, e.rep ? rep : null);
					}
					return u; // the last unit of this alt
				}).Where(u => u.next != null).ToArray();
				if (aus != null) {
					u = pus[part];
					for (int x = 0; x <= 128; x++)
						if (u.next[x] != null && aus.Any(au => au.next[x] != null))
							throw new($"{k}.{part} and {k}.{part - 1} conflict over repeat");
				}
				aus = Aus;
			});
		}
		if (dump) Dump(start, "");
	}

	void BuildByte(ReadOnlyMemory<char> bs,
		ref Unit u, K k, int part, bool end, Unit ok, Unit rep)
	{
		// buid next
		var go = u; var mode = 0; // mismatch to backward
		if (end) // the last byte of alt
			{ go = ok; mode = -1; } // match part
		else if (rep != null) // inside byte repeat
			{ go = rep; mode = -3; } // mismatch to error
		var next = BuildNext(k, part, u, bs.Span, go, mode, false);
		if (rep != null)
			BuildNext(k, part, next, bs.Span, go, mode, true);
		u = next;
	}

	Unit BuildNext(K key, int part, Unit u, ReadOnlySpan<char> ns, Unit go, int mode, bool rep)
	{
		Unit n = u.next?[ns[0]];
		if (u.next == null)
			u.next = new Unit[129];
		else
			// all exist nexts must be the same
			for (int x = 1; x < ns.Length; x++)
				if (u.next[ns[x]] != n)
					throw new($"Prefix '{ns[x]}' of {key}.{part} and {(n ?? u.next[ns[x]]).key}"
						+ $".{(n ?? u.next[ns[x]]).part} must be the same or distinct");
		if (n == null) {
			n = rep ? u // repeat byte
				: new Unit(this) { key = key, part = part, pren = ns.Length, go = go, mode = mode };
			for (int x = 0; x < ns.Length; x++)
				u.next[ns[x]] = n;
			if (rep && u.mode == -1 && mode == -3)
				throw new($"{key}.{part} and {u.key}.{u.part} conflict over repeat match");
		}
		else if (n.pren != ns.Length) // all already nexts must be the same
			throw new($"Prefixs of {key}.{part} and {n.key}.{n.part}"
				+ " must be the same or distinct");
		else if (rep != (n == u)) // already exist next must not conflict
			throw new($"{key}.{part} and {n.key}.{n.part} conflict over byte repeat");
		else if (!rep)
			BuildMode(key, part, n, go, mode); // check mode for already exist next
		return n;
	}

	static void BuildMode(K key, int part, Unit u, Unit go, int mode)
	{
		if (u.mode + mode == -2) // both match
			throw new($"{key}.{part} and {u.key}.{u.part} conflict over match");
		if (u.mode + mode == -6 && u.go != go) // both error
			throw new($"{key}.{part} and {u.key}.{u.part} conflict over error");
		if (u.mode + mode == -3) // error and backward
			throw new($"{key}.{part} and {u.key}.{u.part} conflict over repeat");
		if (u.mode == 0 && mode != 0) {
			u.key = key; u.part = part; u.go = go; u.mode = mode;
		}
		else if (u.mode == -3 && mode == -1) {
			u.go = go; u.mode = mode;
		}
	}
}
