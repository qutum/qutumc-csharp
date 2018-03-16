using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace qutum.parser
{
	public abstract class Lexer<K, T, S> : Scan<IEnumerable<byte>, K, T, S> where T : struct where S : IEnumerable<T>
	{
		sealed class Unit
		{
			internal int id;
			internal Unit[] next; // utf-8: <=bf: [128], <=df: [129], <=ef: [130], <=f7: [131], ff: [132]
			internal int pren; // how many bytes to this unit
			internal K key;
			internal int step;
			internal int mode; // err: -1, back: 0 (no quantifier), ok: 2
			internal Unit go; // go.next != null

			internal Unit(Lexer<K, T, S> l) => id = ++l.id;
		}

		Unit start;
		int id, bf, bt, bn;
		internal Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan;
		internal byte[] bytes = new byte[16];
		internal List<T> tokens = new List<T>();
		internal int loc = -2;

		public Lexer(string grammar, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan)
		{ this.scan = scan; Boot(grammar); }

		public abstract IEnumerable<object> Keys(string name);

		public void Load(IEnumerable<byte> input)
		{
			if (loc > -2) Unload();
			scan.Load(input);
			bf = bt = bn = scan.Loc() + 1; loc = -1;
		}

		public void Unload() { scan.Unload(); tokens.Clear(); loc = -2; }

		public bool Next()
		{
			if (++loc < tokens.Count) return true;
			var u = start;
			Go: bf = bt;
			Next: if (bt >= bn)
				if (scan.Next()) bytes[bn++ & 15] = scan.Token();
				else if (u == start || bt > bn) return loc < tokens.Count;
				else goto Do;
			var b = bytes[bt & 15];
			if (u.next[b < 128 ? b : b > 0xf7 ? 132 : b > 0xef ? 131 : b > 0xdf ? 130 : b > 0xbf ? 129 : 128] is Unit n)
			{
				u = n; ++bt;
				if (u.next != null) goto Next;
			}
			Do: switch (u.mode)
			{
				case 0: u = u.go; --bt; goto Do;
				case 1: Token(u.key, u.step, u.go == start, bf, bt); break;
				default: Error(u.key, u.step, u.go == start, bt < bn ? bytes[bt & 15] : (byte)0, bf, ++bt, bn); break;
			}
			if (u.go == start && loc < tokens.Count) return true;
			u = u.go; goto Go;
		}

		protected abstract void Token(K key, int step, bool end, int from, int to);

		protected abstract void Error(K key, int step, bool end, byte b, int from, int to, int eof);

		public abstract bool Is(K key);

		public abstract bool Is(K key1, K key);

		public int Loc() => loc;

		public T Token() => tokens[loc];

		public abstract S Tokens(int from, int to);

		public void Tokens(int from, int to, T[] array, int ax) => tokens.CopyTo(from, array, ax, to - from);

		static Earley<string, char, char, string> boot = new Earley<string, char, char, string>(@"
			gram  = eol*lex lexs*eol*
			lexs  = eol+lex
			lex   = name S*\=S*step steps* =+
			step  = byte+alt* =+
			alt   = \|byte+ =+
			steps = S+err?rep?byte+alts* =+
			alts  = \|rep?byte+ =+
			err   = \? =+
			rep   = \+ =+
			byte  = B\+? | [range*^?range+]\+? | \\E\+? =+
			name  = W+ =+
			range = R|R-R =+
			eol   = S*\r?\nS*", new BootScan()) { greedy = false, treeKeep = false, treeDump = false };

		static IEnumerable<int> bootRange = Enumerable.Range(32, 127 - 32).Concat(new int[] { '\t', '\n', '\r' }).ToArray();

		void Boot(string gram)
		{
			boot.scan.Load(gram);
			var top = boot.Parse(null);
			if (top.err > 0)
			{
				boot.scan.Unload(); boot.treeDump = true; boot.Parse(gram).Dump(); boot.treeDump = false;
				var e = new Exception(); e.Data["err"] = top; throw e;
			}
			start = new Unit(this) { mode = -1 }; start.go = start;
			foreach (var l in top)
			{
				var k = (K)Keys(boot.Tokens(l.head)).Single();
				var ust = l.Select((z, x) => x < 2 ? start : new Unit(this) { key = k, step = x, mode = -1, go = start })
					.Append(start).ToArray();
				var step = 0; var b1 = new int[1];
				foreach (var st in l.Skip(1))
				{
					++step;
					foreach (var a in st.Where(t => t.name.StartsWith("alt")).Prepend(st))
					{
						var uu = ust[step]; var u = uu;
						var rep = a.Any(t => t.name == "rep") ? u : ust[step + 1];
						var ab = a.Where(t => t.name == "byte");
						if (ab.Count() > 15) throw new Exception($"{k}.{step} exceeds 15 bytes");
						if (a.head.name == "err") u.go = u;
						foreach (var b in ab)
						{
							var x = b.from; var bs = b1;
							if (gram[x] == '\\')
								b1[0] = BootScan.Sym(gram, ref x, b.to, false)[0];
							else if (gram[x] == '[')
							{
								++x; bool ex = false;
								bs = b.Aggregate(x != b.head.from ? bootRange : Array.Empty<int>(),
									(s, p) => (ex |= x != p.from) ?
									s.Except(Enumerable.Range(gram[p.from], gram[(x = p.to) - 1] - gram[p.from] + 1))
									: s.Union(Enumerable.Range(gram[p.from], gram[(x = p.to) - 1] - gram[p.from] + 1))
								).Distinct().ToArray();
								if (bs.Length == 0) throw new Exception($"No byte in {k}.{step}");
								++x;
							}
							else
								b1[0] = gram[x++];
							var ok = b.next == null || b.next.name != "byte"; var err = x != b.to || bs[0] > 127;
							var n = BootNext(u, bs, k, step, uu.go);
							if (x != b.to)
								BootNext(n, bs, k, step, uu.go, u);
							BootMode(n, k, step, ok ? 1 : err ? -1 : 0, ok ? rep : err ? uu.go : u);
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

		Unit BootMode(Unit u, K key, int step, int mode, Unit go)
		{
			if (u.mode != 0)
				if (mode == 0) return null;
				else throw new Exception($"{key}.{step} and {u.key}.{u.step} conflicted");
			u.key = key; u.step = step; u.mode = mode; u.go = go;
			return go;
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

	public struct Token<K> where K : struct
	{
		public K key;
		public int from, to; // input loc
		public bool err;
		public object value;

		public string Dump() => $"{key}{(err ? "!" : "=")}{value}";
	}

	public class LexerEnum<K> : Lexer<K, Token<K>, List<Token<K>>> where K : struct
	{
		public LexerEnum(string grammar, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan = null)
			: base(grammar, scan ?? new ScanByte()) { }

		public override IEnumerable<object> Keys(string name) => new object[] { Enum.Parse<K>(name) };

		protected int from = -1;

		protected override void Error(K key, int step, bool end, byte b, int f, int to, int eof)
		{
			if (from < 0) from = f;
			tokens.Add(new Token<K> { key = key, from = f, to = to, err = true, value = to <= eof ? (char)b : (object)null });
			if (end) from = -1;
		}

		protected override void Token(K key, int step, bool end, int f, int to)
		{
			if (from < 0) from = f;
			if (end)
			{
				var bs = new byte[to - from];
				scan.Tokens(from, to, bs, 0);
				tokens.Add(new Token<K> { key = key, from = from, to = to, value = Encoding.UTF8.GetString(bs) });
				from = -1;
			}
		}

		protected static EqualityComparer<K> Eq = EqualityComparer<K>.Default;

		public override bool Is(K key) => Eq.Equals(tokens[loc].key, key);

		public override bool Is(K key1, K key) => Eq.Equals(key1, key);

		public override List<Token<K>> Tokens(int from, int to) => tokens.GetRange(from, to - from);
	}
}
