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

[assembly: DebuggerTypeProxy(typeof(Dumper.Stack), Target = typeof((SynForm, int, object)))]

namespace qutum.parser;

using Kord = char;
using Nord = ushort;

public partial struct Lexi<K>
{
	public override readonly string ToString() => $"{key}{(err < 0 ? "!" : "=")}{value}";

	public readonly string Dumper(Func<object, string> dumper)
		=> $"{key}{(err < 0 ? "!" : "=")}{dumper(value)}";
}

public partial class Lexier<K> : LexerSeg<K, Lexi<K>>
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
			using var _ = EnvWriter.Use("  ");
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
}

public partial class Synt<N, T>
{
	public object dump;

	public override string ToString() => Dumper(new StrMaker() + from + ':' + to);

	public override string Dumper(object extra)
	{
		if (extra is not Func<int, int, (int, int, int, int)> loc)
			return ToString();
		var (fl, fc, tl, tc) = loc(from, to);
		return Dumper(new StrMaker() + fl + '.' + fc + ':' + tl + '.' + tc);
	}

	public string Dumper(StrMaker s) => s + (err > 0 ? "!!" : err < 0 ? "!" : "")
		+ (info is Synt<N, T> ? s : s + ' ' + info) + ' ' + (dump ?? name)
		+ (info is Synt<N, T> ? s + '\n' + info : s);
}

public partial class SynAlt<N>
{
	public object dump;

	public override string ToString() => dump as string ?? (string)
		(dump is Func<string> d ? dump = d() : dump is Func<object, string> dd ? dump = dd(this)
		: dump?.ToString() ?? label ?? "alt " + name);
}

[DebuggerTypeProxy(typeof(Dumper.Form))]
public partial class SynForm
{
	public object dump;

	public override string ToString() => dump as string ?? (string)
		(dump is Func<string> d ? dump = d() : dump is Func<object, string> dd ? dump = dd(this)
		: dump?.ToString() ?? base.ToString());
}

public static partial class Dumper
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
	public int dump = 0; // no: 0, lexs only for tree leaf: 1, lexs: 2, lexs and Alts: 3
	public Func<object, string> dumper;

	void InitDump()
	{
		dumper = Dumper;
		foreach (var (f, fx) in forms.Each())
			if (f != null)
				f.dump ??= (Func<object, string>)Dumper;
	}

	public string Dumper(object d)
	{
		if (d is Kord key) return key == default ? "eor" : CharSet.Unesc(key);
		if (d is Nord name) return CharSet.Unesc((char)name);
		if (d is SynForm f) {
			StrMaker s = new(); short r;
			if (dumper != Dumper)
				s += dumper(d);
			foreach (var (m, k, other) in f.modes.Yes())
				_ = s - '\n' + (other ? " " : Dumper(k)) +
				(m > No ? s + " shift " + m : s + " redu " + (r = SynForm.Reduce(m)) + " " + alts[r]);
			foreach (var (p, n, other) in f.pushs.Yes())
				_ = s - '\n' + (other ? " " : Dumper(n)) + " push " + p;
			foreach (var (a, want) in f.recs ?? [])
				_ = s - '\n' + "recover " + a + ',' + want + ' ' + alts[a];
			return s.ToString();
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
			var s = new StrMaker() + name + " =";
			foreach (var c in this)
				_ = s + ' ' + SerMaker<K, N>.Dumper_(c);
			return s + "  " + clash switch { 0 => "", 1 => "<", > 1 => ">", _ => "^" }
				+ (rec ? "!!" : "") + (synt > 0 ? "+" : synt < 0 ? "-" : "")
				+ (lex >= 0 ? s + '.' + lex : s) + ' ' + label;
		}
	}
}

public partial class SerMaker<K, N>
{
	public static string Dumper_(object d)
	{
		StrMaker s = default;
		if (d is K key) return key.Equals(default(K)) ? "eor" : CharSet.Unesc(key);
		if (d is (IEnumerable<K> keys2, StrMaker s2))
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
			StrMaker s = new();
			foreach (var ((a, want), heads) in forms[f.index].Is)
				_ = s - '\n' + a.ToString() + " _" + want + Dumper_((heads, s));
			return s;
		}
		return Dumper_(d);
	}

	public void Dump(object d)
	{
		if (d is List<Form> forms)
			using (var env = EnvWriter.Use())
				env.WriteLine($"forms: {forms.Count}");
		else if (d is (Dictionary<Clash, (HashSet<K> keys, short mode)> clashs,
				bool detail, int solvez)) {
			using var env = EnvWriter.Use();
			env.WriteLine(detail ? $"clashes: {clashs.Count} ({solvez} solved)"
						: $"unsolved clashes: {clashs.Count} (besides {solvez} solved)");
			foreach (var ((c, (keys, mode)), cx) in clashs.Each(1)) {
				env.Write(cx + (mode == No ? "  : " : " :: "));
				env.WriteLine(Dumper(keys));
				using var _ = EnvWriter.Use("\t\t");
				foreach (var a in c.redus)
					env.WriteLine($"{(a == SynForm.Reduce(mode) ? "REDUCE" : "reduce")} {a}  {alts[a]}");
				foreach (var a in c.shifts ?? [])
					env.WriteLine($"{(mode > No ? "SHIFT" : "shift")} {a}  {alts[a]}");
			}
		}
	}
}

public static partial class Dumper
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
