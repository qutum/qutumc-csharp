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
		int loc;
		internal string start = "Start";
		internal int largest, largestLoc;
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
			internal byte[] reps; // ?: 0, once: 1, *: 2, +: 3

			internal Con() { }
			internal Con(params object[] s) => this.s = s;

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
				Add(x, loc, 0, -2, -1);
			largest = largestLoc = 0;
			do
			{
				Complete(locs[loc]);
				Predict(locs[loc]);
				for (int c = matchs.Count, p = c; ; )
				{
					Complete(c); if (c == (c = matchs.Count)) break;
					Predict(p); if (p == (p = matchs.Count)) break;
				}
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

		void Predict(int x)
		{
			for (; x < matchs.Count; x++)
			{
				var m = matchs[x]; bool opt = (m.con.reps[m.step] & 1) == 0;
				if (m.con.s[m.step] is Alt a)
				{
					foreach (var con in a.s)
						Add(con, loc, 0, x, -1);
					if (opt)
						Add(a.s[0], loc, a.s[0].s.Length - 1, x, -1);
				}
				else if (opt && m.con.s[m.step] is T)
					Add(m.con, m.from, m.step + 1, x, x); // like completed alt?
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
							Add(pm.con, pm.from, pm.step + 1, px, x);
					}
			}
		}

		bool Shift()
		{
			if (!token.MoveNext()) return false;
			locs.Add(matchs.Count);
			for (int x = locs[loc], y = locs[++loc]; x < y; x++)
			{
				var m = matchs[x];
				if (m.con.s[m.step] is T t && t.Equals(token.Current))
					Add(m.con, m.from, m.step + 1, m.prev, m.tail);
			}
			return locs[loc] < matchs.Count;
		}

		int Add(Con c, int from, int step, int prev, int tail)
		{
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x]; Match t, mt;
				if (m.con == c && m.from == from && m.step == step)
				{
					if (treeGreedy && tail >= 0 && m.tail >= 0 &&
						((t = matchs[tail]).to > (mt = matchs[m.tail]).to || t.to == mt.to && t.from > mt.from))
						matchs[x] = new Match { con = c, from = from, to = loc, step = step, prev = prev, tail = tail };
					return x;
				}
			}
			int y = matchs.Count;
			matchs.Add(new Match { con = c, from = from, to = loc, step = step, prev = prev, tail = tail });
			if (step > 0 && c.reps[step - 1] > 1)
				matchs.Add(new Match { con = c, from = from, to = loc, step = step - 1, prev = prev, tail = tail });
			return y;
		}

		Tree Accepted(int match)
		{
			var m = matchs[match];
			if (treeThru && m.tail >= 0 && m.prev != m.tail && m.con.s.Length == 2 && m.con.reps[0] <= 1)
				return Accepted(m.tail);
			var t = new Tree { name = m.con.name, text = TreeText(m), from = m.from, to = m.to };
			for (m = m.prev != m.tail ? m : matchs[m.prev]; m.tail >= 0; m = matchs[m.prev])
				t.Insert(Accepted(m.tail));
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

		string TreeText(Match m) => treeText ? m.con +
			((object)tokens is string s ? " :: " + s.Substring(m.from, m.to - m.from) : "") : "";

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
		internal T up, prev, next, head, tail;

		internal T Add(T sub)
		{
			if (sub == null)
				return (T)this;
			Debug.Assert(sub.up == null);
			var end = sub;
			for (end.up = (T)this; end.next != null; end.up = (T)this)
				end = end.next;
			if (head == null)
				head = sub;
			else
				(sub.prev = tail).next = sub;
			tail = end;
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
			if (head == null)
				tail = end;
			else
				(end.next = head).prev = end;
			head = sub;
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
			if (up != null && up.head == this)
				up.head = next;
			if (up != null && up.tail == this)
				up.tail = prev;
			up = prev = next = null;
			return (T)this;
		}

		internal T Dump(string ind = "", int pos = 0)
		{
			int f = 1, fix = 1;
			for (var x = head; ; x = x.next, f++)
			{
				if (f == fix)
					Console.WriteLine(DumpSelf(ind, pos < 0 ? "/ " : pos > 0 ? "\\ " : ""));
				if (x == null)
					break;
				x.Dump(pos == 0 ? ind : ind + (pos == (f < fix ? -2 : 2) ? "  " : "| "),
					f < fix ? x == head ? -2 : -1 : x == tail ? 2 : 1);
			}
			return (T)this;
		}

		internal virtual string DumpSelf(string ind, string pos) => $"{ind}{pos} dump";
	}
}
