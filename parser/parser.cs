//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace qutum
{
	class QEarley<T, K>
	{
		Alt start;
		Scan<T, K> scan;
		List<Match> matchs;
		List<int> locs;
		HashSet<int> errs;
		int loc;
		internal int largest, largestLoc, total;
		internal bool greedy = true; // S=AB A=1|A1 B=1|B1  gready: (11)1  no: 1(11) (mostly?)
		internal bool treeThru = false;
		internal bool treeText = false;
		internal bool treeDump = false;

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

		struct Match
		{
			internal Con con;
			internal int from, to, step; // empty (always predicted): from==to (step could be 0 in repeatition)
			internal int prev, tail; // completed: both >=0, predicted: only prev >=0, T? predicted: prev==tail

			public override string ToString() => $"{from}:{to}#{step} {con}";
		}

		QEarley()
		{
			matchs = new List<Match>();
			locs = new List<int>();
			errs = new HashSet<int>();
		}

		internal QEarley(Dictionary<string, string[]> grammar, Scan<T, K> scan, string start = "Start") : this()
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

		internal Tree Parse(T tokens)
		{
			int m = Parsing(tokens);
			Tree s = m >= 0 ? Accepted(m, null) : Rejected();
			scan.Unload(); matchs.Clear(); locs.Clear(); errs.Clear();
			return s;
		}

		internal bool Check(T tokens)
		{
			bool greedy = this.greedy; this.greedy = false;
			int m = Parsing(tokens);
			scan.Unload(); matchs.Clear(); locs.Clear(); errs.Clear();
			this.greedy = greedy;
			return m >= 0;
		}

		int Parsing(T tokens)
		{
			scan.Load(tokens);
			locs.Add(loc = 0);
			foreach (var x in start.s)
				Add(x, 0, 0, 0, -2, -1);
			largest = largestLoc = 0;
			if (treeDump) treeText = true;
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
				bool opt = (m.con.reps[m.step] & 1) == 0; int y = -1;
				if (m.con.s[m.step] is Alt a)
				{
					foreach (var con in a.s)
						y = Add(con, loc, loc, 0, x, -1);
					if (opt)
						Add(m.con, m.from, m.to, m.step + 1, x, y);
				}
				else if (opt && m.con.s[m.step] is K)
					Add(m.con, m.from, m.to, m.step + 1, x, x); // like completed alt?
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
					Add(m.con, m.from, m.to, m.step + 1, m.prev, m.tail);
			}
			return locs[loc] < matchs.Count;
		}

		int Add(Con c, int from, int to, int step, int prev, int tail)
		{
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x]; Match t, mt;
				if (m.con == c && m.from == from && m.step == step)
				{
					if (greedy && tail >= 0 && m.tail >= 0 &&
						((t = matchs[tail]).to > (mt = matchs[m.tail]).to || t.to == mt.to && t.from > mt.from))
						matchs[x] = new Match { con = c, from = from, to = loc, step = step, prev = prev, tail = tail };
					return x;
				}
			}
			int y = matchs.Count;
			matchs.Add(new Match { con = c, from = from, to = loc, step = step, prev = prev, tail = tail });
			if (to < loc && step > 0 && c.reps[step - 1] > 1)
				matchs.Add(new Match { con = c, from = from, to = loc, step = step - 1, prev = prev, tail = tail });
			return y;
		}

		Tree Accepted(int match, Tree insert)
		{
			var m = matchs[match];
			if (m.from == m.to && m.step == 0 && m.con.s.Length > 1)
				return null;
			var t = (m.con.tree < 0 || m.con.tree == 0 && treeThru ? insert : null) ??
				TreeText(new Tree { name = m.con.name, from = m.from, to = m.to }, match, m);
			for (; m.tail >= 0; m = matchs[m.prev])
				if (m.prev != m.tail)
					Accepted(m.tail, t);
			return insert == null || insert == t ? t : insert.Insert(t);
		}

		Tree Rejected()
		{
			int to = locs[loc] < matchs.Count ? loc : loc - 1, x = locs[to];
			var t = new Tree { name = "", text = treeText ? scan.Text(0, to) : "", from = 0, to = to, err = -1 };
			for (int y = matchs.Count - 1, z; (z = y) >= x; y--)
			{
				Prev: var m = matchs[z];
				if (m.con.s[m.step] != null && !errs.Contains(z))
					if (m.step > 0)
					{
						t.Insert(TreeText(new Tree { name = m.con.name, from = m.from, to = m.to, err = m.step }, z, m));
						errs.Add(z);
					}
					else if ((z = m.prev) >= 0)
						goto Prev;
			}
			return t;
		}

		Tree TreeText(Tree t, int x, Match m)
		{
			if (treeText) t.text = scan.Text(m.from, m.to);
			if (treeDump) { t.dump = m.con.ToString() + " :: "; t.text = t.text.Replace("\n", "\\n").Replace("\r", "\\r"); }
			return t;
		}

		internal class Tree : LinkTree<Tree>
		{
			internal string name, text, dump;
			internal int from, to;
			internal int err; // step expected: > 0

			internal override string DumpSelf(string ind, string pos) =>
				$"{ind}{pos}{from}:{to}{(err != 0 ? "!" + err : "")} {dump}{text}";
		}
	}

	class ParserStr : QEarley<string, char>
	{
		internal ParserStr(Dictionary<string, string[]> grammar, string start = "Start")
			: base(grammar, new ScanStr(), start) { }
	}
}
