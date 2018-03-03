//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.parser
{
	public class ParserStr : QEarley<string, char>
	{
		public ParserStr(Dictionary<string, string[]> grammar, string start = "Start")
			: base(grammar, new ScanStr(), start) { }
	}

	public class Tree<T> : LinkTree<Tree<T>>
	{
		public string name, dump;
		public int from, to;
		public T tokens;
		public int err; // step break: > 0

		public override string DumpSelf(string ind, string pos) =>
			$"{ind}{pos}{from}:{to}{(err != 0 ? "!" + err : "")} {dump}";
	}

	sealed class Alt
	{
		internal string name;
		internal Con[] s;
	}

	sealed class Con
	{
		internal string name;
		internal object[] s; // Alt or K or null
		internal byte[] reps; // ?: 0, once: 1, *: 2, +: 3
		internal int tree; // default: 0, treeThru: -1, treeKeep: 1

		public override string ToString() => name + "=" + string.Join(' ',
			s.Where(v => v != null).Select((v, x) => (v is Alt a ? a.name : v.ToString())
				+ (reps[x] == 3 ? "+" : reps[x] == 2 ? "*" : reps[x] == 0 ? "?" : "")));
	}

	public class QEarley<T, K> where T : class
	{
		Alt start;
		Scan<T, K> scan;
		List<Match> matchs;
		List<int> locs;
		HashSet<int> errs;
		int loc;
		public int largest, largestLoc, total;
		public bool greedy = true; // S=AB A=1|A1 B=1|B1  gready: (11)1  no: 1(11) (mostly?)
		public bool treeThru = false;
		public bool treeDump = false;

		struct Match
		{
			internal Con con;
			internal int from, to, step; // empty (always predicted): from==to (step could be 0 in repeatition)
			internal int prev, tail; // completed by alt: both >=0, predicted con: only prev >=0, or: prev==tail

			public override string ToString() => $"{from}:{to}#{step} {con}";
		}

		QEarley()
		{
			matchs = new List<Match>();
			locs = new List<int>();
			errs = new HashSet<int>();
		}

		public QEarley(Dictionary<string, string[]> grammar, Scan<T, K> scan, string start = "Start") : this()
		{
			var prods = new Dictionary<string, Alt>();
			foreach (var kv in grammar)
				prods[kv.Key] = new Alt { name = kv.Key };
			foreach (var kv in grammar)
				prods[kv.Key].s = kv.Value.Select(alt =>
				{
					var s = alt.Split(' ', StringSplitOptions.RemoveEmptyEntries).SelectMany(
						cat => prods.TryGetValue(cat, out Alt a) ? new object[] { a } :
							cat.Select(t => (object)t).ToArray()).Append(null).ToArray();
					var rs = Enumerable.Repeat<byte>(1, s.Length).ToArray();
					rs = rs.Select((r, x) => (byte)(x + 1 >= s.Length ? 1 :
						s[x + 1] as char? == '~' ? 3 : s[x + 1] as char? == '#' ? 2 : s[x + 1] as char? == '?' ? 0 : 1))
						.Where((r, x) => s[x] as char? != '~' && s[x] as char? != '#' && s[x] as char? != '?').ToArray();
					s = s.Where((r, x) => s[x] as char? != '~' && s[x] as char? != '#' && s[x] as char? != '?').ToArray();
					return new Con { name = kv.Key, s = s, reps = rs };
				}).ToArray();
			this.scan = scan;
			this.start = prods[start];
		}

		public Tree<T> Parse(T tokens)
		{
			if (tokens != null) scan.Load(tokens);
			int m = Parsing();
			Tree<T> t = m >= 0 ? Accepted(m, null) : Rejected();
			matchs.Clear(); locs.Clear(); errs.Clear();
			if (tokens != null) scan.Unload();
			return t;
		}

		public bool Check(T tokens)
		{
			if (tokens != null) scan.Load(tokens);
			bool greedy = this.greedy; this.greedy = false;
			int m = Parsing();
			this.greedy = greedy; matchs.Clear(); locs.Clear(); errs.Clear();
			if (tokens != null) scan.Unload();
			return m >= 0;
		}

		int Parsing()
		{
			locs.Add(loc = 0);
			foreach (var x in start.s)
				Add(x, 0, 0, 0, -2, -1);
			largest = largestLoc = 0;
			do
			{
				int c; Complete(locs[loc]); c = matchs.Count;
				int p; Predict(locs[loc]); p = matchs.Count;
				for (; ; )
				{
					Complete(c); if (c == (c = matchs.Count)) break;
					Predict(p); if (p == (p = matchs.Count)) break;
				}
				if (matchs.Count - locs[loc] > largest)
					largest = matchs.Count - locs[largestLoc = loc];
			} while (Shift());
			total = matchs.Count;
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con.name == start.name && m.from == 0 && m.con.s[m.step] == null)
					return x;
			}
			return -1;
		}

		void Predict(int x)
		{
			for (; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con.s[m.step] is Alt a)
					foreach (var con in a.s)
						Add(con, loc, loc, 0, x, -1);
				if ((m.con.reps[m.step] & 1) == 0 && (m.prev != m.tail || matchs[m.prev].step < m.step))
					Add(m.con, m.from, m.to, m.step + 1, x, x);
			}
		}

		void Complete(int empty)
		{
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if ((x >= empty || m.from == loc) && m.con.s[m.step] == null)
					for (int px = locs[m.from], py = m.from < loc ? locs[m.from + 1] : matchs.Count; px < py; px++)
					{
						var pm = matchs[px];
						if (pm.con.s[pm.step] is Alt a && a.name == m.con.name)
							Add(pm.con, pm.from, pm.to, pm.step + 1, px, x);
					}
			}
		}

		bool Shift()
		{
			if (!scan.Next())
				return false;
			locs.Add(matchs.Count);
			for (int x = locs[loc], y = locs[++loc]; x < y; x++)
			{
				var m = matchs[x];
				if (m.con.s[m.step] is K k && scan.Is(k))
					Add(m.con, m.from, m.to, m.step + 1, x, x);
			}
			return locs[loc] < matchs.Count;
		}

		int Add(Con c, int from, int to, int step, int prev, int tail)
		{
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con == c && m.from == from && m.step == step)
				{
					if (greedy && tail >= 0 && m.tail >= 0 && matchs[prev].to > matchs[m.prev].to)
						matchs[x] = new Match { con = c, from = from, to = loc, step = step, prev = prev, tail = tail };
					return x;
				}
			}
			int z = matchs.Count;
			matchs.Add(new Match { con = c, from = from, to = loc, step = step, prev = prev, tail = tail });
			if (to < loc // otherwise already added
				&& step > 0 && c.reps[step - 1] > 1)
				matchs.Add(new Match { con = c, from = from, to = loc, step = step - 1, prev = z, tail = z });
			return z;
		}

		Tree<T> Accepted(int match, Tree<T> insert)
		{
			var m = matchs[match];
			if (m.from == m.to && m.step == 0 && m.con.s.Length > 1)
				return null;
			var t = (m.con.tree < 0 || m.con.tree == 0 && treeThru ? insert : null) ??
				new Tree<T> { name = m.con.name, dump = Dump(m), from = m.from, to = m.to };
			for (; m.tail >= 0; m = matchs[m.prev])
				if (m.prev != m.tail)
					Accepted(m.tail, t);
			return insert == null || insert == t ? t : insert.Insert(t);
		}

		Tree<T> Rejected()
		{
			int to = locs[loc] < matchs.Count ? loc : loc - 1, x = locs[to];
			var t = new Tree<T> { name = "", dump = Dump(matchs[0], to), from = 0, to = to, err = -1 };
			for (int y = matchs.Count - 1, z; (z = y) >= x; y--)
			{
				Prev: var m = matchs[z];
				if (m.con.s[m.step] != null && !errs.Contains(z))
					if (m.step > 0)
					{
						t.Insert(new Tree<T> { name = m.con.name, dump = Dump(m), from = m.from, to = m.to, err = m.step });
						errs.Add(z);
					}
					else if ((z = m.prev) >= 0)
						goto Prev;
			}
			return t;
		}

		string Dump(Match m, int to = -1) => !treeDump ? null : to >= 0 ? scan.Tokens(0, to).ToString() :
			$"{m.con.ToString()} :: {scan.Tokens(m.from, m.to).ToString().Replace("\n", "\\n").Replace("\r", "\\r")}";

		public T Tokens(Tree<T> t) => t.tokens = t.tokens ?? scan.Tokens(t.from, t.to);
	}
}
