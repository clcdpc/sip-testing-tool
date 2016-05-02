using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Drawing;
using System.IO;

namespace Clc.Sip.Helpers
{
	public static class RichTextBoxExtensions
	{		
		public static void AppendText(this RichTextBox box, string text, Color color)
		{
			Color _color;
			string txt;

			foreach (var c in text)
			{
				if (c == '|')
				{
					_color = MainForm.darkTheme ? Color.Yellow : Color.Black;
					txt = string.Format("{0}", c.ToString());
				}
				else
				{
					txt = c.ToString();
					_color = color;

				}
				box.SelectionStart = box.TextLength;
				box.SelectionLength = 0;

				box.SelectionColor = _color;
				box.AppendText(txt);
				box.SelectionColor = box.ForeColor;
			}
		}
	}
}
