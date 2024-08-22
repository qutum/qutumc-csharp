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

[assembly: DebuggerTypeProxy(typeof(Dumper.Form), Target = typeof(SynForm))]
[assembly: DebuggerTypeProxy(typeof(Dumper.Stack), Target = typeof((SynForm, int, object)))]

namespace qutum.parser;

using Kord = char;
using Nord = ushort;

public partial struct Lexi<K>
{
	public override readonly string ToString() => $"{key}{(err < 0 ? "!" : "=")}{value}";

	public readonly string ToString(Func<object, string> dumper)
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
}

public partial class Synt<N, T>
{
	public object dump;

	public override string ToString() => ToString(new StrMaker() + from + ':' + to);

	public override string ToString(object extra)
	{
		if (extra is not Func<int, int, (int, int, int, int)> loc)
			return ToString();
		var (fl, fc, tl, tc) = loc(from, to);
		return ToString(new StrMaker() + fl + '.' + fc + ':' + tl + '.' + tc);
	}

	public string ToString(StrMaker s) => s + (err > 0 ? "!!" : err < 0 ? "!" : "")
		+ (info is Synt<N, T> ? s : s + ' ' + info) + ' ' + (dump ?? name)
		+ (info is Synt<N, T> ? s + '\n' + info : s);
}

public partial class SynGram<K, N>
{
	public partial class Alt
	{
		public override string ToString() => $"{name} = {string.Join(' ', this)}  {clash
			switch { 0 => "", 1 => "<", > 1 => ">", _ => "^" }}{(rec ? "!!" : "")}{(
			synt > 0 ? "+" : synt < 0 ? "-" : "")}{(lex >= 0 ? "_" + lex : "")} {label}";
	}
}

public partial class SynAlt<N>
{
	public object dump;

	public override string ToString() => dump as string ?? (string)
		(dump is Func<string> d ? dump = d() : dump is Func<object, string> dd ? dump = dd(this)
		: dump?.ToString() ?? label ?? "alt " + name);
}

public partial class SynForm
{
	public object dump;

	public override string ToString() => dump as string ?? (string)
		(dump is Func<string> d ? dump = d() : dump is Func<object, string> dd ? dump = dd(this)
		: dump?.ToString() ?? base.ToString());
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
		if (d is Kord key) return CharSet.Unesc(key);
		if (d is Nord name) return CharSet.Unesc((char)name);
		if (d is SynForm f) {
			StrMaker s = new(); short r;
			foreach (var (m, k, other) in f.modes.Yes())
				_ = s - '\n' + (other ? " " : dumper(k)) +
				(m >= 0 ? s + " shift " + m : s + " redu " + (r = SynForm.Reduce(m)) + " " + alts[r]);
			foreach (var (p, n, other) in f.pushs.Yes())
				_ = s - '\n' + (other ? " " : dumper(n)) + " push " + p;
			foreach (var (a, want) in f.recs ?? [])
				_ = s - '\n' + "recover " + a + ',' + want + ' ' + alts[a];
			return s.ToString();
		}
		return d.ToString();
	}
}

public static class Dumper
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

	[DebuggerDisplay("title")] // no effect
	public struct Stack((SynForm form, int loc, object synt) d)
	{
		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public SynForm form = d.form;
		public object synt = d.synt;

		public override readonly string ToString() => $"@{d.loc} {form}"; // no effect
	}
}
