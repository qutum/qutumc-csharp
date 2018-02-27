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
		Dictionary<string, string[]> grammar;
		Dictionary<string, Alt> rules;
		string start;
		IEnumerable<T> tokens;
		IEnumerator<T> token;
		List<Match> matchs;
		List<int> locs;
		int loc;
		internal bool treeSingle = false;
		internal bool treeText = false;

		sealed class Alt
		{
			internal string name;
			internal Con[] cons;

			public override string ToString() => string.Join(" | ", (object[])cons);
		}

		sealed class Con
		{
			internal string name;
			internal object[] s; // Alt or char or null

			public override string ToString() => name + "="
				+ string.Join(' ', s.Where(x => x != null).Select(x => x is Alt a ? a.name : x.ToString()));
		}

		struct Match
		{
			internal Con con;
			internal int from, to, step;
			internal int prev, last; // both >=0 for completion, only prev >=0 for prediction

			public override string ToString() => $"{from}:{to}#{step} {con}";
		}

		internal Earley(Dictionary<string, string[]> grammar, string start = "Start")
		{
			this.grammar = grammar;
			this.start = start;
			rules = new Dictionary<string, Alt>();
			foreach (var kv in grammar)
				rules[kv.Key] = new Alt { name = kv.Key };
			foreach (var kv in grammar)
				rules[kv.Key].cons = kv.Value.Select(alt => new Con
				{
					name = kv.Key,
					s = alt.Split(' ', StringSplitOptions.RemoveEmptyEntries).SelectMany(
						cat => rules.TryGetValue(cat, out Alt g) ? new object[] { g } :
							cat.Select(t => (object)t).ToArray()).Append(null).ToArray()
				}).ToArray();
			matchs = new List<Match>();
			locs = new List<int>();
		}

		internal Tree Parse(IEnumerable<T> tokens)
		{
			int m = Accept(tokens);
			Tree s = Build(m);
			this.tokens = null; token = null;
			matchs.Clear(); locs.Clear();
			return s;
		}

		internal bool Check(IEnumerable<T> tokens)
		{
			int m = Accept(tokens);
			this.tokens = null; token = null;
			matchs.Clear(); locs.Clear();
			return m >= 0;
		}

		int Accept(IEnumerable<T> tokens)
		{
			this.tokens = tokens;
			token = tokens.GetEnumerator();
			locs.Add(loc = 0);
			foreach (var x in rules[start].cons)
				Add(x, loc, 0, -1, -1);
			do
			{
				Complete();
				Predict();
			} while (Shift());
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con.name == start && m.from == 0 && m.con.s[m.step] == null)
					return x;
			}
			return -1;
		}

		void Add(Con gram, int from, int step, int prev, int last)
		{
			for (int x = locs[loc]; x < matchs.Count; x++)
				if (matchs[x].con == gram && matchs[x].from == from && matchs[x].step == step)
					return;
			matchs.Add(new Match { con = gram, from = from, to = loc, step = step, prev = prev, last = last });
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
			return true;
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
					foreach (var g in s.cons)
						Add(g, loc, 0, x, -1);
			}
		}

		Tree Build(int match)
		{
			var m = matchs[match];
			if (!treeSingle && m.con.s.Length == 2 && m.last >= 0)
				return Build(m.last);
			var tree = new Tree
			{
				name = m.con.name,
				text = !treeText ? "" : m.con.ToString() + " : " +
					(tokens.ToString() is string s && s.Length >= m.to ? s.Substring(m.from, m.to - m.from) : ""),
				from = m.from,
				to = m.to
			};
			for (; m.last >= 0; m = m = matchs[m.prev])
				tree.Insert(Build(m.last));
			return tree;
		}

		internal class Tree : LinkTree<Tree>
		{
			internal string name, text;
			internal int from, to;

			internal override string DumpSelf(string ind, string pos) => $"{ind}{pos}{from}:{to} {text}";
		}
	}

	class LinkTree<T> where T : LinkTree<T>
	{
		internal T up;
		internal T prev, next;
		internal T first, last;

		internal T Add(T sub)
		{
			if (sub == null) return (T)this;
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
			if (sub == null) return (T)this;
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
			if (next == null) return (T)this;
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

		internal void Dump(string ind = "", int pos = 0)
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
		}

		internal virtual string DumpSelf(string ind, string pos) => $"{ind}{pos} dump";
	}
}
