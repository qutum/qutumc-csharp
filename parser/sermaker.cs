//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace qutum.parser;

// syntax grammar
public class SynGram<K, N>
{
	public readonly List<Prod> prods = [];
	public class Prod : List<Alt>
	{
		public N name;
		public string hint;
	}
	// { K or N ... }
	public class Alt : List<object>
	{
		public N name;
		public int clash; // reject: 0, solve 1: 1, solve 2: 2
						  // as same rule and index as previous one: ~actual alt index
		public short lex = -1; // save lex at this index to Synt.info, no save: <0
		public sbyte synt; // as Synter.tree: 0, make Synt: 1, omit Synt: -1
		public bool rec; // recover this alt when error found
		public string hint;
		internal short index; // alt index of whole grammar

		public override string ToString() => $"{name} = {string.Join(' ', this)}  {clash
			switch { 0 => "", 1 => "<", > 1 => ">", _ => "^" }}{(rec ? "!!" : "")}{(
			synt > 0 ? "+" : synt < 0 ? "-" : "")}{(lex >= 0 ? "_" + lex : "")} {hint}";
	}

	public SynGram<K, N> n(N name) { prods.Add(new() { name = name }); return this; }
	// K : lexic key, N : syntax name, .. : save last lex
	public SynGram<K, N> this[params object[] cons] {
		get {
			Alt a = [];
			a.name = prods[^1].name;
			prods[^1].Add(a);
			foreach (var c in cons)
				if (c is Range)
					a.lex = a.lex < 0 ? (short)(a.Count - 1)
						: throw new($"{a.name}.{prods[^1].Count - 1} lex {a.lex}");
				else if (c is N or K)
					a.Add(c);
				else
					throw new($"wrong altern content {c?.GetType()}");
			return this;
		}
	}
	public SynGram<K, N> clash { get { prods[^1][^1].clash = 1; return this; } }
	public SynGram<K, N> clashRight { get { prods[^1][^1].clash = 2; return this; } }
	public SynGram<K, N> clashPrev { get { prods[^1][^1].clash = -1; return this; } }
	public SynGram<K, N> recover { get { prods[^1][^1].rec = true; return this; } }
	public SynGram<K, N> synt { get { prods[^1][^1].synt = 1; return this; } }
	public SynGram<K, N> syntOmit { get { prods[^1][^1].synt = -1; return this; } }
	public SynGram<K, N> hint(string w)
	{ _ = prods[^1].Count == 0 ? prods[^1].hint = w : prods[^1][^1].hint = w; return this; }
}

// syntax parser maker
// K for lexical key i.e lexeme
// N for syntax name i.e synteme
public class SerMaker<K, N>
{
	public bool dump = true;
	public int compact = 10;

	readonly Func<K, ushort> keyOrd; // ordinal from 1, default for eor: 0
	readonly Func<N, ushort> nameOrd; // ordinal from 1
	readonly ushort[] keyOs, nameOs; // { ordinal... }
	readonly K[] keys; // at key ordinal index
	readonly N[] names; // at name ordinal index

	private int Key(K k) => Array.BinarySearch(keyOs, keyOrd(k));
	private int Name(N n) => Array.BinarySearch(nameOs, nameOrd(n));

	readonly int accept; // name ordinal index
	readonly SynGram<K, N>.Prod[] prods; // at name ordinal index
	readonly SynGram<K, N>.Alt[] alts;
	readonly (bool empty, HashSet<K> first)[] firsts; // at name ordinal index
	readonly List<Form> forms = [];

	// phase 1: find possible first key of each name
	public void Firsts()
	{
		if (firsts[0].first != null)
			return;
		for (bool loop = true; !(loop = !loop);)
			foreach (var (p, nx) in prods.Each()) {
				var nf = firsts[nx].first ??= [];
				foreach (var a in p) {
					foreach (var c in a) {
						var (cempty, cf) = c is K k ? (false, [k]) : First((N)c);
						loop |= nf.Adds(cf);
						if (!cempty)
							goto A;
					}
					_ = firsts[nx].empty || (loop = firsts[nx].empty = true); A:;
				}
			}
	}
	public (bool empty, IEnumerable<K> first) First(N name) => firsts[Name(name)];

	class Items : Dictionary<(SynGram<K, N>.Alt alt, short wait),
		HashSet<K>> // lookaheads, eor excluded
	{ }
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2231:Overload operator equals")]
	public struct Clash
	{
		internal short shift; // shift to form index if shifts not null
		public HashSet<short> redus; // alt index, no reduce: null
		public HashSet<short> shifts; // alt index, no shift: null
		public override readonly int GetHashCode() =>
			HashCode.Combine(shifts?.Count == 1 ? shifts.First() : shifts?.Count,
				redus?.Count == 1 ? redus.First() : redus.Count);
		public override readonly bool Equals(object o) => o is Clash c
			&& (redus?.SetEquals(c.redus) ?? c.redus == null)
			&& (shifts?.SetEquals(c.shifts) ?? c.shifts == null);
	}
	struct Form
	{
		internal Items Is;
		internal Clash[] clashs; // at key ordinal index
		internal short[] pushs; // push form index, at name ordinal index, no: -1
		internal short[] modes; // solved modes, at key ordinal index, error: -1
	}

	static bool AddItem(Items Is, SynGram<K, N>.Alt alt, short wait, IEnumerable<K> heads)
	{
		if (Is.TryGetValue((alt, wait), out var hs))
			return hs.Adds(heads);
		Is[(alt, wait)] = [.. heads];
		return true;
	}
	int AddForm(Items Is)
	{
		Debug.Assert(Is.Count > 0);
		foreach (var (f, x) in forms.Each())
			if (f.Is.Count == Is.Count) {
				foreach (var (i, h) in Is)
					if (!f.Is.TryGetValue(i, out var fh) || !fh.SetEquals(h))
						goto No;
				return x; No:;
			}
		forms.Add(new() {
			Is = Is, clashs = new Clash[keys.Length], pushs = new short[names.Length],
			modes = new short[keys.Length]
		});
		Array.Fill(forms[^1].modes, (short)-1);
		Array.Fill(forms[^1].pushs, (short)-1);
		return forms.Count - 1;
	}

	// make a whole items
	Items Closure(Items Is)
	{
	Loop: foreach (var ((a, wait), heads) in Is) {
			if (wait >= a.Count || a[wait] is not N name)
				continue;
			var (e, f) = wait + 1 >= a.Count ? (false, heads)
						: a[wait + 1] is K k ? (false, [k]) : First((N)a[wait + 1]);
			var loop = false;
			foreach (var A in prods[Name(name)])
				loop |= AddItem(Is, A, 0, e && heads.Count > 0 ? [.. f, .. heads] : f);
			if (loop)
				goto Loop;
		}
		return Is;
	}

	// make shifting or pushing between forms
	void ShiftPush(Form f, Dictionary<object, short> tos)
	{
		foreach (var ((a, wait), _) in f.Is) {
			if (wait >= a.Count)
				continue;
			if (!tos.TryGetValue(a[wait], out var to)) {
				Items js = [];
				foreach (var ((A, W), heads) in f.Is) {
					if (W < A.Count && A[W].Equals(a[wait]))
						AddItem(js, A, (short)(W + 1), heads);
				}
				tos.Add(a[wait], to = (short)AddForm(Closure(js)));
			}
			if (a[wait] is K k) {
				f.clashs[Key(k)].shift = to; (f.clashs[Key(k)].shifts ??= []).Add(a.index);
			}
			else
				f.pushs[Name((N)a[wait])] = to;
		}
		tos.Clear();
	}

	// phase 2: make all transition forms
	public void Forms()
	{
		if (forms.Count > 0)
			return;
		Items init = [];
		foreach (var a in prods[accept])
			init[(a, 0)] = [];
		AddForm(Closure(init));
		Dictionary<object, short> tos = [];
		for (var x = 0; x < forms.Count; x++) {
			if (x >= 32767) throw new("too many forms");
			ShiftPush(forms[x], tos);
		}
		foreach (var f in forms)
			foreach (var ((a, wait), heads) in f.Is)
				if (wait >= a.Count) // alt could be reduced
					foreach (var head in heads.Append(default)) // lookaheads and eor
						(f.clashs[Key(head)].redus ??= []).Add(a.index);
		if (dump)
			using (var env = EnvWriter.Use())
				env.WriteLine($"forms: {forms.Count}");
	}

	// phase 3: find clashes and solve them
	// solve 1: shift or reduce the latest index alt, reduce the same index (left associative)
	// solve 2: shift or reduce the latest index alt, shift the same index (right associative)
	// all clashing alts should be clashable
	public Dictionary<Clash, (HashSet<K> keys, short mode)> Clashs(bool detail = true)
	{
		Dictionary<Clash, (HashSet<K> keys, short mode)> clashs = [];
		foreach (var f in forms)
			foreach (var (c, kx) in f.clashs.Each())
				if (c.redus == null)
					f.modes[kx] = (short)(c.shifts == null ? -1 // error
										: c.shift); // shift without clash
				else if (c.shifts == null && c.redus.Count == 1)
					f.modes[kx] = SynForm.Reduce(c.redus.First()); // reduce without clash
				else if (clashs.TryGetValue(c, out var ok)) {
					// already solved
					f.modes[kx] = ok.mode;
					ok.keys.Add(keys[kx]);
				}
				else { // solve
					short mode = -1;
					if (c.redus.All(a => alts[a].clash != 0)) {
						int X(int a) => alts[a].clash < 0 ? ~alts[a].clash : a;
						int ra = c.redus.Max(), r = X(ra), i;
						if (c.shifts == null)
							mode = SynForm.Reduce(r); // reduce
						else if (alts[i = X(c.shifts.Max())].clash != 0)
							if (i > r || i == r && alts[i].clash > 1)
								mode = c.shift; // shift
							else
								mode = SynForm.Reduce(ra); // reduce
					}
					f.modes[kx] = mode;
					clashs[c] = ([keys[kx]], mode);
				}
		if (!detail)
			foreach (var (ok, _) in clashs.Where(c => c.Value.mode != -1))
				clashs.Remove(ok);
		if (dump) {
			using var env = EnvWriter.Use();
			env.WriteLine(detail ? "clashes and solutions:" : "unsolved clashes:");
			foreach (var ((c, (keys, mode)), cx) in clashs.Each(1)) {
				env.Write(cx + (mode == -1 ? "  : " : " :: "));
				env.WriteLine(string.Join(' ', keys.Select(k => CharSet.Unesc(k))));
				using var _ = EnvWriter.Indent("\t\t");
				foreach (var a in c.redus)
					env.WriteLine($"{(a == SynForm.Reduce(mode) ? "REDUCE" : "reduce")} {a}  {alts[a]}");
				foreach (var a in c.shifts ?? [])
					env.WriteLine($"{(mode >= 0 ? "SHIFT" : "shift")} {a}  {alts[a]}");
			}
		}
		return clashs.Count > 0 ? clashs : null;
	}

	// phase all and final: make all data for synter
	public (SynAlt<N>[] alts, SynForm[] forms)
		Make(out Dictionary<Clash, (HashSet<K> keys, short mode)> clashs)
	{
		Firsts();
		Forms();
		clashs = Clashs(false);
		if (clashs != null)
			return (null, null);
		// synter alts
		var As = new SynAlt<N>[alts.Length];
		foreach (var (a, ax) in alts.Each())
			As[ax] = new() {
				name = a.name, size = checked((short)a.Count), lex = a.lex,
				synt = a.synt, rec = a.rec, hint = a.hint,
#pragma warning disable CS8974 // Converting method group to non-delegate type
				dump = a.ToString,
			};
		// synter forms
		var Fs = new SynForm[forms.Count];
		foreach (var (f, fx) in forms.Each()) {
			void SS(ushort[] os, short[] fs, ref short[] Fs, ref ushort[] Fos)
			{
				int z;
				if (os[^1] < compact || (z = fs.Count(m => m != -1)) >= os[^1] >>> 3)
					Fs = fs;
				else {
					Fs = new short[z + 1]; Fos = new ushort[z + 1]; Fs[0] = -1;
					for (int X = 1, x = 0; x < os.Length; x++)
						if (fs[x] != -1)
							(Fs[X], Fos[X++]) = (fs[x], os[x]);
				}
			}
			var F = Fs[fx] = new SynForm();
			SS(keyOs, f.modes, ref F.modes, ref F.keys);
			SS(nameOs, f.pushs, ref F.pushs, ref F.names);
			Error(f, F);
		}
		return (As, Fs);
	}

	void Error(Form f, SynForm F)
	{
		// TODO recover error
		F.rec = -1;
		// for better error hint, form with any reduce will always reduce on error keys
		if (F.modes.FirstOrDefault(m => m < -1, (short)-1) is short r and < -1)
			foreach (var (m, x) in F.modes.Each())
				if (m == -1) F.modes[x] = r;
		// error hints
		List<(int kind, int remain, int ax, object expect)> hints = [];
		foreach (var ((a, wait), heads) in f.Is)
			if (wait < a.Count)
				hints.Add((a[wait] is N ? 0 : 1, a.Count - wait, a.alt, a[wait])); // for next content
			else
				hints.Add((2, 0, a.alt, // to reduce
					heads.Count switch { 0 => default(K), 1 => heads.First(), _ => null }));
		hints.Sort();
		// make at most 2 hints
		F.err = string.Join(", ", hints.TakeWhile((h, x) => x <= 2 && h.kind == hints[0].kind)
			.Select((h, x) => x == 2 ? "..."
				: (h.expect == null ? "{0} unexpected by "
					: (default(K).Equals(h.expect) ? "end of read" : h.expect) + " expected by ")
				+ (alts[h.ax].hint ?? alts[h.ax].name.ToString())));
	}

	// phase init: get grammar
	public SerMaker(SynGram<K, N> gram, Func<K, ushort> keyOrd, Func<N, ushort> nameOrd,
		Action<IEnumerable<K>> distinct, bool dump = true)
	{
		this.dump = dump; this.keyOrd = keyOrd; this.nameOrd = nameOrd;
		// names
		SortedDictionary<ushort, N> ns = [];
		foreach (var (p, px) in gram.prods.Each())
			if (!ns.TryAdd(nameOrd(p.name), p.name) && !ns[nameOrd(p.name)].Equals(p.name))
				throw new($"duplicate ordinal of {ns[nameOrd(p.name)]} and {p.name}");
		nameOs = [.. ns.Keys];
		names = [.. ns.Values];
		accept = Name(gram.prods[0].name);

		// prods
		prods = new SynGram<K, N>.Prod[names.Length];
		foreach (var p in gram.prods)
			prods[Name(p.name)] ??= p;
		// alts
		SortedDictionary<ushort, K> ks = new() { { keyOrd(default), default } };
		alts = new SynGram<K, N>.Alt[gram.prods.Sum(p => p.Count)];
		if (alts.Length is 0 or > 32767)
			throw new($"total alterns {alts.Length}");
		short alt = 0;
		foreach (var p in gram.prods) {
			var np = prods[Name(p.name)];
			foreach (var (a, ax) in p.Each()) {
				if (a.Count > 30)
					throw new($"altern size {a.Count}");
				foreach (var c in a)
					if (c is K k)
						ks[keyOrd(k)] = !k.Equals(default) ? k
							: throw new($"end of read in {p.name}.{ax}");
					else if (Name((N)c) < 0)
						throw new($"name {c} in {p.name}.{ax} not found");
				if (a.clash < 0)
					if (alt == 0 || alts[alt - 1].clash == 0)
						throw new($"{p.name}.{ax} no previous clash");
					else
						a.clash = alts[alt - 1].clash < 0 ? alts[alt - 1].clash : ~(alt - 1);
				if (a.lex >= 0 && a[a.lex] is not K)
					throw new($"{p.name}.{ax} lex {a[a.lex]} not lexic key");
				if (a.rec && (a.lex < 0 || a.lex != a.Count - 1))
					throw new($"{p.name}.{ax} recovery lex at {a.lex}");
				alts[a.index = alt++] = a;
				if (np != p)
					np.Add(a); // append alterns to same name production
			}
		}
		// others
		if (keyOrd(default) != 0) throw new($"default key ordinal {keyOrd(default)}");
		if (nameOs[0] == 0) throw new($"name ordinal 0");
		keyOs = [.. ks.Keys];
		keys = [.. ks.Values];
		distinct(keys);
		firsts = new (bool, HashSet<K>)[names.Length];
	}
}
