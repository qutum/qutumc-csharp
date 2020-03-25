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

		public override bool Is(K key) => Eq.Equals(Token().key, key);

		public override bool Is(K key1, K key) => Eq.Equals(key1, key);

		public override IEnumerable<K> Keys(string text) => new[] { Enum.Parse<K>(text) };
	}

	public abstract class Lexer<K, T> : Scan<IEnumerable<byte>, K, T, ArraySegment<T>> where T : struct
	{
		// each unit is just before next byte or after last byte of step
		sealed class Unit
		{
			internal int id;
			internal K key;
			internal int step; // first is 1
			internal int pren; // number of bytes to this unit
			internal Unit[] next; // utf-8: <=bf [128], <=df [129], <=ef [130], <=f7 [131], ff [132]
			internal Unit go; // when next==null or next[byte]==null or backward
							  // go.next != null, to start: token end or error
			internal int mode; // match to token: 1, misatch to error: -1, mismatch to backward: 0
							   // no backward neither cross steps nor inside utf nor inside byte repeat

			internal Unit(Lexer<K, T> l) => id = ++l.id;
		}

		readonly Unit start;
		int id;
		internal Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan;
		int bn; // total bytes gots
		int bf, bt; // from index to index excluded for each step
		byte[] bytes = new byte[17]; // latest bytes, [byte index & 15]
		internal int tokenn, loc = -2;
		internal T[] tokens = new T[65536];
		readonly List<int> lines = new List<int>();

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
				else // scan ended, token not
					goto Go;
			var b = bytes[bt & 15];
			if (u.next[b < 128 ? b : b > 0xf7 ? 132 : b > 0xef ? 131 : b > 0xdf ? 130 : b > 0xbf ? 129 : 128]
					is Unit v) {
				u = v; ++bt;
				if (u.next != null)
					goto Next;
			}
		Go: v = u.go;
			if (u.mode == 0) { // failed to greedy
				u = v; --bt; // one byte backward // TODO backward directly, not one by one
				goto Go;
			}
			if (u.mode > 0) { // match a step 
				var e = v == start;
				Token(u.key, u.step, ref e, bf, bt);
				if (e) v = start;
			}
			else { // error step
				Error(u.key, u.step, v == start || bt >= bn,
					bt < bn ? bytes[bt & 15] : (byte?)null, bf, bt);
				++bt; // shift a byte
			}
			if (v == start && loc < tokenn)
				return true;
			u = v;
			goto Step;
		}

		// make each step of a token
		protected abstract void Token(K key, int step, ref bool end, int from, int to);

		// report an error step, step < 0 for end of scan
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

		public T Token(int x) => tokens[x];

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
			alt1  = \| S* byte+ =+
			step  = S+ mis? rep? byte+ alt* =+
			alt   = \| S* rep? byte+ =+
			mis   = \| S* | \* =+
			rep   = \+ =+
			byte  = B\+? | [range* ^? range*]\+? | \\E\+? =+
			key   = W+ =+
			range = R | R-R | \\E =+
			eol   = S* comm? \r?\n S*
			comm  = \=\= V*",
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
			//     \ mis
			//     \ rep
			//     \ byte+ or range...+ or esc+ ...
			//     \ alt ...
			//       \ rep
			//       \ byte+ or range...+ or esc+ ...
			// \ prod ...
			start = new Unit(this) { mode = -1 }; start.go = start;

			var ns = new byte[127]; int nn;
			// build prod
			foreach (var prod in top) {
				var k = Keys(boot.Tokens(prod.head)).Single();
				// first unit of each step
				var stepus = prod.Select((z, x) =>
						x <= 1 ? start : new Unit(this) { key = k, step = x })
					.Append(start) // token end
					.ToArray();
				prod.Skip(1).Each((st, step) => {
					var u = stepus[++step]; // first step is 1
					if (st.head.name != "mis") // no backward cross steps
						{ u.go = start; u.mode = -1; }
					else if (gram[st.head.from] == '|') // as empty alt
						{ u.go = stepus[step + 1]; u.mode = 1; }
					else // shift byte and retry step like the start
						{ u.go = u; u.mode = -1; }
				});
				Unit[] aus = null;
				// build step
				prod.Skip(1).Each((st, step) => {
					var u = stepus[++step];
					// build alt
					var Aus = st.Where(t => t.name.StartsWith("alt")).Prepend(st).Select(a => {
						u = stepus[step];
						// go for match
						var ok = a.head.name == "rep" || a.head.next?.name == "rep" ? u // repeat step
								: stepus[step + 1];
						// go for error
						var err = u.mode < 0 ? u.go : u;
						// build units from bytes
						var bytes = a.Where(t => t.name == "byte");
						var bn = bytes.Count();
						if (bn > 15)
							throw new Exception($"{k}.{step} exceeds 15 bytes :{boot.Tokens(a)}");
						bytes.Each((b, bx) => {
							var x = b.from; nn = 0;
							if (gram[x] == '\\') {
								foreach (var c in BootScan.Esc(gram, x, b.to, true, true))
									ns[nn++] = (byte)c;
								x += 2;
							}
							// build range
							else if (gram[x] == '[') {
								++x;
								Span<bool> rs = stackalloc bool[128]; bool inc = true;
								if (x != b.head?.from) // omitted inclusive range
									BootScan.RI.CopyTo(rs);
								foreach (var r in b) {
									inc &= x == (x = r.from); // before ^
									if (gram[x] == '\\')
										foreach (var y in BootScan.Esc(gram, x, r.to, false, true))
											rs[y] = inc;
									else
										for (int y = gram[x], z = gram[r.to - 1]; y <= z; y++)
											rs[y] = inc; // R-R
									x = r.to;
								}
								for (byte y = 0; y < 127; y++)
									if (rs[y]) ns[nn++] = y;
								if (nn == 0)
									throw new Exception($"No byte in {k}.{step} :{boot.Tokens(b)}");
								++x; // ]
							}
							else // single byte
								ns[nn++] = (byte)gram[x++];
							// build unit
							var repb = x != b.to; // byte repeat
							var go = u; var mode = 0; // mismatch to backward
							if (bx == bn - 1) // last byte of alt
								{ go = ok; mode = 1; } // match step
							else if (repb || ns[0] > 127) // inside byte repeat or utf
								{ go = err; mode = -1; } // mismatch to error
							var next = BootNext(k, step, u, ns, nn, go, mode, null, err);
							if (repb)
								BootNext(k, step, next, ns, nn, go, mode, u, err);
							u = next; x = b.to;
						});
						return u;
					}).Where(u => u.next != null) // last byte repeat unit of each alt
					.ToArray();
					if (aus != null) {
						u = stepus[step];
						for (int x = 0; x <= 129; x++)
							if (u.next[x] != null && aus.Any(au => au.next[x] != null))
								throw new Exception($"{k}.{step} and {k}.{step - 1} conflict over repeat");
					}
					aus = Aus;
				});
			}
			if (boot.treeDump) BootDump(start, "", "");
			this.scan = scan;
			boot.scan.Unload();
		}

		Unit BootNext(K key, int step, Unit u, byte[] ns, int nn, Unit go, int mode, Unit repFrom, Unit err)
		{
			if (ns[0] > 127)
				return BootNextU(key, step, u, go, mode, repFrom, err);
			Unit n = u.next?[ns[0]];
			if (u.next == null)
				u.next = new Unit[133];
			else
				for (int x = 1; x < nn; x++)
					if (u.next[ns[x]] != n)
						throw new Exception($"Prefix of {key}.{step} and {(n ?? u).key}.{(n ?? u).step}"
							+ " must be the same or distinct");
			if (n == null) {
				n = repFrom != null ? u // repeat a byte
					: new Unit(this) { key = key, step = step, pren = nn, go = go, mode = mode };
				for (int x = 0; x < nn; x++)
					u.next[ns[x]] = n;
			}
			else if (n.pren != nn)
				throw new Exception($"Prefix of {key}.{step} and {n.key}.{n.step}"
					+ " must be the same or distinct");
			else if (repFrom != null != (n == u))
				// key may != u.key
				throw new Exception($"{key}.{step} and {n.key}.{n.step} conflict over byte repeat");
			else if (repFrom == null)
				BootMode(key, step, n, go, mode);
			return n;
		}

		Unit BootNextU(K key, int step, Unit u, Unit go, int mode, Unit repFrom, Unit err)
		{
			var n = (u.next ??= new Unit[133])[129]?.next[128];
			if (n != null) {
				if (repFrom != null != (n == u))
					throw new Exception($"{key}.{step} and {u.key}.{u.step} conflict over utf repeat");
				if (repFrom == null)
					BootMode(key, step, n, go, mode);
				return n;
			}
			if (repFrom != null)
				Array.Copy(repFrom.next, 129, (n = u).next, 129, 4);
			else {
				var a129 = new Unit(this) { key = key, step = step, pren = 1, go = err, mode = -1, next = new Unit[133] };
				var a130 = new Unit(this) { key = key, step = step, pren = 1, go = err, mode = -1, next = new Unit[133] };
				var b130 = new Unit(this) { key = key, step = step, pren = 1, go = err, mode = -1, next = new Unit[133] };
				var a131 = new Unit(this) { key = key, step = step, pren = 1, go = err, mode = -1, next = new Unit[133] };
				var b131 = new Unit(this) { key = key, step = step, pren = 1, go = err, mode = -1, next = new Unit[133] };
				var c131 = new Unit(this) { key = key, step = step, pren = 1, go = err, mode = -1, next = new Unit[133] };
				n = new Unit(this) { key = key, step = step, pren = 1, go = go, mode = mode };
				(u.next[129] = a129).next[128] = n; // c0
				((u.next[130] = a130).next[128] = b130).next[128] = n; // e0
				(((u.next[131] = a131).next[128] = b131).next[128] = c131).next[128] = n; // f0
			}   // TODO [132] // f8
			return n;
		}

		void BootMode(K key, int step, Unit u, Unit go, int mode)
		{
			if (u.mode + mode == 2) // both 1
				throw new Exception($"{key}.{step} and {u.key}.{u.step} conflict over match");
			if (u.mode + mode == -2 && u.go != go) // both -1
				throw new Exception($"{key}.{step} and {u.key}.{u.step} conflict over error");
			if (u.mode + mode == -1) // -1 and 0
				throw new Exception($"{key}.{step} and {u.key}.{u.step} conflict over repeat");
			if (u.mode == 0 && mode != 0) {
				u.key = key; u.step = step; u.go = go; u.mode = mode;
			}
			else if (u.mode < 0 && mode > 0) {
				u.go = go; u.mode = mode;
			}
		}

		void BootDump(Unit u, string ind, string pre, Dictionary<Unit, bool> us = null)
		{
			var uz = us ?? new Dictionary<Unit, bool> { };
			uz[u] = false;
			Console.WriteLine($"{ind}{u.id}: {u.key}.{u.step} " +
				$"{(u.mode < 0 ? "mis" : u.mode > 0 ? "ok" : "back")}.{u.go.id} < {pre}");
			if (!uz.ContainsKey(u.go))
				uz[u.go] = true;
			if (u.next == null)
				return;
			var i = ind + "  ";
			foreach (var n in u.next.Distinct().Where(n => n != null))
				if (n == u)
					Console.WriteLine($"{i}+");
				else {
					var s = u.next.Select((nn, b) => nn != n ? null
						: b > ' ' && b < 127 ? ((char)b).ToString()
						: b == ' ' ? "\\s" : b == '\t' ? "\\t" : b == '\n' ? "\\n" : b == '\r' ? "\\r"
						: b >= 128 ? "\\U"
						: $"\\x{b:x}")
						.Where(x => x != null);
					BootDump(n, i, string.Join(' ', s), uz);
				}
			Go: if (us == null)
				foreach (var go in uz)
					if (go.Value) {
						BootDump(go.Key, ind, "", uz);
						goto Go;
					}
		}
	}
}
