using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace qutum.parser
{
	public struct Token<K> where K : struct
	{
		public K key;
		public bool err;
		public int from, to; // input loc
		public object value;

		public string Dump() => $"{key}{(err ? "!" : "=")}{value}";
	}

	public class LexerEnum<K> : Lexer<K, Token<K>> where K : struct
	{
		public LexerEnum(string grammar, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan = null)
			: base(grammar, scan ?? new ScanByte()) { }

		public override IEnumerable<object> Keys(string name) => new object[] { Enum.Parse<K>(name) };

		protected int from = -1;

		protected override void Token(K key, int step, ref bool end, int f, int to)
		{
			if (from < 0) from = f;
			if (end)
			{
				var bs = new byte[to - from];
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
			Add();
			tokens[tokenn++] = new Token<K> { key = key, from = f, to = to, value = value, err = err };
		}

		protected static EqualityComparer<K> Eq = EqualityComparer<K>.Default;

		public override bool Is(K key) => Eq.Equals(tokens[loc].key, key);

		public override bool Is(K key1, K key) => Eq.Equals(key1, key);
	}

	public abstract class Lexer<K, T> : Scan<IEnumerable<byte>, K, T, ArraySegment<T>> where T : struct
	{
		sealed class Unit
		{
			internal int id;
			internal Unit[] next; // utf-8: <=bf: [128], <=df: [129], <=ef: [130], <=f7: [131], ff: [132]
			internal int pren; // how many bytes to this unit
			internal K key;
			internal int step;
			internal int mode; // no quantifer: err: -2, back: 0, ok: 2; quantifier: err: -1, ok: 3
			internal Unit go; // go.next != null

			internal Unit(Lexer<K, T> l) => id = ++l.id;
		}

		Unit start;
		int id, bf, bt, bn;
		internal Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan;
		internal byte[] bytes = new byte[17];
		internal T[] tokens = new T[65536];
		internal int tokenn, loc = -2;
		internal List<int> lines = new List<int>();

		public Lexer(string grammar, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan)
		{ this.scan = scan; Boot(grammar); }

		public abstract IEnumerable<object> Keys(string name);

		public virtual void Load(IEnumerable<byte> input)
		{
			if (loc > -2) Unload();
			scan.Load(input);
			bf = bt = bn = scan.Loc() + 1;
			loc = -1; lines.Add(-1); lines.Add(0);
		}

		public virtual void Unload() { scan.Unload(); tokenn = 0; loc = -2; lines.Clear(); }

		public bool Next()
		{
			if (++loc < tokenn) return true;
			var u = start;
			Go: bf = bt;
			Next: if (bt >= bn)
				if (scan.Next()) { if ((bytes[bn++ & 15] = scan.Token()) == '\n') lines.Add(bn); }
				else if (u == start || bt > bn)
				{ if (bn >= 0) Error(start.key, -1, true, null, bn, bn = -1); return loc < tokenn; }
				else goto Do;
			var b = bytes[bt & 15];
			if (u.next[b < 128 ? b : b > 0xf7 ? 132 : b > 0xef ? 131 : b > 0xdf ? 130 : b > 0xbf ? 129 : 128] is Unit n)
			{
				u = n; ++bt;
				if (u.next != null) goto Next;
			}
			Do: n = u.go;
			if (u.mode == 0) { u = n; --bt; goto Do; }
			else if (u.mode > 0) { var e = n == start; Token(u.key, u.step, ref e, bf, bt); if (e) n = start; }
			else Error(u.key, u.step, n == start || bt >= bn, bt < bn ? bytes[bt & 15] : (byte?)null, bf, ++bt);
			if (n == start && loc < tokenn) return true;
			u = n; goto Go;
		}

		protected abstract void Token(K key, int step, ref bool end, int from, int to);

		protected abstract void Error(K key, int step, bool end, byte? b, int from, int to);

		protected void Add() { if (tokens.Length <= tokenn) Array.Resize(ref tokens, tokens.Length << 1); }

		public abstract bool Is(K key);

		public abstract bool Is(K key1, K key);

		public int Loc() => loc;

		public T Token() => tokens[loc];

		public ArraySegment<T> Tokens(int from, int to) => new ArraySegment<T>(tokens, from, to - from);

		public T[] Tokens(int from, int to, T[] s, int x) { Array.Copy(tokens, from, s, x, to - from); return s; }

		public int Line(int loc) { var l = lines.BinarySearch(loc); return (l ^ l >> 31) + (l >> 31); }

		static Parser<string, char, char, string> boot = new Parser<string, char, char, string>(@"
			gram  = eol*lex lexs*eol*
			lexs  = eol+lex
			lex   = name S*\=S*step steps* =+
			step  = byte+alt* =+
			alt   = \|byte+ =+
			steps = S+err?rep?byte+alts* =+
			alts  = \|rep?byte+ =+
			err   = \?|\* =+
			rep   = \+ =+
			byte  = B\+? | [range*^?range*]\+? | \\E\+? =+
			name  = W+ =+
			range = R|R-R|\\E =+
			eol   = S*\r?\nS*", new BootScan()) { greedy = false, treeKeep = false, treeDump = false };

		static IEnumerable<int> bootRange = Enumerable.Range(32, 127 - 32).Concat(new int[] { '\t', '\n', '\r' }).ToArray();

		void Boot(string gram)
		{
			boot.scan.Load(gram);
			var top = boot.Parse(null);
			if (top.err != 0)
			{
				boot.scan.Unload(); boot.treeDump = true; boot.Parse(gram).Dump(); boot.treeDump = false;
				var e = new Exception(); e.Data["err"] = top; throw e;
			}
			start = new Unit(this) { mode = -1 }; start.go = start;
			foreach (var l in top)
			{
				var k = (K)Keys(l.head.tokens ?? (l.head.tokens = boot.scan.Tokens(l.head.from, l.head.to))).Single();
				var ust = l.Select((z, x) => x < 2 ? start : new Unit(this) { key = k, step = x, mode = -1, go = start })
					.Append(start).ToArray();
				var step = 0; var b1 = new int[1];
				foreach (var st in l.Skip(1))
				{
					++step;
					foreach (var a in st.Where(t => t.name.StartsWith("alt")).Prepend(st))
					{
						var u = ust[step];
						var rep = a.Any(t => t.name == "rep") ? u : ust[step + 1];
						var ab = a.Where(t => t.name == "byte");
						if (ab.Count() > 15) throw new Exception($"{k}.{step} exceeds 15 bytes");
						var errgo = a.head.name == "err" ? u.go = u : u.mode > 0 ? u : u.go;
						if (a.head.name == "err" && gram[a.head.from] == '?') BootMode(u, k, step, 1, ust[step + 1]);
						foreach (var b in ab)
						{
							var x = b.from; var bs = b1;
							if (gram[x] == '\\')
								b1[0] = BootScan.Sym(gram, ref x, b.to, -1)[0];
							else if (gram[x] == '[')
							{
								++x; bool ex = false;
								bs = b.Aggregate(x != (b.head?.from ?? -1) ? bootRange : Array.Empty<int>(), (s, r) =>
								{
									ex |= x != (x = r.from);
									var e = gram[x] == '\\' ? new int[] { BootScan.Sym(gram, ref x, r.to, 0)[0] }
										: Enumerable.Range(gram[x], 1 - gram[x] + gram[(x = r.to) - 1]);
									return ex ? s.Except(e) : s.Union(e);
								}).Distinct().ToArray();
								if (bs.Length == 0) throw new Exception($"No byte in {k}.{step}");
								++x;
							}
							else
								b1[0] = gram[x++];
							var ok = b.next?.name != "byte"; var q = x != b.to; var err = q || bs[0] > 127;
							var n = BootNext(u, bs, k, step, errgo);
							if (q)
								BootNext(n, bs, k, step, errgo, u);
							BootMode(n, k, step, ok ? q ? 3 : 2 : err ? q ? -1 : -2 : 0, ok ? rep : err ? errgo : u);
							u = n; x = b.to;
						}
					}
				}
			}
			if (boot.treeDump) BootDump(start, "", "");
		}

		Unit BootNext(Unit u, int[] s, K key, int step, Unit err, Unit qua = null)
		{
			if (s.Length == 0) return null;
			if (u.next == null)
				u.next = new Unit[133];
			if (s[0] > 127) return BootNextU(u, key, step, err, qua);
			var ns = s.Select(b => u.next[b]).Distinct();
			if (ns.Count() > 1)
				throw new Exception($"Prefix of {key}.{step} and {u.key}.{u.step} must be same or distinct");
			var n = ns.FirstOrDefault();
			if (n != null)
				return n.pren == s.Length ? qua != null == (n == u) ? n
					: throw new Exception($"{key}.{step} and {u.key}.{u.step} conflict")
					: throw new Exception($"Prefix of {key}.{step} and {u.key}.{u.step} must be same or distinct");
			n = qua?.next[s[0]] ?? new Unit(this) { pren = s.Length };
			foreach (var b in s)
				u.next[b] = n;
			return n;
		}

		Unit BootNextU(Unit u, K key, int step, Unit err, Unit qua)
		{
			var n = u.next[129]?.next[128];
			if (n != null)
				return qua != null == (n == u) ? n : throw new Exception($"{key}.{step} and {u.key}.{u.step} conflict");
			if (qua != null)
			{ u.next[129] = qua.next[129]; u.next[130] = qua.next[130]; u.next[131] = qua.next[131]; }
			else
			{
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
			foreach (var n in u.next.Distinct())
				if (n != null)
					if (n != u)
						BootDump(n, i, string.Join(' ', u.next.Select((nn, b) => nn != n ? null : b >= 128 ? "\\U"
							: b == ' ' ? "\\s" : b == '\t' ? "\\t" : b == '\n' ? "\\n" : b == '\r' ? "\\r"
							: b > ' ' && b < 127 ? ((char)b).ToString() : $"\\x{b:x}").Where(x => x != null)), uz);
					else
						Console.WriteLine($"{i}+");
			Go: if (us == null)
				foreach (var go in uz)
					if (go.Value) { BootDump(go.Key, ind, "", uz); goto Go; }
		}
	}
}
