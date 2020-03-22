//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace qutum
{
	class DebugWriter : StringWriter
	{
		internal TextWriter console;

		internal static void ConsoleBegin()
		{
			Console.OutputEncoding = new UTF8Encoding(false, false);
			if (Debugger.IsAttached && !(Console.Out is DebugWriter))
				Console.SetOut(new DebugWriter { console = Console.Out });
		}

		internal static void ConsoleEnd()
		{
			if (Debugger.IsAttached) {
				Console.WriteLine("Press Enter Key ...");
				Console.ReadLine();
			}
		}

		public override void Flush()
		{
			base.Flush();
			var s = GetStringBuilder().ToString();
			Debug.Write(s);
			Debug.Flush();
			console.Write(s);
			console.Flush();
			GetStringBuilder().Clear();
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
	}
}
