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
	class Earley<T> where T : IEquatable<T>
	{
		Dictionary<string, Alt> prods;
		IEnumerable<T> tokens;
		IEnumerator<T> token;
		List<Match> matchs;
		List<int> locs;
		HashSet<int> errs;
		int loc, largest, largestLoc;
		internal string start = "Start";
		internal bool treeGreedy = true; // S=AB A=1|A1 B=1|B1  gready: (11)1  no: 1(11) (mostly?)
		internal bool treeThru = true; // S=A2 A=B B=1  thru: S=12 B=1  no: S=12 A=1 B=1
		internal bool treeText = false;

		sealed class Alt
		{
			internal string name;
			internal Con[] s;
		}

		sealed class Con
		{
			internal string name;
			internal object[] s; // Alt or T or null

			internal Con() { }
			internal Con(params object[] s) => this.s = s;

			public override string ToString() => name + "="
				+ string.Join(' ', s.Where(x => x != null).Select(x => x is Alt a ? a.name : x.ToString()));
		}

		struct Match
		{
			internal Con con;
			internal int from, to, step;
			internal int prev, last; // completed: both >=0, predicted: only prev >=0 

			public override string ToString() => $"{from}:{to}#{step} {con}";
		}

		Earley()
		{
			prods = new Dictionary<string, Alt>();
			matchs = new List<Match>();
			locs = new List<int>();
			errs = new HashSet<int>();
		}

		internal Earley(Dictionary<string, string[]> grammar) : this()
		{
			foreach (var kv in grammar)
				prods[kv.Key] = new Alt { name = kv.Key };
			foreach (var kv in grammar)
				prods[kv.Key].s = kv.Value.Select(alt => new Con
				{
					name = kv.Key,
					s = alt.Split(' ', StringSplitOptions.RemoveEmptyEntries).SelectMany(
						cat => prods.TryGetValue(cat, out Alt g) ? new object[] { g } :
							cat.Select(t => (object)t).ToArray()).Append(null).ToArray()
				}).ToArray();
		}

		internal Tree Parse(IEnumerable<T> tokens)
		{
			int m = Parsing(tokens);
			Tree s = m >= 0 ? Accepted(m) : Rejected();
			this.tokens = null; token = null;
			matchs.Clear(); locs.Clear(); errs.Clear();
			return s;
		}

		internal bool Check(IEnumerable<T> tokens)
		{
			bool greedy = treeGreedy;
			treeGreedy = false;
			int m = Parsing(tokens);
			this.tokens = null; token = null;
			matchs.Clear(); locs.Clear(); errs.Clear();
			treeGreedy = greedy;
			return m >= 0;
		}

		int Parsing(IEnumerable<T> tokens)
		{
			this.tokens = tokens;
			token = tokens.GetEnumerator();
			locs.Add(loc = 0);
			foreach (var x in prods[start].s)
				Add(x, loc, 0, -1, -1);
			largest = largestLoc = 0;
			do
			{
				Complete();
				Predict();
				if (matchs.Count - locs[loc] > largest)
					largest = matchs.Count - locs[largestLoc = loc];
			} while (Shift());
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con.name == start && m.from == 0 && m.con.s[m.step] == null)
					return x;
			}
			return -1;
		}

		void Add(Con con, int from, int step, int prev, int last)
		{
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con == con && m.from == from && m.step == step)
				{
					if (treeGreedy && last >= 0 && m.last >= 0 && matchs[last].from > matchs[m.last].from)
						matchs[x] = new Match { con = con, from = from, to = loc, step = step, prev = prev, last = last };
					return;
				}
			}
			matchs.Add(new Match { con = con, from = from, to = loc, step = step, prev = prev, last = last });
		}

		void Complete()
		{
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con.s[m.step] == null)
					for (int px = locs[m.from], py = locs[m.from + 1]; px < py; px++)
					{
						var pm = matchs[px];
						if (pm.con.s[pm.step] is Alt g && g.name == m.con.name)
							Add(pm.con, pm.from, pm.step + 1, px, x);
					}
			}
		}

		void Predict()
		{
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con.s[m.step] is Alt s)
					foreach (var g in s.s)
						Add(g, loc, 0, x, -1);
			}
		}

		bool Shift()
		{
			if (!token.MoveNext())
				return false;
			locs.Add(matchs.Count);
			for (int x = locs[loc], y = locs[++loc]; x < y; x++)
			{
				var m = matchs[x];
				if (m.con.s[m.step] is T t && t.Equals(token.Current))
					Add(m.con, m.from, m.step + 1, m.prev, m.last);
			}
			return locs[loc] < matchs.Count;
		}

		Tree Accepted(int match)
		{
			var m = matchs[match];
			if (treeThru && m.con.s.Length == 2 && m.last >= 0)
				return Accepted(m.last);
			var t = new Tree { name = m.con.name, text = TreeText(m), from = m.from, to = m.to };
			for (; m.last >= 0; m = m = matchs[m.prev])
				t.Insert(Accepted(m.last));
			return t;
		}

		Tree Rejected()
		{
			int to = locs[loc] < matchs.Count ? loc : loc - 1, x = locs[to];
			var t = new Tree { name = "", text = "", from = 0, to = to, err = 1 };
			for (int y = matchs.Count - 1, z; (z = y) >= x; y--)
			{
				Prev: var m = matchs[z];
				if (m.con.s[m.step] != null && !errs.Contains(z))
					if (m.step > 0)
					{
						t.Insert(new Tree { name = m.con.name, text = TreeText(m), from = m.from, to = m.to, err = m.step });
						errs.Add(z);
					}
					else if ((z = m.prev) >= 0)
						goto Prev;
			}
			return t;
		}

		string TreeText(Match m) => !treeText ? "" : m.con + " : " +
			(tokens.ToString() is string s && s.Length >= m.to ? s.Substring(m.from, m.to - m.from) : "");

		internal class Tree : LinkTree<Tree>
		{
			internal string name, text;
			internal int from, to;
			internal int err; // step expected: > 0

			internal override string DumpSelf(string ind, string pos) =>
				$"{ind}{pos}{from}:{to}{(err > 0 ? "!" + err : "")} {text}";
		}
	}

	class LinkTree<T> where T : LinkTree<T>
	{
		internal T up;
		internal T prev, next;
		internal T first, last;

		internal T Add(T sub)
		{
			if (sub == null)
				return (T)this;
			Debug.Assert(sub.up == null);
			var end = sub;
			for (end.up = (T)this; end.next != null; end.up = (T)this)
				end = end.next;
			if (first == null)
				first = sub;
			else
				(sub.prev = last).next = sub;
			last = end;
			return (T)this;
		}

		internal T Insert(T sub)
		{
			if (sub == null)
				return (T)this;
			Debug.Assert(sub.up == null);
			var end = sub;
			for (end.up = (T)this; end.next != null; end.up = (T)this)
				end = end.next;
			if (first == null)
				last = end;
			else
				(end.next = first).prev = end;
			first = sub;
			return (T)this;
		}

		internal T Append(T next)
		{
			if (next == null)
				return (T)this;
			Debug.Assert(up == null && next.up == null && next.prev == null);
			var end = (T)this;
			while (end.next != null)
				end = end.next;
			(next.prev = end).next = next;
			return (T)this;
		}

		internal T Remove()
		{
			if (prev != null)
				prev.next = next;
			if (next != null)
				next.prev = prev;
			if (up != null && up.first == this)
				up.first = next;
			if (up != null && up.last == this)
				up.last = prev;
			up = prev = next = null;
			return (T)this;
		}

		internal T Dump(string ind = "", int pos = 0)
		{
			int f = 1, fix = 1;
			for (var x = first; ; x = x.next, f++)
			{
				if (f == fix)
					Console.WriteLine(DumpSelf(ind, pos < 0 ? "/ " : pos > 0 ? "\\ " : ""));
				if (x == null)
					break;
				x.Dump(pos == 0 ? ind : ind + (pos == (f < fix ? -2 : 2) ? "  " : "| "),
					f < fix ? x == first ? -2 : -1 : x == last ? 2 : 1);
			}
			return (T)this;
		}

		internal virtual string DumpSelf(string ind, string pos) => $"{ind}{pos} dump";
	}
}
