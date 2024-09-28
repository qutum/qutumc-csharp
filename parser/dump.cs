//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// synter.stack
[assembly: DebuggerTypeProxy(typeof(Dumps.Stack), Target = typeof((SynForm, int, object)))]

namespace qutum.parser;

using Kord = char;
using Nord = ushort;

public partial struct Lexi<K>
{
	public override readonly string ToString() =>
		$"{key}{(err < 0 ? "!" : err > 0 ? "?" : value != null ? "=" : "")}{value}";

	public readonly string Dumper(Func<object, object> dumper) =>
		new StrMake(out var s) + key + (err < 0 ? "!" : err > 0 ? "?" : value != null ? "=" : "")
		+ (value != null ? s + dumper(value) : s);
}

public partial class Lexier<K>
{
	static void Dump(Unit u, string pre, Dictionary<Unit, bool> us = null)
	{
		using var env = EnvWriter.Use();
		var usNull = us == null;
		us ??= [];
		us[u] = false; // dumped
		if (u.id == 0)
			env.WriteLine("unit:");
		env.WriteLine($"{u.id}: {u.key}.{u.wad} " +
			$"{(u.mode >= 0 ? "back" : u.mode == -1 ? "ok" : "err")}.{u.go.id} < {pre}");
		if (!us.ContainsKey(u.go))
			us[u.go] = true; // not dumped yet
		if (u.next == null)
			return;
		foreach (var n in u.next.Where(n => n != null).Distinct()) {
			var s = u.next.Select((nn, b) => nn != n ? null : CharSet.Unesc((byte)b))
				.Where(x => x != null);
			using var _ = EnvWriter.Indent("  ");
			if (n == u)
				env.WriteLine($"+ < {string.Join(' ', s)}");
			else
				Dump(n, string.Join(' ', s), us);
		}
	Go: if (usNull)
			foreach (var go in us)
				if (go.Value) {
					Dump(go.Key, $"{go.Key.key}.{go.Key.wad - 1}", us);
					goto Go;
				}
	}

	public string Dumper() => string.Join(" ", lexs.Seg(0, loc).Select(t => t.ToString()).ToArray());
}

public partial class LinkTree<T>
{
	public T Dump(Func<LinkTree<T>, object> dumper = null, int after = 1)
	{
		bool first = prev == null && (up == null || after <= 0);
		bool last = next == null && (up == null || after > 0);
		string noInd = up == null && after == 0 ? "" : null;
		var t = head;
		for (; t != null && (dumpOrder == 0 ? t == head : dumpOrder < 0); t = t.next)
			using (var env = EnvWriter.Indent(noInd ?? (first ? "  " : "| ")))
				t.Dump(dumper, -1);
		using (var env = noInd != null ? EnvWriter.Indent(noInd, "   ") : EnvWriter.Indent(
				after > 0 ? first ? "- " : "\\ " : last ? "- " : "/ ",
				t != null ? last ? "  |  " : "| |  " : last ? "     " : "|    "))
			env.WriteLine(dumper?.Invoke(this) ?? this);
		for (; t != null; t = t.next)
			using (var env = EnvWriter.Indent(noInd ?? (last ? "  " : "| ")))
				t.Dump(dumper, 1);
		if (up == null && prev == null)
			for (t = next; t != null; t = t.next)
				t.Dump(dumper, after);
		return (T)this;
	}

	// preorder >0, inorder 0, postorder <0
	public virtual int dumpOrder => 1;
}

public partial class Synt<N, T>
{
	public object dump;

	public string Dumper(StrMake s, Func<Synt<N, T>, object> dumper = null) =>
		(s.s != null ? s : (s = new()) + from + ':' + to)
		+ (err > 0 ? "!!" : err < 0 ? "!" : "")
		+ (info is Synt<N, T> or null ? s : s + ' ' + info) + " : " + (dump ?? name)
		+ (info is Synt<N, T> t ? s + '\n' + (dumper?.Invoke(t) ?? t) : s);

	public override string ToString() => Dumper(default);
}

public partial class SynAlt<N>
{
	public object dump;

	public override string ToString() => dump as string ?? (string)
		(dump is Func<string> d ? dump = d() : dump is Func<object, string> dd ? dump = dd(this)
		: dump?.ToString() ?? label ?? name + " alt");
}

[DebuggerTypeProxy(typeof(Dumps.Form))]
public partial class SynForm
{
	public object dump;

	public override string ToString() => dump as string ?? (string)
		(dump is Func<string> d ? dump = d() : dump is Func<object, string> dd ? dump = dd(this)
		: dump?.ToString() ?? base.ToString());
}

public static partial class Dumps
{
	public struct Form
	{
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		public string dump;
		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public Str[] s;
		public Str err;

		public Form(SynForm f)
		{
			if (f.dump is Form d)
				(dump, s) = (d.dump, d.s);
			else // DebuggerDisplay stupidly ignored for DebuggerTypeProxy
				(_, _, _) = (s = f.ToString().Split('\n').Select(s => (Str)s).ToArray(),
					dump = $"form {f.index} size {s?.Length}",
					f.dump = this);
			err = (Str)f.err;
		}
		public override readonly string ToString() => dump;
	}

	[DebuggerDisplay("title")] // DebuggerDisplay stupidly ignored for DebuggerTypeProxy
	public struct Stack((SynForm form, int loc, object synt) d)
	{
		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public SynForm form = d.form;
		public object synt = d.synt;

		public override readonly string ToString() => $"@{d.loc} {form}"; // still ignored
	}
}

public partial class Synter<K, L, N, T, Ler>
{
	public int dump = 1; // least: 0, and line column: 1, and lexs: 2, and alts: 3
	public Func<object, string> dumper;

	protected virtual void InitDump()
	{
		dumper = Dumper;
		foreach (var (f, fx) in forms.Each())
			if (f != null)
				f.dump ??= (Func<object, string>)Dumper;
	}

	public virtual string Dumper(object d)
	{
		if (d is Kord key) return key == default ? "eor" : CharSet.Unesc(key);
		if (d is Nord name) return CharSet.Unesc((char)name);
		if (d is SynForm f) {
			StrMake s = new(); short r;
			if (dumper != Dumper)
				s += dumper(d);
			if (f.other != No)
				_ = s - '\n' + "  redu " + (r = SynForm.Redu(f.other)) + " " + alts[r];
			foreach (var (g, k) in f.goKs.Yes())
				_ = s - '\n' + Dumper(k) + (g > No ? s + " shift " + g
						: s + " redu " + (r = SynForm.Redu(g)) + " " + alts[r]);
			foreach (var (g, n) in f.goNs.Yes())
				_ = s - '\n' + Dumper(n) + " go " + g;
			foreach (var (a, want) in f.recs ?? [])
				_ = s - '\n' + "recover " + a + '_' + want + ' ' + alts[a];
			return s;
		}
		if (dump > 0 && d is T t) {
			StrMake s = default;
			if (t.from >= 0 && t.dump is not Dumps.Str && dump >= 2) {
				_ = (s = new()) + (t.dump ?? t.name.ToString()) + " :";
				foreach (var l in ler.Lexs(t.from, t.to))
					_ = s + ' ' + l;
				t.dump = (Dumps.Str)(string)s;
				s.s.Clear();
			}
			return (ler as LexierBuf<K>)?.LineCol(t.from, t.to) is var (fl, fc, tl, tc)
				? t.Dumper((s.s != null ? s : new()) + fl + '.' + fc + ':' + tl + '.' + tc, Dumper)
				: t.Dumper(s, Dumper);
		}
		return d.ToString();
	}
}

public partial class SynGram<K, N>
{
	public partial class Alt
	{
		public override string ToString()
		{
			var s = new StrMake() + name + " =";
			foreach (var c in this)
				s = s + ' ' + SerMaker<K, N>.Dumper_(c);
			return s + "  " + clash switch { 0 => "", 1 => "<", > 1 => ">", _ => "^" }
				+ (rec ? "!!" : "") + synt switch { 0 => "", < 0 => "-", 2 => "(", 3 => ")", _ => "+" }
				+ (lex >= 0 ? s + '.' + lex : s) + ' ' + label;
		}
	}
}

public partial class SerMaker<K, N>
{
	public static string Dumper_(object d)
	{
		StrMake s = default;
		if (d is K key) return key.Equals(default(K)) ? "eor" : CharSet.Unesc(key);
		if (d is (IEnumerable<K> keys2, StrMake s2))
			(d, s) = (keys2, s2);
		if (d is IEnumerable<K> keys) {
			var ss = s.s == null; if (ss) s = new();
			foreach (var k in keys)
				_ = s - ' ' + (k.Equals(default(K)) ? "eor" : CharSet.Unesc(k));
			return ss ? s : null;
		}
		return d.ToString();
	}

	public string Dumper(object d)
	{
		if (d is SynForm f) {
			StrMake s = new();
			foreach (var ((a, want), (heads, clo)) in forms[f.index].Is)
				_ = s - '\n' + want + "_ " + a.ToString() + "  " + Dumper_((heads, s)) + "  ~" + clo;
			return s;
		}
		return Dumper_(d);
	}

	public void Dump(object d)
	{
		if (d is List<Form> forms)
			using (var env = EnvWriter.Use())
				env.WriteLine($"forms: {forms.Count} (max {(
					forms.Select(f => f.Is.Count).Max())} items)");
		else if (d is (Dictionary<Clash, (HashSet<K> keys, short go)> clashs,
				bool detail, int solvez)) {
			using var env = EnvWriter.Use();
			env.WriteLine(detail ? $"clashes: {clashs.Count} ({solvez} solved)"
						: $"unsolved clashes: {clashs.Count} (besides {solvez} solved)");
			foreach (var ((c, (keys, go)), cx) in clashs.Each(1)) {
				env.Write(cx + (go == No ? "  : " : " :: "));
				env.WriteLine(Dumper(keys));
				using var _ = EnvWriter.Indent("\t\t");
				foreach (var a in c.redus)
					env.WriteLine($"{(a == SynForm.Redu(go) ? "REDU" : "redu")} {a}  {alts[a]}");
				foreach (var a in c.shifts ?? [])
					env.WriteLine($"{(go > No ? "SHIFT" : "shift")} {a}  {alts[a]}");
			}
		}
	}
}

public static partial class Dumps
{
	[DebuggerDisplay("{d,nq}")]
	public struct Str
	{
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		public string d;
		public override readonly string ToString() => d;
		public static implicit operator string(Str d) => d.d;
		public static explicit operator Str(string d) => new() { d = d };
	}
}
