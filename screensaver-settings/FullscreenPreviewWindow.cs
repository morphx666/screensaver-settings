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

using System;
using Gtk;

public partial class FullscreenPreviewWindow : Window {
	private MainWindow mMainWindow;
	
	public FullscreenPreviewWindow() : base(WindowType.Toplevel) {
		this.Build();
	}
	
	public void Init(MainWindow mainWindow) {
		mMainWindow = mainWindow;
		
		btnExit.Clicked += ExitFullscreen;
		btnNext.Clicked += SelectAnotherSS;
		btnPrevious.Clicked += SelectAnotherSS;
		btnSettings.Clicked += ShowSettingsWindow;
	}
	
	public bool OptionsButtonEnabled {
		set {btnSettings.Sensitive = value;}
	}
	
	private void ExitFullscreen(object sender, EventArgs e) {
		this.Destroy();
	}
	
	private void SelectAnotherSS(object sender, EventArgs e) {
		Button btn = (Button)sender;
		if(btn.Name == "btnNext") {
			mMainWindow.SelectNextSS();
		} else {
			mMainWindow.SelectPreviousSS();
		}
	}
	
	private void ShowSettingsWindow(object sender, EventArgs e) {
		mMainWindow.ShowSSSettings();
	}
	
	public DrawingArea PreviewArea {
		get {return ssPreviewArea;}
	}
	
	public string ScreensaverTitle {
		set {lblName.LabelProp = String.Format("<b>{0}</b>", value);}
    }
}