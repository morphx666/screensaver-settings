/* This file is part of screensaver-settings
   Copyright (C) 2008 xFX JumpStart
 
   screensaver-settings is free software; you can redistribute it and/or
   modify it under the terms of the GNU Lesser General Public License as
   published by the Free Software Foundation; either version 3 of the
   License, or (at your option) any later version.
 
   screensaver-settings is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser
   General Public License for more details.

   You should have received a copy of the GNU Lesser General Public License
   along with screensaver-settings; if not, write to the Free Software
   Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston,
   MA 02110-1301 USA */

// project created on 10/22/2007 at 1:44 PM

using System;
using System.Runtime.InteropServices;
using Gtk;
using System.IO;

class MainClass {
	public static string SS_XML_PATH = "/usr/share/xscreensaver/config/";
	public static string SS_THEME_PATH = "/usr/share/applications/screensavers/";
	public static string SS_BIN_PATH = "/usr/lib/xscreensaver/";
	public static string SU_CMD = "gksudo";
	public static string SU_PARAMS = "-d -m \"{2}\" cp {0} {1}";
	public static string WIN_REDIR = "-window-id";
	
	public static void Main (string[] args)
	{
		Application.Init ();
		
		//Try to support other distros as well...
		if (!Directory.Exists (SS_XML_PATH)) {
			SS_XML_PATH = "/etc/xscreensaver/";
			SU_CMD = "gnomesu";
			SU_PARAMS = "-c='cp {0} {1}'";
		}

		bool sanityCheck = false;

		sanityCheck &= Directory.Exists (SS_XML_PATH);
		sanityCheck &= Directory.Exists (SS_THEME_PATH);
		sanityCheck &= Directory.Exists (SS_BIN_PATH);

		if (sanityCheck) {
			MainWindow win = new MainWindow();
			win.Show();		
			Application.Run();
		} else {
			Dialog dlg = new MessageDialog(null, DialogFlags.DestroyWithParent, MessageType.Info, ButtonsType.Close, "This is a fucked up Linux distro...\nTry installing the following packages:\n\n1) xscreensaver\n2) gnome-screensaver\n3) xscreensaver-data-extra");
			dlg.Run();
			dlg.Destroy();
		}
	}	
	
	public static string[] Split(string text, string separator) {
		return text.Split(new string[] {separator}, StringSplitOptions.None);
	}

	[DllImport("libgdk-x11-2.0.so.0")]
	private static extern ulong gdk_x11_drawable_get_xid(IntPtr drawable);
	
	public static ulong GetPreviewWindowID(DrawingArea da) {
		if(da.IsRealized) {
			ulong id = gdk_x11_drawable_get_xid(da.GdkWindow.Handle);
			string hex = id.ToString("X");
			if(hex.Length > 8) hex = hex.Substring(hex.Length - 8);
			id = ulong.Parse(hex, System.Globalization.NumberStyles.HexNumber);
			return id;
		}
		throw new ApplicationException ("Attempted to get preview window ID before realizing it");
	}
}