using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace qutum.parser
{
	public struct Token<K> where K : struct
	{
		public K key;
		public object value;
		public bool err;
		public int from, to; // from input byte index to index excluded, scan.Loc()

		public string Dump() => $"{key}{(err ? "!" : "=")}{value}";
	}

	public class LexerEnum<K> : Lexer<K, Token<K>> where K : struct
	{
		public LexerEnum(string grammar, Scan<IEnumerable<byte>, byte, byte,
			IEnumerable<byte>> scan = null)
			: base(grammar, scan ?? new ScanByte()) { }

		protected int from = -1;

		protected override void Token(K key, int step, ref bool end, int f, int to)
		{
			if (from < 0) from = f;
			if (end) {
				Span<byte> bs = to - from <= 1024 ? stackalloc byte[to - from] : new byte[to - from];
				scan.Tokens(from, to, bs);
				Add(key, from, to, Encoding.UTF8.GetString(bs));
				from = -1;
			}
		}

		protected override void Error(K key, int step, bool end, byte? b, int f, int to)
		{
			if (from < 0) from = f;
			if (step >= 0) Add(key, f, to, (char?)b ?? (object)"", true);
			if (end) from = -1;
		}

		protected void Add(K key, int f, int to, object value, bool err = false)
		{
			Add(new Token<K> {
				key = key, from = f, to = to, value = value, err = err
			});
		}

		protected static EqualityComparer<K> Eq = EqualityComparer<K>.Default;

		public override bool Is(K key) => Eq.Equals(tokens[loc].key, key);

		public override bool Is(K key1, K key) => Eq.Equals(key1, key);

		public override IEnumerable<K> Keys(string text) => new[] { Enum.Parse<K>(text) };
	}

	public abstract class Lexer<K, T> : Scan<IEnumerable<byte>, K, T, ArraySegment<T>> where T : struct
	{
		sealed class Unit
		{
			internal int id;
			internal int pren; // how many bytes before this unit
			internal K key;
			internal int step; // first is 1
			internal Unit[] next; // utf-8: <=bf [128], <=df [129], <=ef [130], <=f7 [131], ff [132]
			internal int mode; // no quantifier: err: -2, back: 0, ok: 2; quantifier: err: -1, ok: 3
			internal Unit go;   // when next==null or next[byte]==null, go.next != null
								// to start: token or err

			internal Unit(Lexer<K, T> l) => id = ++l.id;
		}

		readonly Unit start;
		int id;
		int bn; // total bytes gots
		int bf, bt; // from index to index excluded for each step
		internal Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan;
		internal byte[] bytes = new byte[17]; // latest bytes, [byte index & 15]
		internal T[] tokens = new T[65536];
		internal int tokenn, loc = -2;
		internal List<int> lines = new List<int>();

		public virtual void Load(IEnumerable<byte> input)
		{
			if (loc > -2) Unload();
			scan.Load(input); Debug.Assert(scan.Loc() == -1);
			bf = bt = bn = 0;
			loc = -1; lines.Add(0);
		}

		public virtual void Unload() { scan.Unload(); tokenn = 0; loc = -2; lines.Clear(); }

		public bool Next()
		{
			if (++loc < tokenn) return true;
			var u = start;
		Step: bf = bt;
		Next: if (bt >= bn)
				if (scan.Next()) {
					if ((bytes[bn++ & 15] = scan.Token()) == '\n') lines.Add(bn);
				}
				else if (u == start || bt > bn) {
					if (bn >= 0) Error(start.key, -1, true, null, bn, bn = -1);
					return loc < tokenn;
				}
				else
					goto Go;
			var b = bytes[bt & 15];
			if (u.next[b < 128 ? b : b > 0xf7 ? 132 : b > 0xef ? 131 : b > 0xdf ? 130 : b > 0xbf ? 129 : 128]
					is Unit v) {
				u = v; ++bt;
				if (u.next != null)
					goto Next;
			}
		Go: v = u.go;
			if (u.mode == 0) {
				u = v; --bt; // back
				goto Go;
			}
			else if (u.mode > 0) {
				var e = v == start;
				Token(u.key, u.step, ref e, bf, bt);
				if (e) v = start;
			}
			else
				Error(u.key, u.step, v == start || bt >= bn,
					bt < bn ? bytes[bt & 15] : (byte?)null, bf, ++bt);
			if (v == start && loc < tokenn)
				return true;
			u = v;
			goto Step;
		}

		// make each step of a token
		protected abstract void Token(K key, int step, ref bool end, int from, int to);

		// an error step found
		protected abstract void Error(K key, int step, bool end, byte? b, int from, int to);

		protected void Add(T token)
		{
			if (tokenn == tokens.Length) Array.Resize(ref tokens, tokens.Length << 1);
			tokens[tokenn++] = token;
		}

		public int Loc() => Math.Min(loc, tokenn);

		public T Token() => tokens[loc];

		public abstract bool Is(K key);

		public abstract bool Is(K key1, K key);

		public ArraySegment<T> Tokens(int from, int to)
		{
			if (to > tokenn) throw new IndexOutOfRangeException();
			return new ArraySegment<T>(tokens, from, to - from);
		}

		public Span<T> Tokens(int from, int to, Span<T> s)
		{
			if (to > tokenn) throw new IndexOutOfRangeException();
			tokens.AsSpan(from, to - from).CopyTo(s);
			return s;
		}

		public abstract IEnumerable<K> Keys(string text);

		// first line and col are 1
		public bool LineCol(int loc, out int line, out int column)
		{
			line = lines.BinarySearch(loc);
			line = (line ^ line >> 31) + (line >> 31) + 1;
			column = loc - lines[line - 1] + 1;
			return loc >= 0 && loc < bn;
		}

		// bootstrap

		static readonly Parser<string, char, char, string> boot
			= new Parser<string, char, char, string>(@"
			gram  = eol* prod prods* eol*
			prods = eol+ prod
			prod  = key S*\=S* step1 step* =+
			step1 = byte+ alt1* =+
			alt1  = \| byte+ =+
			step  = S+ err? rep? byte+ alt* =+
			alt   = \| rep? byte+ =+
			err   = \? | \* =+
			rep   = \+ =+
			byte  = B\+? | [range* ^? range*]\+? | \\E\+? =+
			key   = W+ =+
			range = R | R-R | \\E =+
			eol   = S* \r?\n S*",
			new BootScan()) {
				greedy = false, treeKeep = false, treeDump = false
			};

		public Lexer(string gram, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan)
		{
			boot.scan.Load(gram);
			var top = boot.Parse(null);
			if (top.err != 0) {
				boot.scan.Unload(); boot.treeDump = true;
				boot.Parse(gram).Dump(); boot.treeDump = false;
				var e = new Exception(); e.Data["err"] = top;
				throw e;
			}
			// gram
			// \ prod
			//   \ key
			//   \ step1
			//     \ byte+ or range...+ or esc+ ...
			//     \ alt1 ...
			//       \ byte+ or range...+ or esc+ ...
			//   \ step ...
			//     \ err
			//     \ rep
			//     \ byte+ or range...+ or esc+ ...
			//     \ alt ...
			//       \ rep
			//       \ byte+ or range...+ or esc+ ...
			// \ prod ...
			start = new Unit(this) { mode = -1 };
			start.go = start;

			var bs = new int[127]; int bn;
			// build prod
			foreach (var prod in top) {
				var k = Keys(boot.Tokens(prod.head)).Single();
				// first unit of each step
				var stepus = prod.Select((z, x) =>
						x <= 1 ? start
						: new Unit(this) { key = k, step = x, mode = -1, go = start })
					.Append(start)
					.ToArray();
				// build step
				var step = 0;
				foreach (var st in prod.Skip(1)) {
					++step;
					// build alt
					foreach (var a in st.Where(t => t.name.StartsWith("alt")).Prepend(st)) {
						var u = stepus[step];
						var rep = a.Any(t => t.name == "rep") ? u : stepus[step + 1];
						var bytes = a.Where(t => t.name == "byte");
						if (bytes.Count() > 15)
							throw new Exception($"{k}.{step} exceeds 15 bytes :{boot.Tokens(a)}");
						var errgo = a.head.name == "err" ? u.go = u
									: u.mode > 0 ? u : u.go;
						// skip to next step when error
						if (a.head.name == "err" && gram[a.head.from] == '?')
							BootMode(u, k, step, 1, stepus[step + 1]);
						// build unit
						foreach (var b in bytes) {
							var x = b.from; bn = 0;
							if (gram[x] == '\\')
								bs[bn++] = BootScan.Esc(gram, ref x, b.to, -1)[0];
							// build range
							else if (gram[x] == '[') {
								++x;
								Span<bool> rs = stackalloc bool[128]; bool inc = true;
								if (x != b.head?.from) // omitted inclusive range
									BootScan.RI.CopyTo(rs);
								foreach (var r in b) {
									inc &= x == (x = r.from); // before ^
									if (gram[x] == '\\')
										rs[BootScan.Esc(gram, ref x, r.to, 0)[0]] = inc;
									else
										for (int y = gram[x], z = gram[r.to - 1]; y <= z; y++)
											rs[y] = inc; // R-R
									x = r.to;
								}
								for (int y = 0; y < 127; y++)
									if (rs[y]) bs[bn++] = y;
								if (bn == 0)
									throw new Exception($"No byte in {k}.{step} :{boot.Tokens(b)}");
								++x; // ]
							}
							else // single byte
								bs[bn++] = gram[x++];
							var ok = b.next?.name != "byte"; // last byte in alt
							var repb = x != b.to; // +
							var next = BootNext(u, bs, bn, k, step, errgo);
							if (repb)
								BootNext(next, bs, bn, k, step, errgo, u);
							var err = repb || bs[0] > 127;
							BootMode(next, k, step,
								ok ? repb ? 3 : 2 : err ? repb ? -1 : -2 : 0,
								ok ? rep : err ? errgo : u);
							u = next; x = b.to;
						}
					}
				}
			}
			if (boot.treeDump) BootDump(start, "", "");
			this.scan = scan;
			boot.scan.Unload();
		}

		Unit BootNext(Unit u, int[] bs, int bn, K key, int step, Unit err, Unit repb = null)
		{
			if (u.next == null)
				u.next = new Unit[133];
			if (bs[0] > 127) // utf escape
				return BootNextU(u, key, step, err, repb);
			Unit v = u.next[bs[0]];
			for (int x = 1; x < bn; x++)
				if (u.next[bs[x]] != v)
					throw new Exception($"Prefix of {key}.{step} and {u.key}.{u.step} must be same or distinct");
			if (v != null)
				return v.pren != bn ?
					throw new Exception($"Prefix of {key}.{step} and {u.key}.{u.step} must be same or distinct")
					: repb != null == (v == u) ? v
					: throw new Exception($"{key}.{step} and {u.key}.{u.step} conflict");
			v = repb?.next[bs[0]] ?? new Unit(this) { pren = bn };
			for (int x = 0; x < bn; x++)
				u.next[bs[x]] = v;
			return v;
		}

		Unit BootNextU(Unit u, K key, int step, Unit err, Unit repb)
		{
			var v = u.next[129]?.next[128];
			if (v != null)
				return repb != null == (v == u) ? v : throw new Exception($"{key}.{step} and {u.key}.{u.step} conflict");
			if (repb != null) { u.next[129] = repb.next[129]; u.next[130] = repb.next[130]; u.next[131] = repb.next[131]; }
			else {
				u.next[129] = new Unit(this) { pren = 1, key = key, step = step, mode = -1, go = err, next = new Unit[133] };
				u.next[130] = new Unit(this) { pren = 1, key = key, step = step, mode = -1, go = err, next = new Unit[133] };
				u.next[131] = new Unit(this) { pren = 1, key = key, step = step, mode = -1, go = err, next = new Unit[133] };
				var u130a = new Unit(this) { pren = 1, key = key, step = step, mode = -1, go = err, next = new Unit[133] };
				var u131a = new Unit(this) { pren = 1, key = key, step = step, mode = -1, go = err, next = new Unit[133] };
				var u131b = new Unit(this) { pren = 1, key = key, step = step, mode = -1, go = err, next = new Unit[133] };
				u.next[129].next[128] = (u.next[130].next[128] = u130a).next[128]
				= ((u.next[131].next[128] = u131a).next[128] = u131b).next[128] = new Unit(this) { pren = 1 };
			}
			return u.next[129].next[128];
		}

		void BootMode(Unit u, K key, int step, int mode, Unit go)
		{
			if (u.mode + mode >= 4 || (u.mode & 1) != (mode & 1) && u.go != null || (u.mode & mode) < 0 && u.go != go)
				throw new Exception($"{key}.{step} and {u.key}.{u.step} conflicted");
			if (u.mode != 0 && mode <= 0) return;
			u.key = key; u.step = step; u.mode = mode; u.go = go;
		}

		void BootDump(Unit u, string ind, string pre, Dictionary<Unit, bool> us = null)
		{
			var uz = us ?? new Dictionary<Unit, bool> { };
			uz[u] = false;
			Console.WriteLine($"{ind}{u.id}: {u.key}.{u.step}" +
				$" {(u.mode < 0 ? "err" : u.mode > 0 ? "ok" : "back")}.{u.go?.id ?? 0} < {pre}");
			if (u.go != null && !uz.ContainsKey(u.go)) uz[u.go] = true;
			if (u.next == null) return;
			var i = ind + "  ";
			foreach (var n in u.next.Distinct()) {
				if (n != null)
					if (n != u)
						BootDump(n, i, string.Join(' ', u.next.Select((nn, b) => nn != n ? null : b >= 128 ? "\\U"
							: b == ' ' ? "\\s" : b == '\t' ? "\\t" : b == '\n' ? "\\n" : b == '\r' ? "\\r"
							: b > ' ' && b < 127 ? ((char)b).ToString() : $"\\x{b:x}").Where(x => x != null)), uz);
					else
						Console.WriteLine($"{i}+");
			}
		Go: if (us == null)
				foreach (var go in uz)
					if (go.Value) { BootDump(go.Key, ind, "", uz); goto Go; }
		}
	}
}
