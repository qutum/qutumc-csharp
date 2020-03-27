//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace qutum
{
	public static class Extension
	{
		public static void Each<T>(this IEnumerable<T> s, Action<T, int> a)
		{
			int x = 0; foreach (var t in s) a(t, x++);
		}
	}

	public class LinkTree<T> : IEnumerable<T> where T : LinkTree<T>
	{
		public T up, prev, next, head, tail;

		public T Add(T sub)
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

		public T AddHead(T sub)
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

		public T AddNext(T next)
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

		public T AddSub(T t)
		{
			if (t == null)
				return (T)this;
			var x = t.head;
			x.up = null;
			t.head = t.tail = null;
			return Add(x);
		}

		public T Remove(bool clear = true)
		{
			if (prev != null)
				prev.next = next;
			if (next != null)
				next.prev = prev;
			if (up != null && up.head == this)
				up.head = next;
			if (up != null && up.tail == this)
				up.tail = prev;
			if (clear) up = prev = next = null;
			return (T)this;
		}

		public T Dump(int detail = 0)
		{
			int o = dumpOrder, upo = up?.dumpOrder ?? 1;
			bool afterup = upo > 0 || upo == 0 && this != up.head;
			for (var t = head; ; t = t.next) {
				if (o > 0 ? t == head : o < 0 ? t == null : t == head?.next)
					using (var env = EnvWriter.Indent(up == null ? "" : afterup ? "\\ " : "/ "))
						env.WriteLine(ToString());
				if (t == null)
					break;
				using (var env = EnvWriter.Indent
					(up == null ? "" :
					(o > 0 || o == 0 && t != head
						? afterup && this == up.tail
						: !afterup && this == up.head) ? "  " :
					"| "))
					t.Dump(detail);
			}
			return (T)this;
		}

		// preorder >0, inorder 0, postorder <0
		public virtual int dumpOrder => 1;

		public override string ToString() => "dump";

		public IEnumerator<T> GetEnumerator()
		{
			for (var x = head; x != null; x = x.next)
				yield return x;
		}
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		class Backwarder : IEnumerable<T>
		{
			internal T tail;

			public IEnumerator<T> GetEnumerator()
			{
				for (var x = tail; x != null; x = x.prev)
					yield return x;
			}
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		public IEnumerable<T> Backward() => new Backwarder { tail = tail };
	}

	class EnvWriter : StringWriter, IDisposable
	{
		static readonly EnvWriter env = new EnvWriter();

		TextWriter output;
		bool pressKey;
		readonly List<string> indents = new List<string>();
		bool lineStart = true;

		public static EnvWriter Begin(bool pressKey = false)
		{
			env.pressKey = pressKey;
			if (env.indents.Contains(null))
				return env;
			env.output = Console.Out;
			Console.OutputEncoding = new UTF8Encoding(false, false);
			Console.SetOut(env);
			env.indents.Add(null);
			return env;
		}

		public static EnvWriter Use() => Indent("");

		public static EnvWriter Indent(string ind = "\t")
		{
			env.indents.Add(ind ?? throw new ArgumentNullException());
			return env;
		}

		protected override void Dispose(bool _)
		{
			if (env.indents.Count == 0)
				return;
			var ind = env.indents[^1];
			env.indents.RemoveAt(env.indents.Count - 1);
			if (ind == null) {
				Console.SetOut(output);
				try {
					if (pressKey && !Console.IsInputRedirected
							&& GetConsoleProcessList(procs, 1) == 1) {
						Write("Press Any Key ... "); Flush();
						Console.ReadKey(true);
					}
				}
				catch (DllNotFoundException) { }
			}
		}

		public override void Flush()
		{
			base.Flush();
			foreach (var cs in GetStringBuilder().GetChunks()) {
				int f = 0, t = 0;
				while (t >= 0 && f < cs.Length) {
					t = cs.Span.Slice(f).IndexOfAny('\n', '\r');
					if (t != 0) {
						if (lineStart)
							foreach (var ind in indents)
								if (ind != null && ind.Length > 0)
									Print(ind.AsMemory());
						lineStart = false;
						Print(t < 0 ? cs[f..] : cs.Slice(f, t));
					}
					lineStart = t >= 0 && cs.Span[f + t] == '\n';
					if (lineStart)
						Print(cs.Slice(f + t, 1));
					f += t + 1;
				}
			}
			Print(null);
			GetStringBuilder().Clear();
		}

		void Print(ReadOnlyMemory<char>? s)
		{
			if (s != null)
				output.Write(s);
			else
				output.Flush();
			if (Debugger.IsAttached)
				if (s != null)
					Debug.Write(s);
				else
					Debug.Flush();
		}

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

		public override void WriteLine(char[] buffer, int index, int count)
		{
			base.WriteLine(buffer, index, count); Flush();
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

		[DllImport("kernel32.dll", SetLastError = false)]
		static extern uint GetConsoleProcessList(uint[] procs, uint n);
		static readonly uint[] procs = new uint[1];
	}
}
