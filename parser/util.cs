//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace qutum;

public static class Extension
{
	public static bool IncLess(this ref int d, int z) => ++d < z || (d = z) < z;

	public static ArraySegment<T> Seg<T>(this T[] s) => new(s);
	public static ArraySegment<T> Seg<T>(this T[] s, int from, int to) => new(s, from, to - from);

	public static IEnumerable<T> Enum<T>(this ReadOnlyMemory<T> s)
	{
		for (var x = 0; x < s.Length; x++) yield return s.Span[x];
	}

	public static ReadOnlyMemory<char> Mem(this string s) => s.AsMemory();
	public static ReadOnlyMemory<char> Mem(this string s, int from, int to) => s.AsMemory(from, to - from);

	public static bool Adds<T>(this ISet<T> s, IEnumerable<T> add)
	{
		if (add == null) return false;
		var z = s.Count; s.UnionWith(add); return s.Count > z;
	}

	public static IEnumerable<(T d, int x)> Each<T>(this IEnumerable<T> s, int offset = 0)
	{
		foreach (var d in s) yield return (d, offset++);
	}

	public static IEnumerable<T?> May<T>(this IEnumerable<T> s) where T : struct
	{
		bool no = true;
		foreach (var d in s) { no = false; yield return d; }
		if (no) yield return null;
	}

	public static T? FirstOrNull<T>(this IEnumerable<T> s, Func<T, bool> If) where T : struct
	{
		foreach (var d in s) if (If(d)) return d;
		return null;
	}
}

public class LinkTree<T> : IEnumerable<T> where T : LinkTree<T>
{
	public T up, prev, next, head, tail;

	public T First()
	{
		var t = (T)this;
		while (t.prev != null)
			t = t.prev;
		return t;
	}

	public T Last()
	{
		var t = (T)this;
		while (t.next != null)
			t = t.next;
		return t;
	}

	// after this.tail add sub and all sub.next
	public T Add(T sub)
	{
		if (sub == null)
			return (T)this;
		Debug.Assert(sub.up == null && sub.prev == null);
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

	// before this.head add sub and all sub.next 
	public T AddHead(T sub)
	{
		if (sub == null)
			return (T)this;
		Debug.Assert(sub.up == null && sub.prev == null);
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

	// after tail add subs of t
	public T AddSubOf(T t)
	{
		if (t?.head == null)
			return (T)this;
		var x = t.head;
		t.head = t.tail = null;
		x.up = null;
		return Add(x);
	}

	// after last this.next append next and all next.next
	public T Append(T next)
	{
		if (next == null)
			return (T)this;
		Debug.Assert(up == null && next.up == null && next.prev == null);
		(next.prev = Last()).next = next;
		return (T)this;
	}

	// after last this.next append subs of t
	public T AppendSubOf(T t)
	{
		if (t?.head == null)
			return (T)this;
		Debug.Assert(up == null);
		var x = t.head;
		t.head = t.tail = null;
		(x.prev = Last()).next = x;
		for (x.up = null; x.next != null; x.up = null)
			x = x.next;
		return (T)this;
	}

	// remove self from up
	public T Remove(bool clear = true)
	{
		if (prev != null)
			prev.next = next;
		if (next != null)
			next.prev = prev;
		if (up?.head == this)
			up.head = next;
		if (up?.tail == this)
			up.tail = prev;
		if (clear)
			up = prev = next = null;
		return (T)this;
	}

	public T Dump(object extra = null, int after = 1)
	{
		bool first = prev == null && (up == null || after <= 0);
		bool last = next == null && (up == null || after > 0);
		string noInd = up == null && after == 0 ? "" : null;
		var t = head;
		for (; t != null && (dumpOrder == 0 ? t == head : dumpOrder < 0); t = t.next)
			using (var env = EnvWriter.Use(noInd ?? (first ? "  " : "| ")))
				t.Dump(extra, -1);
		using (var env = noInd != null ? EnvWriter.Use(noInd, "   ") : EnvWriter.Use(
				after > 0 ? first ? "- " : "\\ " : last ? "- " : "/ ",
				t != null ? last ? "  |  " : "| |  " : last ? "     " : "|    "))
			env.WriteLine(Dumper(extra));
		for (; t != null; t = t.next)
			using (var env = EnvWriter.Use(noInd ?? (last ? "  " : "| ")))
				t.Dump(extra, 1);
		if (up == null && prev == null)
			for (t = next; t != null; t = t.next)
				t.Dump(extra, after);
		return (T)this;
	}

	// preorder >0, inorder 0, postorder <0
	public virtual int dumpOrder => 1;

	public virtual string Dumper(object extra) => extra?.ToString() ?? "dump";

	// enumerate subs
	public IEnumerator<T> GetEnumerator()
	{
		for (var x = head; x != null; x = x.next)
			yield return x;
	}
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public IEnumerable<T> Backward()
	{
		for (var x = tail; x != null; x = x.prev)
			yield return x;
	}
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0250:Make struct 'readonly'")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0251:Make member 'readonly'")]
public struct StrMaker
{
	internal readonly StringBuilder s;

	public StrMaker() => s = new();
	public StrMaker(out StrMaker s) => s = this;
	public static implicit operator StrMaker(string s) => new StrMaker() + s;
	public static implicit operator string(StrMaker s) => s.ToString();

	public static StrMaker operator +(StrMaker s, object d)
	{
		if (d is not StrMaker m || m.s != s.s) s.s.Append(d); return s;
	}
	public static StrMaker operator -(StrMaker s, object d) => s.s.Length > 0 ? s + d : s;
	public StrMaker F(string format, params object[] args)
	{
		s.AppendFormat(format, args); return this;
	}

	public int Size => s.Length;
	public override string ToString() => s.ToString();
}

public class EnvWriter : IndWriter, IDisposable
{
	protected EnvWriter() : base(null) { }

	static readonly EnvWriter env = new();
	bool pressKey;

	public static EnvWriter Begin(bool pressKey = false)
	{
		env.pressKey = pressKey;
		if (env.output == null) {
			Console.OutputEncoding = new UTF8Encoding(false, false);
			env.output = Console.Out;
			Console.SetOut(env);
		}
		return env;
	}

	public static EnvWriter Use() => (EnvWriter)env.Indent("");

	public static EnvWriter Use(string ind = "\t", string ind2 = null) => (EnvWriter)env.Indent(ind, ind2);

	protected override void Dispose(bool _)
	{
		if (output != null && indents.Count == 0) {
			Console.SetOut(output);
			output = null;
			try {
				if (pressKey && !Console.IsInputRedirected
						&& GetConsoleProcessList(procs, 1) == 1) {
					Write("Press Any Key ... "); Flush();
					Console.ReadKey(true);
				}
			}
			catch (DllNotFoundException) { }
		}
		base.Dispose(true);
	}

	protected override void Print(ReadOnlyMemory<char>? s)
	{
		base.Print(s);
		if (Debugger.IsAttached) Debug.Write(s);
	}
	protected override void Print() { base.Print(); if (Debugger.IsAttached) Debug.Flush(); }

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054")]
	[DllImport("kernel32.dll", SetLastError = false)]
	static extern uint GetConsoleProcessList(uint[] procs, uint n);
	static readonly uint[] procs = new uint[1];
}

public class IndWriter(TextWriter output) : StringWriter, IDisposable
{
	protected TextWriter output = output;
	protected readonly List<(string ind, string ind2)> indents = [];
	protected bool lineStart = true;

	public IndWriter Indent() => Indent("");
	public IndWriter Indent(string ind = "\t", string ind2 = null)
	{
		indents.Add((ind ?? throw new ArgumentNullException(nameof(ind)), ind2));
		return this;
	}

	protected override void Dispose(bool _)
	{
		if (indents.Count > 0)
			indents.RemoveAt(indents.Count - 1);
	}

	public override void Flush()
	{
		base.Flush();
		foreach (var cs in GetStringBuilder().GetChunks()) {
			int f = 0, t = 0;
			while (t >= 0 && f < cs.Length) {
				t = cs.Span[f..].IndexOfAny('\n', '\r');
				if (t != 0) {
					if (lineStart) {
						for (var x = 0; x < indents.Count; x++)
							if (indents[x].ind?.Length > 0)
								Print(indents[x].ind.AsMemory());
						for (var x = 0; x < indents.Count; x++)
							if (indents[x].ind2 != null)
								indents[x] = (indents[x].ind2, null);
					}
					lineStart = false;
					Print(t < 0 ? cs[f..] : cs.Slice(f, t));
				}
				lineStart = t >= 0 && cs.Span[f + t] == '\n';
				if (lineStart)
					Print(cs.Slice(f + t, 1));
				f += t + 1;
			}
		}
		Print();
		GetStringBuilder().Clear();
	}

	protected virtual void Print(ReadOnlyMemory<char>? s) => output.Write(s);
	protected virtual void Print() => output.Flush();

	public override void WriteLine()
	{
		base.WriteLine(); Flush();
	}

	public override void WriteLine(bool value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(char value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(char[] buffer)
	{
		base.WriteLine(buffer); Flush();
	}

	public override void WriteLine(char[] buffer, int index, int size)
	{
		base.WriteLine(buffer, index, size); Flush();
	}

	public override void WriteLine(decimal value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(double value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(int value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(long value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(object value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(float value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(string value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(string format, object arg0)
	{
		base.WriteLine(format, arg0); Flush();
	}

	public override void WriteLine(string format, object arg0, object arg1)
	{
		base.WriteLine(format, arg0, arg1); Flush();
	}

	public override void WriteLine(string format, object arg0, object arg1, object arg2)
	{
		base.WriteLine(format, arg0, arg1, arg2); Flush();
	}

	public override void WriteLine(string format, params object[] arg)
	{
		base.WriteLine(format, arg); Flush();
	}

	public override void WriteLine(uint value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(ulong value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(ReadOnlySpan<char> buffer)
	{
		base.WriteLine(buffer); Flush();
	}

	public override void WriteLine(StringBuilder value)
	{
		base.WriteLine(value); Flush();
	}
}
