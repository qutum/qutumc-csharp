//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

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
		public int from, to; // from input index to index excluded, scan.Loc()
		public int err; // ~token index this error found before

		public override string ToString() => $"{key}{(err < 0 ? "!" : "=")}{value}";
	}

	public class Lexer<K> : LexerBase<K, Token<K>> where K : struct
	{
		public Lexer(string grammar,
			Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan = null)
			: base(grammar, scan ?? new ScanByte()) { }

		protected int from = -1;
		public bool errMerge = false; // add error token into corrent tokens
		public readonly List<Token<K>> errs = new List<Token<K>>();

		public override void Unload()
		{
			base.Unload();
			errs.Clear();
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

		protected override void Error(K key, int part, bool end, byte? b, int f, int to)
		{
			if (from < 0) from = f;
			if (part >= 0) AddErr(key, f, to, (char?)b ?? (object)"");
			if (end) from = -1;
		}

		protected void Add(K key, int f, int to, object value)
		{
			Add(new Token<K> {
				key = key, from = f, to = to, value = value
			});
		}

		protected void AddErr(K key, int f, int to, object value)
		{
			var e = new Token<K> {
				key = key, from = f, to = to, value = value, err = ~tokenn
			};
			if (errMerge) Add(e);
			else errs.Add(e);
		}

		protected static EqualityComparer<K> Eq = EqualityComparer<K>.Default;

		public override bool Is(K key) => Eq.Equals(Token().key, key);

		public override bool Is(K key1, K key) => Eq.Equals(key1, key);

		public override IEnumerable<K> Keys(string text) => new[] { Enum.Parse<K>(text) };

		public (int fromL, int fromC, int toL, int toC) LineCol(int from, int to)
		{
			var (fl, fc) = LineCol(Token(from).from);
			var (tl, tc) = LineCol(Token(to - 1).to - 1);
			return (fl, fc, tl, tc);
		}
	}

	public abstract class LexerBase<K, T> : Scan<IEnumerable<byte>, K, T, ArraySegment<T>>
		where T : struct
	{
		// each unit is just before next byte or after last byte of part
		sealed class Unit
		{
			internal int id;
			internal K key;
			internal int part; // first is 1
			internal int pren; // number of bytes to this unit
			internal Unit[] next; // utf-8: <=bf [128], <=df [129], <=ef [130], <=f7 [131], ff [132]
			internal Unit go; // when next==null or next[byte]==null or backward
							  // go.next != null, to start: token end or error
			internal int mode; // match to token: 1, misatch to error: -1, mismatch to backward: 0
							   // no backward neither cross parts nor inside utf nor inside byte repeat

			internal Unit(LexerBase<K, T> l) => id = ++l.id;
		}

		readonly Unit start;
		int id;
		internal Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan;
		int bn; // total bytes gots
		int bf, bt; // from index to index excluded for each part
		readonly byte[] bytes = new byte[17]; // latest bytes, [byte index & 15]
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
			if (u.mode > 0) { // match a part 
				var e = v == start;
				Token(u.key, u.part, ref e, bf, bt);
				if (e) v = start;
			}
			else { // error part
				Error(u.key, u.part, v == start || bt >= bn,
					bt < bn ? bytes[bt & 15] : (byte?)null, bf, bt);
				++bt; // shift a byte
			}
			if (v == start && loc < tokenn)
				return true;
			u = v;
			goto Step;
		}

		// make each part of a token
		protected abstract void Token(K key, int part, ref bool end, int from, int to);

		// report an error part, part < 0 for end of scan
		protected abstract void Error(K key, int part, bool end, byte? b, int from, int to);

		protected void Add(T token)
		{
			if (tokenn == tokens.Length) Array.Resize(ref tokens, tokens.Length << 1);
			tokens[tokenn++] = token;
		}

		public int Loc() => Math.Min(loc, tokenn);

		public T Token() => tokens[loc];

		public abstract bool Is(K key);

		public abstract bool Is(K key1, K key);

		public T Token(int x) => x < tokenn ? tokens[x] : throw new IndexOutOfRangeException();

		public ArraySegment<T> Tokens(int from, int to)
		{
			if (to > tokenn) throw new IndexOutOfRangeException();
			return new ArraySegment<T>(tokens, from, to - from);
		}

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

		// bootstrap

		static readonly ParserStr boot = new ParserStr(@"
			gram  = eol* prod prods* eol*
			prods = eol+ prod
			prod  = key S*\=S* part1 part* =+
			key   = W+ =+
			part1 = byte+ alt1* =+
			alt1  = \| S* byte+ =+
			part  = S+ mis? rep? byte+ alt* =+
			alt   = \| S* rep? byte+ =+
			mis   = \| S* | \* =+
			rep   = \+ =+
			byte  = B qua? | [range* ^? range*] qua? | \\E qua? =+
			range = R | R-R | \\E =+
			qua   = \+ | \* =+
			eol   = S* comm? \r?\n S*
			comm  = \=\= V*",
			new BootScan()) {
				greedy = false, treeKeep = false, treeDump = 0
			};

		public LexerBase(string gram, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan)
		{
			boot.scan.Load(gram);
			var top = boot.Parse(null);
			if (top.err != 0) {
				boot.scan.Unload();
				var dump = boot.treeDump; boot.treeDump = 3;
				boot.Parse(gram).Dump(); boot.treeDump = dump;
				var e = new Exception(); e.Data["err"] = top;
				throw e;
			}
			// gram
			// \ prod
			//   \ key
			//   \ part1
			//     \ byte or range... or esc ...
			//       \ qua
			//     \ alt1 ...
			//       \ byte or range... or esc ...
			//         \ qua
			//   \ part ...
			//     \ mis
			//     \ rep
			//     \ byte or range... or esc ...
			//       \ qua
			//     \ alt ...
			//       \ rep
			//       \ byte or range... or esc ...
			//         \ qua
			// \ prod ...
			start = new Unit(this) { mode = -1 }; start.go = start;

			var ns = new byte[130];
			// build prod
			foreach (var prod in top) {
				var k = Keys(boot.Tokens(prod.head)).Single();
				// first unit of each part
				var pus = prod.Select((z, x) =>
						x <= 1 ? start : new Unit(this) { key = k, part = x })
					.Append(start) // token end
					.ToArray();
				prod.Skip(1).Each((st, part) => {
					var u = pus[++part]; // first part is 1
					if (st.head.name != "mis") // no backward cross parts
						{ u.go = start; u.mode = -1; }
					else if (gram[st.head.from] == '|') // as empty alt
						{ u.go = pus[part + 1]; u.mode = 1; }
					else // shift byte and retry part like the start
						{ u.go = u; u.mode = -1; }
				});
				Unit[] aus = null;
				// build part
				prod.Skip(1).Each((p, part) => {
					var u = pus[++part];
					// build alt
					var Aus = p.Where(t => t.name.StartsWith("alt")).Prepend(p).Select(a => {
						u = pus[part];
						// go for match
						var ok = a.head.name == "rep" || a.head.next?.name == "rep" ? u // part repeat
								: pus[part + 1];
						// go for error
						var err = u.mode < 0 ? u.go : u;
						// build units from bytes
						var bytes = a.Where(t => t.name == "byte");
						var bn = bytes.Count();
						if (bn > 15)
							throw new Exception($"{k}.{part} exceeds 15 bytes :{boot.Tokens(a)}");
						bytes.Each((b, bx) => {
							BootByte(gram, ref u, k, part, ok, err, b, bx, bn, ns);
						});
						return u;
					}).Where(u => u.next != null) // last byte repeat unit of each alt
					.ToArray();
					if (aus != null) {
						u = pus[part];
						for (int x = 0; x <= 129; x++)
							if (u.next[x] != null && aus.Any(au => au.next[x] != null))
								throw new Exception($"{k}.{part} and {k}.{part - 1} conflict over repeat");
					}
					aus = Aus;
				});
			}
			if (boot.treeDump > 0) BootDump(start, "");
			this.scan = scan;
			boot.scan.Unload();
		}

		void BootByte(string gram, ref Unit u, K k, int part, Unit ok, Unit err,
			Tree<string> b, int bx, int bn, byte[] ns)
		{
			var x = b.from;
			int nn = 0;
			if (gram[x] == '\\') {
				foreach (var c in BootScan.Unesc(gram, x, b.to, true))
					ns[++nn] = (byte)c;
				x += 2;
			}
			// build range
			else if (gram[x] == '[') {
				++x;
				Span<bool> rs = stackalloc bool[129]; bool inc = true;
				if (x != b.head?.from) // omitted inclusive range
					BootScan.RI.CopyTo(rs);
				foreach (var r in b) {
					inc &= x == (x = r.from); // before ^
					if (gram[x] == '\\')
						foreach (var c in BootScan.Unesc(gram, x, r.to, true))
							rs[c] = inc;
					else
						for (char c = gram[x], cc = gram[r.to - 1]; c <= cc; c++)
							rs[c] = inc; // R-R
					x = r.to;
				}
				for (byte y = 0; y <= 128; y++)
					if (rs[y]) ns[++nn] = y;
				if (nn == 0)
					throw new Exception($"No byte in {k}.{part} :{boot.Tokens(b)}");
				++x; // ]
			}
			else // single byte
				ns[++nn] = (byte)gram[x++];

			var qua = b.tail?.name == "qua" ? b.tail : null; // byte repeat
			var more = qua != null && gram[qua.from] == '+';
			var any = qua != null && gram[qua.from] == '*';
			if (bx == 0 && any)
				throw new Exception($"First byte of {k}.{part} must not have * repeat");
			// buid next
			var go = u; var mode = 0; // mismatch to backward
			if (bx == bn - 1) // last byte of alt
				{ go = ok; mode = 1; } // match part
			else if (more || any || ns[nn] > 127 // inside byte repeat or utf
				|| b.next.tail?.name == "qua"
					&& gram[b.next.tail.from] == '*') // next byte is * repeat
				{ go = err; mode = -1; } // mismatch to error
			var next = BootNext(k, part, u, ns, nn, go, mode, any ? u : null, err);
			if (more)
				BootNext(k, part, next, ns, nn, go, mode, u, err);
			u = next;
		}

		Unit BootNext(K key, int part, Unit u, byte[] ns, int nn, Unit go, int mode, Unit repFrom, Unit err)
		{
			var utf = ns[nn] > 127;
			if (utf) --nn;
			Unit n = utf ? u.next?[129]?.next[128] : u.next?[ns[1]];
			if (u.next == null)
				u.next = new Unit[133];
			else
				// all already nexts must be the same
				for (int x = 1; x <= nn; x++)
					if (u.next[ns[x]] != n)
						throw new Exception($"Prefix of {key}.{part} and {(n ?? u).key}.{(n ?? u).part}"
							+ " must be the same or distinct");
			if (n == null) {
				n = repFrom != null ? u // repeat byte or utf
					: new Unit(this) { key = key, part = part, pren = nn, go = go, mode = mode };
				for (int x = 1; x <= nn; x++)
					u.next[ns[x]] = n;
				if (utf)
					if (repFrom == null || repFrom == u)
						BootNextU(key, part, u, n, err); // build utf for * repeat or first unit of + repeat
					else // build for next unit of + repeat
						Array.Copy(repFrom.next, 129, n.next, 129, 4);
				if (repFrom == u) // check and set mode for already exist next of * repeat
					BootMode(key, part, n, go, mode);
			}
			else if (n.pren != nn) // all already nexts must be the same
				throw new Exception($"Prefix of {key}.{part} and {n.key}.{n.part}"
					+ " must be the same or distinct");
			else if (repFrom != null != (n == u)) // already exist next must not conflict
				throw new Exception($"{key}.{part} and {n.key}.{n.part} conflict over byte repeat");
			else if (repFrom == null || repFrom == u)
				BootMode(key, part, n, go, mode); // check mode for already exist next
			return n;
		}

		void BootNextU(K key, int part, Unit u, Unit n, Unit err)
		{
			var a129 = new Unit(this) { key = key, part = part, pren = 1, go = err, mode = -1, next = new Unit[133] };
			var a130 = new Unit(this) { key = key, part = part, pren = 1, go = err, mode = -1, next = new Unit[133] };
			var b130 = new Unit(this) { key = key, part = part, pren = 1, go = err, mode = -1, next = new Unit[133] };
			var a131 = new Unit(this) { key = key, part = part, pren = 1, go = err, mode = -1, next = new Unit[133] };
			var b131 = new Unit(this) { key = key, part = part, pren = 1, go = err, mode = -1, next = new Unit[133] };
			var c131 = new Unit(this) { key = key, part = part, pren = 1, go = err, mode = -1, next = new Unit[133] };
			(u.next[129] = a129).next[128] = n; // c0
			((u.next[130] = a130).next[128] = b130).next[128] = n; // e0
			(((u.next[131] = a131).next[128] = b131).next[128] = c131).next[128] = n; // f0
		}

		static void BootMode(K key, int part, Unit u, Unit go, int mode)
		{
			if (u.mode + mode == 2) // both 1
				throw new Exception($"{key}.{part} and {u.key}.{u.part} conflict over match");
			if (u.mode + mode == -2 && u.go != go) // both -1
				throw new Exception($"{key}.{part} and {u.key}.{u.part} conflict over error");
			if (u.mode + mode == -1) // -1 and 0
				throw new Exception($"{key}.{part} and {u.key}.{u.part} conflict over repeat");
			if (u.mode == 0 && mode != 0) {
				u.key = key; u.part = part; u.go = go; u.mode = mode;
			}
			else if (u.mode < 0 && mode > 0) {
				u.go = go; u.mode = mode;
			}
		}

		static void BootDump(Unit u, string pre, Dictionary<Unit, bool> us = null)
		{
			using var env = EnvWriter.Use();
			var uz = us ?? new Dictionary<Unit, bool> { };
			uz[u] = false;
			env.WriteLine($"{u.id}: {u.key}.{u.part} " +
				$"{(u.mode < 0 ? "mis" : u.mode > 0 ? "ok" : "back")}.{u.go.id} < {pre}");
			if (!uz.ContainsKey(u.go))
				uz[u.go] = true;
			if (u.next == null)
				return;
			foreach (var n in u.next.Append(u.next?[129]?.next[128]).Distinct().Where(n => n != null))
				if (n == u)
					env.WriteLine("  +");
				else if (n.next?[128] == null) {
					var s = u.next.Append(u.next?[129]?.next[128]).Select(
						(nn, b) => nn != n ? null
						: b > ' ' && b < 127 ? ((char)b).ToString()
						: b == ' ' ? "\\s" : b == '\t' ? "\\t" : b == '\n' ? "\\n" : b == '\r' ? "\\r"
						: b >= 128 ? "\\u"
						: $"\\x{b:x}")
						.Where(x => x != null);
					using var ind = EnvWriter.Indent("  ");
					BootDump(n, string.Join(' ', s), uz);
				}
			Go: if (us == null)
				foreach (var go in uz)
					if (go.Value) {
						BootDump(go.Key, $"{go.Key.key}.{go.Key.part - 1}", uz);
						goto Go;
					}
		}
	}
}
